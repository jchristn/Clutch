'use strict';

/**
 * Clutch Admin SDK (JavaScript)
 *
 * REST client for Clutch administration and observability: tokens, tenants, users,
 * application keys, lock inspection, audit, request history, health, and server info.
 * Lock acquisition itself is performed over the WebSocket API via clutch-lock-sdk.js.
 *
 * Uses the global fetch API (Node.js 18+ or the browser).
 */

/**
 * Lock modes. Determines compatibility with other holders on the same key.
 * @readonly @enum {string}
 */
const LockMode = Object.freeze({
    Read: 'Read',
    Write: 'Write',
    Delete: 'Delete'
});

/**
 * Behavior applied when a lock cannot be granted immediately.
 * @readonly @enum {string}
 */
const LockBehavior = Object.freeze({
    FailFast: 'FailFast',
    Wait: 'Wait'
});

/**
 * Outcome of a lock acquisition request.
 * @readonly @enum {string}
 */
const AcquireResult = Object.freeze({
    Acquired: 'Acquired',
    Denied: 'Denied',
    Timeout: 'Timeout',
    PolicyConflict: 'PolicyConflict'
});

/**
 * Error thrown when a Clutch REST or WebSocket operation fails.
 */
class ClutchError extends Error {
    /**
     * @param {string} message - Human-readable error message.
     * @param {number} [statusCode=0] - HTTP status code, or 0 when not applicable.
     * @param {string|null} [responseBody=null] - Raw response body, when available.
     */
    constructor(message, statusCode = 0, responseBody = null) {
        super(message);
        this.name = 'ClutchError';
        this.statusCode = statusCode;
        this.responseBody = responseBody;
    }
}

/**
 * REST client for the Clutch API.
 */
class ClutchAdminClient {
    /**
     * @param {string} endpoint - Base URL, e.g. "http://127.0.0.1:8090".
     * @param {string|null} [token=null] - Optional pre-existing bearer token.
     */
    constructor(endpoint, token = null) {
        if (!endpoint) throw new ClutchError('endpoint is required');
        this.endpoint = endpoint.replace(/\/+$/, '');
        this.token = token;
    }

    /**
     * Builds a query string from an object, omitting null and undefined values.
     * @param {object} params - Query parameters.
     * @returns {string} A query string beginning with '?', or an empty string.
     * @private
     */
    _query(params) {
        const search = new URLSearchParams();
        for (const [key, value] of Object.entries(params)) {
            if (value === null || value === undefined || value === '') continue;
            search.append(key, String(value));
        }
        const s = search.toString();
        return s.length > 0 ? `?${s}` : '';
    }

    /**
     * Sends an HTTP request and returns the parsed response.
     * @param {string} method - HTTP method.
     * @param {string} path - Request path beginning with '/'.
     * @param {object} [options] - Options.
     * @param {object} [options.body] - JSON body to send.
     * @param {boolean} [options.auth=true] - Whether to send the bearer token.
     * @returns {Promise<any>} The parsed response body, or null for empty responses.
     * @throws {ClutchError} When the request fails or returns a non-2xx status.
     * @private
     */
    async _request(method, path, { body, auth = true } = {}) {
        const headers = { 'Accept': 'application/json' };
        if (auth) {
            if (!this.token) throw new ClutchError(`A bearer token is required for ${method} ${path}. Authenticate first.`);
            headers['Authorization'] = `Bearer ${this.token}`;
        }
        const init = { method, headers };
        if (body !== undefined && body !== null) {
            headers['Content-Type'] = 'application/json';
            init.body = JSON.stringify(body);
        }

        let response;
        try {
            response = await fetch(`${this.endpoint}${path}`, init);
        } catch (err) {
            throw new ClutchError(`The request to ${method} ${path} failed: ${err.message}`);
        }

        const text = await response.text();
        if (!response.ok) {
            throw new ClutchError(`${method} ${path} returned ${response.status}: ${text}`, response.status, text);
        }
        if (!text) return null;
        try {
            return JSON.parse(text);
        } catch (err) {
            throw new ClutchError(`Failed to parse the response for ${method} ${path}: ${err.message}`, response.status, text);
        }
    }

