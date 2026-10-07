# Changelog

All notable changes to Clutch are documented here. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project uses [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.3.0] - 2026-10-06

Native AOT. The server, `Clutch.Core`, and the `Clutch.Sdk` package are now trimming and Native AOT compatible, and every commit is tested on Linux, Windows, and macOS. The JSON on the wire is unchanged. Still alpha: APIs, schema, settings, and behavior can change without notice.

### Server

- All JSON goes through source-generated `System.Text.Json` metadata: REST request and response bodies, WebSocket frames, MCP tool results, the settings file, session token payloads, and stored request headers. The serializer options keep their naming, casing, and null handling, so every response keeps the same property names, order, and values as before. A type missing from the metadata now fails in every test run, not only in a native binary.
- Anonymous response objects are replaced with named response types (lock acquire, release, heartbeat, and session release; server info; settings save, restart, and database test; request-history delete; and every WebSocket frame), and MCP tool input schemas are `JsonElement` values.
- String enum converters are the generic `JsonStringEnumConverter<TEnum>` form.
- `Clutch.Server` and `Clutch.Core` are `IsAotCompatible`, with trimming and AOT warnings treated as build errors. The server publishes as a native binary with `-p:PublishAot=true`. Microsoft.Data.SqlClient still reports its own trimming and AOT warnings, so test SQL Server deployments before running them natively.
- The server and Core assemblies are now versioned `0.3.0`, so `GET /v1.0/api/server-info` reports `0.3.0.0` (it previously reported the default `1.0.0.0`).
- Dependencies: Voltaic 2.2.1 → 2.3.0 and Watson 7.2.2 → 7.3.0, both of which are themselves Native AOT compatible.
- The MCP server and the OpenAPI document report the product version (`v0.3.0` and `0.3.0`) instead of a hardcoded `v1.0`; the version lives in `Constants.ProductVersion`.

### Fixed

- The in-process Prometheus endpoint no longer fails at startup with default settings. The former default `Telemetry.PrometheusHostname` of `*` is not accepted by the OpenTelemetry listener (`Invalid URI`), which disabled telemetry entirely. The default is now `localhost`, and wildcard values (`*`, `+`, `0.0.0.0`, `::`, `[::]`), including the `*` saved in existing settings files, are treated as `localhost` and rewritten on the next start. The listener answers only requests addressed to its configured hostname, so scrape it by that name. The Docker deployment pushes OTLP and is unaffected.

### SDK

- `Clutch.Sdk` 0.3.0 is `IsAotCompatible` and works in trimmed and Native AOT applications. Request bodies are named internal types and the clients use source-generated metadata; the public API and the JSON they send are unchanged.
- The SDK test application's checks moved into `SdkChecks`, which the Native AOT test also compiles.
- The JavaScript SDK package version moves to 0.3.0 to match the platform release.

### Testing and CI

- New `src/Test.Aot`: a Native AOT console application that starts a full node in-process on SQLite and runs 56 checks against it: the settings file, raw REST (OpenAPI, tokens, error bodies, tenants, users, credentials, the HTTP lock lifecycle, lock audit, settings, request history, and the tenant purge), the SDK checks, WebSocket ping and error frames, and MCP (handshake, `tools/list` schemas, and every tool). It is published with trimming and AOT warnings as errors and passes as a native binary on net8.0 and net10.0. It also scrapes the Prometheus endpoint, as does a new telemetry suite in the shared Touchstone tests.
- New GitHub Actions workflows. `tests.yml` runs on every commit on Linux, Windows, and macOS for net8.0 and net10.0: the Touchstone suite through the console, xUnit, and NUnit runners (SQLite everywhere; PostgreSQL, MySQL, and SQL Server on Linux), the Native AOT checks under the JIT, the C#, JavaScript, and Python SDK harnesses against a live server, and the dashboard lint and build. `native-aot.yml` publishes and runs `Test.Aot` and publishes `Clutch.Server` natively on all three platforms, failing on any trimming or AOT warning outside the SqlClient allow-list.

