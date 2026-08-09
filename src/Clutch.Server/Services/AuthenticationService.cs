namespace Clutch.Server.Services
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Clutch.Core.Database;
    using Clutch.Core.Enums;
    using Clutch.Core.Models;
    using Clutch.Core.Security;
    using Clutch.Server.Settings;
    using WatsonWebserver.Core;

    /// <summary>
    /// Resolves inbound requests to a typed <see cref="RequestContext"/> and issues session tokens.
    /// Supports admin API key, bearer/x-token session tokens, and access-key application credentials.
    /// </summary>
    public class AuthenticationService
    {
        #region Private-Members

        private readonly DatabaseDriverBase _Database;
        private readonly TokenService _TokenService;
        private readonly AuthSettings _AuthSettings;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="database">Database driver.</param>
        /// <param name="tokenService">Token service.</param>
        /// <param name="authSettings">Authentication settings.</param>
        /// <exception cref="ArgumentNullException">Thrown when a required argument is null.</exception>
        public AuthenticationService(DatabaseDriverBase database, TokenService tokenService, AuthSettings authSettings)
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));
            _TokenService = tokenService ?? throw new ArgumentNullException(nameof(tokenService));
            _AuthSettings = authSettings ?? throw new ArgumentNullException(nameof(authSettings));
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Watson authentication hook for authenticated (post-authentication) routes. On success, attaches
        /// a <see cref="RequestContext"/> to the context metadata. On failure, sends a 401 and stops routing.
        /// </summary>
        /// <param name="context">HTTP context.</param>
        /// <returns>Awaitable task.</returns>
        public async Task AuthenticateRequestAsync(HttpContextBase context)
        {
            RequestContext resolved = await ResolveAsync(context, context.Token).ConfigureAwait(false);
            if (!resolved.IsAuthenticated)
            {
                context.Response.StatusCode = 401;
                context.Response.ContentType = "application/json";
                await context.Response.Send("{\"error\":\"Unauthorized\",\"message\":\"Authentication required or invalid.\"}").ConfigureAwait(false);
                return;
            }

            context.Metadata = resolved;
        }

        /// <summary>
        /// Resolve a request to a request context without sending a response. Returns an unauthenticated
        /// context when no valid credential is present.
        /// </summary>
        /// <param name="context">HTTP context.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The resolved request context.</returns>
        public async Task<RequestContext> ResolveAsync(HttpContextBase context, CancellationToken token = default)
        {
            string? apiKey = context.Request.Headers["x-api-key"];
            if (!string.IsNullOrEmpty(apiKey))
            {
                if (PasswordHasher.FixedTimeEquals(apiKey, _AuthSettings.AdminApiKey))
                {
                    RequestContext adminContext = new RequestContext();
                    adminContext.IsAuthenticated = true;
                    adminContext.IsAdmin = true;
                    adminContext.PrincipalName = "System Administrator (api key)";
                    return adminContext;
                }
                return RequestContext.Unauthenticated();
            }

            string? bearer = ExtractBearer(context);
            if (!string.IsNullOrEmpty(bearer))
            {
                return await ResolveTokenAsync(bearer, token).ConfigureAwait(false);
            }

            string? accessKey = context.Request.Headers["x-access-key"];
            if (!string.IsNullOrEmpty(accessKey))
            {
                Credential? credential = await AuthenticateCredentialLoginAsync(accessKey, token).ConfigureAwait(false);
                if (credential != null) return await BuildCredentialContextAsync(credential, token).ConfigureAwait(false);
            }

            return RequestContext.Unauthenticated();
        }

        /// <summary>
        /// Authenticate a WebSocket upgrade request from the access-key header (or query parameter). The
        /// access key is the sole connect credential; a secret key is never accepted.
        /// </summary>
        /// <param name="context">HTTP context of the upgrade request.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The credential request context, or null if authentication failed.</returns>
        public async Task<RequestContext?> AuthenticateWebSocketAsync(HttpContextBase context, CancellationToken token = default)
        {
            string? accessKey = context.Request.Headers["x-clutch-access-key"];
            if (string.IsNullOrEmpty(accessKey)) accessKey = context.Request.Headers["x-access-key"];
            if (string.IsNullOrEmpty(accessKey)) accessKey = QueryValue(context, "accessKey");
            if (string.IsNullOrEmpty(accessKey)) return null;

            Credential? credential = await _Database.Credentials.ReadByAccessKeyAsync(accessKey, token).ConfigureAwait(false);
            if (credential == null || !credential.Active) return null;
            if (credential.ExpiresUtc.HasValue && credential.ExpiresUtc.Value < DateTime.UtcNow) return null;

            await _Database.Credentials.TouchLastUsedAsync(credential.Id, token).ConfigureAwait(false);
            return await BuildCredentialContextAsync(credential, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Validate a user login by tenant, email, and password.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="email">Email.</param>
        /// <param name="password">Plaintext password.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The user if valid, otherwise null.</returns>
        public async Task<User?> AuthenticateUserLoginAsync(string tenantId, string email, string password, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(tenantId) || string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password)) return null;

            User? user = await _Database.Users.ReadByEmailAsync(tenantId, email, token).ConfigureAwait(false);
            if (user == null || !user.Active) return null;
            if (!PasswordHasher.Verify(password, user.PasswordSha256)) return null;
            return user;
        }

        /// <summary>
        /// Validate a credential by its access key. The access key is the sole credential; no secret key
        /// is required or accepted.
        /// </summary>
        /// <param name="accessKey">Access key.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The credential if valid, otherwise null.</returns>
        public async Task<Credential?> AuthenticateCredentialLoginAsync(string accessKey, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(accessKey)) return null;

            Credential? credential = await _Database.Credentials.ReadByAccessKeyAsync(accessKey, token).ConfigureAwait(false);
            if (credential == null || !credential.Active) return null;
            if (credential.ExpiresUtc.HasValue && credential.ExpiresUtc.Value < DateTime.UtcNow) return null;

            await _Database.Credentials.TouchLastUsedAsync(credential.Id, token).ConfigureAwait(false);
            return credential;
        }

        /// <summary>
        /// Issue a session token for a user, creating a backing session record.
        /// </summary>
        /// <param name="user">Authenticated user.</param>
        /// <param name="sourceIp">Source IP, if known.</param>
        /// <param name="userAgent">User agent, if known.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The opaque token string.</returns>
        public async Task<string> IssueSessionForUserAsync(User user, string? sourceIp, string? userAgent, CancellationToken token = default)
        {
            if (user == null) throw new ArgumentNullException(nameof(user));

            AuthSession session = new AuthSession();
            session.TenantId = user.TenantId;
            session.UserId = user.Id;
            session.PrincipalType = PrincipalTypeEnum.User;
            session.SourceIp = sourceIp;
            session.UserAgent = userAgent;
            session.ExpiresUtc = DateTime.UtcNow.AddMinutes(_TokenService.LifetimeMinutes);
            session = await _Database.Sessions.CreateAsync(session, token).ConfigureAwait(false);

            TokenPayload payload = new TokenPayload();
            payload.SessionId = session.Id;
            payload.TokenId = session.TokenId;
            payload.PrincipalType = PrincipalTypeEnum.User;
            payload.TenantId = user.TenantId;
            payload.UserId = user.Id;
            payload.IssuedUtc = session.CreatedUtc;
            payload.ExpiresUtc = session.ExpiresUtc;
            return _TokenService.Issue(payload);
        }

        /// <summary>
        /// Issue a session token for a credential, creating a backing session record.
        /// </summary>
        /// <param name="credential">Authenticated credential.</param>
        /// <param name="sourceIp">Source IP, if known.</param>
        /// <param name="userAgent">User agent, if known.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The opaque token string.</returns>
        public async Task<string> IssueSessionForCredentialAsync(Credential credential, string? sourceIp, string? userAgent, CancellationToken token = default)
        {
            if (credential == null) throw new ArgumentNullException(nameof(credential));

            AuthSession session = new AuthSession();
            session.TenantId = credential.TenantId;
            session.CredentialId = credential.Id;
            session.PrincipalType = PrincipalTypeEnum.Credential;
            session.SourceIp = sourceIp;
            session.UserAgent = userAgent;
            session.ExpiresUtc = DateTime.UtcNow.AddMinutes(_TokenService.LifetimeMinutes);
            session = await _Database.Sessions.CreateAsync(session, token).ConfigureAwait(false);

            TokenPayload payload = new TokenPayload();
            payload.SessionId = session.Id;
            payload.TokenId = session.TokenId;
            payload.PrincipalType = PrincipalTypeEnum.Credential;
            payload.TenantId = credential.TenantId;
            payload.CredentialId = credential.Id;
            payload.IssuedUtc = session.CreatedUtc;
            payload.ExpiresUtc = session.ExpiresUtc;
            return _TokenService.Issue(payload);
        }

        /// <summary>
        /// Revoke the session backing a token.
        /// </summary>
        /// <param name="tokenString">The opaque token string.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if a session was revoked.</returns>
        public async Task<bool> RevokeTokenAsync(string tokenString, CancellationToken token = default)
        {
            TokenPayload? payload = _TokenService.Validate(tokenString);
            if (payload == null) return false;
            return await _Database.Sessions.RevokeAsync(payload.SessionId, "Logout.", token).ConfigureAwait(false);
        }

        #endregion

        #region Private-Methods

        private async Task<RequestContext> ResolveTokenAsync(string tokenString, CancellationToken token)
        {
            TokenPayload? payload = _TokenService.Validate(tokenString);
            if (payload == null) return RequestContext.Unauthenticated();

            AuthSession? session = await _Database.Sessions.ReadByTokenIdAsync(payload.TokenId, token).ConfigureAwait(false);
            if (session == null || !session.Active) return RequestContext.Unauthenticated();
            if (session.RevokedUtc.HasValue) return RequestContext.Unauthenticated();
            if (session.ExpiresUtc < DateTime.UtcNow) return RequestContext.Unauthenticated();
            if (session.TenantId != payload.TenantId) return RequestContext.Unauthenticated();

            await _Database.Sessions.TouchLastUsedAsync(session.Id, token).ConfigureAwait(false);

            if (payload.PrincipalType == PrincipalTypeEnum.User && !string.IsNullOrEmpty(payload.UserId))
            {
                User? user = await _Database.Users.ReadAsync(payload.TenantId, payload.UserId, token).ConfigureAwait(false);
                if (user == null || !user.Active) return RequestContext.Unauthenticated();

                RequestContext context = new RequestContext();
                context.IsAuthenticated = true;
                context.PrincipalType = PrincipalTypeEnum.User;
                context.TenantId = user.TenantId;
                context.UserId = user.Id;
                context.SessionId = session.Id;
                context.IsAdmin = user.IsSystemAdmin;
                context.IsTenantAdmin = user.IsTenantAdmin;
                context.PrincipalName = user.Email;
                context.User = user;
                return context;
            }

            if (payload.PrincipalType == PrincipalTypeEnum.Credential && !string.IsNullOrEmpty(payload.CredentialId))
            {
                Credential? credential = await _Database.Credentials.ReadAsync(payload.TenantId, payload.CredentialId, token).ConfigureAwait(false);
                if (credential == null || !credential.Active) return RequestContext.Unauthenticated();
                RequestContext context = await BuildCredentialContextAsync(credential, token).ConfigureAwait(false);
                context.SessionId = session.Id;
                return context;
            }

            return RequestContext.Unauthenticated();
        }

        private async Task<RequestContext> BuildCredentialContextAsync(Credential credential, CancellationToken token)
        {
            RequestContext context = new RequestContext();
            context.IsAuthenticated = true;
            context.PrincipalType = PrincipalTypeEnum.Credential;
            context.TenantId = credential.TenantId;
            context.CredentialId = credential.Id;
            context.PrincipalName = credential.Name;
            context.Credential = credential;

            // A credential inherits its owning user's admin tier when an owner is set.
            if (!string.IsNullOrEmpty(credential.UserId))
            {
                User? owner = await _Database.Users.ReadAsync(credential.TenantId, credential.UserId, token).ConfigureAwait(false);
                if (owner != null && owner.Active)
                {
                    context.IsAdmin = owner.IsSystemAdmin;
                    context.IsTenantAdmin = owner.IsTenantAdmin;
                }
            }

            return context;
        }

        private static string? QueryValue(HttpContextBase context, string key)
        {
            if (context.Request.Query == null || context.Request.Query.Elements == null) return null;
            return context.Request.Query.Elements[key];
        }

        private static string? ExtractBearer(HttpContextBase context)
        {
            string? authorization = context.Request.Headers["Authorization"];
            if (!string.IsNullOrEmpty(authorization))
            {
                if (authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    return authorization.Substring(7).Trim();
                }
                return authorization.Trim();
            }

            string? xToken = context.Request.Headers["x-token"];
            if (!string.IsNullOrEmpty(xToken)) return xToken.Trim();

            return null;
        }

        #endregion
    }
}
