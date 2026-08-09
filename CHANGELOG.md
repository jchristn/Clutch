# Changelog

All notable changes to Clutch are documented here. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project uses [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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

[0.1.0]: https://github.com/jchristn/Clutch/releases/tag/v0.1.0