## [0.2.0] - 2026-08-12

Bring your own database. Clutch now runs on PostgreSQL, MySQL, SQL Server, or SQLite — you point it at a database you already own, name the tables it uses, and decide whether it may create them. Still alpha: APIs, schema, settings, and behavior can change without notice.

### Breaking

- Default table names moved from the bare v0.1.0 forms (`tenants`, `lock_holders`, …) to `clutch_`-prefixed names (`clutch_tenants`, `clutch_lock_holders`, …) so Clutch's tables are self-identifying inside a database you already own. There is no automatic migration from a v0.1.0 database. To keep an existing database, either override the table names back to the bare forms in server settings, or rename the tables to the new defaults using the shipped `sql/{provider}/schema.sql` as the reference.

### Server

- Four database providers behind one provider-neutral driver: **PostgreSQL** and **MySQL** and **SQL Server** for multi-node clustering, and **SQLite** for single-node, development, and embedded use. Startup warns when SQLite is selected.
- Configurable schema: per-purpose table names, an optional schema/namespace (PostgreSQL and SQL Server), and an optional global prefix. Table names are validated against a strict identifier allowlist. Clutch owns the column layout.
- `ManageSchema` setting (default true). When false, Clutch issues no DDL and instead verifies at startup that every configured table exists, failing with a clear error if one is missing. Idempotent `sql/{provider}/schema.sql` scripts ship for every provider.
- Cross-node coordination standardized on bounded polling. The PostgreSQL `LISTEN/NOTIFY` path (and `pg_notify`) is removed; the database transaction remains the sole authority for every grant, and waiter wakeup latency is bounded by the poll interval. Acquire serialization is provider-specific — `FOR UPDATE` on PostgreSQL and MySQL, `UPDLOCK`/`HOLDLOCK` range locks on SQL Server, and an IMMEDIATE transaction on SQLite — with automatic retry on transient deadlocks and serialization failures.
- New `POST /v1.0/api/settings/database/test` endpoint (system admin) to validate a database configuration before saving. New `CLUTCH_DB_TYPE`, `CLUTCH_DB_FILEPATH`, `CLUTCH_DB_SCHEMA`, and `CLUTCH_DB_MANAGE_SCHEMA` environment overrides.

### Dashboard

- Server Settings gains a database configuration section: provider selector with provider-appropriate connection fields, per-purpose table-name mapping, a schema-management toggle, and a "Test connection" action.
- The Server Settings view — including every new database field, the table-name labels, and the test-connection strings — is fully localized in English, German, and Japanese.

### Testing and operations

- The shared Touchstone suite runs as a provider matrix — the full lock-engine correctness, tenant isolation, polling-wakeup, and randomized concurrency soak suites execute once per available provider. SQLite runs in-process; PostgreSQL, MySQL, and SQL Server run against containers via `docker/compose.test.yaml`. The suite has been run green (22/22 per provider, soak included) against all four engines.
- The `Test.Automated` runner accepts database-selection flags — `--type`, `--host`, `--port`, `--database`, `--schema`, `--username`, `--password`, `--filepath`, and `--providers` — as an alternative to the `CLUTCH_TEST_*` environment variables, so a single provider or a matrix can be targeted from the command line. `--help` lists them.

### Maintenance (2026-10-03, images rebuilt as `v0.2.0` and `latest`)

