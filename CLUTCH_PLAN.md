# Clutch — Build-Out Plan

Clutch is an in-memory-accelerated, database-authoritative **distributed lock management platform**. Client applications connect over WebSockets within a tenant using an application access key and acquire or release named locks in one of three modes — non-mutating (read), mutating (write/update), and delete — with configurable concurrency and lifespan policies. A REST API and a React dashboard provide administration, live lock visibility, and audit history. The platform is horizontally scalable: multiple server nodes share a single Postgres database that is the sole source of truth for lock state.

**Repository:** https://github.com/jchristn/Clutch (branch `main`). All `RepositoryUrl`/`PackageProjectUrl` metadata, the dashboard GitHub link, and doc links point here.

**Release: v0.1.0 (alpha).** This is the first cut. Assemblies, package versions, `CHANGELOG.md`, Docker image tags, and SDK package versions are all stamped `0.1.0`. The `README.md` must open with a prominent alpha notice stating that this is an early alpha release and that everything — APIs, WebSocket protocol, schema, settings, SDK surfaces, and behavior — is subject to change without notice and is not yet recommended for production use.

This document is the actionable build plan. Every task is a checkbox a developer can annotate. Work top-to-bottom within a milestone; milestones are ordered so each builds on the last. Update status inline:

- `- [ ]` not started
- `- [~]` in progress
- `- [x]` complete
- Append `— <note>` for blockers, decisions, or deviations.

---

## 1. Locked Design Decisions

These were confirmed with the product owner and are not open for re-litigation during implementation. Changing any of them requires an explicit plan revision.

### 1.1 Coherency model — Postgres is the only authority

Every lock decision (acquire, release, expiry reclaim) executes inside a Postgres transaction. There is no in-memory fast path that can grant or deny a lock. Concurrent acquire attempts on the same key across nodes are serialized by taking a row lock on that key's definition row (`SELECT ... FOR UPDATE`), so two nodes can never both believe they won an incompatible lock.

Memory holds only non-authoritative state, and the plan must never let it drift into the decision path:

- **Local waiter registry** — per node, `requestId → (session, key, mode, deadline)` for callers currently blocked in "wait for availability" mode, so a `NOTIFY` wakeup retries exactly the affected waiters instead of re-scanning the DB.
- **Session → held-locks index** — per node, so that when a WebSocket closes we can delete that session's holder rows in one pass.
- **Dashboard snapshot cache** — optional, eventually-consistent, read-only; refreshed from the DB and never consulted for correctness.

Cross-node coordination uses Postgres `LISTEN/NOTIFY` on a release channel carrying `tenantId + key`. A bounded polling fallback (default 1000 ms, configurable) retries waiters even if a notification is missed.

### 1.2 Lease, ownership, and fencing

- A lock holder is owned by the **WebSocket session** that acquired it. Closing the socket releases all of that session's holders.
- Each holder also carries a **TTL lease** (`leaseExpiresUtc`). Clients renew via heartbeat. A half-open/zombie connection's holders expire without waiting on TCP timeout.
- Acquire returns a **monotonic fencing token** (per key, sourced from the DB) so a downstream resource can reject a stale holder that believes it still owns the lock.
- Expiry is enforced two ways: a background sweeper reclaims expired holders on quiet keys, and every acquire transaction first deletes expired holders for the key it touches (self-healing on contended keys).

### 1.3 Lock semantics — MRSW + exclusive delete, first-acquirer policy

Three modes: `Read` (non-mutating/shared), `Write` (mutating/update), `Delete` (removal, fully exclusive and draining).

The **first acquirer of a key fixes the policy**, persisted on the lock definition and applied to every later acquirer:

- `ReadExclusivity` — `Shared(maxReaders)`; default unlimited.
- `WriteExclusivity` — `Exclusive(1)` (default) or `Shared(maxWriters)`.
- `WriteBlocksReads` — whether a held write excludes new reads; default `true`.
- `DefaultLeaseMs`, `MaxLeaseMs`, `MaxHoldMs` — lifespan constraints.

Compatibility matrix (rows = currently held, columns = requested):

| held \ requested | READ | WRITE | DELETE |
|---|---|---|---|
| READ | OK up to `maxReaders` | block unless `WriteBlocksReads=false` | block |
| WRITE | block unless `WriteBlocksReads=false` | block unless `WriteExclusivity=Shared(N)` and under N | block |
| DELETE | block | block | block |

Later acquirers may **not** change a key's policy. An acquire that supplies a conflicting policy is accepted but the supplied policy is ignored (documented behavior); an explicit `strictPolicy` request flag can instead return a `PolicyConflict` error — implement the flag, default off.

### 1.4 Database providers — Postgres only (documented divergence)

`BACKEND_ARCHITECTURE.md` mandates four providers (`Sqlite`, `Mysql`, `Postgresql`, `SqlServer`). Clutch ships **Postgresql only**, by product-owner directive and because cross-node waiter coordination relies on Postgres `LISTEN/NOTIFY`. The `DatabaseDriverBase`, `DatabaseDriverFactory`, `DatabaseTypeEnum`, and `Database/Interfaces/*` abstractions are still built so the pattern is honored and a future provider can slot in. This divergence is recorded in §2 and must be called out in the README and handoff.

---

## 2. Compliance & Deliberate Divergences

- [ ] Record in `README.md` and handoff: **Postgres-only** database provider (diverges from four-provider mandate; §1.4).
- [ ] Record: **RBAC omitted** by directive. `AUTHENTICATION.md` describes full RBAC (roles/permissions/assignments). Clutch implements tenant isolation + the three-tier bypass model only (`IsSystemAdmin` / `IsTenantAdmin` / regular user). No `userroles`, `permissions`, `rolepermissionmaps`, `userroleassignments`, `credentialscopeassignments` tables.
- [ ] Everything else follows the reference docs: Watson 7 HTTP+WS stack, thin `Program.cs` + instance server host, typed `RequestContext`, `PrettyId` prefixed IDs, provider-neutral DB base with handwritten SQL, versioned/tracked/idempotent migrations, first-boot seeding, request capture + `/v1.0/api/request-history`, `Server.UseOpenApi()`, typed DTOs, strict C# style (no `var`, no tuples, no partial classes, XML docs, in-namespace `using`, one entity per file, null-checking setters, clamped numerics, `ConfigureAwait(false)`, `CancellationToken` on async).
- [ ] Dashboard follows `FRONTEND_ARCHITECTURE.md` + `DASHBOARD_STYLE_AND_USABILITY.md` + `I18N.md`: React 19 / Vite 6 / React Router 7, hand-rolled `fetch` `ApiClient`, hand-rolled SVG charts, Home / Request History / API Explorer / Settings out of the gate, i18n foundation, light+dark themes, responsive QA at 1280/768/390.
- [ ] Tests follow `BACKEND_TEST_ARCHITECTURE.md` using Touchstone: `Test.Shared` descriptors consumed by `Test.Automated` (console), `Test.Xunit`, and `Test.Nunit` runners.
- [ ] Repo follows `REPOSITORY_REQUIREMENTS.md`: `.gitignore`, `.dockerignore`, `README.md`, `DOCKERHUB_README.md`, `CHANGELOG.md`, `LICENSE.md` (MIT), source under `src/`/`test|Test.*`/`dashboard/`/`sdk/`. Docker uses `.yaml` compose with **explicit named/tagged images** (`jchristn77/clutch-server:v0.1.0`, `jchristn77/clutch-ui:v0.1.0`) rather than build contexts — this is the sanctioned "explicit image names and tags" exception in `REPOSITORY_REQUIREMENTS.md`.

