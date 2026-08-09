namespace Clutch.Server.WebSocket
{
    using System;
    using System.Net.WebSockets;
    using System.Threading;
    using System.Threading.Tasks;
    using Clutch.Server.Serialization;
    using WatsonWebserver.Core.WebSockets;

    /// <summary>
    /// A Clutch WebSocket session. Wraps the Watson session with a Clutch-generated identity used to bind
    /// held locks, and serializes outbound sends so concurrent handlers cannot interleave frames.
    /// </summary>
    public class ClutchWsSession
    {
        #region Public-Members

        /// <summary>
        /// Clutch-generated session identifier. Held locks are bound to this value.
        /// </summary>
        public string SessionId { get; }

        /// <summary>
        /// The tenant this session is authenticated to.
        /// </summary>
        public string TenantId { get; }

        /// <summary>
        /// The authenticating credential identifier.
        /// </summary>
        public string CredentialId { get; }

        #endregion

        #region Private-Members

        private readonly WebSocketSession _Session;
        private readonly SemaphoreSlim _SendLock = new SemaphoreSlim(1, 1);

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="session">Watson WebSocket session.</param>
        /// <param name="sessionId">Clutch session identifier.</param>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="credentialId">Credential identifier.</param>
        /// <exception cref="ArgumentNullException">Thrown when session is null.</exception>
        public ClutchWsSession(WebSocketSession session, string sessionId, string tenantId, string credentialId)
        {
            _Session = session ?? throw new ArgumentNullException(nameof(session));
            SessionId = sessionId;
            TenantId = tenantId;
            CredentialId = credentialId;
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Serialize an object to JSON and send it as a text frame. Sends are serialized per session.
        /// </summary>
        /// <param name="body">Body object.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Awaitable task.</returns>
        public async Task SendAsync(object body, CancellationToken token)
        {
            string text = Json.Serialize(body);
            await _SendLock.WaitAsync(token).ConfigureAwait(false);
            try
            {
                await _Session.SendTextAsync(text, token).ConfigureAwait(false);
            }
            catch
            {
                // The socket may have closed; ignore send failures.
            }
            finally
            {
                _SendLock.Release();
            }
        }

        /// <summary>
        /// Close the session.
        /// </summary>
        /// <param name="status">Close status.</param>
        /// <param name="description">Close description.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Awaitable task.</returns>
        public async Task CloseAsync(WebSocketCloseStatus status, string description, CancellationToken token)
        {
            try
            {
                await _Session.CloseAsync(status, description, token).ConfigureAwait(false);
            }
            catch
            {
                // Ignore close failures.
            }
        }

        #endregion
    }
}
