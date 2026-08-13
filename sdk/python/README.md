# Clutch SDK for Python

The official Python SDK for [Clutch](https://github.com/jchristn/Clutch), a distributed lock platform. It provides two clients:

- **`ClutchAdminClient`** (`clutch_admin_sdk.py`) — a REST client for administration and observability, built on `requests`.
- **`ClutchLockClient`** (`clutch_lock_sdk.py`) — a WebSocket client for lock acquisition, built on `websocket-client`.

> Alpha (v0.2.0). Endpoints and shapes are subject to change.

## Installation

```
pip install -r requirements.txt
```

Requires Python 3.8+.

## Admin client

```python
from clutch_admin_sdk import ClutchAdminClient

admin = ClutchAdminClient("http://127.0.0.1:8090")
admin.authenticate_with_key("clutch-default-access-key")

info = admin.get_server_info()
tenants = admin.list_tenants()
holders = admin.list_locks(tenants[0]["id"])

# Application keys authenticate with the access key alone.
cred = admin.create_credential(tenants[0]["id"], "worker-key")
print(cred["accessKey"])
```

Responses are plain dicts (camelCase keys, as returned by the server). Failures raise `ClutchError`, which carries `status_code` and `response_body`. The client supports the context-manager protocol (`with ClutchAdminClient(...) as admin:`).

## Lock client

```python
from clutch_admin_sdk import LockMode
from clutch_lock_sdk import ClutchLockClient

locks = ClutchLockClient("http://127.0.0.1:8090", "clutch-default-access-key")
welcome = locks.connect()

held = locks.acquire("orders/42", LockMode.Write)
print(held["holderId"], held["fencingToken"])

# heartbeats are sent automatically at welcome["heartbeatIntervalMs"]
locks.release(held["holderId"])
locks.close()
```

`acquire(key, mode, behavior=..., timeout_ms=..., lease_ms=..., policy=...)` — `behavior` is `LockBehavior.FailFast` (default) or `LockBehavior.Wait`. A denied, timed-out, or policy-conflicting acquire raises `ClutchLockDeniedError` whose `result` is one of `Denied`, `Timeout`, `PolicyConflict`.

Assign optional callbacks: `locks.on_heartbeat = fn`, `locks.on_error = fn`, `locks.on_close = fn`.

## Enumerations

- `LockMode`: `Read`, `Write`, `Delete`.
- `LockBehavior`: `FailFast`, `Wait`.
- `AcquireResult`: `Acquired`, `Denied`, `Timeout`, `PolicyConflict`.

## Test harness (non-interactive)

```
python test_harness.py http://127.0.0.1:8090 clutch-default-access-key
```

The process exits `0` when every assertion passes, `1` otherwise.

## Interactive console

```
python console.py http://127.0.0.1:8090
```

Type `help` at the `clutch>` prompt. A typical session: `login key <accessKey>`, `serverinfo`, `tenants`, `connect <accessKey>`, `acquire orders/42 Write`, `held`, `release 1`.

## License

MIT. See [LICENSE.md](../../LICENSE.md).

> The backing database (PostgreSQL, MySQL, SQL Server, or SQLite) is a server-side choice and is transparent to clients; the SDK reports it only as a display string via `getServerInfo`.