---

## 3. Repository Layout

```
C:\Code\Clutch\
├── CLUTCH_PLAN.md
├── README.md
├── DOCKERHUB_README.md
├── CHANGELOG.md
├── LICENSE.md
├── REST_API.md
├── WEBSOCKETS_API.md
├── DOCKER.md
├── Clutch.postman_collection.json
├── clutch.json                      # default settings (also mounted in docker)
├── .gitignore
├── .dockerignore
├── assets/                          # logo.png (logo + NuGet package icon), logo.ico (favicon)
│
├── src/
│   ├── Clutch.sln
│   ├── Clutch.Core/                 # domain library
│   │   ├── Constants.cs
│   │   ├── Database/
│   │   │   ├── DatabaseDriverBase.cs
│   │   │   ├── DatabaseDriverFactory.cs
│   │   │   ├── DatabaseSettings.cs
│   │   │   ├── DatabaseTypeEnum.cs
│   │   │   ├── SchemaMigration.cs
│   │   │   ├── Interfaces/
│   │   │   │   ├── ITenantMethods.cs
│   │   │   │   ├── IUserMethods.cs
│   │   │   │   ├── ICredentialMethods.cs
│   │   │   │   ├── IAuthSessionMethods.cs
│   │   │   │   ├── ILockDefinitionMethods.cs
│   │   │   │   ├── ILockHolderMethods.cs
│   │   │   │   ├── ILockAuditMethods.cs
│   │   │   │   └── IRequestHistoryMethods.cs
│   │   │   └── Postgresql/
│   │   │       ├── PostgresqlDatabaseDriver.cs
│   │   │       ├── Sanitizer.cs
│   │   │       ├── Converters.cs
│   │   │       ├── Notifications/PostgresNotificationListener.cs
│   │   │       ├── Implementations/  # one file per interface
│   │   │       └── Queries/          # SetupQueries.cs + per-entity query classes
│   │   ├── Enums/                    # LockModeEnum, LockBehaviorEnum, WriteExclusivityEnum, LockEventTypeEnum, PrincipalTypeEnum, AuthSchemeEnum ...
│   │   ├── Helpers/IdGenerator.cs
│   │   ├── Models/                   # Tenant, User, Credential, AuthSession, LockDefinition, LockHolder, LockAuditEntry, RequestHistoryEntry
│   │   ├── Requests/                 # typed request DTOs (REST + WS)
│   │   ├── Responses/                # typed response DTOs
│   │   ├── Security/                 # RequestContext, TokenService, PasswordHasher, CredentialKeyGenerator
│   │   └── Services/
│   │       ├── Interfaces/
│   │       └── Implementations/      # LockEngine, LockCoordinator (NOTIFY/waiters), LeaseSweeper, RetentionService
│   │
│   └── Clutch.Server/
│       ├── Program.cs               # thin: Bootstrapper.Run(args)
│       ├── Bootstrapper.cs
│       ├── ClutchServer.cs          # instance host: REST + WS wiring
│       ├── Settings/                # Settings, RestSettings, DatabaseSettings ref, LoggingSettings, AuthSettings, LockSettings, RequestHistorySettings, TelemetrySettings
│       ├── Serialization/           # StrictEnumConverterFactory, JSON options
│       ├── Services/                # AuthenticationService, AuthorizationService, RequestHistoryCaptureService, WebSocketConnectionManager
│       ├── Middleware/              # Preflight, PostRouting/capture
│       ├── Routes/                  # HealthRoutes, AuthRoutes, TenantRoutes, UserRoutes, CredentialRoutes, LockRoutes, LockAuditRoutes, RequestHistoryRoutes, SettingsRoutes
│       ├── WebSocket/               # LockWebSocketHandler, ClutchWsSession, WsMessage models
│       ├── Dockerfile
│       └── clutch.json
│
├── test/                            # (or src/Test.*) Touchstone
│   ├── Test.Shared/                 # ClutchSuites.cs + Suites/* (Touchstone.Core only, no console output)
│   ├── Test.Automated/              # Program.cs console runner (Touchstone.Cli)
│   ├── Test.Xunit/                  # Fact-style (RunAll) + Theory-style (per-descriptor) adapters
│   ├── Test.Nunit/                  # Fact-style (RunAll) + TestCaseSource (per-descriptor) adapters
│   └── Test.Throughput/             # console load generator: spins up docker stack, drives it, reports ops/sec
│
├── sdk/
│   ├── README.md
│   ├── csharp/                      # Clutch.Sdk.sln → Clutch.Sdk (Admin+Lock) + Clutch.Sdk.Test (automated) + Clutch.Sdk.Console (interactive)
│   ├── js/                          # clutch-admin-sdk.js, clutch-lock-sdk.js, test/ (automated), console.js (interactive), package.json, README
│   └── python/                      # clutch_admin_sdk.py, clutch_lock_sdk.py, test_harness.py (automated), console.py (interactive), requirements.txt, README
│
├── dashboard/                       # React 19 + Vite 6
│   ├── src/ (main.jsx, App.jsx, views/, components/, context/, hooks/, utils/, i18n/, assets/)
│   ├── public/ (logo.png, logo.ico favicon, index.html favicon-wired)
│   ├── Dockerfile, nginx.conf (SPA), package.json, vite.config.js, eslint.config.js
│
└── docker/
    ├── compose.yaml                 # postgres + 2 server nodes + dashboard + nginx LB + prometheus + grafana
    ├── nginx/nginx.conf             # upstream over node1/node2, ws upgrade, ip_hash
    ├── server/clutch.node1.json, clutch.node2.json
    ├── postgres/init/01-init.sql    # optional bootstrap
    ├── prometheus/prometheus.yaml   # scrape config: both clutch nodes' /metrics
    ├── grafana/
    │   └── provisioning/
    │       ├── datasources/prometheus.yaml
    │       └── dashboards/dashboards.yaml + clutch-*.json   # preconfigured dashboards
    └── factory/
        ├── reset.sh, reset.bat
        └── templates/               # pristine settings + compose copy
```

