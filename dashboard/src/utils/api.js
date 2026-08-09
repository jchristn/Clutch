/**
 * API client for communicating with the Clutch backend server.
 *
 * Hand-rolled over the browser `fetch` API (no axios). All calls send the bearer
 * token when present. The API Explorer uses `executeExplorer` to get raw Response
 * access; every other call returns parsed JSON and throws `ApiError` on non-2xx.
 */
class ApiClient {
  /**
   * @param {string} baseUrl - Base URL of the server (with or without trailing slash).
   * @param {string|null} token - Bearer token for authentication.
   */
  constructor(baseUrl, token = null) {
    this.baseUrl = (baseUrl || '').replace(/\/$/, '');
    this.token = token;
  }

  _headers(extra = {}) {
    const headers = { 'Content-Type': 'application/json', ...extra };
    if (this.token) headers['Authorization'] = `Bearer ${this.token}`;
    // Advertise the active UI locale so server-authored text can vary by locale.
    try {
      const locale = document.documentElement.lang;
      if (locale) headers['Accept-Language'] = locale;
    } catch {
      /* no-op */
    }
    return headers;
  }

  async _request(method, path, { query = null, body = null, headers = {} } = {}) {
    const url = new URL(this.baseUrl + path);
    if (query) {
      for (const [k, v] of Object.entries(query)) {
        if (v !== undefined && v !== null && v !== '') url.searchParams.append(k, v);
      }
    }

    const init = { method, headers: this._headers(headers) };
    if (body !== null && body !== undefined) init.body = JSON.stringify(body);

    const response = await fetch(url.toString(), init);

    if (response.status === 401) {
      window.dispatchEvent(new CustomEvent('auth:unauthorized'));
    }

    if (!response.ok) {
      const errorBody = await response.text().catch(() => '');
      throw new ApiError(response.status, errorBody || response.statusText);
    }

    if (response.status === 204) return null;
    const text = await response.text();
    if (!text) return null;
    try {
      return JSON.parse(text);
    } catch {
      return text;
    }
  }

  // ------------------------------------------------------------------
  // Auth + health
  // ------------------------------------------------------------------

  /** Anonymous health probe. */
  async healthCheck() {
    return this._request('GET', '/v1.0/api/health');
  }

  /** Validate the current bearer token by resolving the principal. */
  async validateToken() {
    return this._request('GET', '/v1.0/token');
  }

  /** Login with an application key (access/secret). Returns { token, principalType, tenantId }. */
  async loginWithKey(accessKey, secretKey) {
    return this._request('POST', '/v1.0/token', { body: { accessKey, secretKey } });
  }

  /** Login with tenant credentials (email/password). Returns { token, principalType, tenantId }. */
  async loginWithPassword(tenantId, email, password) {
    return this._request('POST', '/v1.0/token', { body: { tenantId, email, password } });
  }

  /** Revoke the current session token. */
  async revokeToken() {
    return this._request('DELETE', '/v1.0/token');
  }

  /** Server info: product, version, node, telemetry, principal. */
  async getServerInfo() {
    return this._request('GET', '/v1.0/api/server-info');
  }

  // ------------------------------------------------------------------
  // Tenants
  // ------------------------------------------------------------------

  async listTenants() {
    return this._request('GET', '/v1.0/api/tenants');
  }

  async createTenant(body) {
    return this._request('POST', '/v1.0/api/tenants', { body });
  }

  async getTenant(id) {
    return this._request('GET', `/v1.0/api/tenants/${encodeURIComponent(id)}`);
  }

  async updateTenant(id, body) {
    return this._request('PUT', `/v1.0/api/tenants/${encodeURIComponent(id)}`, { body });
  }

  async deleteTenant(id) {
    return this._request('DELETE', `/v1.0/api/tenants/${encodeURIComponent(id)}`);
  }

  // ------------------------------------------------------------------
  // Users
  // ------------------------------------------------------------------

  async listUsers(tenantId) {
    return this._request('GET', `/v1.0/api/tenants/${encodeURIComponent(tenantId)}/users`);
  }

  async createUser(tenantId, body) {
    return this._request('POST', `/v1.0/api/tenants/${encodeURIComponent(tenantId)}/users`, { body });
  }

  async getUser(tenantId, id) {
    return this._request('GET', `/v1.0/api/tenants/${encodeURIComponent(tenantId)}/users/${encodeURIComponent(id)}`);
  }

  async updateUser(tenantId, id, body) {
    return this._request('PUT', `/v1.0/api/tenants/${encodeURIComponent(tenantId)}/users/${encodeURIComponent(id)}`, { body });
  }

  async deleteUser(tenantId, id) {
    return this._request('DELETE', `/v1.0/api/tenants/${encodeURIComponent(tenantId)}/users/${encodeURIComponent(id)}`);
  }