    // ---- Tokens ----

    /**
     * Authenticates with an application key. Stores the returned token on this client.
     * @param {string} accessKey - The access key.
     * @param {string} secretKey - The secret key.
     * @returns {Promise<object>} The token response { token, principalType, tenantId }.
     */
    async authenticateWithKey(accessKey, secretKey) {
        const result = await this._request('POST', '/v1.0/token', { body: { accessKey, secretKey }, auth: false });
        this.token = result.token;
        return result;
    }

    /**
     * Authenticates with user credentials. Stores the returned token on this client.
     * @param {string} tenantId - The tenant identifier.
     * @param {string} email - The user's email.
     * @param {string} password - The user's password.
     * @returns {Promise<object>} The token response { token, principalType, tenantId }.
     */
    async authenticateWithPassword(tenantId, email, password) {
        const result = await this._request('POST', '/v1.0/token', { body: { tenantId, email, password }, auth: false });
        this.token = result.token;
        return result;
    }

    /**
     * Validates the current bearer token and returns the resolved principal context.
     * @returns {Promise<object>} The token details.
     */
    async getTokenDetails() {
        return this._request('GET', '/v1.0/token');
    }

    /**
     * Revokes the current session (logout) and clears the stored token.
     * @returns {Promise<void>}
     */
    async logout() {
        await this._request('DELETE', '/v1.0/token');
        this.token = null;
    }

    // ---- Health and server info ----

    /**
     * Retrieves server health. Anonymous.
     * @returns {Promise<object>} The health response.
     */
    async getHealth() {
        return this._request('GET', '/v1.0/api/health', { auth: false });
    }

    /**
     * Retrieves information about the running server.
     * @returns {Promise<object>} The server info.
     */
    async getServerInfo() {
        return this._request('GET', '/v1.0/api/server-info');
    }

    // ---- Tenants ----

    /**
     * Lists tenants.
     * @returns {Promise<object[]>} The list of tenants.
     */
    async listTenants() {
        return this._request('GET', '/v1.0/api/tenants');
    }

    /**
     * Retrieves a single tenant.
     * @param {string} tenantId - The tenant identifier.
     * @returns {Promise<object>} The tenant.
     */
    async getTenant(tenantId) {
        return this._request('GET', `/v1.0/api/tenants/${encodeURIComponent(tenantId)}`);
    }

    /**
     * Creates a tenant. Requires a system administrator.
     * @param {string} name - The tenant name.
     * @param {object} [options] - Optional fields.
     * @param {number} [options.lockHistoryRetentionDays] - Retention days.
     * @param {number} [options.defaultLeaseMs] - Default lease, in milliseconds.
     * @param {number} [options.maxLeaseMs] - Maximum lease, in milliseconds.
     * @returns {Promise<object>} The created tenant.
     */
    async createTenant(name, options = {}) {
        const body = {
            name,
            lockHistoryRetentionDays: options.lockHistoryRetentionDays,
            defaultLeaseMs: options.defaultLeaseMs,
            maxLeaseMs: options.maxLeaseMs
        };
        return this._request('POST', '/v1.0/api/tenants', { body });
    }

    /**
     * Updates a tenant.
     * @param {string} tenantId - The tenant identifier.
     * @param {object} fields - The fields to update.
     * @returns {Promise<object>} The updated tenant.
     */
    async updateTenant(tenantId, fields) {
        return this._request('PUT', `/v1.0/api/tenants/${encodeURIComponent(tenantId)}`, { body: fields });
    }

    /**
     * Deletes a tenant. Cascades to users, credentials, locks, and audit.
     * @param {string} tenantId - The tenant identifier.
     * @returns {Promise<void>}
     */
    async deleteTenant(tenantId) {
        await this._request('DELETE', `/v1.0/api/tenants/${encodeURIComponent(tenantId)}`);
    }

    // ---- Users ----