- [ ] Naming: solution `Clutch`, projects `Clutch.Core`, `Clutch.Server`, `Clutch.Sdk`. Namespaces match.
- [ ] Target framework `net8.0` (bump to `net8.0;net10.0` only if the toolchain is present).

---

## 4. Data Model & Schema

All tables carry `id` (PrettyId string, ≤64 chars, PK), `createdutc`, `lastupdateutc`, `active` (bool), and `isprotected` (bool) per `AUTHENTICATION.md`. Tenant-owned tables carry `tenantid` and index it. PrettyId prefixes are defined centrally in `Constants.cs`.

Prefixes: `ten_` tenant, `usr_` user, `crd_` credential (application key), `ses_` auth session, `lkd_` lock definition, `lkh_` lock holder, `lka_` lock audit entry, `req_` request-history entry.

- [ ] **tenants** — `id`, `name`, `lockhistoryretentiondays` (default 7, clamp 1–3650), `defaultleasems`, `maxleasems`, `active`, `isprotected`, timestamps. Seeded default tenant on first boot if absent.
- [ ] **users** — `id`, `tenantid`, `firstname`, `lastname`, `email`, `passwordsha256`, `issystemadmin` (bool), `istenantadmin` (bool), `active`, `isprotected`, timestamps. Composite unique `(tenantid, email)`. Seeded default sysadmin user on first boot if absent.
- [ ] **credentials** (application keys) — `id`, `tenantid`, `userid` (nullable owner), `name`, `accesskey` (`access_` + ≥32 chars), `secretkeyencrypted`, `secretkeylast4`, `authmode`, `lastusedutc`, `expiresutc`, `active`, `isprotected`, timestamps. Unique `(tenantid, accesskey)`; index `accesskey`. Seeded default application key on first boot if absent (secret shown once in startup log for local dev only).
- [ ] **authsessions** — `id`, `tenantid`, `userid` (nullable), `credentialid` (nullable), `principaltype`, `authscheme`, `tokenid`, `sourceip`, `useragent`, `expiresutc`, `lastusedutc`, `revokedutc`, `revocationreason`, `active`, `isprotected`, timestamps.
- [ ] **lock_definitions** — `id`, `tenantid`, `lockkey`, `readexclusivity_maxreaders` (nullable=∞), `writeexclusivity` (enum), `writeexclusivity_maxwriters`, `writeblocksreads` (bool), `defaultleasems`, `maxleasems`, `maxholdms`, `fencingcounter` (bigint, monotonic), `firstacquiredby_credentialid`, timestamps, `active`. Composite unique `(tenantid, lockkey)`. This is the row locked `FOR UPDATE` to serialize acquires.
- [ ] **lock_holders** — `id`, `tenantid`, `lockkey`, `lockdefinitionid`, `mode` (enum), `credentialid`, `sessionid` (ws `session.Id`), `nodeid`, `fencingtoken` (bigint), `acquiredutc`, `leaseexpiresutc`, `lastheartbeatutc`, `active`. Indexes: `(tenantid, lockkey)`, `(sessionid)`, `(leaseexpiresutc)`.
- [ ] **lock_audit** — `id`, `tenantid`, `lockkey`, `mode`, `eventtype` (enum: `Acquired`, `Released`, `Waited`, `Denied`, `Expired`, `Revoked`, `HeartbeatRenewed`, `PolicyCreated`), `credentialid`, `sessionid`, `nodeid`, `fencingtoken`, `reason`, `createdutc`. Indexes: `(tenantid, lockkey, createdutc)`, `(tenantid, mode, createdutc)`, `(createdutc)` for retention pruning. Powers both the audit view and the domain lock chart.
- [ ] **request_history** — full `RequestHistoryEntry` shape from `BACKEND_ARCHITECTURE.md` (headers/bodies with truncation + redaction, tenant-aware). Index `(tenantid, createdutc)`, `(createdutc)`.
- [ ] **schema_migrations** — `version` (int), `description`, `appliedutc`. Migrations idempotent, tracked, additive.

Tasks:

- [ ] Model classes in `Models/` (one per file, validated setters, XML docs).
- [ ] Migration set `SchemaMigration` v1 with all tables + indexes; runner records versions and is safe to re-run.
- [ ] First-boot seeding: default tenant, default sysadmin user, default application key — only if missing; idempotent.
- [ ] Cascading delete: deleting a user deletes its credentials; deleting a tenant deletes users/credentials/lock rows/audit; deleting a credential releases its holders.

---

## 5. Lock Engine (`Clutch.Core/Services`)

The engine is provider-agnostic at its surface and executes through `ILockDefinitionMethods` / `ILockHolderMethods` / `ILockAuditMethods`. All correctness lives here and in the SQL.

### 5.1 Acquire (transactional)

- [ ] `AcquireAsync(tenantId, credentialId, sessionId, nodeId, key, mode, behavior, requestedPolicy, requestedLeaseMs, token)`:
  1. BEGIN transaction.
  2. Upsert-select `lock_definitions` row `FOR UPDATE` (serializes per key across all nodes). If absent, insert with `requestedPolicy` (caller is first acquirer → emit `PolicyCreated` audit).
  3. Delete expired holders for the key (`leaseexpiresutc < now`), audit each as `Expired`.
  4. Load current active holders; evaluate the §1.3 matrix against `mode` under the definition's policy.
  5. Compatible → increment `fencingcounter`, compute `leaseExpiresUtc = now + clamp(requestedLeaseMs, ≤ maxLeaseMs, default defaultLeaseMs)`, insert `lock_holders`, COMMIT, `NOTIFY` (no waiters to wake on acquire, but audit), return `Acquired{ holderId, fencingToken, leaseExpiresUtc }`.
  6. Incompatible + `FailFast` → ROLLBACK/COMMIT read-only, audit `Denied`, return `Denied{ reason }`.
  7. Incompatible + `Wait` → COMMIT, register waiter in local registry with deadline, audit `Waited`, await `NOTIFY`/poll/timeout, then retry from step 1. On deadline → `Denied{ reason: "timeout" }`.
- [ ] Constant, bounded retry loop for `Wait`; jittered backoff on serialization failures.

### 5.2 Release / heartbeat / expiry

- [ ] `ReleaseAsync(...)` — transaction: delete the holder verifying ownership (`holderId` + `sessionId` or `fencingToken`), COMMIT, `NOTIFY tenant+key`, audit `Released`. Idempotent (releasing an already-gone holder is a success no-op).
- [ ] `HeartbeatAsync(sessionId, holderIds[])` — extend `leaseexpiresutc` (clamped to `maxHoldMs` from `acquiredutc`), update `lastheartbeatutc`, audit `HeartbeatRenewed` (throttled). Holders past `maxHoldMs` are not renewable.
- [ ] `ReleaseAllForSessionAsync(sessionId)` — on ws close; delete all holders for the session, `NOTIFY` each affected key.
- [ ] `LeaseSweeper` background service — interval sweep (default 1s) deleting expired holders on quiet keys and `NOTIFY`ing; also the retry pulse for waiters when a `NOTIFY` is missed.

