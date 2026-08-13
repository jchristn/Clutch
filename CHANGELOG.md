# Changelog

All notable changes to Clutch are documented here. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project uses [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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

### Testing and operations

- The shared Touchstone suite runs as a provider matrix — the full lock-engine correctness, tenant isolation, polling-wakeup, and randomized concurrency soak suites execute once per available provider. SQLite runs in-process; PostgreSQL, MySQL, and SQL Server run against containers via `docker/compose.test.yaml`.

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

[0.2.0]: https://github.com/jchristn/Clutch/releases/tag/v0.2.0
[0.1.0]: https://github.com/jchristn/Clutch/releases/tag/v0.1.0
