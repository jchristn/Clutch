namespace Clutch.Server.Routes
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using Clutch.Core.Database;
    using Clutch.Core.Enumeration;
    using Clutch.Core.Enums;
    using Clutch.Core.Models;
    using Clutch.Core.Requests;
    using Clutch.Core.Responses;
    using Clutch.Core.Security;
    using Clutch.Core.Services;
    using Clutch.Server.Services;
    using Clutch.Server.Telemetry;
    using WatsonWebserver;
    using WatsonWebserver.Core;
    using WatsonWebserver.Core.OpenApi;

    /// <summary>
    /// Lock routes. In addition to observing state and force-releasing a key (administration), these routes
    /// let a REST client acquire, release, and heartbeat locks exactly as a WebSocket client does — the same
    /// lock engine backs both. A REST holder is not tied to a connection, so it lives on its TTL lease: renew
    /// it with heartbeat, release it explicitly, or let the lease lapse.
    /// </summary>
    public class LockRoutes
    {
        #region Private-Members

        private readonly DatabaseDriverBase _Database;
        private readonly AuthorizationService _Authorization;
        private readonly LockEngine _Engine;
        private readonly ClutchTelemetry _Telemetry;
        private readonly string _NodeId;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="database">Database driver.</param>
        /// <param name="authorization">Authorization service.</param>
        /// <param name="engine">Lock engine.</param>
        /// <param name="telemetry">Telemetry sink.</param>
        /// <param name="nodeId">Identifier of this node.</param>
        /// <exception cref="ArgumentNullException">Thrown when a required argument is null.</exception>
        public LockRoutes(DatabaseDriverBase database, AuthorizationService authorization, LockEngine engine, ClutchTelemetry telemetry, string nodeId)
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));
            _Authorization = authorization ?? throw new ArgumentNullException(nameof(authorization));
            _Engine = engine ?? throw new ArgumentNullException(nameof(engine));
            _Telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
            _NodeId = nodeId ?? throw new ArgumentNullException(nameof(nodeId));
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Register routes.
        /// </summary>
        /// <param name="server">Webserver.</param>
        public void Register(Webserver server)
        {
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1.0/api/tenants/{tid}/locks", ListAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("List active locks", "Locks"));
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1.0/api/tenants/{tid}/locks/{key}", ReadKeyAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("List holders on a key", "Locks"));

            // Client operations (any authenticated principal within the tenant), mirroring the WebSocket.
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, "/v1.0/api/tenants/{tid}/locks/{key}/acquire", AcquireAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("Acquire a lock", "Locks"));
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, "/v1.0/api/tenants/{tid}/locks/{key}/release", ReleaseHolderAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("Release a held lock", "Locks"));
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, "/v1.0/api/tenants/{tid}/lock-sessions/{sid}/heartbeat", HeartbeatAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("Renew lock leases", "Locks"));
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, "/v1.0/api/tenants/{tid}/lock-sessions/{sid}/release", ReleaseSessionAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("Release all locks for a session", "Locks"));

            // Administration.
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, "/v1.0/api/tenants/{tid}/locks/{key}/force-release", ForceReleaseAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("Force-release a key", "Locks"));
        }

        #endregion

        #region Private-Methods

        private async Task ListAsync(HttpContextBase context)
        {
            RequestContext ctx = RouteHelpers.Context(context);
            string tid = RouteHelpers.Param(context, "tid") ?? string.Empty;
            if (!_Authorization.CanReadTenant(ctx, tid))
            {
                await RouteHelpers.ErrorAsync(context, 403, "Forbidden", "Not permitted to read this tenant.").ConfigureAwait(false);
                return;
            }

            string? name = RouteHelpers.Query(context, "name");
            LockModeEnum? mode = ParseMode(RouteHelpers.Query(context, "mode"));
            EnumerationResult<LockHolder> holders = await _Database.LockHolders.EnumerateByTenantAsync(tid, name, mode, RouteHelpers.Enumeration(context), context.Token).ConfigureAwait(false);
            await RouteHelpers.JsonAsync(context, 200, holders).ConfigureAwait(false);
        }

        private async Task ReadKeyAsync(HttpContextBase context)
        {
            RequestContext ctx = RouteHelpers.Context(context);
            string tid = RouteHelpers.Param(context, "tid") ?? string.Empty;
            string key = Uri.UnescapeDataString(RouteHelpers.Param(context, "key") ?? string.Empty);
            if (!_Authorization.CanReadTenant(ctx, tid))
            {
                await RouteHelpers.ErrorAsync(context, 403, "Forbidden", "Not permitted to read this tenant.").ConfigureAwait(false);
                return;
            }

            LockDefinition? definition = await _Database.LockDefinitions.ReadAsync(tid, key, context.Token).ConfigureAwait(false);
            List<LockHolder> holders = await _Database.LockHolders.EnumerateByKeyAsync(tid, key, context.Token).ConfigureAwait(false);
            await RouteHelpers.JsonAsync(context, 200, new { definition = definition, holders = holders }).ConfigureAwait(false);
        }

        private async Task AcquireAsync(HttpContextBase context)
        {
            RequestContext ctx = RouteHelpers.Context(context);
            string tid = RouteHelpers.Param(context, "tid") ?? string.Empty;
            string key = Uri.UnescapeDataString(RouteHelpers.Param(context, "key") ?? string.Empty);
            if (!_Authorization.CanReadTenant(ctx, tid))
            {
                await RouteHelpers.ErrorAsync(context, 403, "Forbidden", "Not permitted to operate in this tenant.").ConfigureAwait(false);
                return;
            }

            LockAcquireHttpRequest? body = RouteHelpers.Body<LockAcquireHttpRequest>(context);
            if (body == null)
            {
                await RouteHelpers.ErrorAsync(context, 400, "BadRequest", "A JSON body with at least 'mode' is required.").ConfigureAwait(false);
                return;
            }

            string sessionId = string.IsNullOrEmpty(body.SessionId) ? Guid.NewGuid().ToString("N") : body.SessionId!;

            AcquireRequest request = new AcquireRequest();
            request.TenantId = tid;
            request.LockKey = key;
            request.Mode = body.Mode;
            request.CredentialId = ctx.CredentialId ?? string.Empty;
            request.SessionId = sessionId;
            request.NodeId = _NodeId;
            request.RequestedLeaseMs = body.LeaseMs;
            request.Policy = body.Policy;
            request.StrictPolicy = body.StrictPolicy;

            long startTimestamp = Stopwatch.GetTimestamp();
            LockResult result = await _Engine.AcquireAsync(request, body.Behavior, body.TimeoutMs, context.Token).ConfigureAwait(false);
            double seconds = Stopwatch.GetElapsedTime(startTimestamp).TotalSeconds;
            _Telemetry.RecordAcquire(body.Mode.ToString(), result.Result.ToString().ToLowerInvariant(), seconds);

            if (result.IsGranted() && result.Holder != null)
            {
                await RouteHelpers.JsonAsync(context, 201, new
                {
                    result = result.Result.ToString(),
                    granted = true,
                    key = key,
                    mode = body.Mode.ToString(),
                    holderId = result.Holder.Id,
                    sessionId = sessionId,
                    fencingToken = result.FencingToken,
                    leaseExpiresUtc = result.LeaseExpiresUtc
                }).ConfigureAwait(false);
                return;
            }

            await RouteHelpers.JsonAsync(context, 409, new
            {
                result = result.Result.ToString(),
                granted = false,
                key = key,
                sessionId = sessionId,
                reason = result.Reason
            }).ConfigureAwait(false);
        }

        private async Task ReleaseHolderAsync(HttpContextBase context)
        {
            RequestContext ctx = RouteHelpers.Context(context);
            string tid = RouteHelpers.Param(context, "tid") ?? string.Empty;
            string key = Uri.UnescapeDataString(RouteHelpers.Param(context, "key") ?? string.Empty);
            if (!_Authorization.CanReadTenant(ctx, tid))
            {
                await RouteHelpers.ErrorAsync(context, 403, "Forbidden", "Not permitted to operate in this tenant.").ConfigureAwait(false);
                return;
            }

            LockReleaseHttpRequest? body = RouteHelpers.Body<LockReleaseHttpRequest>(context);
            if (body == null || string.IsNullOrEmpty(body.HolderId) || string.IsNullOrEmpty(body.SessionId))
            {
                await RouteHelpers.ErrorAsync(context, 400, "BadRequest", "A JSON body with 'holderId' and 'sessionId' is required.").ConfigureAwait(false);
                return;
            }

            bool released = await _Engine.ReleaseAsync(tid, body.HolderId!, body.SessionId!, context.Token).ConfigureAwait(false);
            if (released) _Telemetry.RecordRelease("any");
            await RouteHelpers.JsonAsync(context, 200, new { key = key, holderId = body.HolderId, released = released }).ConfigureAwait(false);
        }

        private async Task HeartbeatAsync(HttpContextBase context)
        {
            RequestContext ctx = RouteHelpers.Context(context);
            string tid = RouteHelpers.Param(context, "tid") ?? string.Empty;
            string sid = RouteHelpers.Param(context, "sid") ?? string.Empty;
            if (!_Authorization.CanReadTenant(ctx, tid))
            {
                await RouteHelpers.ErrorAsync(context, 403, "Forbidden", "Not permitted to operate in this tenant.").ConfigureAwait(false);
                return;
            }

            LockHeartbeatHttpRequest? body = RouteHelpers.Body<LockHeartbeatHttpRequest>(context);
            List<string> ids = body?.HolderIds ?? new List<string>();
            List<LockHolder> renewed = await _Engine.HeartbeatAsync(sid, ids, context.Token).ConfigureAwait(false);

            List<object> summary = new List<object>();
            foreach (LockHolder holder in renewed)
            {
                summary.Add(new { holderId = holder.Id, leaseExpiresUtc = holder.LeaseExpiresUtc });
            }
            await RouteHelpers.JsonAsync(context, 200, new { sessionId = sid, renewed = summary }).ConfigureAwait(false);
        }

        private async Task ReleaseSessionAsync(HttpContextBase context)
        {
            RequestContext ctx = RouteHelpers.Context(context);
            string tid = RouteHelpers.Param(context, "tid") ?? string.Empty;
            string sid = RouteHelpers.Param(context, "sid") ?? string.Empty;
            if (!_Authorization.CanReadTenant(ctx, tid))
            {
                await RouteHelpers.ErrorAsync(context, 403, "Forbidden", "Not permitted to operate in this tenant.").ConfigureAwait(false);
                return;
            }

            List<string> released = await _Engine.ReleaseAllForSessionAsync(sid, context.Token).ConfigureAwait(false);
            foreach (string _ in released) _Telemetry.RecordRelease("any");
            await RouteHelpers.JsonAsync(context, 200, new { sessionId = sid, released = released, count = released.Count }).ConfigureAwait(false);
        }

        private async Task ForceReleaseAsync(HttpContextBase context)
        {
            RequestContext ctx = RouteHelpers.Context(context);
            string tid = RouteHelpers.Param(context, "tid") ?? string.Empty;
            string key = Uri.UnescapeDataString(RouteHelpers.Param(context, "key") ?? string.Empty);
            if (!_Authorization.CanAdministerTenant(ctx, tid))
            {
                await RouteHelpers.ErrorAsync(context, 403, "Forbidden", "Not permitted to administer this tenant.").ConfigureAwait(false);
                return;
            }

            List<LockHolder> holders = await _Database.LockHolders.EnumerateByKeyAsync(tid, key, context.Token).ConfigureAwait(false);
            int released = 0;
            foreach (LockHolder holder in holders)
            {
                LockHolder? revoked = await _Database.LockHolders.RevokeAsync(tid, holder.Id, "Force-released by administrator.", context.Token).ConfigureAwait(false);
                if (revoked != null)
                {
                    released++;
                    _Telemetry.RecordRelease(revoked.Mode.ToString());
                }
            }
            await RouteHelpers.JsonAsync(context, 200, new { key = key, released = released }).ConfigureAwait(false);
        }

        private static LockModeEnum? ParseMode(string? value)
        {
            if (string.IsNullOrEmpty(value)) return null;
            if (Enum.TryParse<LockModeEnum>(value, true, out LockModeEnum parsed)) return parsed;
            return null;
        }

        #endregion
    }
}