    /**
     * Lists users within a tenant.
     * @param {string} tenantId - The tenant identifier.
     * @returns {Promise<object[]>} The list of users.
     */
    async listUsers(tenantId) {
        return this._request('GET', `/v1.0/api/tenants/${encodeURIComponent(tenantId)}/users`);
    }

    /**
     * Retrieves a single user.
     * @param {string} tenantId - The tenant identifier.
     * @param {string} userId - The user identifier.
     * @returns {Promise<object>} The user.
     */
    async getUser(tenantId, userId) {
        return this._request('GET', `/v1.0/api/tenants/${encodeURIComponent(tenantId)}/users/${encodeURIComponent(userId)}`);
    }

    /**
     * Creates a user within a tenant.
     * @param {string} tenantId - The tenant identifier.
     * @param {object} user - { email, password, firstName?, lastName?, isSystemAdmin?, isTenantAdmin?, active? }.
     * @returns {Promise<object>} The created user.
     */
    async createUser(tenantId, user) {
        return this._request('POST', `/v1.0/api/tenants/${encodeURIComponent(tenantId)}/users`, { body: user });
    }

    /**
     * Updates a user within a tenant.
     * @param {string} tenantId - The tenant identifier.
     * @param {string} userId - The user identifier.
     * @param {object} fields - The fields to update.
     * @returns {Promise<object>} The updated user.
     */
    async updateUser(tenantId, userId, fields) {
        return this._request('PUT', `/v1.0/api/tenants/${encodeURIComponent(tenantId)}/users/${encodeURIComponent(userId)}`, { body: fields });
    }

    /**
     * Deletes a user within a tenant.
     * @param {string} tenantId - The tenant identifier.
     * @param {string} userId - The user identifier.
     * @returns {Promise<void>}
     */
    async deleteUser(tenantId, userId) {
        await this._request('DELETE', `/v1.0/api/tenants/${encodeURIComponent(tenantId)}/users/${encodeURIComponent(userId)}`);
    }

    // ---- Credentials ----

    /**
     * Lists application keys within a tenant. Secrets are redacted.
     * @param {string} tenantId - The tenant identifier.
     * @returns {Promise<object[]>} The list of credentials.
     */
    async listCredentials(tenantId) {
        return this._request('GET', `/v1.0/api/tenants/${encodeURIComponent(tenantId)}/credentials`);
    }

    /**
     * Retrieves a single application key. The secret is redacted.
     * @param {string} tenantId - The tenant identifier.
     * @param {string} credentialId - The credential identifier.
     * @returns {Promise<object>} The credential.
     */
    async getCredential(tenantId, credentialId) {
        return this._request('GET', `/v1.0/api/tenants/${encodeURIComponent(tenantId)}/credentials/${encodeURIComponent(credentialId)}`);
    }

    /**
     * Creates an application key. The raw secret key is returned only in this response.
     * @param {string} tenantId - The tenant identifier.
     * @param {string} name - The credential name.
     * @param {object} [options] - { userId?, expiresUtc? }.
     * @returns {Promise<object>} The created credential, including the raw secret key.
     */
    async createCredential(tenantId, name, options = {}) {
        const body = { name, userId: options.userId, expiresUtc: options.expiresUtc };
        return this._request('POST', `/v1.0/api/tenants/${encodeURIComponent(tenantId)}/credentials`, { body });
    }

    /**
     * Deletes an application key.
     * @param {string} tenantId - The tenant identifier.
     * @param {string} credentialId - The credential identifier.
     * @returns {Promise<void>}
     */
    async deleteCredential(tenantId, credentialId) {
        await this._request('DELETE', `/v1.0/api/tenants/${encodeURIComponent(tenantId)}/credentials/${encodeURIComponent(credentialId)}`);
    }

    // ---- Locks ----

    /**
     * Lists active lock holders within a tenant.
     * @param {string} tenantId - The tenant identifier.
     * @param {object} [filters] - { name?, mode? }.
     * @returns {Promise<object[]>} The list of holders.
     */
    async listLocks(tenantId, filters = {}) {
        const q = this._query({ name: filters.name, mode: filters.mode });
        return this._request('GET', `/v1.0/api/tenants/${encodeURIComponent(tenantId)}/locks${q}`);
    }

