# Clutch REST API

> Alpha (v0.1.0). Endpoints and shapes are subject to change.

The REST API handles administration and observability: tokens, tenants, users, application keys, lock inspection, audit, request history, and server info. It also exposes **client lock operations** (acquire, release, heartbeat) so a caller can manage locks over REST exactly as a client does over the [WebSocket API](WEBSOCKETS_API.md) — the same lock engine backs both.

- Base URL: `http://<host>:<port>` (default port `8080`; the local dev examples use `8090`).
- All request and response bodies are JSON with camelCase property names.
- OpenAPI document: `GET /openapi.json`.

## Authentication

Most routes require a bearer token obtained from `POST /v1.0/token`. Send it as `Authorization: Bearer <token>` (or `x-token: <token>`). A system administrator API key may instead be sent as `x-api-key: <key>` on any route.

Authorization is a three-tier model (no RBAC): **system admin** (`isAdmin`) manages all tenants; **tenant admin** (`isTenantAdmin`) manages its own tenant; a **regular user** has read-only access within its tenant. Server-side enforcement is authoritative; any client-side gating is cosmetic.

Status codes: `200` ok, `201` created, `204` no content, `400` bad request, `401` unauthorized, `403` forbidden, `404` not found, `503` degraded (health).

## Health

`GET /v1.0/api/health` — anonymous.

```json
{ "status": "healthy", "node": "node1", "database": true, "utc": "2026-08-09T03:20:00Z" }
```

## Tokens

**`POST /v1.0/token`** — anonymous. Log in with an application key or with user credentials.

```json
{ "accessKey": "clutch-default-access-key" }
```
or
```json
{ "tenantId": "ten_...", "email": "admin@clutch.local", "password": "clutchadmin" }
```
Response: `{ "token": "<opaque>", "principalType": "Credential", "tenantId": "ten_..." }`

- **`GET /v1.0/token`** — validate the current token; returns the resolved principal context.
- **`GET /v1.0/token/details`** — same as above.
- **`DELETE /v1.0/token`** — revoke the current session (logout); `204`.

## Tenants

- **`GET /v1.0/api/tenants`** — system admin sees all tenants; others see only their own.
- **`POST /v1.0/api/tenants`** — system admin. Body: `{ name, lockHistoryRetentionDays, defaultLeaseMs, maxLeaseMs }`. `201`.
- **`GET /v1.0/api/tenants/{id}`** — read.
- **`PUT /v1.0/api/tenants/{id}`** — system admin or the tenant's admin.
- **`DELETE /v1.0/api/tenants/{id}`** — system admin. Cascades to users, credentials, locks, and audit.

Tenant shape: `{ id, name, lockHistoryRetentionDays, defaultLeaseMs, maxLeaseMs, active, isProtected, createdUtc, lastUpdateUtc }`.

## Users

Scoped to a tenant. Read requires tenant read access; mutation requires tenant admin (or system admin).

- **`GET /v1.0/api/tenants/{tid}/users`**
- **`POST /v1.0/api/tenants/{tid}/users`** — `{ email, password, firstName, lastName, isSystemAdmin, isTenantAdmin, active }`. Only a system admin may set `isSystemAdmin`. `201`.
- **`GET|PUT|DELETE /v1.0/api/tenants/{tid}/users/{id}`**

Password hashes are never returned.

## Application keys (credentials)

Scoped to a tenant.

- **`GET /v1.0/api/tenants/{tid}/credentials`** — lists application keys.
- **`POST /v1.0/api/tenants/{tid}/credentials`** — `{ name, userId?, expiresUtc? }`. The server generates the access key and returns it. The access key is the sole credential — there is no secret key. `201`.
- **`GET|DELETE /v1.0/api/tenants/{tid}/credentials/{id}`**

Create response: `{ id, tenantId, userId, name, accessKey, authMode, expiresUtc, active, createdUtc }`. The access key is presented on connect (`x-clutch-access-key`); treat it as a secret. No secret key is ever issued or accepted.

## Locks — client operations

These routes acquire, hold, and release locks over REST, mirroring the WebSocket frames of the same name. Any authenticated principal within the tenant may call them. Unlike a WebSocket holder — which is released automatically when the socket closes — a **REST holder has no connection to close**, so it lives entirely on its TTL lease: renew it with `heartbeat`, release it explicitly, or let the lease lapse and be swept.

A **session** groups the holders a caller owns. Omit `sessionId` on the first `acquire` and the server assigns one (returned in the response); pass that same `sessionId` back to acquire more holders under it, and to release or heartbeat them. The session id is the ownership secret for release/heartbeat — treat it accordingly.

The lock key is a path segment, so a key containing `/` (for example `orders/42`) must be **URL-encoded** in the path (`orders%2F42`); the server decodes it, so the key seen by the engine is identical to the one a WebSocket client sends in its frame.

