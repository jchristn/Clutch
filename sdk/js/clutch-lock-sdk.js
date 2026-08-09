'use strict';

/**
 * Clutch Lock SDK (JavaScript)
 *
 * WebSocket client for the Clutch lock protocol. Manages one connection per tenant, acquires and
 * releases locks, correlates responses by request identifier, and automatically sends heartbeats
 * to keep held leases alive. Uses the `ws` package for Node.js.
 */

const { EventEmitter } = require('events');
const WebSocket = require('ws');
const { ClutchError, LockMode, LockBehavior, AcquireResult } = require('./clutch-admin-sdk');

/**
 * Error thrown when a lock acquisition is denied, times out, or conflicts with an existing policy.
 */
class ClutchLockDeniedError extends ClutchError {
    /**
     * @param {string} result - One of AcquireResult (Denied, Timeout, PolicyConflict).
     * @param {string} key - The requested key.
     * @param {string} message - A human-readable explanation.
     */
    constructor(result, key, message) {
        super(message);
        this.name = 'ClutchLockDeniedError';
        this.result = result;
        this.key = key;
    }
}

/**
 * Builds the WebSocket URL from an HTTP(S) endpoint.
 * @param {string} endpoint - Base URL, e.g. "http://127.0.0.1:8090".
 * @returns {string} The lock connect URL.
 */
function buildWebSocketUrl(endpoint) {
    let trimmed = endpoint.replace(/\/+$/, '');
    if (trimmed.startsWith('https://')) trimmed = 'wss://' + trimmed.slice('https://'.length);
    else if (trimmed.startsWith('http://')) trimmed = 'ws://' + trimmed.slice('http://'.length);
    return `${trimmed}/v1.0/lock/connect`;
}

/**
 * WebSocket lock client.
 *
 * Events:
 *  - 'heartbeat' (renewed[])  server renewed one or more held leases.
 *  - 'error' (message)        an unsolicited error frame was received.
 *  - 'close' (reason)         the connection closed.
 */
class ClutchLockClient extends EventEmitter {
    /**
     * @param {string} endpoint - Base URL, e.g. "http://127.0.0.1:8090".
     * @param {string} accessKey - The application access key.
     */
    constructor(endpoint, accessKey) {
        super();
        if (!endpoint) throw new ClutchError('endpoint is required');
        if (!accessKey) throw new ClutchError('accessKey is required');
        this.url = buildWebSocketUrl(endpoint);
        this.accessKey = accessKey;
        this.welcome = null;
        this._ws = null;
        this._pending = new Map();
        this._held = new Map();
        this._heartbeatTimer = null;
        this._nextRequestId = 1;
    }

    /**
     * Opens the connection, waits for the welcome frame, and starts the heartbeat loop.
     * @returns {Promise<object>} The welcome info { sessionId, tenantId, defaultLeaseMs, heartbeatIntervalMs }.
     */
    connect() {
        return new Promise((resolve, reject) => {
            const headers = { 'x-clutch-access-key': this.accessKey };

            this._ws = new WebSocket(this.url, { headers });

            const onOpenError = (err) => reject(new ClutchError(`Failed to connect to ${this.url}: ${err.message}`));
            this._ws.once('error', onOpenError);

            this._ws.on('message', (data) => this._dispatch(data.toString()));

            this._ws.on('close', (code, reasonBuffer) => {
                const reason = reasonBuffer ? reasonBuffer.toString() : `code ${code}`;
                this._stopHeartbeat();
                this._failAllPending(new ClutchError(`The connection closed: ${reason}`));
                this.emit('close', reason);
            });

            this._welcomeResolve = (welcome) => {
                this._ws.removeListener('error', onOpenError);
                this._ws.on('error', (err) => this.emit('error', err.message));
                this._startHeartbeat();
                resolve(welcome);
            };
            this._welcomeReject = reject;
        });
    }

    /**
     * Acquires a lock on a key.
     * @param {string} key - The key to lock.
     * @param {string} mode - One of LockMode (Read, Write, Delete).
     * @param {object} [options] - { behavior?, timeoutMs?, leaseMs?, policy? }.
     * @returns {Promise<object>} The acquired lock { key, mode, holderId, fencingToken, leaseExpiresUtc }.
     * @throws {ClutchLockDeniedError} When the acquisition is denied, times out, or conflicts with a policy.
     */
    async acquire(key, mode, options = {}) {
        this._ensureConnected();
        const requestId = this._requestId();
        const frame = {
            type: 'acquire',
            requestId,
            key,
            mode,
            behavior: options.behavior || LockBehavior.FailFast
        };
        if (options.timeoutMs !== undefined) frame.timeoutMs = options.timeoutMs;
        if (options.leaseMs !== undefined) frame.leaseMs = options.leaseMs;
        if (options.policy !== undefined) frame.policy = options.policy;

        const response = await this._sendAndAwait(requestId, frame);
        if (response.type === 'acquired') {
            const held = {
                key: response.key,
                mode: response.mode,
                holderId: response.holderId,
                fencingToken: response.fencingToken,
                leaseExpiresUtc: response.leaseExpiresUtc
            };
            this._held.set(held.holderId, held);
            return held;
        }
        if (response.type === 'denied') {
            throw new ClutchLockDeniedError(response.result || AcquireResult.Denied, key, response.reason || 'The lock request was denied.');
        }
        if (response.type === 'error') {
            throw new ClutchError(`The server rejected the acquire request for '${key}': ${response.message}`);
        }
        throw new ClutchError(`Unexpected response type '${response.type}' to the acquire request for '${key}'.`);
    }

