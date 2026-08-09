# Clutch SDK for .NET

`Clutch.Sdk` is the official .NET SDK for [Clutch](https://github.com/jchristn/Clutch), a distributed lock platform. It provides two client surfaces:

- **`ClutchAdminClient`** — a REST client for administration and observability: tokens, tenants, users, application keys, lock inspection, audit, request history, health, and server info.
- **`ClutchLockClient`** — a WebSocket client that acquires and releases locks, correlates responses by request identifier, and automatically sends heartbeats to keep held leases alive.

> Alpha (v0.1.0). Endpoints and shapes are subject to change.

## Installation

```
dotnet add package Clutch.Sdk
```

Target framework: `net8.0`.

## Admin client

```csharp
using Clutch.Sdk;

using ClutchAdminClient admin = new ClutchAdminClient("http://127.0.0.1:8090");

// Authenticate with an application key (or AuthenticateWithPasswordAsync for user login).
TokenResponse token = await admin.AuthenticateWithKeyAsync("clutch-default-access-key");
string tenantId = token.TenantId!;

ServerInfo info = await admin.GetServerInfoAsync();
List<Tenant> tenants = await admin.ListTenantsAsync();
List<LockHolder> holders = await admin.ListLocksAsync(tenantId);

// Application keys authenticate with the access key alone.
Credential cred = await admin.CreateCredentialAsync(tenantId, "worker-key");
Console.WriteLine(cred.AccessKey);
```

The client is `IDisposable`; every async method accepts a `CancellationToken`. Failures throw `ClutchException`, which carries the HTTP `StatusCode` and raw `ResponseBody` when available.

## Lock client

Lock acquisition happens over a WebSocket. Every lock the connection holds is released automatically when the connection closes.

```csharp
using Clutch.Sdk;

using ClutchLockClient locks = new ClutchLockClient("http://127.0.0.1:8090", "clutch-default-access-key");
WelcomeInfo welcome = await locks.ConnectAsync();

AcquiredLock held = await locks.AcquireAsync("orders/42", LockMode.Write);
Console.WriteLine($"holder={held.HolderId} fencing={held.FencingToken}");

// ... do protected work; heartbeats are sent automatically at welcome.HeartbeatIntervalMs ...

await locks.ReleaseAsync(held.HolderId);
await locks.CloseAsync();
```

`AcquireAsync` accepts an `AcquireOptions` with `Behavior` (`FailFast` or `Wait`), `TimeoutMs`, `LeaseMs`, and an optional `LockPolicy` honored only when this caller creates the key. A denied, timed-out, or policy-conflicting acquire throws `LockDeniedException` whose `Result` is one of `Denied`, `Timeout`, or `PolicyConflict`.

Events: `HeartbeatReceived`, `ErrorReceived`, and `Closed`.

## Enumerations

- `LockMode`: `Read`, `Write`, `Delete`.
- `LockBehavior`: `FailFast`, `Wait`.
- `AcquireResult`: `Acquired`, `Denied`, `Timeout`, `PolicyConflict`.

## Solution layout

| Project | Description |
|---------|-------------|
| `Clutch.Sdk` | The SDK library (packable to NuGet). |
| `Clutch.Sdk.Test` | Non-interactive test application. Asserts behavior and exits `0` (pass) or `1` (fail). |
| `Clutch.Sdk.Console` | Interactive REPL for driving the SDK by hand. |

### Run the test application

```
dotnet run --project Clutch.Sdk.Test -- http://127.0.0.1:8090 clutch-default-access-key
```

Arguments: `<endpoint> <accessKey>`. The process exits `0` when every check passes, `1` otherwise.

### Run the interactive console

```
dotnet run --project Clutch.Sdk.Console -- http://127.0.0.1:8090
```

Type `help` at the `clutch>` prompt. A typical session: `login key <accessKey>`, `serverinfo`, `tenants`, `connect <accessKey>`, `acquire orders/42 Write`, `held`, `release 1`.

## License

MIT. See [LICENSE.md](../../LICENSE.md).