### 5.3 Cross-node coordination

- [ ] `PostgresNotificationListener` — dedicated Npgsql connection running `LISTEN clutch_lock_release`; on payload `tenantId|key`, signal the local waiter registry to retry waiters on that key.
- [ ] `LockCoordinator` — owns the local waiter registry and the session→holders index; exposes `RegisterWaiter`, `SignalKey`, `CancelWaitersForSession`.
- [ ] Polling fallback interval configurable (`LockSettings.WaiterPollMs`, default 1000).

### 5.4 Domain summary for the chart

- [ ] `SummarizeLocksAsync(filter)` — buckets `lock_audit` events by time and by `(lockkey|mode)` series for the dashboard chart. Filter: `tenantId?`, `lockNameContains?`, `modes[]?`, `fromUtc`, `toUtc`, `bucketCount`. Server emits every bucket including empties. Bucket presets: Last Hour = 60×1min, Last Day = 96×15min, Last Week = 84×2hr.

---

## 6. WebSocket Protocol (`WEBSOCKETS_API.md`)

Single Watson host, websockets enabled on the REST port, path `/v1.0/lock/connect`.

- [ ] **Connect/auth** — client sends `x-clutch-access-key` (+ `x-clutch-secret-key` for `DirectHeader` mode) headers on the upgrade request. Server resolves the credential → tenant, validates active + not expired, creates a `ClutchWsSession` bound to `session.Id`. Failure → close with `PolicyViolation`. Tenant is derived from the key; a mismatched `x-clutch-tenant` header (if present) is rejected.
- [ ] **Client → server** messages (JSON, `type`):
  - `acquire` — `{ requestId, key, mode, behavior, timeoutMs?, leaseMs?, policy? }` (policy honored only when creating a new key).
  - `release` — `{ requestId, key, holderId | fencingToken }`.
  - `heartbeat` — `{ holderIds: [...] }`.
  - `ping`.
- [ ] **Server → client** messages:
  - `acquired` — `{ requestId, key, mode, holderId, fencingToken, leaseExpiresUtc }`.
  - `waiting` — `{ requestId, key }` (optional progress signal).
  - `denied` — `{ requestId, key, reason }`.
  - `released` — `{ requestId, key }`.
  - `expired` — `{ holderId, key }` (server-initiated).
  - `revoked` — `{ holderId, key, reason }` (admin force-release / credential disabled).
  - `pong`, `error { requestId?, code, message }`.
- [ ] `WebSocketConnectionManager` tracks sessions per node; on close → `ReleaseAllForSessionAsync`.
- [ ] Heartbeat/lease timing surfaced to client on connect (`hello`/`welcome` frame with `defaultLeaseMs`, `heartbeatIntervalMs`).

---

## 7. REST API Surface (`REST_API.md`)

Versioned `/v1.0/...`. Typed DTOs, explicit status codes, no tuples, `req.CancellationToken` threaded through. `Server.UseOpenApi()` + `Server.UseHealthCheck()`. Preflight + PostRouting (with request capture) wired per `BACKEND_ARCHITECTURE.md`.

- [ ] **Auth** — `POST /v1.0/token` (email/password or access/secret → session token), `GET /v1.0/token`, `GET /v1.0/token/details`, `DELETE /v1.0/token`. AES-256 opaque token, random IV per token.
- [ ] **Health** — `GET /v1.0/api/health`.
- [ ] **Tenants** (sysadmin) — CRUD `/v1.0/api/tenants[/{id}]`, includes retention-days setting.
- [ ] **Users** (sysadmin any tenant; tenant-admin own tenant) — CRUD `/v1.0/api/tenants/{tid}/users[/{id}]` with `issystemadmin`/`istenantadmin` flags.
- [ ] **Credentials / application keys** — CRUD `/v1.0/api/tenants/{tid}/credentials[/{id}]`; secret shown once on create, redacted thereafter.
- [ ] **Locks (read/observe)** — `GET /v1.0/api/tenants/{tid}/locks` (current live locks + holders, filter by name/mode), `GET .../locks/{key}`, admin `POST .../locks/{key}/release` (force-release → `revoked`).
- [ ] **Lock audit** — `GET /v1.0/api/tenants/{tid}/lock-audit` (paginated, filters: name, mode, event, from/to), `GET .../lock-audit/summary` (the bucketed chart data, §5.4).
- [ ] **Request history** — full surface at `/v1.0/api/request-history` (list, `{id}`, `summary`, delete, bulk delete) per `BACKEND_ARCHITECTURE.md`.
- [ ] **Settings/server info** — `GET /v1.0/api/server-info` (endpoint, version, node id, auth context, feature flags).
- [ ] Authorization: sysadmin bypass all; tenant-admin scoped to own tenant; regular user read-only within tenant. Client-side checks are UI-only; server re-enforces on every request.

---

## 8. Server Implementation Milestones

### M1 — Skeleton & settings
- [ ] `Clutch.sln`, `Clutch.Core`, `Clutch.Server` projects; `Watson` 7.0.15, `Npgsql` 9.0.3, `PrettyId` 2.0.0, `SyslogLogging`, `Timestamps`.
- [ ] `Constants.cs` (ID prefixes), `Helpers/IdGenerator.cs`.
- [ ] Settings classes + JSON load. **On every startup: load → re-serialize `clutch.json` back to disk** so newly added properties persist with defaults. Env overrides applied in-memory only (`CLUTCH_SETTINGS_FILE`, `CLUTCH_DB_*`, `CLUTCH_AUTH_SIGNING_KEY`, `CLUTCH_NODE_ID`).
- [ ] Thin `Program.cs` → `Bootstrapper.Run` → `ClutchServer`.

### M2 — Database layer (Postgres)
- [ ] `DatabaseDriverBase`, `DatabaseTypeEnum` (Postgresql only enumerated, others reserved), `DatabaseDriverFactory`, `DatabaseSettings`.
- [ ] `Database/Interfaces/*` for all entities.
- [ ] `PostgresqlDatabaseDriver` + `Implementations/*` + `Queries/*` (handwritten SQL), `Sanitizer`, `Converters`.
- [ ] `SchemaMigration` v1 + tracked runner; `InitializeAsync` runs migrations then seeds defaults.
- [ ] `PostgresNotificationListener` on a dedicated connection.

### M3 — Security & auth
- [ ] `PasswordHasher` (SHA-256), `CredentialKeyGenerator` (`access_`/`secret_`), `TokenService` (AES-256, random IV), constant-time comparisons.
- [ ] `RequestContext` (TenantId, UserId, CredentialId, IsAdmin, IsTenantAdmin, IsAuthenticated, PrincipalType).
- [ ] `AuthenticationService.AuthenticateApiRequestAsync` (bearer/x-token, x-api-key admin, access/secret) + WS connect auth.
- [ ] `AuthorizationService` (sysadmin/tenant-admin/regular bypass tiers; admin endpoints never fall back to open access).