    /**
     * Releases a held lock. Releasing is idempotent.
     * @param {string} holderId - The holder identifier returned when the lock was acquired.
     * @returns {Promise<boolean>} True when a holder was released; false when it was already gone.
     */
    async release(holderId) {
        this._ensureConnected();
        const held = this._held.get(holderId);
        const key = held ? held.key : '';
        const requestId = this._requestId();
        const response = await this._sendAndAwait(requestId, { type: 'release', requestId, key, holderId });
        this._held.delete(holderId);
        if (response.type === 'error') {
            throw new ClutchError(`The server rejected the release request for holder '${holderId}': ${response.message}`);
        }
        return response.released === true;
    }

    /**
     * Sends a heartbeat for the supplied holders, or for all held locks when none are given.
     * @param {string[]|null} [holderIds=null] - Holders to renew, or null for all held.
     * @returns {Promise<void>}
     */
    async heartbeat(holderIds = null) {
        this._ensureConnected();
        const ids = holderIds || Array.from(this._held.keys());
        if (ids.length === 0) return;
        this._send({ type: 'heartbeat', holderIds: ids });
    }

    /**
     * Closes the connection, releasing every lock held by this connection on the server side.
     * @returns {Promise<void>}
     */
    close() {
        return new Promise((resolve) => {
            this._stopHeartbeat();
            if (!this._ws || this._ws.readyState === WebSocket.CLOSED) {
                resolve();
                return;
            }
            this._ws.once('close', () => resolve());
            try {
                this._ws.close(1000, 'client closing');
            } catch (err) {
                resolve();
            }
        });
    }

    /**
     * Returns the locks currently held on this connection.
     * @returns {object[]} The held locks.
     */
    heldLocks() {
        return Array.from(this._held.values());
    }

    // ---- internals ----

    /** @private */
    _requestId() {
        return `r${this._nextRequestId++}`;
    }

    /** @private */
    _ensureConnected() {
        if (!this._ws || this._ws.readyState !== WebSocket.OPEN) {
            throw new ClutchError('The lock client is not connected. Call connect() first.');
        }
    }

    /** @private */
    _send(frame) {
        this._ws.send(JSON.stringify(frame));
    }

    /** @private */
    _sendAndAwait(requestId, frame) {
        return new Promise((resolve, reject) => {
            this._pending.set(requestId, { resolve, reject });
            try {
                this._send(frame);
            } catch (err) {
                this._pending.delete(requestId);
                reject(new ClutchError(`Failed to send a frame: ${err.message}`));
            }
        });
    }

    /** @private */
    _dispatch(text) {
        if (!text) return;
        let msg;
        try {
            msg = JSON.parse(text);
        } catch (err) {
            return;
        }

        switch (msg.type) {
            case 'welcome':
                this.welcome = {
                    sessionId: msg.sessionId,
                    tenantId: msg.tenantId,
                    defaultLeaseMs: msg.defaultLeaseMs,
                    heartbeatIntervalMs: msg.heartbeatIntervalMs
                };
                if (this._welcomeResolve) this._welcomeResolve(this.welcome);
                break;
            case 'acquired':
            case 'denied':
            case 'released':
                this._complete(msg);
                break;
            case 'heartbeat':
                this._handleHeartbeat(msg);
                break;
            case 'error':
                if (msg.requestId && this._pending.has(msg.requestId)) {
                    this._complete(msg);
                } else {
                    this.emit('error', msg.message || 'unknown error');
                }
                break;
            case 'pong':
            default:
                break;
        }
    }

    /** @private */
    _complete(msg) {
        if (!msg.requestId) return;
        const entry = this._pending.get(msg.requestId);
        if (entry) {
            this._pending.delete(msg.requestId);
            entry.resolve(msg);
        }
    }

    /** @private */
    _handleHeartbeat(msg) {
        const renewed = [];
        if (Array.isArray(msg.renewed)) {
            for (const item of msg.renewed) {
                const held = this._held.get(item.holderId);
                if (held) {
                    held.leaseExpiresUtc = item.leaseExpiresUtc;
                    renewed.push(held);
                }
            }
        }
        if (renewed.length > 0) this.emit('heartbeat', renewed);
    }

    /** @private */
    _startHeartbeat() {
        const interval = Math.max(1000, (this.welcome && this.welcome.heartbeatIntervalMs) || 10000);
        this._heartbeatTimer = setInterval(() => {
            if (this._held.size === 0) return;
            try {
                this._send({ type: 'heartbeat', holderIds: Array.from(this._held.keys()) });
            } catch (err) {
                // The close handler will surface a dead connection.
            }
        }, interval);
        if (this._heartbeatTimer.unref) this._heartbeatTimer.unref();
    }

    /** @private */
    _stopHeartbeat() {
        if (this._heartbeatTimer) {
            clearInterval(this._heartbeatTimer);
            this._heartbeatTimer = null;
        }
    }

    /** @private */
    _failAllPending(error) {
        for (const entry of this._pending.values()) {
            entry.reject(error);
        }
        this._pending.clear();
    }
}

module.exports = { ClutchLockClient, ClutchLockDeniedError, ClutchError, LockMode, LockBehavior, AcquireResult };
