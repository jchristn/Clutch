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

  /**
   * Translate a caller's { pageNumber, pageSize, ...filters } into the server's paginated
   * EnumerationQuery params { maxResults, skip, ...filters }. Filters pass through untouched.
   */
  _enumParams(params = {}) {
    const { pageNumber, pageSize, ...rest } = params || {};
    const out = { ...rest };
    if (pageSize != null) {
      out.maxResults = pageSize;
      out.skip = ((pageNumber || 1) - 1) * pageSize;
    }
    return out;
  }

  /**
   * Normalize a server EnumerationResult ({ objects, totalRecords, skip, maxResults, ... }) into the
   * page shape the dashboard tables consume ({ items, objects, pageNumber, pageSize, totalCount }).
   */
  _normalizeEnum(result, requestedPageNumber, requestedPageSize) {
    const objects = Array.isArray(result?.objects) ? result.objects : Array.isArray(result) ? result : [];
    const total = result?.totalRecords ?? objects.length;
    const size = result?.maxResults ?? requestedPageSize ?? (objects.length || 25);
    const skip = result?.skip ?? 0;
    const pageNumber = size > 0 ? Math.floor(skip / size) + 1 : requestedPageNumber || 1;
    return {
      items: objects,
      objects,
      pageNumber,
      pageSize: size,
      totalCount: total,
      totalRecords: total,
      recordsRemaining: result?.recordsRemaining ?? Math.max(0, total - (skip + objects.length)),
      endOfResults: result?.endOfResults ?? true
    };
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

  /** Login with an application key (access key only). Returns { token, principalType, tenantId }. */
  async loginWithKey(accessKey) {
    return this._request('POST', '/v1.0/token', { body: { accessKey } });
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
  // Server settings (system-admin only; secrets redacted as "***")
  // ------------------------------------------------------------------

  /** Full on-disk settings object (camelCase, secrets redacted). */
  async getSettings() {
    return this._request('GET', '/v1.0/api/settings');
  }

  /** Persist edited settings. Returns { saved, restartRequired, message, settings }. */
  async updateSettings(body) {
    return this._request('PUT', '/v1.0/api/settings', { body });
  }

  /** Request a node restart. Returns 202 { restarting, node }. */
  async restartServer() {
    return this._request('POST', '/v1.0/api/settings/restart');
  }

  // ------------------------------------------------------------------
  // Tenants
  // ------------------------------------------------------------------

  /** Paginated. params: { pageNumber, pageSize, ordering } -> { items, pageNumber, pageSize, totalCount }. */
  async listTenants(params = {}) {
    const res = await this._request('GET', '/v1.0/api/tenants', { query: this._enumParams(params) });
    return this._normalizeEnum(res, params.pageNumber, params.pageSize);
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

  /**
   * Destroy a tenant and every record scoped to it (system-admin only).
   * body: { tenantId, confirmTenantId, reason, includeAuditRecords, includeRequestHistory }
   * Returns { operationId, tenantId, tenantName, deleted, startedUtc, completedUtc }.
   */
  async nukeTenant(body) {
    return this._request('POST', '/v1.0/api/admin/nuke/tenant', { body });
  }

  // ------------------------------------------------------------------
  // Users
  // ------------------------------------------------------------------

  /** Paginated. params: { pageNumber, pageSize, ordering } -> { items, pageNumber, pageSize, totalCount }. */
  async listUsers(tenantId, params = {}) {
    const res = await this._request('GET', `/v1.0/api/tenants/${encodeURIComponent(tenantId)}/users`, { query: this._enumParams(params) });
    return this._normalizeEnum(res, params.pageNumber, params.pageSize);
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

  /** Paginated. params: { pageNumber, pageSize, ordering } -> { items, pageNumber, pageSize, totalCount }. */
  async listCredentials(tenantId, params = {}) {
    const res = await this._request('GET', `/v1.0/api/tenants/${encodeURIComponent(tenantId)}/credentials`, { query: this._enumParams(params) });
    return this._normalizeEnum(res, params.pageNumber, params.pageSize);
  }

  /** Create a credential. Response includes the accessKey. */
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

  /** List active holders (paginated). filters: { name, mode, pageNumber, pageSize } -> { items, pageNumber, pageSize, totalCount }. */
  async listLocks(tenantId, filters = {}) {
    const res = await this._request('GET', `/v1.0/api/tenants/${encodeURIComponent(tenantId)}/locks`, { query: this._enumParams(filters) });
    return this._normalizeEnum(res, filters.pageNumber, filters.pageSize);
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
    const res = await this._request('GET', `/v1.0/api/tenants/${encodeURIComponent(tenantId)}/lock-audit`, { query: this._enumParams(filters) });
    return this._normalizeEnum(res, filters.pageNumber, filters.pageSize);
  }

  /** filters: { name, mode, fromUtc, toUtc, bucketCount } -> LockChartSummary */
  async getLockAuditSummary(tenantId, filters = {}) {
    return this._request('GET', `/v1.0/api/tenants/${encodeURIComponent(tenantId)}/lock-audit/summary`, { query: filters });
  }

  // ------------------------------------------------------------------
  // Request history
  // ------------------------------------------------------------------

  async getRequestHistory(filters = {}) {
    const res = await this._request('GET', '/v1.0/api/request-history', { query: this._enumParams(filters) });
    return this._normalizeEnum(res, filters.pageNumber, filters.pageSize);
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
