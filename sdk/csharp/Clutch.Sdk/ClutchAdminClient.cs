namespace Clutch.Sdk
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Client for the Clutch REST API, which handles administration and observability: tokens, tenants, users,
    /// application keys, lock inspection, audit, request history, health, and server info.
    /// </summary>
    /// <remarks>
    /// This client is safe to reuse for multiple requests. It implements <see cref="IDisposable"/> to release the
    /// underlying HTTP resources. Lock acquisition itself is performed over the WebSocket API via <see cref="ClutchLockClient"/>.
    /// </remarks>
    public class ClutchAdminClient : IDisposable
    {
        private readonly string _Endpoint;
        private readonly HttpClient _HttpClient;
        private readonly bool _OwnsHttpClient;
        private readonly JsonSerializerOptions _JsonOptions;
        private string? _Token;
        private bool _Disposed;

        /// <summary>
        /// The bearer token currently used for authenticated requests, or null when not authenticated.
        /// Set automatically by the authenticate methods; may also be assigned directly.
        /// </summary>
        public string? Token
        {
            get
            {
                return _Token;
            }
            set
            {
                _Token = value;
            }
        }

        /// <summary>
        /// The base endpoint the client targets.
        /// </summary>
        public string Endpoint
        {
            get
            {
                return _Endpoint;
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ClutchAdminClient"/> class.
        /// </summary>
        /// <param name="endpoint">The base URL of the Clutch server, for example "http://127.0.0.1:8090".</param>
        /// <param name="token">An optional pre-existing bearer token.</param>
        /// <param name="httpClient">An optional <see cref="HttpClient"/> to use. When null, one is created and owned by this instance.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="endpoint"/> is null.</exception>
        public ClutchAdminClient(string endpoint, string? token = null, HttpClient? httpClient = null)
        {
            if (endpoint == null) throw new ArgumentNullException(nameof(endpoint));

            _Endpoint = endpoint.TrimEnd('/');
            _Token = token;

            if (httpClient != null)
            {
                _HttpClient = httpClient;
                _OwnsHttpClient = false;
            }
            else
            {
                _HttpClient = new HttpClient();
                _OwnsHttpClient = true;
            }

            _HttpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            _JsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
            _JsonOptions.Converters.Add(new JsonStringEnumConverter());
        }

        #region Tokens

        /// <summary>
        /// Authenticates using an application key. On success the resulting token is stored on <see cref="Token"/>.
        /// </summary>
        /// <param name="accessKey">The access key.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>The token response, including the bearer token and resolved principal context.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="accessKey"/> is null.</exception>
        /// <exception cref="ClutchException">Thrown when authentication fails.</exception>
        public async Task<TokenResponse> AuthenticateWithKeyAsync(string accessKey, CancellationToken cancellationToken = default)
        {
            if (accessKey == null) throw new ArgumentNullException(nameof(accessKey));

            object body = new { accessKey = accessKey };
            TokenResponse response = await SendAsync<TokenResponse>(HttpMethod.Post, "/v1.0/token", body, false, cancellationToken).ConfigureAwait(false);
            _Token = response.Token;
            return response;
        }

        /// <summary>
        /// Authenticates using user credentials. On success the resulting token is stored on <see cref="Token"/>.
        /// </summary>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <param name="email">The user's email address.</param>
        /// <param name="password">The user's password.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>The token response, including the bearer token and resolved principal context.</returns>
        /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
        /// <exception cref="ClutchException">Thrown when authentication fails.</exception>
        public async Task<TokenResponse> AuthenticateWithPasswordAsync(string tenantId, string email, string password, CancellationToken cancellationToken = default)
        {
            if (tenantId == null) throw new ArgumentNullException(nameof(tenantId));
            if (email == null) throw new ArgumentNullException(nameof(email));
            if (password == null) throw new ArgumentNullException(nameof(password));

            object body = new { tenantId = tenantId, email = email, password = password };
            TokenResponse response = await SendAsync<TokenResponse>(HttpMethod.Post, "/v1.0/token", body, false, cancellationToken).ConfigureAwait(false);
            _Token = response.Token;
            return response;
        }

        /// <summary>
        /// Validates the current bearer token and returns the resolved principal context.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>The token details.</returns>
        /// <exception cref="ClutchException">Thrown when the request fails.</exception>
        public async Task<TokenDetails> GetTokenDetailsAsync(CancellationToken cancellationToken = default)
        {
            return await SendAsync<TokenDetails>(HttpMethod.Get, "/v1.0/token", null, true, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Revokes the current session (logout) and clears <see cref="Token"/>.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <exception cref="ClutchException">Thrown when the request fails.</exception>
        public async Task LogoutAsync(CancellationToken cancellationToken = default)
        {
            await SendAsync(HttpMethod.Delete, "/v1.0/token", null, true, cancellationToken).ConfigureAwait(false);
            _Token = null;
        }

        #endregion

        #region Health and server info

        /// <summary>
        /// Retrieves server health. This endpoint is anonymous.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>The health response.</returns>
        /// <exception cref="ClutchException">Thrown when the request fails.</exception>
        public async Task<HealthResponse> GetHealthAsync(CancellationToken cancellationToken = default)
        {
            return await SendAsync<HealthResponse>(HttpMethod.Get, "/v1.0/api/health", null, false, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Retrieves information about the running server.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>The server info.</returns>
        /// <exception cref="ClutchException">Thrown when the request fails.</exception>
        public async Task<ServerInfo> GetServerInfoAsync(CancellationToken cancellationToken = default)
        {
            return await SendAsync<ServerInfo>(HttpMethod.Get, "/v1.0/api/server-info", null, true, cancellationToken).ConfigureAwait(false);
        }

        #endregion

        #region Tenants

        /// <summary>
        /// Lists tenants. A system administrator sees all tenants; other principals see only their own.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>The list of tenants.</returns>
        /// <exception cref="ClutchException">Thrown when the request fails.</exception>
        public async Task<List<Tenant>> ListTenantsAsync(CancellationToken cancellationToken = default)
        {
            return await SendAsync<List<Tenant>>(HttpMethod.Get, "/v1.0/api/tenants", null, true, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Retrieves a single tenant.
        /// </summary>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>The tenant.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="tenantId"/> is null.</exception>
        /// <exception cref="ClutchException">Thrown when the request fails.</exception>
        public async Task<Tenant> GetTenantAsync(string tenantId, CancellationToken cancellationToken = default)
        {
            if (tenantId == null) throw new ArgumentNullException(nameof(tenantId));
            return await SendAsync<Tenant>(HttpMethod.Get, $"/v1.0/api/tenants/{Uri.EscapeDataString(tenantId)}", null, true, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a tenant. Requires a system administrator.
        /// </summary>
        /// <param name="name">The tenant name.</param>
        /// <param name="lockHistoryRetentionDays">The number of days to retain lock history. When null, the server default applies.</param>
        /// <param name="defaultLeaseMs">The default lease duration, in milliseconds. When null, the server default applies.</param>
        /// <param name="maxLeaseMs">The maximum lease duration, in milliseconds. When null, the server default applies.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>The created tenant.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is null.</exception>
        /// <exception cref="ClutchException">Thrown when the request fails.</exception>
        public async Task<Tenant> CreateTenantAsync(string name, int? lockHistoryRetentionDays = null, int? defaultLeaseMs = null, int? maxLeaseMs = null, CancellationToken cancellationToken = default)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));
            object body = new { name = name, lockHistoryRetentionDays = lockHistoryRetentionDays, defaultLeaseMs = defaultLeaseMs, maxLeaseMs = maxLeaseMs };
            return await SendAsync<Tenant>(HttpMethod.Post, "/v1.0/api/tenants", body, true, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Updates a tenant. Requires a system administrator or the tenant's administrator.
        /// </summary>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <param name="name">The new name, or null to leave unchanged.</param>
        /// <param name="lockHistoryRetentionDays">The new retention days, or null to leave unchanged.</param>
        /// <param name="defaultLeaseMs">The new default lease, or null to leave unchanged.</param>
        /// <param name="maxLeaseMs">The new maximum lease, or null to leave unchanged.</param>
        /// <param name="active">The new active flag, or null to leave unchanged.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>The updated tenant.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="tenantId"/> is null.</exception>
        /// <exception cref="ClutchException">Thrown when the request fails.</exception>
        public async Task<Tenant> UpdateTenantAsync(string tenantId, string? name = null, int? lockHistoryRetentionDays = null, int? defaultLeaseMs = null, int? maxLeaseMs = null, bool? active = null, CancellationToken cancellationToken = default)
        {
            if (tenantId == null) throw new ArgumentNullException(nameof(tenantId));
            object body = new { name = name, lockHistoryRetentionDays = lockHistoryRetentionDays, defaultLeaseMs = defaultLeaseMs, maxLeaseMs = maxLeaseMs, active = active };
            return await SendAsync<Tenant>(HttpMethod.Put, $"/v1.0/api/tenants/{Uri.EscapeDataString(tenantId)}", body, true, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Deletes a tenant. Requires a system administrator. Cascades to users, credentials, locks, and audit.
        /// </summary>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="tenantId"/> is null.</exception>
        /// <exception cref="ClutchException">Thrown when the request fails.</exception>
        public async Task DeleteTenantAsync(string tenantId, CancellationToken cancellationToken = default)
        {
            if (tenantId == null) throw new ArgumentNullException(nameof(tenantId));
            await SendAsync(HttpMethod.Delete, $"/v1.0/api/tenants/{Uri.EscapeDataString(tenantId)}", null, true, cancellationToken).ConfigureAwait(false);
        }

        #endregion

        #region Users

        /// <summary>
        /// Lists users within a tenant.
        /// </summary>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>The list of users.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="tenantId"/> is null.</exception>
        /// <exception cref="ClutchException">Thrown when the request fails.</exception>
        public async Task<List<User>> ListUsersAsync(string tenantId, CancellationToken cancellationToken = default)
        {
            if (tenantId == null) throw new ArgumentNullException(nameof(tenantId));
            return await SendAsync<List<User>>(HttpMethod.Get, $"/v1.0/api/tenants/{Uri.EscapeDataString(tenantId)}/users", null, true, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Retrieves a single user within a tenant.
        /// </summary>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <param name="userId">The user identifier.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>The user.</returns>
        /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
        /// <exception cref="ClutchException">Thrown when the request fails.</exception>
        public async Task<User> GetUserAsync(string tenantId, string userId, CancellationToken cancellationToken = default)
        {
            if (tenantId == null) throw new ArgumentNullException(nameof(tenantId));
            if (userId == null) throw new ArgumentNullException(nameof(userId));
            return await SendAsync<User>(HttpMethod.Get, $"/v1.0/api/tenants/{Uri.EscapeDataString(tenantId)}/users/{Uri.EscapeDataString(userId)}", null, true, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a user within a tenant. Requires a tenant administrator. Only a system administrator may set <paramref name="isSystemAdmin"/>.
        /// </summary>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <param name="email">The user's email address.</param>
        /// <param name="password">The user's password.</param>
        /// <param name="firstName">The user's first name.</param>
        /// <param name="lastName">The user's last name.</param>
        /// <param name="isSystemAdmin">Whether the user is a system administrator.</param>
        /// <param name="isTenantAdmin">Whether the user is a tenant administrator.</param>
        /// <param name="active">Whether the user is active.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>The created user.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="tenantId"/>, <paramref name="email"/>, or <paramref name="password"/> is null.</exception>
        /// <exception cref="ClutchException">Thrown when the request fails.</exception>
        public async Task<User> CreateUserAsync(string tenantId, string email, string password, string? firstName = null, string? lastName = null, bool isSystemAdmin = false, bool isTenantAdmin = false, bool active = true, CancellationToken cancellationToken = default)
        {
            if (tenantId == null) throw new ArgumentNullException(nameof(tenantId));
            if (email == null) throw new ArgumentNullException(nameof(email));
            if (password == null) throw new ArgumentNullException(nameof(password));
            object body = new { email = email, password = password, firstName = firstName, lastName = lastName, isSystemAdmin = isSystemAdmin, isTenantAdmin = isTenantAdmin, active = active };
            return await SendAsync<User>(HttpMethod.Post, $"/v1.0/api/tenants/{Uri.EscapeDataString(tenantId)}/users", body, true, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Updates a user within a tenant. Only supplied values are changed.
        /// </summary>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <param name="userId">The user identifier.</param>
        /// <param name="email">The new email, or null to leave unchanged.</param>
        /// <param name="password">The new password, or null to leave unchanged.</param>
        /// <param name="firstName">The new first name, or null to leave unchanged.</param>
        /// <param name="lastName">The new last name, or null to leave unchanged.</param>
        /// <param name="isTenantAdmin">The new tenant-admin flag, or null to leave unchanged.</param>
        /// <param name="active">The new active flag, or null to leave unchanged.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>The updated user.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="tenantId"/> or <paramref name="userId"/> is null.</exception>
        /// <exception cref="ClutchException">Thrown when the request fails.</exception>
        public async Task<User> UpdateUserAsync(string tenantId, string userId, string? email = null, string? password = null, string? firstName = null, string? lastName = null, bool? isTenantAdmin = null, bool? active = null, CancellationToken cancellationToken = default)
        {
            if (tenantId == null) throw new ArgumentNullException(nameof(tenantId));
            if (userId == null) throw new ArgumentNullException(nameof(userId));
            object body = new { email = email, password = password, firstName = firstName, lastName = lastName, isTenantAdmin = isTenantAdmin, active = active };
            return await SendAsync<User>(HttpMethod.Put, $"/v1.0/api/tenants/{Uri.EscapeDataString(tenantId)}/users/{Uri.EscapeDataString(userId)}", body, true, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Deletes a user within a tenant.
        /// </summary>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <param name="userId">The user identifier.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
        /// <exception cref="ClutchException">Thrown when the request fails.</exception>
        public async Task DeleteUserAsync(string tenantId, string userId, CancellationToken cancellationToken = default)
        {
            if (tenantId == null) throw new ArgumentNullException(nameof(tenantId));
            if (userId == null) throw new ArgumentNullException(nameof(userId));
            await SendAsync(HttpMethod.Delete, $"/v1.0/api/tenants/{Uri.EscapeDataString(tenantId)}/users/{Uri.EscapeDataString(userId)}", null, true, cancellationToken).ConfigureAwait(false);
        }

        #endregion

        #region Credentials

        /// <summary>
        /// Lists application keys within a tenant.
        /// </summary>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>The list of credentials.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="tenantId"/> is null.</exception>
        /// <exception cref="ClutchException">Thrown when the request fails.</exception>
        public async Task<List<Credential>> ListCredentialsAsync(string tenantId, CancellationToken cancellationToken = default)
        {
            if (tenantId == null) throw new ArgumentNullException(nameof(tenantId));
            return await SendAsync<List<Credential>>(HttpMethod.Get, $"/v1.0/api/tenants/{Uri.EscapeDataString(tenantId)}/credentials", null, true, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Retrieves a single application key within a tenant.
        /// </summary>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <param name="credentialId">The credential identifier.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>The credential.</returns>
        /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
        /// <exception cref="ClutchException">Thrown when the request fails.</exception>
        public async Task<Credential> GetCredentialAsync(string tenantId, string credentialId, CancellationToken cancellationToken = default)
        {
            if (tenantId == null) throw new ArgumentNullException(nameof(tenantId));
            if (credentialId == null) throw new ArgumentNullException(nameof(credentialId));
            return await SendAsync<Credential>(HttpMethod.Get, $"/v1.0/api/tenants/{Uri.EscapeDataString(tenantId)}/credentials/{Uri.EscapeDataString(credentialId)}", null, true, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates an application key within a tenant.
        /// </summary>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <param name="name">The credential name.</param>
        /// <param name="userId">An optional user identifier to associate the credential with.</param>
        /// <param name="expiresUtc">An optional expiry time, in UTC.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>The created credential, including its access key.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="tenantId"/> or <paramref name="name"/> is null.</exception>
        /// <exception cref="ClutchException">Thrown when the request fails.</exception>
        public async Task<Credential> CreateCredentialAsync(string tenantId, string name, string? userId = null, DateTime? expiresUtc = null, CancellationToken cancellationToken = default)
        {
            if (tenantId == null) throw new ArgumentNullException(nameof(tenantId));
            if (name == null) throw new ArgumentNullException(nameof(name));
            object body = new { name = name, userId = userId, expiresUtc = expiresUtc };
            return await SendAsync<Credential>(HttpMethod.Post, $"/v1.0/api/tenants/{Uri.EscapeDataString(tenantId)}/credentials", body, true, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Deletes an application key within a tenant.
        /// </summary>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <param name="credentialId">The credential identifier.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
        /// <exception cref="ClutchException">Thrown when the request fails.</exception>
        public async Task DeleteCredentialAsync(string tenantId, string credentialId, CancellationToken cancellationToken = default)
        {
            if (tenantId == null) throw new ArgumentNullException(nameof(tenantId));
            if (credentialId == null) throw new ArgumentNullException(nameof(credentialId));
            await SendAsync(HttpMethod.Delete, $"/v1.0/api/tenants/{Uri.EscapeDataString(tenantId)}/credentials/{Uri.EscapeDataString(credentialId)}", null, true, cancellationToken).ConfigureAwait(false);
        }

        #endregion

        #region Locks

        /// <summary>
        /// Lists active lock holders within a tenant.
        /// </summary>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <param name="name">An optional key substring filter.</param>
        /// <param name="mode">An optional lock mode filter.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>The list of active holders.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="tenantId"/> is null.</exception>
        /// <exception cref="ClutchException">Thrown when the request fails.</exception>
        public async Task<List<LockHolder>> ListLocksAsync(string tenantId, string? name = null, LockMode? mode = null, CancellationToken cancellationToken = default)
        {
            if (tenantId == null) throw new ArgumentNullException(nameof(tenantId));
            Dictionary<string, string?> query = new Dictionary<string, string?>();
            query["name"] = name;
            query["mode"] = mode?.ToString();
            string path = $"/v1.0/api/tenants/{Uri.EscapeDataString(tenantId)}/locks" + BuildQuery(query);
            return await SendAsync<List<LockHolder>>(HttpMethod.Get, path, null, true, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Retrieves the definition and holders for a single lock key.
        /// </summary>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <param name="key">The lock key.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>The lock key detail.</returns>
        /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
        /// <exception cref="ClutchException">Thrown when the request fails.</exception>
        public async Task<LockKeyDetail> GetLockAsync(string tenantId, string key, CancellationToken cancellationToken = default)
        {
            if (tenantId == null) throw new ArgumentNullException(nameof(tenantId));
            if (key == null) throw new ArgumentNullException(nameof(key));
            return await SendAsync<LockKeyDetail>(HttpMethod.Get, $"/v1.0/api/tenants/{Uri.EscapeDataString(tenantId)}/locks/{Uri.EscapeDataString(key)}", null, true, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Force-releases every holder on a key. Requires a tenant administrator.
        /// </summary>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <param name="key">The lock key.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>The release result, including the number of holders released.</returns>
        /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
        /// <exception cref="ClutchException">Thrown when the request fails.</exception>
        public async Task<ReleaseLockResult> ReleaseLockAsync(string tenantId, string key, CancellationToken cancellationToken = default)
        {
            if (tenantId == null) throw new ArgumentNullException(nameof(tenantId));
            if (key == null) throw new ArgumentNullException(nameof(key));
            return await SendAsync<ReleaseLockResult>(HttpMethod.Post, $"/v1.0/api/tenants/{Uri.EscapeDataString(tenantId)}/locks/{Uri.EscapeDataString(key)}/release", null, true, cancellationToken).ConfigureAwait(false);
        }

        #endregion

        #region Lock audit

        /// <summary>
        /// Retrieves a page of lock audit entries within a tenant.
        /// </summary>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <param name="name">An optional key substring filter.</param>
        /// <param name="mode">An optional lock mode filter.</param>
        /// <param name="fromUtc">An optional inclusive lower bound, in UTC.</param>
        /// <param name="toUtc">An optional exclusive upper bound, in UTC.</param>
        /// <param name="pageNumber">An optional one-based page number.</param>
        /// <param name="pageSize">An optional page size.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>A page of audit entries.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="tenantId"/> is null.</exception>
        /// <exception cref="ClutchException">Thrown when the request fails.</exception>
        public async Task<PagedResult<LockAuditEntry>> GetLockAuditAsync(string tenantId, string? name = null, LockMode? mode = null, DateTime? fromUtc = null, DateTime? toUtc = null, int? pageNumber = null, int? pageSize = null, CancellationToken cancellationToken = default)
        {
            if (tenantId == null) throw new ArgumentNullException(nameof(tenantId));
            Dictionary<string, string?> query = new Dictionary<string, string?>();
            query["name"] = name;
            query["mode"] = mode?.ToString();
            query["fromUtc"] = FormatUtc(fromUtc);
            query["toUtc"] = FormatUtc(toUtc);
            query["pageNumber"] = pageNumber?.ToString(CultureInfo.InvariantCulture);
            query["pageSize"] = pageSize?.ToString(CultureInfo.InvariantCulture);
            string path = $"/v1.0/api/tenants/{Uri.EscapeDataString(tenantId)}/lock-audit" + BuildQuery(query);
            return await SendAsync<PagedResult<LockAuditEntry>>(HttpMethod.Get, path, null, true, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Retrieves a time-bucketed summary of lock acquire activity within a tenant.
        /// </summary>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <param name="name">An optional key substring filter.</param>
        /// <param name="mode">An optional lock mode filter.</param>
        /// <param name="fromUtc">An optional inclusive lower bound, in UTC.</param>
        /// <param name="toUtc">An optional exclusive upper bound, in UTC.</param>
        /// <param name="bucketCount">An optional number of buckets.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>The lock audit summary.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="tenantId"/> is null.</exception>
        /// <exception cref="ClutchException">Thrown when the request fails.</exception>
        public async Task<LockAuditSummary> GetLockAuditSummaryAsync(string tenantId, string? name = null, LockMode? mode = null, DateTime? fromUtc = null, DateTime? toUtc = null, int? bucketCount = null, CancellationToken cancellationToken = default)
        {
            if (tenantId == null) throw new ArgumentNullException(nameof(tenantId));
            Dictionary<string, string?> query = new Dictionary<string, string?>();
            query["name"] = name;
            query["mode"] = mode?.ToString();
            query["fromUtc"] = FormatUtc(fromUtc);
            query["toUtc"] = FormatUtc(toUtc);
            query["bucketCount"] = bucketCount?.ToString(CultureInfo.InvariantCulture);
            string path = $"/v1.0/api/tenants/{Uri.EscapeDataString(tenantId)}/lock-audit/summary" + BuildQuery(query);
            return await SendAsync<LockAuditSummary>(HttpMethod.Get, path, null, true, cancellationToken).ConfigureAwait(false);
        }

        #endregion

        #region Request history

        /// <summary>
        /// Retrieves a page of request history. Bodies are omitted in list responses.
        /// </summary>
        /// <param name="method">An optional HTTP method filter.</param>
        /// <param name="statusCode">An optional status code filter.</param>
        /// <param name="pathContains">An optional path substring filter.</param>
        /// <param name="fromUtc">An optional inclusive lower bound, in UTC.</param>
        /// <param name="toUtc">An optional exclusive upper bound, in UTC.</param>
        /// <param name="pageNumber">An optional one-based page number.</param>
        /// <param name="pageSize">An optional page size.</param>
        /// <param name="tenantId">An optional tenant identifier to widen scope; honored for system administrators.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>A page of request history entries.</returns>
        /// <exception cref="ClutchException">Thrown when the request fails.</exception>
        public async Task<PagedResult<RequestHistoryEntry>> GetRequestHistoryAsync(string? method = null, int? statusCode = null, string? pathContains = null, DateTime? fromUtc = null, DateTime? toUtc = null, int? pageNumber = null, int? pageSize = null, string? tenantId = null, CancellationToken cancellationToken = default)
        {
            Dictionary<string, string?> query = new Dictionary<string, string?>();
            query["method"] = method;
            query["statusCode"] = statusCode?.ToString(CultureInfo.InvariantCulture);
            query["pathContains"] = pathContains;
            query["fromUtc"] = FormatUtc(fromUtc);
            query["toUtc"] = FormatUtc(toUtc);
            query["pageNumber"] = pageNumber?.ToString(CultureInfo.InvariantCulture);
            query["pageSize"] = pageSize?.ToString(CultureInfo.InvariantCulture);
            query["tenantId"] = tenantId;
            string path = "/v1.0/api/request-history" + BuildQuery(query);
            return await SendAsync<PagedResult<RequestHistoryEntry>>(HttpMethod.Get, path, null, true, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Retrieves a bucketed summary of request history.
        /// </summary>
        /// <param name="method">An optional HTTP method filter.</param>
        /// <param name="statusCode">An optional status code filter.</param>
        /// <param name="pathContains">An optional path substring filter.</param>
        /// <param name="fromUtc">An optional inclusive lower bound, in UTC.</param>
        /// <param name="toUtc">An optional exclusive upper bound, in UTC.</param>
        /// <param name="bucketMinutes">An optional bucket size, in minutes.</param>
        /// <param name="tenantId">An optional tenant identifier to widen scope; honored for system administrators.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>The request history summary.</returns>
        /// <exception cref="ClutchException">Thrown when the request fails.</exception>
        public async Task<RequestHistorySummary> GetRequestHistorySummaryAsync(string? method = null, int? statusCode = null, string? pathContains = null, DateTime? fromUtc = null, DateTime? toUtc = null, int? bucketMinutes = null, string? tenantId = null, CancellationToken cancellationToken = default)
        {
            Dictionary<string, string?> query = new Dictionary<string, string?>();
            query["method"] = method;
            query["statusCode"] = statusCode?.ToString(CultureInfo.InvariantCulture);
            query["pathContains"] = pathContains;
            query["fromUtc"] = FormatUtc(fromUtc);
            query["toUtc"] = FormatUtc(toUtc);
            query["bucketMinutes"] = bucketMinutes?.ToString(CultureInfo.InvariantCulture);
            query["tenantId"] = tenantId;
            string path = "/v1.0/api/request-history/summary" + BuildQuery(query);
            return await SendAsync<RequestHistorySummary>(HttpMethod.Get, path, null, true, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Retrieves a single request history entry, including redacted headers and truncated bodies.
        /// </summary>
        /// <param name="id">The entry identifier.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>The request history entry.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="id"/> is null.</exception>
        /// <exception cref="ClutchException">Thrown when the request fails.</exception>
        public async Task<RequestHistoryEntry> GetRequestHistoryEntryAsync(string id, CancellationToken cancellationToken = default)
        {
            if (id == null) throw new ArgumentNullException(nameof(id));
            return await SendAsync<RequestHistoryEntry>(HttpMethod.Get, $"/v1.0/api/request-history/{Uri.EscapeDataString(id)}", null, true, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Deletes a single request history entry.
        /// </summary>
        /// <param name="id">The entry identifier.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="id"/> is null.</exception>
        /// <exception cref="ClutchException">Thrown when the request fails.</exception>
        public async Task DeleteRequestHistoryEntryAsync(string id, CancellationToken cancellationToken = default)
        {
            if (id == null) throw new ArgumentNullException(nameof(id));
            await SendAsync(HttpMethod.Delete, $"/v1.0/api/request-history/{Uri.EscapeDataString(id)}", null, true, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Bulk-deletes request history entries matching the supplied filter.
        /// </summary>
        /// <param name="method">An optional HTTP method filter.</param>
        /// <param name="statusCode">An optional status code filter.</param>
        /// <param name="pathContains">An optional path substring filter.</param>
        /// <param name="fromUtc">An optional inclusive lower bound, in UTC.</param>
        /// <param name="toUtc">An optional exclusive upper bound, in UTC.</param>
        /// <param name="tenantId">An optional tenant identifier to widen scope; honored for system administrators.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>The delete result, including the number of entries deleted.</returns>
        /// <exception cref="ClutchException">Thrown when the request fails.</exception>
        public async Task<DeleteResult> DeleteRequestHistoryAsync(string? method = null, int? statusCode = null, string? pathContains = null, DateTime? fromUtc = null, DateTime? toUtc = null, string? tenantId = null, CancellationToken cancellationToken = default)
        {
            Dictionary<string, string?> query = new Dictionary<string, string?>();
            query["method"] = method;
            query["statusCode"] = statusCode?.ToString(CultureInfo.InvariantCulture);
            query["pathContains"] = pathContains;
            query["fromUtc"] = FormatUtc(fromUtc);
            query["toUtc"] = FormatUtc(toUtc);
            query["tenantId"] = tenantId;
            string path = "/v1.0/api/request-history" + BuildQuery(query);
            return await SendAsync<DeleteResult>(HttpMethod.Delete, path, null, true, cancellationToken).ConfigureAwait(false);
        }

        #endregion

        #region Private-Methods

        private static string? FormatUtc(DateTime? value)
        {
            if (value == null) return null;
            return value.Value.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);
        }

        private static string BuildQuery(Dictionary<string, string?> parameters)
        {
            StringBuilder sb = new StringBuilder();
            foreach (KeyValuePair<string, string?> pair in parameters)
            {
                if (string.IsNullOrEmpty(pair.Value)) continue;
                sb.Append(sb.Length == 0 ? '?' : '&');
                sb.Append(Uri.EscapeDataString(pair.Key));
                sb.Append('=');
                sb.Append(Uri.EscapeDataString(pair.Value));
            }
            return sb.ToString();
        }

        private async Task<T> SendAsync<T>(HttpMethod method, string path, object? body, bool requireAuth, CancellationToken cancellationToken)
        {
            string responseBody = await SendCoreAsync(method, path, body, requireAuth, cancellationToken).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(responseBody))
            {
                throw new ClutchException($"The server returned an empty body for {method} {path}.");
            }

            try
            {
                T? result = JsonSerializer.Deserialize<T>(responseBody, _JsonOptions);
                if (result == null)
                {
                    throw new ClutchException($"The server response for {method} {path} deserialized to null.");
                }
                return result;
            }
            catch (JsonException ex)
            {
                throw new ClutchException($"Failed to parse the server response for {method} {path}: {ex.Message}", ex);
            }
        }

        private async Task SendAsync(HttpMethod method, string path, object? body, bool requireAuth, CancellationToken cancellationToken)
        {
            await SendCoreAsync(method, path, body, requireAuth, cancellationToken).ConfigureAwait(false);
        }

        private async Task<string> SendCoreAsync(HttpMethod method, string path, object? body, bool requireAuth, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string url = $"{_Endpoint}{path}";

            using HttpRequestMessage request = new HttpRequestMessage(method, url);

            if (requireAuth)
            {
                if (string.IsNullOrEmpty(_Token))
                {
                    throw new ClutchException($"A bearer token is required for {method} {path}, but none is set. Authenticate first.");
                }
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _Token);
            }

            if (body != null)
            {
                string json = JsonSerializer.Serialize(body, _JsonOptions);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            }

            HttpResponseMessage response;
            try
            {
                response = await _HttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            }
            catch (HttpRequestException ex)
            {
                throw new ClutchException($"The request to {method} {path} failed: {ex.Message}", ex);
            }
            catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                throw new ClutchException($"The request to {method} {path} timed out.", ex);
            }

            using (response)
            {
                string responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    throw new ClutchException($"{method} {path} returned {(int)response.StatusCode} ({response.StatusCode}): {responseBody}", (int)response.StatusCode, responseBody);
                }

                return responseBody;
            }
        }

        #endregion

        #region IDisposable

        /// <summary>
        /// Releases the resources used by the client.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases the resources used by the client.
        /// </summary>
        /// <param name="disposing">True when called from <see cref="Dispose()"/>; false when called from a finalizer.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_Disposed) return;
            if (disposing)
            {
                if (_OwnsHttpClient)
                {
                    _HttpClient.Dispose();
                }
            }
            _Disposed = true;
        }

        #endregion
    }
}