  // ------------------------------------------------------------------
  // Credentials (application keys)
  // ------------------------------------------------------------------

  async listCredentials(tenantId) {
    return this._request('GET', `/v1.0/api/tenants/${encodeURIComponent(tenantId)}/credentials`);
  }

  /** Create a credential. Response includes accessKey + secretKey (shown once). */
  async createCredential(tenantId, body) {
    return this._request('POST', `/v1.0/api/tenants/${encodeURIComponent(tenantId)}/credentials`, { body });
  }

  async getCredential(tenantId, id) {
    return this._request('GET', `/v1.0/api/tenants/${encodeURIComponent(tenantId)}/credentials/${encodeURIComponent(id)}`);
  }

  async deleteCredential(tenantId, id) {
    return this._request('DELETE', `/v1.0/api/tenants/${encodeURIComponent(tenantId)}/credentials/${encodeURIComponent(id)}`);
  }

  // ------------------------------------------------------------------
  // Locks (observe + admin force-release)
  // ------------------------------------------------------------------

  /** List active holders. filters: { name, mode } */
  async listLocks(tenantId, filters = {}) {
    return this._request('GET', `/v1.0/api/tenants/${encodeURIComponent(tenantId)}/locks`, { query: filters });
  }

  /** Get a single lock: { definition, holders }. */
  async getLock(tenantId, key) {
    return this._request('GET', `/v1.0/api/tenants/${encodeURIComponent(tenantId)}/locks/${encodeURIComponent(key)}`);
  }

  /** Admin force-release. Returns { key, released }. */
  async releaseLock(tenantId, key) {
    return this._request('POST', `/v1.0/api/tenants/${encodeURIComponent(tenantId)}/locks/${encodeURIComponent(key)}/release`);
  }

  // ------------------------------------------------------------------
  // Lock audit + chart summary
  // ------------------------------------------------------------------

  /** filters: { name, mode, fromUtc, toUtc, pageNumber, pageSize } -> { items, pageNumber, pageSize, totalCount } */
  async getLockAudit(tenantId, filters = {}) {
    return this._request('GET', `/v1.0/api/tenants/${encodeURIComponent(tenantId)}/lock-audit`, { query: filters });
  }

  /** filters: { name, mode, fromUtc, toUtc, bucketCount } -> LockChartSummary */
  async getLockAuditSummary(tenantId, filters = {}) {
    return this._request('GET', `/v1.0/api/tenants/${encodeURIComponent(tenantId)}/lock-audit/summary`, { query: filters });
  }

  // ------------------------------------------------------------------
  // Request history
  // ------------------------------------------------------------------

  async getRequestHistory(filters = {}) {
    return this._request('GET', '/v1.0/api/request-history', { query: filters });
  }

  async getRequestHistorySummary(filters = {}) {
    return this._request('GET', '/v1.0/api/request-history/summary', { query: filters });
  }

  async getRequestHistoryEntry(id) {
    return this._request('GET', `/v1.0/api/request-history/${encodeURIComponent(id)}`);
  }

  async deleteRequestHistoryEntry(id) {
    return this._request('DELETE', `/v1.0/api/request-history/${encodeURIComponent(id)}`);
  }

  /** Bulk delete matching filters. Returns { deletedCount }. */
  async deleteRequestHistoryBulk(filters = {}) {
    return this._request('DELETE', '/v1.0/api/request-history', { query: filters });
  }

  // ------------------------------------------------------------------
  // OpenAPI (API Explorer)
  // ------------------------------------------------------------------

  async getOpenApiSpec() {
    return this._request('GET', '/openapi.json');
  }

  /**
   * Execute an arbitrary request built by the API Explorer. Returns the raw
   * Response so the caller can inspect status, headers, and streaming bodies.
   */
  async executeExplorer({ method, path, query, headers, body }) {
    const url = new URL(this.baseUrl + path);
    if (query) {
      for (const [k, v] of Object.entries(query)) {
        if (v !== undefined && v !== null && v !== '') url.searchParams.append(k, v);
      }
    }
    const finalHeaders = this._headers(headers || {});
    const hasBody = body !== null && body !== undefined && body !== '';
    if (!hasBody) delete finalHeaders['Content-Type'];
    return fetch(url.toString(), {
      method,
      headers: finalHeaders,
      body: hasBody ? body : undefined
    });
  }
}

/** Normalized API error with status code and body text. */
class ApiError extends Error {
  constructor(status, body) {
    super(`HTTP ${status}: ${body}`);
    this.name = 'ApiError';
    this.status = status;
    this.body = body;
  }
}

export default ApiClient;
export { ApiError };
