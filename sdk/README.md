# Clutch SDKs

Client SDKs for [Clutch](https://github.com/jchristn/Clutch), a distributed lock platform. Each SDK exposes two client surfaces that mirror the server's two APIs:

- an **admin client** over the REST API (administration and observability: tokens, tenants, users, application keys, lock inspection, audit, request history, health, server info), and
- a **lock client** over the WebSocket API (acquire, release, and automatic heartbeat management for held leases).

Every SDK ships a **non-interactive test application** (assertions, exit code `0`/`1`) and an **interactive console application** you can drive by hand.

Which database backs the server — PostgreSQL, MySQL, SQL Server, or SQLite — is a server-side configuration choice and is transparent to clients. The SDKs never send or manage database settings; the active backend is surfaced only as a display string on `GET /v1.0/api/server-info`.

> Alpha (v0.2.0). Endpoints and shapes are subject to change. Loopback examples use `127.0.0.1` rather than `localhost` to avoid the Windows IPv6 (`::1`) resolution stall.

## Language matrix

| Language | Directory | Admin client | Lock client | Dependencies | Test app | Console app |
|----------|-----------|--------------|-------------|--------------|----------|-------------|
| C# (.NET 8) | [`csharp/`](csharp/) | `ClutchAdminClient` | `ClutchLockClient` | none (BCL `HttpClient` + `ClientWebSocket`) | `Clutch.Sdk.Test` | `Clutch.Sdk.Console` |
| JavaScript (Node 18+) | [`js/`](js/) | `ClutchAdminClient` (`clutch-admin-sdk.js`) | `ClutchLockClient` (`clutch-lock-sdk.js`) | `ws` | `test-harness.js` | `console.js` |
| Python (3.8+) | [`python/`](python/) | `ClutchAdminClient` (`clutch_admin_sdk.py`) | `ClutchLockClient` (`clutch_lock_sdk.py`) | `requests`, `websocket-client` | `test_harness.py` | `console.py` |

Enumerations are consistent across languages: `LockMode` (`Read`, `Write`, `Delete`), `LockBehavior` (`FailFast`, `Wait`), and `AcquireResult` (`Acquired`, `Denied`, `Timeout`, `PolicyConflict`).

## Running the test applications

Each test application takes `<endpoint> <accessKey>` and exits `0` when every assertion passes, `1` otherwise. Against a local dev server the defaults are `http://127.0.0.1:8090` and `clutch-default-access-key`. Clients authenticate with the access key alone.

**C#**

```
cd csharp
dotnet build Clutch.Sdk.sln
dotnet run --project Clutch.Sdk.Test -- http://127.0.0.1:8090 clutch-default-access-key
```

**JavaScript**

```
cd js
npm install
node test-harness.js http://127.0.0.1:8090 clutch-default-access-key
```

**Python**

```
cd python
pip install -r requirements.txt
python test_harness.py http://127.0.0.1:8090 clutch-default-access-key
```

Each harness exercises the same behavior: health, authentication, token details, server info, tenant and credential CRUD, lock inspection, request-history summary, WebSocket connect/welcome, acquire/release, monotonic fencing-token increase, shared readers, a write denying a fail-fast read, a waiting read timing out, and heartbeat renewal.

## Running the interactive consoles

Each console presents a `clutch>` prompt; type `help` for the command list. A typical session is `login key <accessKey>`, `serverinfo`, `tenants`, `connect <accessKey>`, `acquire orders/42 Write`, `held`, `release 1`.

**C#**

```
cd csharp
dotnet run --project Clutch.Sdk.Console -- http://127.0.0.1:8090
```

**JavaScript**

```
cd js
npm run console            # or: node console.js http://127.0.0.1:8090
```

**Python**

```
cd python
python console.py http://127.0.0.1:8090
```

## API reference

- REST API: [`../REST_API.md`](../REST_API.md)
- WebSocket API: [`../WEBSOCKETS_API.md`](../WEBSOCKETS_API.md)

## License

MIT. See [LICENSE.md](../LICENSE.md).