    /**
     * Retrieves the definition and holders for a single lock key.
     * @param {string} tenantId - The tenant identifier.
     * @param {string} key - The lock key.
     * @returns {Promise<object>} The lock key detail { definition, holders }.
     */
    async getLock(tenantId, key) {
        return this._request('GET', `/v1.0/api/tenants/${encodeURIComponent(tenantId)}/locks/${encodeURIComponent(key)}`);
    }

    /**
     * Force-releases every holder on a key. Requires a tenant administrator.
     * @param {string} tenantId - The tenant identifier.
     * @param {string} key - The lock key.
     * @returns {Promise<object>} The release result { key, released }.
     */
    async releaseLock(tenantId, key) {
        return this._request('POST', `/v1.0/api/tenants/${encodeURIComponent(tenantId)}/locks/${encodeURIComponent(key)}/release`);
    }

    // ---- Lock audit ----

    /**
     * Retrieves a page of lock audit entries.
     * @param {string} tenantId - The tenant identifier.
     * @param {object} [filters] - { name?, mode?, fromUtc?, toUtc?, pageNumber?, pageSize? }.
     * @returns {Promise<object>} A page { items, pageNumber, pageSize, totalCount }.
     */
    async getLockAudit(tenantId, filters = {}) {
        const q = this._query(filters);
        return this._request('GET', `/v1.0/api/tenants/${encodeURIComponent(tenantId)}/lock-audit${q}`);
    }

    /**
     * Retrieves a time-bucketed summary of lock acquire activity.
     * @param {string} tenantId - The tenant identifier.
     * @param {object} [filters] - { name?, mode?, fromUtc?, toUtc?, bucketCount? }.
     * @returns {Promise<object>} The lock audit summary.
     */
    async getLockAuditSummary(tenantId, filters = {}) {
        const q = this._query(filters);
        return this._request('GET', `/v1.0/api/tenants/${encodeURIComponent(tenantId)}/lock-audit/summary${q}`);
    }

    // ---- Request history ----

    /**
     * Retrieves a page of request history.
     * @param {object} [filters] - { method?, statusCode?, pathContains?, fromUtc?, toUtc?, pageNumber?, pageSize?, tenantId? }.
     * @returns {Promise<object>} A page { items, pageNumber, pageSize, totalCount }.
     */
    async getRequestHistory(filters = {}) {
        const q = this._query(filters);
        return this._request('GET', `/v1.0/api/request-history${q}`);
    }

    /**
     * Retrieves a bucketed summary of request history.
     * @param {object} [filters] - { method?, statusCode?, pathContains?, fromUtc?, toUtc?, bucketMinutes?, tenantId? }.
     * @returns {Promise<object>} The request history summary.
     */
    async getRequestHistorySummary(filters = {}) {
        const q = this._query(filters);
        return this._request('GET', `/v1.0/api/request-history/summary${q}`);
    }

    /**
     * Retrieves a single request history entry.
     * @param {string} id - The entry identifier.
     * @returns {Promise<object>} The request history entry.
     */
    async getRequestHistoryEntry(id) {
        return this._request('GET', `/v1.0/api/request-history/${encodeURIComponent(id)}`);
    }

    /**
     * Deletes a single request history entry.
     * @param {string} id - The entry identifier.
     * @returns {Promise<void>}
     */
    async deleteRequestHistoryEntry(id) {
        await this._request('DELETE', `/v1.0/api/request-history/${encodeURIComponent(id)}`);
    }

    /**
     * Bulk-deletes request history entries matching the supplied filter.
     * @param {object} [filters] - { method?, statusCode?, pathContains?, fromUtc?, toUtc?, tenantId? }.
     * @returns {Promise<object>} The delete result { deletedCount }.
     */
    async deleteRequestHistory(filters = {}) {
        const q = this._query(filters);
        return this._request('DELETE', `/v1.0/api/request-history${q}`);
    }
}

module.exports = { ClutchAdminClient, ClutchError, LockMode, LockBehavior, AcquireResult };
