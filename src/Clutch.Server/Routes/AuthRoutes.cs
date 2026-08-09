namespace Clutch.Server.Routes
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Clutch.Core.Models;
    using Clutch.Core.Requests;
    using Clutch.Core.Security;
    using Clutch.Server.Services;
    using WatsonWebserver;
    using WatsonWebserver.Core;
    using WatsonWebserver.Core.OpenApi;

    /// <summary>
    /// Token routes: login (anonymous), validate, details, and logout.
    /// </summary>
    public class AuthRoutes
    {
        #region Private-Members

        private readonly AuthenticationService _Authentication;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="authentication">Authentication service.</param>
        /// <exception cref="ArgumentNullException">Thrown when authentication is null.</exception>
        public AuthRoutes(AuthenticationService authentication)
        {
            _Authentication = authentication ?? throw new ArgumentNullException(nameof(authentication));
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Register routes.
        /// </summary>
        /// <param name="server">Webserver.</param>
        public void Register(Webserver server)
        {
            server.Routes.PreAuthentication.Static.Add(
                HttpMethod.POST, "/v1.0/token", LoginAsync, null,
                openApiMetadata: OpenApiRouteMetadata.Create("Create a session token", "Auth"));
            server.Routes.PostAuthentication.Static.Add(
                HttpMethod.GET, "/v1.0/token", ValidateAsync, null,
                openApiMetadata: OpenApiRouteMetadata.Create("Validate the current token", "Auth"));
            server.Routes.PostAuthentication.Static.Add(
                HttpMethod.GET, "/v1.0/token/details", DetailsAsync, null,
                openApiMetadata: OpenApiRouteMetadata.Create("Return the current principal details", "Auth"));
            server.Routes.PostAuthentication.Static.Add(
                HttpMethod.DELETE, "/v1.0/token", LogoutAsync, null,
                openApiMetadata: OpenApiRouteMetadata.Create("Revoke the current token", "Auth"));
        }

        #endregion

        #region Private-Methods

        private async Task LoginAsync(HttpContextBase context)
        {
            LoginRequest? request = RouteHelpers.Body<LoginRequest>(context);
            if (request == null)
            {
                await RouteHelpers.ErrorAsync(context, 400, "BadRequest", "A login body is required.").ConfigureAwait(false);
                return;
            }

            string? userAgent = context.Request.Headers["User-Agent"];

            if (!string.IsNullOrEmpty(request.AccessKey))
            {
                Credential? credential = await _Authentication.AuthenticateCredentialLoginAsync(request.AccessKey, context.Token).ConfigureAwait(false);
                if (credential == null)
                {
                    await RouteHelpers.ErrorAsync(context, 401, "Unauthorized", "Invalid access key.").ConfigureAwait(false);
                    return;
                }
                string token = await _Authentication.IssueSessionForCredentialAsync(credential, null, userAgent, context.Token).ConfigureAwait(false);
                await RouteHelpers.JsonAsync(context, 200, BuildTokenResponse(token, "Credential", credential.TenantId)).ConfigureAwait(false);
                return;
            }

            if (!string.IsNullOrEmpty(request.Email) && !string.IsNullOrEmpty(request.Password) && !string.IsNullOrEmpty(request.TenantId))
            {
                User? user = await _Authentication.AuthenticateUserLoginAsync(request.TenantId, request.Email, request.Password, context.Token).ConfigureAwait(false);
                if (user == null)
                {
                    await RouteHelpers.ErrorAsync(context, 401, "Unauthorized", "Invalid tenant, email, or password.").ConfigureAwait(false);
                    return;
                }
                string token = await _Authentication.IssueSessionForUserAsync(user, null, userAgent, context.Token).ConfigureAwait(false);
                await RouteHelpers.JsonAsync(context, 200, BuildTokenResponse(token, "User", user.TenantId)).ConfigureAwait(false);
                return;
            }

            await RouteHelpers.ErrorAsync(context, 400, "BadRequest", "Provide an accessKey, or tenantId/email/password.").ConfigureAwait(false);
        }

        private async Task ValidateAsync(HttpContextBase context)
        {
            RequestContext ctx = RouteHelpers.Context(context);
            await RouteHelpers.JsonAsync(context, 200, BuildContextResponse(ctx)).ConfigureAwait(false);
        }

        private async Task DetailsAsync(HttpContextBase context)
        {
            RequestContext ctx = RouteHelpers.Context(context);
            await RouteHelpers.JsonAsync(context, 200, BuildContextResponse(ctx)).ConfigureAwait(false);
        }

        private async Task LogoutAsync(HttpContextBase context)
        {
            string? authorization = context.Request.Headers["Authorization"];
            string tokenString = string.Empty;
            if (!string.IsNullOrEmpty(authorization))
            {
                tokenString = authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? authorization.Substring(7).Trim() : authorization.Trim();
            }
            else
            {
                string? xToken = context.Request.Headers["x-token"];
                if (!string.IsNullOrEmpty(xToken)) tokenString = xToken.Trim();
            }

            if (!string.IsNullOrEmpty(tokenString)) await _Authentication.RevokeTokenAsync(tokenString, context.Token).ConfigureAwait(false);
            context.Response.StatusCode = 204;
            await context.Response.Send().ConfigureAwait(false);
        }

        private static Dictionary<string, object?> BuildTokenResponse(string token, string principalType, string tenantId)
        {
            Dictionary<string, object?> response = new Dictionary<string, object?>();
            response["token"] = token;
            response["principalType"] = principalType;
            response["tenantId"] = tenantId;
            return response;
        }

        private static Dictionary<string, object?> BuildContextResponse(RequestContext ctx)
        {
            Dictionary<string, object?> response = new Dictionary<string, object?>();
            response["authenticated"] = ctx.IsAuthenticated;
            response["principalType"] = ctx.PrincipalType?.ToString();
            response["tenantId"] = ctx.TenantId;
            response["userId"] = ctx.UserId;
            response["credentialId"] = ctx.CredentialId;
            response["isAdmin"] = ctx.IsAdmin;
            response["isTenantAdmin"] = ctx.IsTenantAdmin;
            response["principalName"] = ctx.PrincipalName;
            return response;
        }

        #endregion
    }
}