### M4 — Lock engine & coordinator
- [ ] `LockEngine` (acquire/release/heartbeat/expiry, §5), `LockCoordinator` (waiters + session index), `LeaseSweeper`, `RetentionService` (prune `lock_audit` per tenant `lockhistoryretentiondays`, prune `request_history` per `RetentionDays`).
- [ ] Concurrency correctness: `SELECT ... FOR UPDATE` serialization; expired-holder self-heal; fencing monotonicity.

### M5 — Web host: REST + WS
- [ ] `ClutchServer` wiring: `AuthenticateApiRequest`, `UseHealthCheck`, `UseOpenApi`, Preflight, PostRouting + capture, `WebSockets.Enable = true`.
- [ ] Route registrars (§7), one per feature.
- [ ] `LockWebSocketHandler` at `/v1.0/lock/connect`; `WebSocketConnectionManager`; release-on-disconnect.
- [ ] `RequestHistoryCaptureService` (fire-and-forget, redaction, truncation).

### M6 — Telemetry & Observability (Radiant model)

Full telemetry, modeled on `C:\Code\Radiant`, which is an **OpenTelemetry**-based telemetry SDK shipped as the `Radiant` NuGet package (not prometheus-net). Code emits through the .NET BCL (`System.Diagnostics.Metrics.Meter` / `ActivitySource`); a `RadiantHost.Start(RadiantSettings)` hosts the OTel MeterProvider/TracerProvider and exposes a Prometheus scrape endpoint via `AddPrometheusHttpListener` on a side port (default `9464`). Instrument **both layers**: (a) the Watson webserver (HTTP + WebSocket transport) and (b) the platform logic / lock data path.

- [ ] **Reference the `Radiant` NuGet package** (OpenTelemetry 1.17.0 under the hood). In the bootstrapper, build a `RadiantSettings` (from `TelemetrySettings` in `clutch.json`), `RadiantHost.Start(...)`, register the meter/activity-source names in `Sources`, and dispose/flush on shutdown. Emit domain metrics via `host.Client` (`Increment`/`Record`/`Add`/`RegisterGauge`) or raw BCL `Meter`; the Watson middleware uses a BCL `Meter` subscribed by name.
- [ ] **Convention catalog** — declare Clutch's metrics as a `Convention[]` (Radiant's `Convention.Counter/Histogram/UpDownCounter/Gauge` with UCUM units + allowed low-cardinality labels + `LatencyBuckets`) and `settings.Metrics.DefineAll(ClutchConventions.All)`. Instrument names follow OTel dotted-lowercase style (render to Prometheus as `_total`/`_seconds_bucket` etc.). Labels are **low-cardinality only** (`mode`, `outcome`, `node`, `http.request.method`, `http.response.status_code`, `http.route`) — never lock keys or tenant/session ids on labels (those go on spans/logs).
- [ ] **Exposition:** Prometheus scrape endpoint on the OTel HttpListener side port (`TelemetrySettings.PrometheusPort`, default 9464, path `/metrics`), independent of the Watson REST port. Gated by `TelemetrySettings.Enabled` (default true).
- [ ] **Webserver instrumentation** (transport layer), emitted from the Preflight/PostRouting hooks using OTel HTTP semantic conventions: `http.server.request.duration` (histogram, labels method/status/route), `http.server.active_requests` (in-flight up-down counter), request/response body-size histograms, plus a `clutch.http.request` count. WebSocket: `clutch.ws.connections` (up-down gauge, active per node), `clutch.ws.frames` in/out counters, connect/disconnect counters, auth-failure counter.
- [ ] **Data-path instrumentation** (platform logic) — the numbers that make Clutch observable as a *lock manager* (dotted names; Prometheus rendering shown):
  - `clutch.lock.acquire` counter, labels `mode`,`outcome=granted|denied|timeout|error` → `clutch_lock_acquire_total`
  - `clutch.lock.release` / `clutch.lock.expired` / `clutch.lock.revoked` counters
  - `clutch.lock.held` up-down gauge (held holders by `mode`), `clutch.lock.waiters` up-down gauge (blocked waiters)
  - `clutch.lock.acquire.wait.duration` histogram (`s`) — time a `Wait` acquire blocked before grant/timeout
  - `clutch.lock.acquire.txn.duration` histogram (`s`) — DB transaction latency of the decision path (quantifies the "every op hits Postgres" cost)
  - `clutch.notify.received` / `clutch.notify.missed` counters (cross-node LISTEN/NOTIFY health), `clutch.lease.sweep.reclaimed` counter
  - retention/prune counters, DB error counters.
- [ ] **Settings:** `TelemetrySettings { Enabled, ServiceName, NodeLabel, PrometheusEnable, PrometheusHostname, PrometheusPort, PrometheusPath, OtlpEndpoint?, OtlpEnable, MetricsIncludeRuntime, MetricsIncludeProcess }` persisted in `clutch.json`; logged (without secrets) at startup. `ServiceName = "clutch"`, `service.instance.id`/`NodeLabel` = `CLUTCH_NODE_ID`.
- [ ] Emit the auth counters from `AUTHENTICATION.md` (`auth_requests_total`, `session_events_total`) through the same host.
- [ ] Optional (available via Radiant, wire if low-cost): traces via `ActivitySource` around the acquire/release transaction, exported OTLP → Tempo; structured logs → Loki. Metrics + Prometheus + Grafana are the required baseline.

---

## 9. Dashboard (`dashboard/`)

Route inventory (build before coding pages):

| Route | Job | Roles | Backend | Notes |
|---|---|---|---|---|
| `/` Login | Connect | all | `/v1.0/token`, health | server URL + token/credentials, language selector |
| `/dashboard/home` | System state | all | lock summary, request summary, counts | KPIs + lock chart + request activity chart + CTAs |
| `/dashboard/locks` | Live locks | all | `GET .../locks` | table: key, mode, holders, lease, session; force-release (admin) |
| `/dashboard/lock-activity` | Domain chart + audit | all | `lock-audit`, `lock-audit/summary` | the required tenant/name/type filtered chart |
| `/dashboard/tenants` | Manage tenants | sysadmin | tenants CRUD | retention-days field |
| `/dashboard/users` | Manage users | sysadmin, tenant-admin | users CRUD | admin flags |
| `/dashboard/credentials` | App keys | sysadmin, tenant-admin | credentials CRUD | secret shown once |
| `/dashboard/requests` | Request History | all | request-history + summary | required drill-down modal |
| `/dashboard/explorer` | API Explorer | all | `/openapi.json` | OpenAPI-driven |
| `/dashboard/settings` | Server info | all | server-info | endpoint/version/node/auth context |