- **`POST /v1.0/api/tenants/{tid}/locks/{key}/acquire`** — acquire a lock on `{key}`.
  Body: `{ mode, behavior?, timeoutMs?, leaseMs?, sessionId?, policy?, strictPolicy? }` where `mode` is `Read`|`Write`|`Delete` and `behavior` is `FailFast` (default) or `Wait`. `timeoutMs` applies only to `Wait`. `policy` is honored **only when this caller creates the key** (first acquirer); on an existing key it is ignored unless `strictPolicy` is true, in which case a conflicting policy is rejected.
  Granted → `201` `{ result: "Granted", granted: true, key, mode, holderId, sessionId, fencingToken, leaseExpiresUtc }`.
  Not granted → `409` `{ result, granted: false, key, sessionId, reason }` where `result` is `Denied` (incompatible, fail-fast), `Timeout` (waited past the deadline), or `PolicyConflict`.
- **`POST /v1.0/api/tenants/{tid}/locks/{key}/release`** — release one held lock. Body: `{ holderId, sessionId }`. Only a holder owned by `sessionId` is released. Idempotent. Returns `{ key, holderId, released }` (`released` is `false` if the holder was already gone).
- **`POST /v1.0/api/tenants/{tid}/lock-sessions/{sid}/heartbeat`** — renew the leases of holders owned by session `{sid}`. Body: `{ holderIds: [ ... ] }`. Returns `{ sessionId, renewed: [ { holderId, leaseExpiresUtc } ] }`. A holder that has reached its key's `maxHoldMs` is not renewable and is omitted from `renewed`.
- **`POST /v1.0/api/tenants/{tid}/lock-sessions/{sid}/release`** — release **every** holder owned by session `{sid}` (the REST equivalent of a WebSocket disconnect). Returns `{ sessionId, released: [ holderId, ... ], count }`.

`fencingToken` is a per-key monotonic counter returned on grant; pass it to any downstream resource so a stale holder can be rejected.

## Locks — observe + administer

- **`GET /v1.0/api/tenants/{tid}/locks`** — active holders. Query: `name` (key substring), `mode` (`Read`|`Write`|`Delete`).
- **`GET /v1.0/api/tenants/{tid}/locks/{key}`** — `{ definition, holders }` for one key.
- **`POST /v1.0/api/tenants/{tid}/locks/{key}/force-release`** — tenant admin. Force-releases every holder on the key regardless of session. Returns `{ key, released }`.

Holder shape: `{ id, tenantId, lockKey, mode, credentialId, sessionId, nodeId, fencingToken, acquiredUtc, leaseExpiresUtc, lastHeartbeatUtc }`.

## Lock audit and activity chart

- **`GET /v1.0/api/tenants/{tid}/lock-audit`** — paginated audit entries. Query: `name`, `mode`, `fromUtc`, `toUtc`, `pageNumber`, `pageSize`. Returns `{ items, pageNumber, pageSize, totalCount }`.
- **`GET /v1.0/api/tenants/{tid}/lock-audit/summary`** — time-bucketed acquire counts for the lock-activity chart. Query: `name`, `mode`, `fromUtc`, `toUtc`, `bucketCount`. Returns `{ fromUtc, toUtc, bucketCount, bucketStartsUtc: [...], series: [{ lockKey, mode, label, counts: [...] }] }`.

Audit event types: `PolicyCreated`, `Acquired`, `Released`, `Waited`, `Denied`, `Expired`, `Revoked`, `HeartbeatRenewed`.

## Request history

Tenant-scoped from the token; a system admin may widen scope with `?tenantId=`.

- **`GET /v1.0/api/request-history`** — list (bodies omitted). Query: `method`, `statusCode`, `pathContains`, `fromUtc`, `toUtc`, `pageNumber`, `pageSize`, `tenantId` (admin). Returns `{ items, pageNumber, pageSize, totalCount }`.
- **`GET /v1.0/api/request-history/summary`** — bucketed counts. Query adds `bucketMinutes`. Returns `{ totalCount, totalSuccess, totalFailure, averageDurationMs, buckets: [{ bucketStartUtc, bucketEndUtc, successCount, failureCount, averageDurationMs }] }`.
- **`GET /v1.0/api/request-history/{id}`** — full entry including redacted headers and (truncated) bodies.
- **`DELETE /v1.0/api/request-history/{id}`** — `204`.
- **`DELETE /v1.0/api/request-history`** — bulk delete matching the filter; returns `{ deletedCount }`.

Secret-bearing headers (`Authorization`, `x-token`, anything matching `*api-key*`/`*token*`/`*secret*`) are redacted; bodies are truncated to a configurable byte threshold.

## Server info

**`GET /v1.0/api/server-info`** — `{ product, version, node, database, webSocketConnections, telemetry: { enabled, prometheusPort, prometheusPath }, principal: { authenticated, tenantId, isAdmin, isTenantAdmin, principalName } }`.

## Telemetry

Prometheus metrics are exposed on a separate port (default `9464`) at `/metrics`, not on the REST port. Metrics include `clutch_lock_acquire_total{mode,outcome}`, `clutch_lock_release_total`, `clutch_lock_acquire_duration`, `clutch_http_request_total`, and process/runtime series.
