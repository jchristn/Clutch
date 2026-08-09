namespace Clutch.Server.WebSocket
{
    using System;
    using System.Collections.Generic;
    using System.Net.WebSockets;
    using System.Threading;
    using System.Threading.Tasks;
    using Clutch.Core.Enums;
    using Clutch.Core.Models;
    using Clutch.Core.Requests;
    using Clutch.Core.Responses;
    using Clutch.Core.Security;
    using Clutch.Core.Services;
    using Clutch.Server.Serialization;
    using Clutch.Server.Services;
    using SyslogLogging;
    using WatsonWebserver.Core;
    using WatsonWebserver.Core.WebSockets;

    /// <summary>
    /// Handles the lock WebSocket protocol. Clients authenticate on the upgrade request, then send
    /// acquire, release, heartbeat, and ping messages. All held locks are bound to the connection and are
    /// released when it closes.
    /// </summary>
    public class LockWebSocketHandler
    {
        #region Private-Members

        private readonly AuthenticationService _Authentication;
        private readonly LockEngine _Engine;
        private readonly WebSocketConnectionManager _Manager;
        private readonly LoggingModule _Logging;
        private readonly string _NodeId;
        private readonly int _DefaultLeaseMs;
        private readonly int _HeartbeatIntervalMs;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="authentication">Authentication service.</param>
        /// <param name="engine">Lock engine.</param>
        /// <param name="manager">Connection manager.</param>
        /// <param name="logging">Logging module.</param>
        /// <param name="nodeId">Node identifier.</param>
        /// <param name="defaultLeaseMs">Default lease advertised in the welcome frame.</param>
        /// <param name="heartbeatIntervalMs">Recommended client heartbeat interval.</param>
        /// <exception cref="ArgumentNullException">Thrown when a required argument is null.</exception>
        public LockWebSocketHandler(
            AuthenticationService authentication,
            LockEngine engine,
            WebSocketConnectionManager manager,
            LoggingModule logging,
            string nodeId,
            int defaultLeaseMs,
            int heartbeatIntervalMs)
        {
            _Authentication = authentication ?? throw new ArgumentNullException(nameof(authentication));
            _Engine = engine ?? throw new ArgumentNullException(nameof(engine));
            _Manager = manager ?? throw new ArgumentNullException(nameof(manager));
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));
            _NodeId = string.IsNullOrEmpty(nodeId) ? "node" : nodeId;
            _DefaultLeaseMs = defaultLeaseMs;
            _HeartbeatIntervalMs = heartbeatIntervalMs;
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Handle a WebSocket connection for the lifetime of the socket.
        /// </summary>
        /// <param name="context">HTTP context of the upgrade request.</param>
        /// <param name="session">Watson WebSocket session.</param>
        /// <returns>Awaitable task.</returns>
        public async Task HandleAsync(HttpContextBase context, WebSocketSession session)
        {
            RequestContext? auth = await _Authentication.AuthenticateWebSocketAsync(context, context.Token).ConfigureAwait(false);
            if (auth == null || !auth.IsAuthenticated || string.IsNullOrEmpty(auth.TenantId))
            {
                await session.CloseAsync(WebSocketCloseStatus.PolicyViolation, "authentication failed", context.Token).ConfigureAwait(false);
                return;
            }

            string sessionId = Guid.NewGuid().ToString("N");
            ClutchWsSession clutchSession = new ClutchWsSession(session, sessionId, auth.TenantId, auth.CredentialId ?? string.Empty);
            _Manager.Add(clutchSession);
            _Logging.Debug("[Clutch.Ws] session " + sessionId + " connected (tenant " + auth.TenantId + ")");

            await clutchSession.SendAsync(new
            {
                type = "welcome",
                sessionId = sessionId,
                tenantId = auth.TenantId,
                defaultLeaseMs = _DefaultLeaseMs,
                heartbeatIntervalMs = _HeartbeatIntervalMs
            }, context.Token).ConfigureAwait(false);

            try
            {
                await foreach (WebSocketMessage message in session.ReadMessagesAsync(context.Token).ConfigureAwait(false))
                {
                    if (message == null || message.MessageType != WebSocketMessageType.Text) continue;

                    WsInboundMessage? inbound = Json.Deserialize<WsInboundMessage>(message.Text);
                    if (inbound == null)
                    {
                        await clutchSession.SendAsync(new { type = "error", message = "Invalid message." }, context.Token).ConfigureAwait(false);
                        continue;
                    }

                    await DispatchAsync(clutchSession, inbound, context.Token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                // Connection closed.
            }
            catch (Exception e)
            {
                _Logging.Warn("[Clutch.Ws] session " + sessionId + " error: " + e.Message);
            }
            finally
            {
                _Manager.Remove(sessionId);
                try
                {
                    await _Engine.ReleaseAllForSessionAsync(sessionId, CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    _Logging.Warn("[Clutch.Ws] cleanup failed for session " + sessionId + ": " + e.Message);
                }
                _Logging.Debug("[Clutch.Ws] session " + sessionId + " disconnected");
            }
        }

        #endregion

        #region Private-Methods

        private Task DispatchAsync(ClutchWsSession session, WsInboundMessage message, CancellationToken token)
        {
            switch (message.Type)
            {
                case "acquire":
                    // Acquire may block (wait behavior); run it off the read loop so other messages proceed.
                    _ = Task.Run(() => HandleAcquireAsync(session, message, token));
                    return Task.CompletedTask;
                case "release":
                    return HandleReleaseAsync(session, message, token);
                case "heartbeat":
                    return HandleHeartbeatAsync(session, message, token);
                case "ping":
                    return session.SendAsync(new { type = "pong" }, token);
                default:
                    return session.SendAsync(new { type = "error", requestId = message.RequestId, message = "Unknown message type '" + message.Type + "'." }, token);
            }
        }

        private async Task HandleAcquireAsync(ClutchWsSession session, WsInboundMessage message, CancellationToken token)
        {
            try
            {
                if (string.IsNullOrEmpty(message.Key) || !message.Mode.HasValue)
                {
                    await session.SendAsync(new { type = "error", requestId = message.RequestId, message = "acquire requires 'key' and 'mode'." }, token).ConfigureAwait(false);
                    return;
                }

                AcquireRequest request = new AcquireRequest();
                request.TenantId = session.TenantId;
                request.LockKey = message.Key;
                request.Mode = message.Mode.Value;
                request.CredentialId = session.CredentialId;
                request.SessionId = session.SessionId;
                request.NodeId = _NodeId;
                request.RequestedLeaseMs = message.LeaseMs;
                request.Policy = message.Policy;

                LockBehaviorEnum behavior = message.Behavior ?? LockBehaviorEnum.FailFast;
                LockResult result = await _Engine.AcquireAsync(request, behavior, message.TimeoutMs, token).ConfigureAwait(false);

                if (result.IsGranted() && result.Holder != null)
                {
                    await session.SendAsync(new
                    {
                        type = "acquired",
                        requestId = message.RequestId,
                        key = message.Key,
                        mode = message.Mode.Value.ToString(),
                        holderId = result.Holder.Id,
                        fencingToken = result.FencingToken,
                        leaseExpiresUtc = result.LeaseExpiresUtc
                    }, token).ConfigureAwait(false);
                }
                else
                {
                    await session.SendAsync(new
                    {
                        type = "denied",
                        requestId = message.RequestId,
                        key = message.Key,
                        result = result.Result.ToString(),
                        reason = result.Reason
                    }, token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                // Connection closing.
            }
            catch (Exception e)
            {
                await session.SendAsync(new { type = "error", requestId = message.RequestId, message = e.Message }, token).ConfigureAwait(false);
            }
        }

        private async Task HandleReleaseAsync(ClutchWsSession session, WsInboundMessage message, CancellationToken token)
        {
            if (string.IsNullOrEmpty(message.HolderId))
            {
                await session.SendAsync(new { type = "error", requestId = message.RequestId, message = "release requires 'holderId'." }, token).ConfigureAwait(false);
                return;
            }

            bool released = await _Engine.ReleaseAsync(session.TenantId, message.HolderId, session.SessionId, token).ConfigureAwait(false);
            await session.SendAsync(new
            {
                type = "released",
                requestId = message.RequestId,
                key = message.Key,
                holderId = message.HolderId,
                released = released
            }, token).ConfigureAwait(false);
        }

        private async Task HandleHeartbeatAsync(ClutchWsSession session, WsInboundMessage message, CancellationToken token)
        {
            List<string> ids = message.HolderIds ?? new List<string>();
            List<LockHolder> renewed = await _Engine.HeartbeatAsync(session.SessionId, ids, token).ConfigureAwait(false);
            List<object> summary = new List<object>();
            foreach (LockHolder holder in renewed)
            {
                summary.Add(new { holderId = holder.Id, leaseExpiresUtc = holder.LeaseExpiresUtc });
            }
            await session.SendAsync(new { type = "heartbeat", requestId = message.RequestId, renewed = summary }, token).ConfigureAwait(false);
        }

        #endregion
    }
}
