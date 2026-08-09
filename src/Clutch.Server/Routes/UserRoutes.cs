namespace Clutch.Server.Routes
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Clutch.Core.Database;
    using Clutch.Core.Models;
    using Clutch.Core.Requests;
    using Clutch.Core.Security;
    using Clutch.Server.Services;
    using WatsonWebserver;
    using WatsonWebserver.Core;
    using WatsonWebserver.Core.OpenApi;

    /// <summary>
    /// User administration routes, scoped to a tenant.
    /// </summary>
    public class UserRoutes
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
        public UserRoutes(DatabaseDriverBase database, AuthorizationService authorization)
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
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1.0/api/tenants/{tid}/users", ListAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("List users", "Users"));
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, "/v1.0/api/tenants/{tid}/users", CreateAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("Create a user", "Users"));
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1.0/api/tenants/{tid}/users/{id}", ReadAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("Read a user", "Users"));
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.PUT, "/v1.0/api/tenants/{tid}/users/{id}", UpdateAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("Update a user", "Users"));
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.DELETE, "/v1.0/api/tenants/{tid}/users/{id}", DeleteAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("Delete a user", "Users"));
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
            List<User> users = await _Database.Users.EnumerateAsync(tid, context.Token).ConfigureAwait(false);
            foreach (User user in users) user.PasswordSha256 = string.Empty;
            await RouteHelpers.JsonAsync(context, 200, users).ConfigureAwait(false);
        }

        private async Task CreateAsync(HttpContextBase context)
        {
            RequestContext ctx = RouteHelpers.Context(context);
            string tid = RouteHelpers.Param(context, "tid") ?? string.Empty;
            if (!_Authorization.CanAdministerTenant(ctx, tid))
            {
                await RouteHelpers.ErrorAsync(context, 403, "Forbidden", "Not permitted to manage users in this tenant.").ConfigureAwait(false);
                return;
            }
            CreateUserRequest? request = RouteHelpers.Body<CreateUserRequest>(context);
            if (request == null || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            {
                await RouteHelpers.ErrorAsync(context, 400, "BadRequest", "Email and password are required.").ConfigureAwait(false);
                return;
            }

            User user = new User();
            user.TenantId = tid;
            user.Email = request.Email;
            user.FirstName = request.FirstName;
            user.LastName = request.LastName;
            user.PasswordSha256 = PasswordHasher.Hash(request.Password!);
            user.IsSystemAdmin = ctx.IsAdmin && request.IsSystemAdmin;
            user.IsTenantAdmin = request.IsTenantAdmin;
            user.Active = request.Active;

            User created = await _Database.Users.CreateAsync(user, context.Token).ConfigureAwait(false);
            created.PasswordSha256 = string.Empty;
            await RouteHelpers.JsonAsync(context, 201, created).ConfigureAwait(false);
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
            User? user = await _Database.Users.ReadAsync(tid, id, context.Token).ConfigureAwait(false);
            if (user == null)
            {
                await RouteHelpers.ErrorAsync(context, 404, "NotFound", "User not found.").ConfigureAwait(false);
                return;
            }
            user.PasswordSha256 = string.Empty;
            await RouteHelpers.JsonAsync(context, 200, user).ConfigureAwait(false);
        }

        private async Task UpdateAsync(HttpContextBase context)
        {
            RequestContext ctx = RouteHelpers.Context(context);
            string tid = RouteHelpers.Param(context, "tid") ?? string.Empty;
            string id = RouteHelpers.Param(context, "id") ?? string.Empty;
            if (!_Authorization.CanAdministerTenant(ctx, tid))
            {
                await RouteHelpers.ErrorAsync(context, 403, "Forbidden", "Not permitted to manage users in this tenant.").ConfigureAwait(false);
                return;
            }
            User? existing = await _Database.Users.ReadAsync(tid, id, context.Token).ConfigureAwait(false);
            if (existing == null)
            {
                await RouteHelpers.ErrorAsync(context, 404, "NotFound", "User not found.").ConfigureAwait(false);
                return;
            }
            CreateUserRequest? request = RouteHelpers.Body<CreateUserRequest>(context);
            if (request == null)
            {
                await RouteHelpers.ErrorAsync(context, 400, "BadRequest", "A user body is required.").ConfigureAwait(false);
                return;
            }
            existing.Email = string.IsNullOrWhiteSpace(request.Email) ? existing.Email : request.Email;
            existing.FirstName = request.FirstName;
            existing.LastName = request.LastName;
            existing.IsTenantAdmin = request.IsTenantAdmin;
            if (ctx.IsAdmin) existing.IsSystemAdmin = request.IsSystemAdmin;
            existing.Active = request.Active;
            if (!string.IsNullOrEmpty(request.Password)) existing.PasswordSha256 = PasswordHasher.Hash(request.Password!);

            User saved = await _Database.Users.UpdateAsync(existing, context.Token).ConfigureAwait(false);
            saved.PasswordSha256 = string.Empty;
            await RouteHelpers.JsonAsync(context, 200, saved).ConfigureAwait(false);
        }

        private async Task DeleteAsync(HttpContextBase context)
        {
            RequestContext ctx = RouteHelpers.Context(context);
            string tid = RouteHelpers.Param(context, "tid") ?? string.Empty;
            string id = RouteHelpers.Param(context, "id") ?? string.Empty;
            if (!_Authorization.CanAdministerTenant(ctx, tid))
            {
                await RouteHelpers.ErrorAsync(context, 403, "Forbidden", "Not permitted to manage users in this tenant.").ConfigureAwait(false);
                return;
            }
            bool deleted = await _Database.Users.DeleteAsync(tid, id, context.Token).ConfigureAwait(false);
            if (!deleted)
            {
                await RouteHelpers.ErrorAsync(context, 404, "NotFound", "User not found.").ConfigureAwait(false);
                return;
            }
            context.Response.StatusCode = 204;
            await context.Response.Send().ConfigureAwait(false);
        }

        #endregion
    }
}
