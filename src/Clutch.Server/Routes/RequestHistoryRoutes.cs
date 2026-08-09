namespace Clutch.Server.Routes
{
    using System;
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
    /// Request history routes. Tenant-scoped from the request context; a system administrator may widen
    /// the scope with a tenantId query parameter.
    /// </summary>
    public class RequestHistoryRoutes
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
        public RequestHistoryRoutes(DatabaseDriverBase database, AuthorizationService authorization)
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
            server.Routes.PostAuthentication.Static.Add(HttpMethod.GET, "/v1.0/api/request-history", ListAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("List request history", "Request History"));
            server.Routes.PostAuthentication.Static.Add(HttpMethod.GET, "/v1.0/api/request-history/summary", SummaryAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("Request history summary", "Request History"));
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1.0/api/request-history/{id}", ReadAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("Read a request history entry", "Request History"));
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.DELETE, "/v1.0/api/request-history/{id}", DeleteAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("Delete a request history entry", "Request History"));
            server.Routes.PostAuthentication.Static.Add(HttpMethod.DELETE, "/v1.0/api/request-history", DeleteManyAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("Bulk delete request history", "Request History"));
        }

        #endregion

        #region Private-Methods

        private RequestHistoryFilter BuildFilter(HttpContextBase context, RequestContext ctx)
        {
            RequestHistoryFilter filter = new RequestHistoryFilter();
            filter.TenantId = _Authorization.ResolveTenantScope(ctx, RouteHelpers.Query(context, "tenantId"));
            filter.Method = RouteHelpers.Query(context, "method");
            filter.StatusCode = RouteHelpers.QueryInt(context, "statusCode");
            filter.PathContains = RouteHelpers.Query(context, "pathContains");
            filter.FromUtc = ParseDate(RouteHelpers.Query(context, "fromUtc"));
            filter.ToUtc = ParseDate(RouteHelpers.Query(context, "toUtc"));
            filter.PageNumber = RouteHelpers.QueryInt(context, "pageNumber") ?? 1;
            filter.PageSize = RouteHelpers.QueryInt(context, "pageSize") ?? 25;
            filter.BucketMinutes = RouteHelpers.QueryInt(context, "bucketMinutes") ?? 15;
            return filter;
        }

        private async Task ListAsync(HttpContextBase context)
        {
            RequestContext ctx = RouteHelpers.Context(context);
            RequestHistoryFilter filter = BuildFilter(context, ctx);
            RequestHistoryPage page = await _Database.RequestHistory.EnumerateAsync(filter, context.Token).ConfigureAwait(false);
            await RouteHelpers.JsonAsync(context, 200, page).ConfigureAwait(false);
        }

        private async Task SummaryAsync(HttpContextBase context)
        {
            RequestContext ctx = RouteHelpers.Context(context);
            RequestHistoryFilter filter = BuildFilter(context, ctx);
            RequestHistorySummary summary = await _Database.RequestHistory.SummarizeAsync(filter, context.Token).ConfigureAwait(false);
            await RouteHelpers.JsonAsync(context, 200, summary).ConfigureAwait(false);
        }

        private async Task ReadAsync(HttpContextBase context)
        {
            RequestContext ctx = RouteHelpers.Context(context);
            string id = RouteHelpers.Param(context, "id") ?? string.Empty;
            string? scope = ctx.IsAdmin ? null : ctx.TenantId;
            RequestHistoryEntry? entry = await _Database.RequestHistory.ReadAsync(scope, id, context.Token).ConfigureAwait(false);
            if (entry == null)
            {
                await RouteHelpers.ErrorAsync(context, 404, "NotFound", "Request history entry not found.").ConfigureAwait(false);
                return;
            }
            await RouteHelpers.JsonAsync(context, 200, entry).ConfigureAwait(false);
        }

        private async Task DeleteAsync(HttpContextBase context)
        {
            RequestContext ctx = RouteHelpers.Context(context);
            string id = RouteHelpers.Param(context, "id") ?? string.Empty;
            string? scope = ctx.IsAdmin ? null : ctx.TenantId;
            bool deleted = await _Database.RequestHistory.DeleteAsync(scope, id, context.Token).ConfigureAwait(false);
            if (!deleted)
            {
                await RouteHelpers.ErrorAsync(context, 404, "NotFound", "Request history entry not found.").ConfigureAwait(false);
                return;
            }
            context.Response.StatusCode = 204;
            await context.Response.Send().ConfigureAwait(false);
        }

        private async Task DeleteManyAsync(HttpContextBase context)
        {
            RequestContext ctx = RouteHelpers.Context(context);
            RequestHistoryFilter filter = BuildFilter(context, ctx);
            int deleted = await _Database.RequestHistory.DeleteManyAsync(filter, context.Token).ConfigureAwait(false);
            await RouteHelpers.JsonAsync(context, 200, new { deletedCount = deleted }).ConfigureAwait(false);
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
