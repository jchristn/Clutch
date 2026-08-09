# Clutch SDK for JavaScript

The official JavaScript/Node.js SDK for [Clutch](https://github.com/jchristn/Clutch), a distributed lock platform. It provides two clients:

- **`ClutchAdminClient`** (`clutch-admin-sdk.js`) — a REST client for administration and observability, built on the global `fetch` API.
- **`ClutchLockClient`** (`clutch-lock-sdk.js`) — a WebSocket client for lock acquisition, built on the [`ws`](https://www.npmjs.com/package/ws) package.

> Alpha (v0.1.0). Endpoints and shapes are subject to change.

## Requirements

- Node.js 18 or newer (for the global `fetch` API).
- The `ws` package: `npm install`.

## Admin client

```js
const { ClutchAdminClient } = require('./clutch-admin-sdk');

const admin = new ClutchAdminClient('http://127.0.0.1:8090');
await admin.authenticateWithKey('clutch-default-access-key', 'clutch-default-secret-key');

const info = await admin.getServerInfo();
const tenants = await admin.listTenants();
const holders = await admin.listLocks(tenants[0].id);

// Application keys: the raw secret is returned only once, at creation.
const cred = await admin.createCredential(tenants[0].id, 'worker-key');
console.log(cred.secretKey);
```

Failures throw `ClutchError`, which carries `statusCode` and `responseBody`.

## Lock client

```js
const { ClutchLockClient, LockMode } = require('./clutch-lock-sdk');

const locks = new ClutchLockClient('http://127.0.0.1:8090', 'clutch-default-access-key', 'clutch-default-secret-key');
const welcome = await locks.connect();

const held = await locks.acquire('orders/42', LockMode.Write);
console.log(held.holderId, held.fencingToken);

// heartbeats are sent automatically at welcome.heartbeatIntervalMs
await locks.release(held.holderId);
await locks.close();
```

`acquire(key, mode, options)` accepts `{ behavior, timeoutMs, leaseMs, policy }`. `behavior` is `'FailFast'` (default) or `'Wait'`. A denied, timed-out, or policy-conflicting acquire throws `ClutchLockDeniedError` whose `result` is one of `Denied`, `Timeout`, `PolicyConflict`.

The lock client is an `EventEmitter`. Events: `'heartbeat'` (renewed leases), `'error'` (unsolicited error frame), `'close'` (connection closed).

## Enumerations

- `LockMode`: `Read`, `Write`, `Delete`.
- `LockBehavior`: `FailFast`, `Wait`.
- `AcquireResult`: `Acquired`, `Denied`, `Timeout`, `PolicyConflict`.

## Test harness (non-interactive)

```
npm install
node test-harness.js http://127.0.0.1:8090 clutch-default-access-key clutch-default-secret-key
```

The process exits `0` when every assertion passes, `1` otherwise.

## Interactive console

```
npm run console
```

Type `help` at the `clutch>` prompt. A typical session: `login key <accessKey> <secretKey>`, `serverinfo`, `tenants`, `connect <accessKey> <secretKey>`, `acquire orders/42 Write`, `held`, `release 1`.

## License

MIT. See [LICENSE.md](../../LICENSE.md).
