namespace Clutch.Server.Routes
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Clutch.Core.Database;
    using Clutch.Core.Enumeration;
    using Clutch.Core.Models;
    using Clutch.Core.Requests;
    using Clutch.Core.Responses;
    using Clutch.Core.Security;
    using Clutch.Server.Services;
    using WatsonWebserver;
    using WatsonWebserver.Core;
    using WatsonWebserver.Core.OpenApi;

    /// <summary>
    /// Application key (credential) administration routes, scoped to a tenant.
    /// </summary>
    public class CredentialRoutes
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
        public CredentialRoutes(DatabaseDriverBase database, AuthorizationService authorization)
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
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1.0/api/tenants/{tid}/credentials", ListAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("List application keys", "Credentials"));
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, "/v1.0/api/tenants/{tid}/credentials", CreateAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("Create an application key", "Credentials"));
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1.0/api/tenants/{tid}/credentials/{id}", ReadAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("Read an application key", "Credentials"));
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.DELETE, "/v1.0/api/tenants/{tid}/credentials/{id}", DeleteAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("Delete an application key", "Credentials"));
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
            EnumerationResult<Credential> result = await _Database.Credentials.EnumerateAsync(tid, RouteHelpers.Enumeration(context), context.Token).ConfigureAwait(false);
            List<CredentialResponse> projected = new List<CredentialResponse>();
            foreach (Credential credential in result.Objects) projected.Add(CredentialResponse.FromModel(credential));
            EnumerationResult<CredentialResponse> response = new EnumerationResult<CredentialResponse>
            {
                Success = result.Success,
                MaxResults = result.MaxResults,
                Skip = result.Skip,
                TotalRecords = result.TotalRecords,
                RecordsRemaining = result.RecordsRemaining,
                EndOfResults = result.EndOfResults,
                TimestampUtc = result.TimestampUtc,
                Objects = projected
            };
            await RouteHelpers.JsonAsync(context, 200, response).ConfigureAwait(false);
        }

        private async Task CreateAsync(HttpContextBase context)
        {
            RequestContext ctx = RouteHelpers.Context(context);
            string tid = RouteHelpers.Param(context, "tid") ?? string.Empty;
            if (!_Authorization.CanAdministerTenant(ctx, tid))
            {
                await RouteHelpers.ErrorAsync(context, 403, "Forbidden", "Not permitted to manage application keys in this tenant.").ConfigureAwait(false);
                return;
            }
            CreateCredentialRequest? request = RouteHelpers.Body<CreateCredentialRequest>(context);
            if (request == null || string.IsNullOrWhiteSpace(request.Name))
            {
                await RouteHelpers.ErrorAsync(context, 400, "BadRequest", "A credential name is required.").ConfigureAwait(false);
                return;
            }

            Credential credential = new Credential();
            credential.TenantId = tid;
            credential.UserId = request.UserId;
            credential.Name = request.Name;
            credential.AccessKey = CredentialKeyGenerator.GenerateAccessKey();
            credential.ExpiresUtc = request.ExpiresUtc;

            Credential created = await _Database.Credentials.CreateAsync(credential, context.Token).ConfigureAwait(false);
            await RouteHelpers.JsonAsync(context, 201, CredentialResponse.FromModel(created)).ConfigureAwait(false);
        }

        private async Task ReadAsync(HttpContextBase context)
        {
            RequestContext ctx = RouteHelpers.Context(context);
            string tid = RouteHelpers.Param(context, "tid") ?? string.Empty;
            string id = RouteHelpers.Param(context, "id") ?? string.Empty;
            if (!_Authorization.CanReadTenant(ctx, tid))
            {
                await RouteHelpers.ErrorAsync(context, 403, "Forbidden", "Not permitted to read this tenant.").ConfigureAwait(false);
                return;
            }
            Credential? credential = await _Database.Credentials.ReadAsync(tid, id, context.Token).ConfigureAwait(false);
            if (credential == null)
            {
                await RouteHelpers.ErrorAsync(context, 404, "NotFound", "Application key not found.").ConfigureAwait(false);
                return;
            }
            await RouteHelpers.JsonAsync(context, 200, CredentialResponse.FromModel(credential)).ConfigureAwait(false);
        }

        private async Task DeleteAsync(HttpContextBase context)
        {
            RequestContext ctx = RouteHelpers.Context(context);
            string tid = RouteHelpers.Param(context, "tid") ?? string.Empty;
            string id = RouteHelpers.Param(context, "id") ?? string.Empty;
            if (!_Authorization.CanAdministerTenant(ctx, tid))
            {
                await RouteHelpers.ErrorAsync(context, 403, "Forbidden", "Not permitted to manage application keys in this tenant.").ConfigureAwait(false);
                return;
            }
            bool deleted = await _Database.Credentials.DeleteAsync(tid, id, context.Token).ConfigureAwait(false);
            if (!deleted)
            {
                await RouteHelpers.ErrorAsync(context, 404, "NotFound", "Application key not found.").ConfigureAwait(false);
                return;
            }
            context.Response.StatusCode = 204;
            await context.Response.Send().ConfigureAwait(false);
        }

        #endregion
    }
}