- [ ] **Branding:** `assets/logo.png` copied to `dashboard/public/`; shown on the **login card** and in the **upper-left corner of the dashboard shell** (topbar/sidebar header). `assets/logo.ico` copied to `dashboard/public/` and wired as the **favicon** in `index.html`. `document.title` reflects "Clutch".
- [ ] **Login page:** just the login card — server URL field, token/credentials, language selector — and the logo. **No extra flair** (no hero, no marketing panel, no background art). Model on the `Login` component in `FRONTEND_ARCHITECTURE.md` and the login treatment in the reference dashboards under `c:\code\agents\requirements`.
- [ ] Shell (all required **out of the gate**): grouped sidebar with **organized sections** (labelled nav/TOC groups, not a flat list), route headers, protected routes, auth context, shared `ApiClient`. Topbar carries: **server URL** (readable + copyable), tenant/role badges, health/live status, a **GitHub icon link** (upper-right, new tab, `aria-label`), theme toggle, **language selector** (i18n), and a **logout icon button — not text** (upper-right, `aria-label`).
- [ ] **Light/dark mode** from first implementation, theme persisted; **i18n** foundation active from first implementation (see i18n task below). Both are non-negotiable first-pass items, not follow-ons.
- [ ] Shared components: DataTable (above-table pagination bar, sort, filter, row actions, no-wrap IDs), Pagination, FilterBar, Modal, ConfirmModal, JsonViewerModal, Toast, StatusBadge, CopyButton/CopyableId, ActionMenu (portaled).
- [ ] **Two charts, both hand-rolled SVG:**
  - `ActivityChart` — stacked success/failure bars for Request History + Home (per frontend arch).
  - `LockChart` — the domain chart: **line chart** (series per lock name/type) or **stacked bar chart** toggle; range selector Last Hour (60×1min) / Last Day (96×15min) / Last Week (84×2hr); refresh button; sysadmin gets a **tenant dropdown**, plus **lock-name filter** and **lock-type (mode) filter**; tenant-admin/regular users are pinned to their own tenant. Portal-rendered tooltip, theme-aware via CSS vars, gap-filled buckets, locale-formatted axes.
- [ ] Home: KPI tiles (active locks, waiters, denials/hr, avg hold ms, tenants, keys), lock chart, request activity chart, recent denials/expiries with links, CTA cards (add key, view locks, open explorer).
- [ ] Record pages: `+ Add`, view/edit/delete modals, View JSON; destructive actions behind confirm modals (never browser `confirm`).
- [ ] Request History: KPI strip, ActivityChart, backend filters, paginated table, `RequestDetailsModal` (metadata/headers/bodies/raw JSON, copy controls).
- [ ] API Explorer: OpenAPI-driven, inherited auth, history in localStorage, destructive confirm, live code snippets.
- [ ] i18n foundation: `src/i18n/{index,localeRegistry,resources,formatters}.js` + `LanguageSelector`; all strings via catalogs; `lang`/`dir` sync; formatters explicit-locale; English + one long-Latin + one CJK + one RTL pseudo-locale.
- [ ] Tokens + light/dark themes; responsive QA at 1280/768/390; mandatory Playwright visual QA of shell/home/locks/lock-activity/requests/detail-modal/explorer/settings in both themes.

---

## 10. SDKs (`sdk/{csharp,js,python}`)

Two client surfaces per language: an **Admin client** (REST: tenants/users/keys/locks/audit) and a **Lock client** (WebSocket: connect, acquire, release, heartbeat, fencing token). **Each language ships two runnable apps in addition to the library:**

- a **test application** — non-interactive, assertion-driven, exit-code-based (0 pass / non-zero fail), CI-friendly; exercises admin + lock surfaces end-to-end against a running server. Takes `<endpoint> <access_key>` args.
- an **interactive console application** — a REPL/menu a person drives by hand to connect, acquire/release/heartbeat locks, watch `expired`/`revoked` pushes, and run admin operations live. Reads the same connection args, then prompts.

Both apps consume the SDK exactly as an external caller would (no reach-through into internals), so they double as usage examples.

- [ ] **C#** — `Clutch.Sdk.sln` containing:
  - `Clutch.Sdk` — `ClutchAdminClient` + `ClutchLockClient : IDisposable`. Lock client uses `ClientWebSocket` with access-key header, auto-heartbeat loop, `AcquireAsync`/`ReleaseAsync` returning fencing tokens, reconnect handling.
  - [ ] **NuGet packaging — `Clutch.Sdk.csproj` produces a fully-populated package.** Set `<GeneratePackageOnBuild>true</GeneratePackageOnBuild>`, `<IncludeSymbols>true</IncludeSymbols>`, `<SymbolPackageFormat>snupkg</SymbolPackageFormat>`, `<GenerateDocumentationFile>true</GenerateDocumentationFile>`. All metadata populated: `PackageId` (`Clutch.Sdk`), `Version` `0.1.0`, `Authors`, `Company`, `Product`, `Description`, `PackageTags` (e.g. `distributed-lock;locking;clutch;websocket`), `Copyright`, `PackageProjectUrl`, `RepositoryUrl`, `RepositoryType` `git`, `PackageReadmeFile` `README.md`, `PackageLicenseFile` `LICENSE.md`, `PackageIcon` `logo.png`, and `PackageReleaseNotes` (points to CHANGELOG 0.1.0 alpha).
  - [ ] Pack the assets: `<ItemGroup>` `<None Include>` entries for the SDK `README.md`, root `LICENSE.md`, and `assets/logo.png` with `Pack="true"` and correct `PackagePath` so README/LICENSE/icon ship **inside** the `.nupkg`. Verify with `dotnet pack` that the package contains all three plus the `.snupkg` symbols. (JS and Python packages carry their own equivalents: `package.json` metadata + bundled README/LICENSE; `pyproject.toml`/`setup` metadata + README/LICENSE.)
  - `Clutch.Sdk.Test` — automated console test app (assertions + exit codes).
  - `Clutch.Sdk.Console` — interactive console app (menu-driven live usage).
- [ ] **JS** — `clutch-admin-sdk.js` (fetch) + `clutch-lock-sdk.js` (`ws`/browser `WebSocket`); `test/` automated harness (`npm test`, exit codes); `console.js` interactive CLI via `readline` (`npm run console`); `package.json`, README.
- [ ] **Python** — `clutch_admin_sdk.py` (`requests`) + `clutch_lock_sdk.py` (`websocket-client`); `test_harness.py` automated (exit codes); `console.py` interactive REPL; `requirements.txt`, README.
- [ ] Consistent result/enum names across the three languages (`LockMode`, `LockBehavior`, `AcquireResult`).
- [ ] `sdk/README.md` language matrix + quick-start, documenting how to run each test app and each interactive console.

---

## 11. Documentation, Postman, Repo Housekeeping

