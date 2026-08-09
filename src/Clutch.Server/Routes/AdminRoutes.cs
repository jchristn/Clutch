namespace Clutch.Server.Routes
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Clutch.Core.Database;
    using Clutch.Core.Models;
    using Clutch.Core.Requests;
    using Clutch.Core.Responses;
    using Clutch.Core.Security;
    using Clutch.Server.Services;
    using WatsonWebserver;
    using WatsonWebserver.Core;
    using WatsonWebserver.Core.OpenApi;

    /// <summary>
    /// System-administrator-only destructive operations.
    /// </summary>
    public class AdminRoutes
    {
        #region Private-Members

        private const int _MinimumReasonLength = 10;

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
        public AdminRoutes(DatabaseDriverBase database, AuthorizationService authorization)
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
            server.Routes.PostAuthentication.Static.Add(HttpMethod.POST, "/v1.0/api/admin/nuke/tenant", NukeTenantAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("Nuke a tenant", "Administration"));
        }

        #endregion

        #region Private-Methods

        private async Task NukeTenantAsync(HttpContextBase context)
        {
            RequestContext ctx = RouteHelpers.Context(context);
            if (!_Authorization.CanManageTenants(ctx))
            {
                await RouteHelpers.ErrorAsync(context, 403, "Forbidden", "Only a system administrator may nuke a tenant.").ConfigureAwait(false);
                return;
            }

            NukeTenantRequest? request = RouteHelpers.Body<NukeTenantRequest>(context);
            if (request == null || string.IsNullOrWhiteSpace(request.TenantId))
            {
                await RouteHelpers.ErrorAsync(context, 400, "BadRequest", "A tenant identifier is required.").ConfigureAwait(false);
                return;
            }
            if (!string.Equals(request.TenantId, request.ConfirmTenantId, StringComparison.Ordinal))
            {
                await RouteHelpers.ErrorAsync(context, 400, "BadRequest", "The confirmation tenant identifier does not match.").ConfigureAwait(false);
                return;
            }
            if (request.Reason == null || request.Reason.Trim().Length < _MinimumReasonLength)
            {
                await RouteHelpers.ErrorAsync(context, 400, "BadRequest", "An administrative reason of at least ten characters is required.").ConfigureAwait(false);
                return;
            }

            Tenant? tenant = await _Database.Tenants.ReadAsync(request.TenantId, context.Token).ConfigureAwait(false);
            if (tenant == null)
            {
                await RouteHelpers.ErrorAsync(context, 404, "NotFound", "Tenant not found.").ConfigureAwait(false);
                return;
            }
            if (tenant.IsProtected)
            {
                await RouteHelpers.ErrorAsync(context, 409, "ProtectedTenant", "This tenant is protected and cannot be nuked.").ConfigureAwait(false);
                return;
            }

            DateTime started = DateTime.UtcNow;
            Dictionary<string, long> deleted = await _Database.Tenants
                .NukeAsync(request.TenantId, request.IncludeAuditRecords, request.IncludeRequestHistory, context.Token)
                .ConfigureAwait(false);

            NukeTenantResult result = new NukeTenantResult
            {
                TenantId = tenant.Id,
                TenantName = tenant.Name,
                Reason = request.Reason.Trim(),
                Deleted = deleted,
                StartedUtc = started,
                CompletedUtc = DateTime.UtcNow
            };

            await RouteHelpers.JsonAsync(context, 200, result).ConfigureAwait(false);
        }

        #endregion
    }
}
