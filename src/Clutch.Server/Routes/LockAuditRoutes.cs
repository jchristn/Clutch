namespace Clutch.Server.Routes
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Clutch.Core.Database;
    using Clutch.Core.Enums;
    using Clutch.Core.Requests;
    using Clutch.Core.Responses;
    using Clutch.Core.Security;
    using Clutch.Server.Services;
    using WatsonWebserver;
    using WatsonWebserver.Core;
    using WatsonWebserver.Core.OpenApi;

    /// <summary>
    /// Lock audit and lock-activity chart routes, scoped to a tenant.
    /// </summary>
    public class LockAuditRoutes
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
        public LockAuditRoutes(DatabaseDriverBase database, AuthorizationService authorization)
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
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1.0/api/tenants/{tid}/lock-audit", ListAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("List lock audit entries", "Lock Audit"));
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1.0/api/tenants/{tid}/lock-audit/summary", SummaryAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("Lock activity chart summary", "Lock Audit"));
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

            LockAuditFilter filter = new LockAuditFilter();
            filter.TenantId = tid;
            filter.LockKeyContains = RouteHelpers.Query(context, "name");
            LockModeEnum? mode = ParseMode(RouteHelpers.Query(context, "mode"));
            if (mode.HasValue) filter.Modes = new List<LockModeEnum> { mode.Value };
            filter.FromUtc = ParseDate(RouteHelpers.Query(context, "fromUtc"));
            filter.ToUtc = ParseDate(RouteHelpers.Query(context, "toUtc"));
            RouteHelpers.ApplyEnumeration(context, filter);

            var result = await _Database.LockAudit.EnumerateAsync(filter, context.Token).ConfigureAwait(false);
            await RouteHelpers.JsonAsync(context, 200, result).ConfigureAwait(false);
        }

        private async Task SummaryAsync(HttpContextBase context)
        {
            RequestContext ctx = RouteHelpers.Context(context);
            string tid = RouteHelpers.Param(context, "tid") ?? string.Empty;
            if (!_Authorization.CanReadTenant(ctx, tid))
            {
                await RouteHelpers.ErrorAsync(context, 403, "Forbidden", "Not permitted to read this tenant.").ConfigureAwait(false);
                return;
            }

            LockChartFilter filter = new LockChartFilter();
            filter.TenantId = tid;
            filter.LockNameContains = RouteHelpers.Query(context, "name");
            LockModeEnum? mode = ParseMode(RouteHelpers.Query(context, "mode"));
            if (mode.HasValue) filter.Modes = new List<LockModeEnum> { mode.Value };
            filter.FromUtc = ParseDate(RouteHelpers.Query(context, "fromUtc")) ?? DateTime.UtcNow.AddDays(-1);
            filter.ToUtc = ParseDate(RouteHelpers.Query(context, "toUtc")) ?? DateTime.UtcNow;
            filter.BucketCount = RouteHelpers.QueryInt(context, "bucketCount") ?? 96;

            LockChartSummary summary = await _Database.LockAudit.SummarizeAsync(filter, context.Token).ConfigureAwait(false);
            await RouteHelpers.JsonAsync(context, 200, summary).ConfigureAwait(false);
        }

        private static LockModeEnum? ParseMode(string? value)
        {
            if (string.IsNullOrEmpty(value)) return null;
            if (Enum.TryParse<LockModeEnum>(value, true, out LockModeEnum parsed)) return parsed;
            return null;
        }

        private static DateTime? ParseDate(string? value)
        {
            if (string.IsNullOrEmpty(value)) return null;
            if (DateTime.TryParse(value, null, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out DateTime parsed)) return parsed;
            return null;
        }

        #endregion
    }
}
