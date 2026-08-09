namespace Clutch.Server.Routes
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Clutch.Core.Database;
    using Clutch.Core.Enums;
    using Clutch.Core.Models;
    using Clutch.Core.Security;
    using Clutch.Server.Services;
    using WatsonWebserver;
    using WatsonWebserver.Core;
    using WatsonWebserver.Core.OpenApi;

    /// <summary>
    /// Lock observation and administration routes. Lock acquisition happens over the WebSocket; these
    /// routes expose the current lock state and allow an administrator to force-release a key.
    /// </summary>
    public class LockRoutes
    {
        #region Private-Members

        private readonly DatabaseDriverBase _Database;
        private readonly AuthorizationService _Authorization;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="database">Database driver.</param>
        /// <param name="authorization">Authorization service.</param>
        /// <exception cref="ArgumentNullException">Thrown when a required argument is null.</exception>
        public LockRoutes(DatabaseDriverBase database, AuthorizationService authorization)
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));
            _Authorization = authorization ?? throw new ArgumentNullException(nameof(authorization));
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
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, "/v1.0/api/tenants/{tid}/locks/{key}/release", ReleaseAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("Force-release a key", "Locks"));
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
            List<LockHolder> holders = await _Database.LockHolders.EnumerateByTenantAsync(tid, name, mode, context.Token).ConfigureAwait(false);
            await RouteHelpers.JsonAsync(context, 200, holders).ConfigureAwait(false);
        }

        private async Task ReadKeyAsync(HttpContextBase context)
        {
            RequestContext ctx = RouteHelpers.Context(context);
            string tid = RouteHelpers.Param(context, "tid") ?? string.Empty;
            string key = RouteHelpers.Param(context, "key") ?? string.Empty;
            if (!_Authorization.CanReadTenant(ctx, tid))
            {
                await RouteHelpers.ErrorAsync(context, 403, "Forbidden", "Not permitted to read this tenant.").ConfigureAwait(false);
                return;
            }

            LockDefinition? definition = await _Database.LockDefinitions.ReadAsync(tid, key, context.Token).ConfigureAwait(false);
            List<LockHolder> holders = await _Database.LockHolders.EnumerateByKeyAsync(tid, key, context.Token).ConfigureAwait(false);
            await RouteHelpers.JsonAsync(context, 200, new { definition = definition, holders = holders }).ConfigureAwait(false);
        }

        private async Task ReleaseAsync(HttpContextBase context)
        {
            RequestContext ctx = RouteHelpers.Context(context);
            string tid = RouteHelpers.Param(context, "tid") ?? string.Empty;
            string key = RouteHelpers.Param(context, "key") ?? string.Empty;
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
                if (revoked != null) released++;
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
