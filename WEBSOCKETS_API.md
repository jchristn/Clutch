# Clutch WebSocket API

> Alpha (v0.1.0). The protocol is subject to change.

Lock acquisition happens over a WebSocket. A client application opens one connection per tenant using an application key, then sends and receives JSON text frames. All locks a connection holds are released automatically when the connection closes, so the socket itself is the lease of last resort.

The WebSocket is hosted on the **same port** as the REST API. There is no separate port.

## Endpoint

```
ws://<host>:<port>/v1.0/lock/connect
```

Use `wss://` when the server is configured for TLS.

## Authentication

Authenticate on the upgrade request with the application key. Two ways to present it:

- **Header (preferred, used by the SDKs):** `x-clutch-access-key: <accessKey>`, and optionally `x-clutch-secret-key: <secretKey>`. When the secret is supplied it is verified in constant time; when it is omitted, the access key alone authenticates the connection (treat the access key as a secret).
- **Query string (for browsers and quick tests that cannot set upgrade headers):** `?accessKey=<accessKey>` and optionally `&secretKey=<secretKey>`.

The tenant is resolved from the key. If authentication fails the server closes the socket with close status `PolicyViolation`.

```
ws://localhost:8090/v1.0/lock/connect?accessKey=clutch-default-access-key
```

## Frames

Every frame is a JSON object with a `type` field. The server never sends binary frames.

### Server → client: `welcome`

Sent once, immediately after a successful connection.

```json
{
  "type": "welcome",
  "sessionId": "0f3c9d2e...",
  "tenantId": "ten_...",
  "defaultLeaseMs": 30000,
  "heartbeatIntervalMs": 10000
}
```

`sessionId` identifies this connection's lock ownership. `heartbeatIntervalMs` is the recommended interval at which the client should send heartbeats to keep held leases alive.

### Client → server: `acquire`

Request a lock. `mode` is `Read`, `Write`, or `Delete`. `behavior` is `FailFast` (default) or `Wait`.

```json
{
  "type": "acquire",
  "requestId": "r1",
  "key": "orders/42",
  "mode": "Write",
  "behavior": "Wait",
  "timeoutMs": 5000,
  "leaseMs": 30000,
  "policy": {
    "readMaxHolders": -1,
    "writeExclusivity": "Exclusive",
    "writeMaxHolders": 1,
    "writeBlocksReads": true,
    "defaultLeaseMs": 30000,
    "maxLeaseMs": 300000,
    "maxHoldMs": 3600000
  }
}
```

- `requestId` is echoed back so the client can correlate the response.
- `timeoutMs` applies only when `behavior` is `Wait`; it is clamped to the server maximum.
- `leaseMs` is the requested lease, clamped to the key's `maxLeaseMs`. Omit to use the key's default.
- `policy` is honored **only when this caller is the first to acquire the key** and thereby creates it. On an existing key the policy is ignored (the first acquirer's policy stands). `readMaxHolders: -1` means unlimited readers.

### Server → client: `acquired`

```json
{
  "type": "acquired",
  "requestId": "r1",
  "key": "orders/42",
  "mode": "Write",
  "holderId": "lkh_...",
  "fencingToken": 7,
  "leaseExpiresUtc": "2026-08-09T03:20:00Z"
}
```

`holderId` is used to release the lock. `fencingToken` is a per-key monotonic counter; pass it to any downstream resource so a stale holder can be rejected.

### Server → client: `denied`

Sent when a `FailFast` acquire is not immediately grantable, or when a `Wait` acquire reaches its timeout.

```json
{
  "type": "denied",
  "requestId": "r1",
  "key": "orders/42",
  "result": "Denied",
  "reason": "A write lock is currently held and blocks reads on this key."
}
```

`result` is `Denied` (fail-fast, incompatible), `Timeout` (waited past the deadline), or `PolicyConflict` (strict policy request that conflicts with the existing definition).

### Client → server: `release`

```json
{ "type": "release", "requestId": "r2", "key": "orders/42", "holderId": "lkh_..." }
```

### Server → client: `released`

```json
{ "type": "released", "requestId": "r2", "key": "orders/42", "holderId": "lkh_...", "released": true }
```

Releasing is idempotent; `released` is `false` if the holder was already gone.

### Client → server: `heartbeat`

Renew the leases of one or more holders owned by this connection. Send at roughly `heartbeatIntervalMs`.

```json
{ "type": "heartbeat", "holderIds": ["lkh_...", "lkh_..."] }
```

### Server → client: `heartbeat`

```json
{
  "type": "heartbeat",
  "renewed": [
    { "holderId": "lkh_...", "leaseExpiresUtc": "2026-08-09T03:21:00Z" }
  ]
}
```

A holder that has reached its key's `maxHoldMs` is not renewable and will be absent from `renewed`.

### `ping` / `pong`

```json
{ "type": "ping" }
```

```json
{ "type": "pong" }
```

### Server → client: `error`

```json
{ "type": "error", "requestId": "r1", "message": "acquire requires 'key' and 'mode'." }
```

## Lifecycle and safety

- A lock is owned by the connection that acquired it. When the socket closes for any reason, every lock held by that `sessionId` is released and its key's waiters are woken.
- Each hold also carries a TTL lease. A client that stops sending heartbeats loses its locks when the lease expires, even if the socket is half-open. Renew before `leaseExpiresUtc`.
- `Wait` acquires do not block other messages on the same connection; the client may have several outstanding requests, correlated by `requestId`.
- Acquisition is decided in the database inside a single transaction, so grants are consistent across every node behind the load balancer.

## Minimal example (Node)

```js
const ws = new WebSocket('ws://localhost:8090/v1.0/lock/connect?accessKey=clutch-default-access-key');
ws.addEventListener('message', (ev) => {
  const msg = JSON.parse(ev.data);
  if (msg.type === 'welcome') {
    ws.send(JSON.stringify({ type: 'acquire', requestId: 'r1', key: 'orders/42', mode: 'Write', behavior: 'FailFast' }));
  } else if (msg.type === 'acquired') {
    // ... do protected work, then release ...
    ws.send(JSON.stringify({ type: 'release', requestId: 'r2', key: 'orders/42', holderId: msg.holderId }));
  }
});
```