- [ ] `README.md` — opens with a prominent **alpha notice** (v0.1.0; early alpha, everything subject to change, not production-ready); then product overview, use cases, architecture diagram, coherency model summary, quick start (docker), settings reference, **documented divergences** (Postgres-only, RBAC omitted). Reviewed against `WRITING_DOCUMENTS.md` for human voice.
- [ ] `DOCKERHUB_README.md` — Docker Hub variant carrying the same alpha notice; images/logos via explicit repo `assets/` URLs.
- [ ] `CHANGELOG.md` — Keep a Changelog format; first and only entry is **`0.1.0`** (initial alpha release).
- [ ] `LICENSE.md` — MIT.
- [ ] `REST_API.md` — every endpoint, auth, request/response examples, status codes.
- [ ] `WEBSOCKETS_API.md` — connect/auth, message types both directions, lease/heartbeat/fencing, examples, reconnect guidance.
- [ ] `DOCKER.md` — compose topology, node config, factory reset.
- [ ] Per-SDK READMEs (C#, JS, Python) + `sdk/README.md`.
- [ ] **Postman collection** `Clutch.postman_collection.json` — covers auth, tenants, users, credentials, locks, lock-audit, request-history, server-info, health. Requirements:
  - **Documentation:** collection-level description (overview, auth flow, how variables work) plus a per-request description for every request (purpose, required params, expected responses/status codes). Folders grouped by resource with folder-level descriptions.
  - **Extensive variables:** no hard-coded URLs, ids, or tokens anywhere. Use collection/environment variables for `baseUrl`, `tenantId`, `userId`, `credentialId`, `accessKey`, `secretKey`, `lockKey`, and `token`. Ship a matching `Clutch.postman_environment.json` (local defaults for the docker stack).
  - **Auto-wiring:** the token request has a test script that captures the returned session token into `{{token}}`; create requests capture returned ids into their respective variables so later requests chain without manual editing. Requests reference `{{baseUrl}}` and send `Authorization: Bearer {{token}}`.
- [ ] `.gitignore`, `.dockerignore`, `assets/logo.png` (logo + package icon), `assets/logo.ico` (favicon).

---

## 12. Docker (`docker/`) — 2 nodes + nginx LB + Postgres

- [ ] `compose.yaml` (`.yaml`, **named/tagged images — no build contexts**): `postgres` (postgres:17, named volume, healthcheck) → `clutch-node1` + `clutch-node2` (both `image: jchristn77/clutch-server:v0.1.0`, distinct `CLUTCH_NODE_ID`, shared DB env, `depends_on: postgres healthy`) → `clutch-dashboard` (`image: jchristn77/clutch-ui:v0.1.0`) → `clutch-nginx` (load balancer, published port) → `prometheus` (prom/prometheus, scrapes both nodes' `/metrics`) → `grafana` (grafana/grafana, provisioned datasource + dashboards, named volume).
- [ ] **Prometheus** `docker/prometheus/prometheus.yaml` — scrape job targeting each node's OTel Prometheus exporter port `clutch-node1:9464` and `clutch-node2:9464` at `/metrics` (the side port from `TelemetrySettings.PrometheusPort`, not the REST port). Expose 9464 on both server services. `resource_to_telemetry_conversion` gives `service_name`/`node` labels. (Radiant routes through an `otel-collector`; for a two-node metrics-only baseline, direct scrape of 9464 is simpler — add the collector only if OTLP/traces/logs to Tempo/Loki are wired.)
- [ ] **Grafana** `docker/grafana/provisioning/` — `datasources/prometheus.yaml` (Prometheus datasource) + `dashboards/dashboards.yaml` (provider) + one or more `clutch-*.json` dashboards **preconfigured** to render on first boot: a transport dashboard (request rate/latency/status, WS connections) and a lock dashboard (acquires/denials/timeouts by mode, held/waiters gauges, wait + txn latency histograms, NOTIFY health). Model the provisioning layout on Radiant/LiteGraph.
- [ ] Dockerfiles (`src/Clutch.Server/Dockerfile`, `dashboard/Dockerfile`) exist to **build and push** `jchristn77/clutch-server:v0.1.0` and `jchristn77/clutch-ui:v0.1.0`; provide `build.sh`/`build.bat` to build+tag+push. `compose.yaml` only references the published tags.
- [ ] `docker/nginx/nginx.conf` — `upstream clutch { ip_hash; server clutch-node1:8080; server clutch-node2:8080; }`; proxy `/v1.0/` and `/openapi.json` to upstream; **WebSocket upgrade** headers (`Upgrade`/`Connection`, long read timeout) for `/v1.0/lock/connect`; `ip_hash` for connection affinity (correctness does not depend on it — DB is authority — but it stabilizes long-lived sockets). Model on `C:\Code\Hydra\docker\nginx\nginx.conf`.
- [ ] Per-node settings `docker/server/clutch.node1.json`, `clutch.node2.json` (same DB, distinct node id/log file).
- [ ] `docker/postgres/init/01-init.sql` optional (DB/user creation; schema is created by the app migrations at startup).
- [ ] Since `compose.yaml` references published tags, `docker/factory/templates/` includes the pinned `compose.yaml`; local iteration on server/dashboard code requires a rebuild+push (or a documented `--build` override compose file for dev).
- [ ] `docker/factory/` — `templates/` (pristine settings + compose copy), `reset.sh`/`reset.bat`: confirm `RESET`, `docker compose down`, drop the postgres volume, restore templates, clear logs. Model on Pneuma's `docker/factory`.
- [ ] Verify: `docker compose up` → both nodes register, migrations apply once, seed is idempotent, a lock acquired via node1 is visible/blocking via node2, disconnect releases, nginx balances and upgrades websockets.

---

## 13. Testing (`test/` — Touchstone)

- [ ] `Test.Shared` (Touchstone.Core only, no console output) — the single source of test descriptors consumed by every runner below.
- [ ] `Test.Automated` (Touchstone.Cli console runner, exit codes, `--results results.json`).
- [ ] `Test.Xunit` (`Touchstone.XunitAdapter`) — Fact-style `RunAll` + Theory-style one-row-per-descriptor for IDE Test Explorer.
- [ ] `Test.Nunit` (`Touchstone.NunitAdapter`) — Fact-style `RunAll` + `TestCaseSource` one-case-per-descriptor.
- [ ] All four runners execute the same `ClutchSuites.All`; no test logic is duplicated across runners.
- [ ] **Model suites** — validated setters, clamping, ID prefixes.
- [ ] **Auth suites** — password hash, token round-trip (AES/IV), credential key format, constant-time compare, session validate/revoke.
- [ ] **DB contract suites** — migrations idempotent, first-boot seeding once, tenant/user/credential CRUD, tenant-scoped enumeration never leaks cross-tenant.
- [ ] **Lock engine suites (core correctness):**
  - read shared up to `maxReaders`; `maxReaders` blocks the next reader.
  - write exclusive; `writeBlocksReads` true/false paths; write `Shared(N)`.
  - delete blocks everything; everything blocks delete.
  - first-acquirer policy fixed; later mismatched policy ignored (and `strictPolicy` returns `PolicyConflict`).
  - fencing token strictly monotonic per key.
  - lease expiry reclaims holders; heartbeat extends; `maxHoldMs` caps renewal.
  - release-on-disconnect frees holders.
  - **cross-node/concurrency:** N parallel acquirers on one key never exceed policy (drive `ILockHolderMethods` against a real Postgres via the test compose or a disposable DB); `Wait` behavior wakes on release and honors timeout.
- [ ] **Randomized soak / load suite (behavior + consistency + concurrency under stress).** A time-boxed fuzz test, configurable `DurationSeconds` (default 15, also run at 30) and `ClientCount` (random, e.g. 8–64). Setup: a **fixed array of lock keys** (e.g. 16 named keys with pre-seeded, varied policies — some `Shared` reads, some `Exclusive`/`Shared(N)` writes, mixed `WriteBlocksReads`). Each simulated client loops for the duration issuing operations against a **random index** in the key array with a **random mode** (`Read`/`Write`/`Delete`), **random behavior** (`FailFast`/`Wait` with random short timeouts), random lease, and random hold-then-release timing. Because inputs are random, outcomes are not precomputed — correctness is checked by an **invariant oracle** that must hold at every observation:
  - No two *incompatible* holders ever coexist on a key (evaluate the §1.3 matrix against the engine's authoritative holder set, read back from the DB — never from the client's optimistic view).
  - Reader/writer counts never exceed the key's policy (`maxReaders`, `WriteExclusivity`).
  - Every granted holder carries a **strictly monotonic** fencing token for its key; no token is ever reused or goes backward, even across contended grants.
  - A `FailFast` denial is only ever returned when the key was genuinely incompatible at decision time; a `Wait` grant only appears after a compatible window opened; no `Wait` outlives its deadline.
  - Every acquired holder is eventually released or expires; at end-of-run, after quiescence, **zero active holders remain** and the audit trail reconciles (acquired == released + expired + revoked).
  - No lost/leaked locks and no deadlock: throughput keeps progressing for the whole window (a watchdog asserts forward progress).
  - [ ] Run the same suite in **single-node** and **two-node** configurations (two engine instances sharing one Postgres) to exercise `LISTEN/NOTIFY` cross-node wakeups under load.
  - [ ] Seed the RNG from a logged value so any failing run is reproducible; on invariant violation, dump the seed, the offending key, and the holder set.
- [ ] **Tenant isolation suites** — locks/audit/keys never cross tenants.
- [ ] **Retention suites** — audit pruned per tenant days; request history pruned per retention.
- [ ] Dashboard: routing/login/table/empty-error smoke where practical; Playwright visual QA artifacts.

### 13.1 Test.Throughput — end-to-end load benchmark

A standalone console project (`test/Test.Throughput`), **not** part of `ClutchSuites.All` — it is a benchmark/load generator that measures real deployed throughput over the wire, not a pass/fail correctness suite. It drives the full Docker stack (nginx → 2 nodes → Postgres) exactly as a fleet of real clients would, over WebSockets.

- [ ] **CLI arguments** (with sensible defaults):
  - `--duration <seconds>` — how long to sustain load (required-ish; default 30).
  - `--threads <n>` — number of emulated concurrent clients (each its own WS connection + op loop); default e.g. 16.
  - `--endpoint <url>` — target base URL (default the local nginx LB, e.g. `http://localhost:8080`); lets the same tool point at a remote deployment.
  - `--access-key <key>` — application key to authenticate the WS clients (default the seeded factory key).
  - `--keys <n>` — size of the shared lock-key namespace to contend over (default 32); `--no-docker` to skip stack spin-up and hit an already-running endpoint.
- [ ] **Lifecycle:** unless `--no-docker`, the tool runs `docker compose up -d` on `docker/compose.yaml`, waits for `/v1.0/api/health` on the LB (and for both nodes) to go green, runs the load, then tears the stack down (`docker compose down`) on exit. A guard restores the stack to a clean state even if the run aborts.
- [ ] **Workload:** each emulated client opens a WS connection and issues a randomized mix of `acquire`/`release`/`heartbeat` operations (random key from the shared namespace, random mode, mostly `FailFast` so throughput isn't dominated by intentional waiting), acquiring and promptly releasing to keep operations flowing. Every operation is counted by type and outcome (granted/denied/error) and timed.
- [ ] **Formatted results at end**, printed as an aligned report:
  - **Throughput** — total operations/second (and a breakdown: acquires/sec, releases/sec).
  - **Runtime** — actual measured wall-clock duration of the load phase.
  - **Emulated clients** — thread/connection count used.
  - **Request distribution by type** — a table of each operation type (Read acquire, Write acquire, Delete acquire, Release, Heartbeat) with absolute count, percentage of total, and granted/denied split.
  - Latency summary (optional but recommended): p50/p95/p99 per operation type.
  - Total operations and error count.
- [ ] Deterministic-ish: log the RNG seed and echo the effective configuration (endpoint, threads, duration, key count) at the top of the report so a run is reproducible and comparable across builds.
- [ ] Document usage in `README.md`/`DOCKER.md`: `dotnet run --project test/Test.Throughput -- --duration 30 --threads 32 --endpoint http://localhost:8080`.

---

## 14. Final Verification Checklist

- [ ] `dotnet build src/Clutch.sln` clean (no errors/warnings).
- [ ] `dotnet run --project test/Test.Automated` all pass (exit 0).
- [ ] `dashboard` builds (`npm run build`) and lints clean.
- [ ] `docker compose up` full stack healthy; two-node lock coherency manually verified.
- [ ] OpenAPI served at `/openapi.json`; API Explorer introspects it.
- [ ] Settings round-trip on startup confirmed (add a property, restart, see it persisted).
- [ ] README accurate; divergences documented; CHANGELOG updated.
- [ ] Visual QA (desktop/tablet/mobile, light/dark) captured or summarized in handoff.
- [ ] `/metrics` exposes webserver + lock data-path metrics; Prometheus scrapes both nodes; Grafana dashboards render on first boot with live data.
- [ ] Repo initialized at `https://github.com/jchristn/Clutch`, branch `main`; committed and pushed.

---

## 15. Milestone Order (suggested)

1. M1 skeleton + settings → 2. M2 Postgres + migrations + seed → 3. M3 auth → 4. M4 lock engine + coordinator (with DB concurrency tests early) → 5. M5 REST + WS host → 6. Request history + retention → 7. Dashboard shell + charts + record pages → 8. SDKs → 9. Docker 2-node + nginx + factory → 10. Docs + Postman → 11. Full verification + visual QA.

Build the lock engine and its concurrency tests (§13) **before** the dashboard. The engine is the product; everything else presents it.