- Dependencies: Microsoft.Data.SqlClient 7.1.0 → 7.1.1, Voltaic 2.0.0 → 2.2.1, Watson 7.2.0 → 7.2.2, SyslogLogging 2.2.2 → 2.3.1; test tooling Touchstone 0.1.12 → 0.2.0, NUnit 4.6.1 → 5.0.0, coverlet.collector 10.0.1 → 10.1.0.
- MCP: with Voltaic 2.2, a `tools/call` whose arguments fail the tool's input schema (for example, missing or non-string `tenantId`) now returns a tool result with `isError: true` naming the property, instead of a JSON-RPC `-32602` error, as the MCP specification prescribes. An empty `tenantId` is reported with the message `tenantId is required.` rather than a generic internal-error text.
- Fix: tenant delete and tenant nuke now retry on transient database conflicts (deadlock, serialization failure, lock-wait timeout), like every lock mutation. Previously a tenant delete that collided with concurrent lock activity on SQL Server could fail as a deadlock victim.
- Tests: the MCP required-argument suite asserts the new `isError` contract, and the NUnit per-case runner executes through `TestExecutor.ExecuteCaseAsync`, so each provider's cases are reported individually (110 cases plus the run-all test).
- The Clutch.Sdk NuGet package is unchanged (it has no third-party dependencies) and remains at 0.2.0.

## [0.1.0] - 2026-08-08

Initial alpha release. Everything — APIs, WebSocket protocol, database schema, settings, SDK surfaces, and behavior — is subject to change without notice and is not yet recommended for production.

### Server

- Postgres-authoritative distributed lock engine. Every acquire and release is a single transaction that takes a per-key row lock (`SELECT … FOR UPDATE`), so nodes can never grant incompatible locks.
- Three lock modes — read (shared), write (exclusive among writers), delete (fully exclusive) — with MRSW semantics and a per-key policy fixed by the first acquirer (max readers, write exclusivity, whether a write blocks reads, lease bounds).
- Fail-fast and bounded-wait acquisition; blocked waiters on any node are woken via Postgres `LISTEN/NOTIFY` with a polling fallback.
- WebSocket lock protocol at `/v1.0/lock/connect`: session-bound ownership (closing the socket releases its locks), TTL leases with heartbeat renewal, and a monotonic fencing token per key.
- REST API for tokens, tenants, users, application keys, lock inspection and force-release, lock audit + activity chart, request history, and server info; OpenAPI at `/openapi.json`.
- Multi-tenant with a three-tier model (system admin / tenant admin / regular user); no RBAC. AES-256 session tokens, SHA-256 password and secret verifiers, idempotent first-boot seeding.
- Request-history capture with secret redaction and body truncation; per-tenant lock-audit retention and request-history retention with background pruning; lease sweeper for expired holders.
- Telemetry via the Radiant OpenTelemetry host: lock, HTTP, and process metrics exposed for Prometheus on a side port.
- PostgreSQL is the only implemented database provider; the provider abstraction remains for future providers.

### Dashboard

- React 19 / Vite 6 operator console: Home, Locks, Lock Activity, Tenants, Users, Credentials, Request History, OpenAPI-driven API Explorer, and Settings.
- Two hand-rolled SVG charts (request activity and lock activity), light/dark themes, and internationalization (English, German, Japanese, and an RTL/expansion pseudo-locale).

### SDKs

- C#, JavaScript, and Python SDKs, each with an admin (REST) client and a lock (WebSocket) client, plus a test application and an interactive console. The C# SDK ships as a NuGet package.

### Testing and operations

- Shared Touchstone suites (compatibility, lock engine correctness, fencing, lease expiry, wait/timeout, LISTEN/NOTIFY, tenant isolation, and a randomized concurrency soak) run through console, xUnit, and NUnit runners.
- `Test.Throughput` load benchmark reporting operations per second and a request distribution.
- Docker deployment: two server nodes behind an nginx load balancer, Postgres, and Prometheus + Grafana with a provisioned dashboard; factory reset and build scripts.
- REST and WebSocket API references, Docker documentation, and a documented, variable-driven Postman collection.

[0.3.0]: https://github.com/jchristn/Clutch/releases/tag/v0.3.0
[0.2.0]: https://github.com/jchristn/Clutch/releases/tag/v0.2.0
[0.1.0]: https://github.com/jchristn/Clutch/releases/tag/v0.1.0
