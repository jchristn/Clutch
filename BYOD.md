# Clutch — Bring Your Own Database (BYOD) Plan

Clutch today is Postgres-only. The database abstraction (`DatabaseDriverBase`, the eight `I*Methods` contracts, `DatabaseDriverFactory`, `DatabaseTypeEnum`) was built provider-neutral from the start, but only `PostgresqlDatabaseDriver` exists and the factory throws for every other value. This plan takes Clutch from one provider to four — **SQLite, MySQL, PostgreSQL, and SQL Server** — and lets an operator point Clutch at a database they already own, choose the table names Clutch uses for each purpose, and decide whether Clutch is allowed to create those tables at all.

**Release: v0.2.0 (alpha).** A minor increment from v0.1.0. The software is still alpha, so schema, settings, SDK surfaces, and behavior remain subject to change. Every assembly, package, Docker tag, and doc version stamp moves from `0.1.0` to `0.2.0` (see §14).

**Repository:** https://github.com/jchristn/Clutch (branch `main`).

## How to use this document

Every task below is a checkbox a developer annotates as work proceeds. Work top-to-bottom within a milestone; milestones are ordered so each builds on the one before it. Update status inline:

- `- [ ]` not started
- `- [~]` in progress
- `- [x]` complete
- Append `— <note>` for a blocker, a decision, or a deviation.

The milestones are the build order. §11 (portability hazards) and §12 (table catalog) are reference material the provider milestones point back to — read them before writing any provider driver.

---

## 1. Locked Design Decisions

Confirmed with the product owner. Changing any of these requires a plan revision, not an implementation-time judgment call.

### 1.1 Four providers, one contract

Clutch implements `SQLite`, `MySQL`, `PostgreSQL`, and `SQL Server`. All four satisfy the same eight `I*Methods` interfaces and the same `DatabaseDriverBase` lifecycle. Callers above the driver — the lock engine, the routes, the MCP server, the sweeper, the retention service — never learn which provider is behind the abstraction. The interfaces already prove this is achievable: they reference only the BCL and `Clutch.Core.Models`, nothing Npgsql.

### 1.2 Coordination is polling everywhere — `LISTEN/NOTIFY` is removed

The Postgres `LISTEN/NOTIFY` cross-node wakeup path is **deleted**, not abstracted. Every provider — including Postgres — wakes blocked waiters with the bounded polling fallback that already exists (`LockEngine` retries the acquire transaction every `WaiterPollMs`). This gives one coordination code path across all four providers instead of four different push mechanisms (Postgres pub/sub, SQL Server Service Broker, nothing for MySQL/SQLite).

Correctness does not change: the database transaction remains the sole authority for every grant. Polling only affects **wakeup latency** for a caller blocked in wait mode, bounded by `WaiterPollMs` (default 1000 ms, configurable, and already lowered per-request by `Math.Min(remaining, WaiterPollMs)` in `LockEngine.AcquireAsync`).

The in-process `LockCoordinator` stays. It is a free, same-node optimization: a local release/revoke/expiry signals local waiters immediately via `SignalComposite`, so single-node latency stays near-zero. What goes away is the database round-trip (`pg_notify`) and the dedicated listener connection. Cross-node waiters simply retry on their poll interval.

- Delete `Clutch.Core/Database/Postgresql/Notifications/PostgresNotificationListener.cs`.
- Delete `Constants.LockReleaseChannel` and every `NotifyAsync` / `pg_notify` call in the holder methods.
- Ensure every local release path calls `LockCoordinator.SignalComposite` directly (in-process), replacing the round-trip the listener used to deliver back to the same node. `ReleaseAllForSessionAsync` already does this in `LockEngine`; single `ReleaseAsync`, `RevokeAsync`, and expiry reclaim must do it too.
- Remove the `_Listener` field and its wiring from `ClutchServer`.

### 1.3 Configurable table names and schema, fixed columns

The operator configures the **table name** Clutch uses for each of its nine purposes, plus an optional **schema/namespace** and an optional **name prefix**. Clutch continues to own the column layout entirely — the narrow, fixed-width columns on `lock_definitions` and `lock_holders` are what make the hot path cache-resident and fast, and that design is not up for negotiation per-deployment. There is no per-column mapping onto arbitrary pre-existing layouts.

Each purpose has a default name of the form `clutch_{purpose}` — `clutch_tenants`, `clutch_lock_holders`, and so on — so Clutch's tables are self-identifying and unlikely to collide with tables already living in a database the operator owns. A user override replaces the default for that one purpose only. The nine purposes and their defaults are enumerated in §12. A resolved table name is `{schema}.{prefix}{configuredName}`, quoted per provider.

### 1.4 SQLite is single-node only

SQLite backs development, testing, and single-server or embedded deployments. It cannot safely serve multiple server nodes writing concurrently over a shared file, and Clutch will not pretend otherwise. Multi-node clustering is supported on PostgreSQL, MySQL, and SQL Server. When Clutch is configured for SQLite, startup logs a warning that the deployment must be single-node, and the docs say so plainly. The full test suite still runs against SQLite in single-node mode.

### 1.5 Clutch creates its tables by default, with an opt-out

By default Clutch runs its tracked migrations against the target database and creates any missing tables and indexes using the configured names (`CREATE TABLE IF NOT EXISTS` semantics, per provider). A `Database.ManageSchema` flag (default `true`) turns this off: when `false`, Clutch issues no DDL, verifies at startup that every required table exists, and refuses to start with a clear error if one is missing. For every provider Clutch also ships a reviewable, idempotent `sql/{provider}/schema.sql` so a DBA can create the tables by hand in a least-privilege setup.

---

## 2. Compliance and deliverables

The plan must satisfy the requirement set in `c:\code\agents\requirements` in full. Each item below is a gate, checked during the final sweep (§14).

- [ ] **`CODE_STYLE.md`** — every new file: namespace-first with `using` inside the namespace, Microsoft/System usings first then others (each alphabetized), XML docs on all public members/methods, `_PascalCase` private fields, no `var`, no tuples, `ConfigureAwait(false)`, `CancellationToken` on every async method, guard clauses, specific exception types with context, `<exception>` tags, one class/enum per file, no `Console.WriteLine` in library code. Handwritten SQL is retained by design (§1.3) — it is templated with resolved, validated identifiers, never string-concatenated with user data.
- [ ] **`BACKEND_ARCHITECTURE.md`** — the four-provider mandate this doc always specified is now honored rather than deferred. Provider-neutral base, handwritten SQL, versioned/tracked/idempotent migrations, first-boot seeding all preserved across every provider.
- [ ] **`BACKEND_TEST_ARCHITECTURE.md`** — Touchstone descriptors in `Test.Shared`, consumed unchanged by `Test.Automated`, `Test.Xunit`, `Test.Nunit`. The suite becomes a provider matrix (§10).
- [ ] **`FRONTEND_ARCHITECTURE.md` + `DASHBOARD_STYLE_AND_USABILITY.md` + `I18N.md`** — dashboard changes use the existing declarative `SECTIONS`/`renderField` model, `.panel`/`.field`/`.settings-grid` styling, `useToast` feedback, and add English i18n keys (with `de`/`ja`/`zz` deep-merge fallbacks) under `views.serverSettings`. No nested cards.
- [ ] **`REPOSITORY_REQUIREMENTS.md`** — source stays under `src/` / `Test.*` / `dashboard/` / `sdk/`; `.gitignore`/`.dockerignore` updated for new engines; `README.md`, `DOCKERHUB_README.md`, `CHANGELOG.md` updated; Docker compose stays `.yaml` with explicit tagged images. SDK loopback URLs stay on `127.0.0.1`.
- [ ] **`WRITING_DOCUMENTS.md`** — README, DOCKERHUB_README, CHANGELOG, and the new BYOD user guide are re-read for human voice: real prose per section, no `This.../These...` throat-clearing, varied cadence, no formulaic conclusions.
- [ ] Record the one deliberate divergence carried forward and now partly resolved: v0.1.0 shipped Postgres-only "because cross-node coordination relies on `LISTEN/NOTIFY`." v0.2.0 removes that dependency (§1.2), which is what unlocks the other three providers. Note this in the README and CHANGELOG.

---

## 3. Architecture changes at a glance

The seam already exists; this work fills it in and generalizes three Postgres-specific helpers.

```
                         DatabaseDriverFactory.Create(DatabaseSettings)
                                        │  switch on Type
        ┌───────────────┬───────────────┼───────────────┬───────────────┐
        SqliteDriver     MysqlDriver    PostgresqlDriver  SqlServerDriver
        │               │               │               │
        └───────────────┴──── implement 8× I*Methods ───┴───────────────┘
                                        │
                     shared: SqlDialect (identifier quoting, paging,
                     upsert, row-lock, interval math, boolean literals),
                     TableCatalog (resolved names), SchemaMigration
                                        │
                              LockEngine / routes / MCP / sweeper
                              (unchanged — see only DatabaseDriverBase)

  coordination: LockCoordinator (in-process, all providers)
                + LockEngine polling every WaiterPollMs (all providers)
                — no LISTEN/NOTIFY, no listener connection
```

Three Postgres-bound helpers become per-provider or dialect-parameterized:

- **`Converters.cs`** is hard-typed to `NpgsqlDataReader`. Each provider needs its own reader→model mapping (`Microsoft.Data.Sqlite`, `MySqlConnector`, `Microsoft.Data.SqlClient` readers). Extract a common shape so the mapping logic is written once and the reader access differs only in type.
- **`EnumerationSql.cs`** builds `ORDER BY … OFFSET … LIMIT …`. Paging syntax and identifier quoting differ per provider; route it through `SqlDialect`.
- **`SetupQueries.cs`** is raw Postgres DDL. Each provider gets its own `SetupQueries` with the correct types (`TIMESTAMPTZ`→`datetime2`/`DATETIME(6)`/`TEXT`, `JSONB`→`NVARCHAR(MAX)`/`JSON`/`TEXT`, `BOOLEAN`→`BIT`, `DOUBLE PRECISION`→`FLOAT`/`DOUBLE`/`REAL`) and index-creation idioms, all driven by the resolved `TableCatalog`.

---

## 4. Milestone 0 — Foundations: settings, factory, dialect seam

No provider is implemented yet. This milestone makes the configuration surface and the shared scaffolding real so the four drivers slot into a stable shape.

- [ ] **`DatabaseSettings` — provider-neutral connection model.** In `src/Clutch.Core/Database/DatabaseSettings.cs`, keep `Type`, `Host`, `Port`, `DatabaseName`, `Username`, `Password`, `MaxPoolSize`. Add: `Schema` (nullable; PostgreSQL/SQL Server namespace), `FilePath` (nullable; SQLite database file), `TablePrefix` (nullable), `ManageSchema` (bool, default `true`, §1.5), `Tables` (a `TableNamingSettings` object, §12), and an optional `AdditionalOptions` string appended verbatim to the connection string for provider-specific knobs. Every setter keeps guard clauses and clamps per `CODE_STYLE.md`.
- [ ] **Per-provider connection strings.** Replace the single `ToPostgresConnectionString()` with a builder that dispatches on `Type` (or a small builder per provider). SQLite uses `FilePath` and ignores host/port/user/password. Defaults (`Port=5432`, `Password="postgres"`) become provider-aware — do not force a Postgres port onto a SQL Server config. Add an `<exception>`-documented `Validate()` that rejects incoherent combinations (e.g. SQLite with a host, non-SQLite with no host).
- [ ] **`TableNamingSettings` + `TableCatalog`.** New file `TableNamingSettings.cs`: nine nullable name overrides (§12) plus `Schema`/`Prefix` convenience. A null/blank override for a purpose resolves to its `clutch_{purpose}` default; a non-null value replaces it. New `TableCatalog` (resolved once at driver construction) applies the `clutch_` defaults, layers the optional prefix and schema, and exposes the final, validated identifier for each purpose. Validate every resolved name and the schema/prefix against a strict identifier allowlist (`^[A-Za-z_][A-Za-z0-9_]*$`) and throw `ArgumentException` on anything else — this is the only defense against injection through a configured table name, since these names are concatenated into SQL.
- [ ] **`SqlDialect` abstraction.** New folder `src/Clutch.Core/Database/Sql/`. Define an abstract `SqlDialect` (or interface) with: `QuoteIdentifier(name)` (`"x"` / `` `x` `` / `[x]`), `ParameterPrefix`, `Paging(skip, max)` text, boolean literal rendering, `NowExpression`, a lease-interval expression helper (replacing `make_interval`), a `RowLockHint`/transaction strategy descriptor, and a `SupportsReturning` flag. One concrete dialect per provider. This is where §11's hazards get resolved once instead of scattered through each driver.
- [ ] **Factory dispatch.** `DatabaseDriverFactory.Create` returns the real driver for all four `DatabaseTypeEnum` values; delete the `NotSupportedException` arms. Keep the `default:` arm for unknown values.
- [ ] **`DatabaseTypeEnum` doc comments.** Drop the "Reserved for future implementation" language now that each value is implemented.
- [ ] **Env overrides.** In `Bootstrapper.ApplyEnvironmentOverrides`, add `CLUTCH_DB_TYPE` (parse into `DatabaseTypeEnum`), `CLUTCH_DB_FILEPATH` (SQLite), `CLUTCH_DB_SCHEMA`, and `CLUTCH_DB_MANAGE_SCHEMA`. Existing host/port/name/user/password overrides remain.
- [ ] **NuGet references.** Add `Microsoft.Data.Sqlite`, `MySqlConnector`, and `Microsoft.Data.SqlClient` to `Clutch.Core.csproj`. Multi-target stays `net8.0;net10.0`. Confirm license compatibility (all three are MIT/Apache-style and standard).
- [ ] Build `Clutch.Core` clean — no warnings — before writing any driver.

---

## 5. Milestone 1 — Refactor the Postgres driver onto the shared seam

Do Postgres first, on the new `SqlDialect`/`TableCatalog`/generalized-`Converters` shape, and keep it green against the existing tests. This proves the seam before three new providers depend on it, and it is the reference implementation the others are written against.

- [ ] Introduce `PostgresqlDialect` and route the existing `PostgresqlDatabaseDriver` SQL through the resolved `TableCatalog` (table names) and dialect (quoting/paging). Behavior identical; names now come from config.
- [ ] Generalize `Converters.cs`: keep a Postgres reader mapper but factor the model-shaping logic so SQLite/MySQL/SQL Server mappers reuse it. `DateTime` kind handling (`SpecifyKind(..., Utc)`) becomes a documented per-provider concern.
- [ ] Remove `pg_notify`/`NotifyAsync` and the listener per §1.2; wire local `SignalComposite` on every local release path.
- [ ] Full `Test.Shared` suite green against Postgres on the refactored driver, including `soak-randomized`. Update or retire `notify-fires` (§10).
- [ ] `sql/postgresql/schema.sql` generated from the resolved default catalog and committed.

---

## 6. Milestone 2 — SQLite provider

Reference the §11 hazard table throughout. SQLite is the simplest engine but has the sharpest concurrency constraint, which §1.4 already bounds to single-node.

- [ ] `src/Clutch.Core/Database/Sqlite/` mirroring the Postgres tree: `SqliteDatabaseDriver`, `SqliteDialect`, `Converters`, `Queries/SetupQueries`, `Implementations/` (eight method classes).
- [ ] Driver uses `Microsoft.Data.Sqlite`. Open with `busy_timeout` set and WAL journal mode enabled. `DatabaseName`/`Host` ignored; `FilePath` is the target. Support `:memory:` / shared-cache for tests.
- [ ] **Acquire serialization without `FOR UPDATE`.** SQLite has no row lock. Use `BEGIN IMMEDIATE` to take the database write lock at the top of `TryAcquireAsync`, serializing concurrent acquirers, with `busy_timeout` for contention. Document that this is coarser than a row lock but correct and adequate for single-node.
- [ ] **`RETURNING`** — SQLite 3.35+ supports it; the bundled `Microsoft.Data.Sqlite` SQLite is new enough. Verify the version and keep `RETURNING`, else fall back to `SELECT`-then-`DELETE` inside the transaction.
- [ ] Types: `TIMESTAMPTZ`→`TEXT` (ISO-8601 UTC), `JSONB`→`TEXT`, `BOOLEAN`→`INTEGER`, `DOUBLE PRECISION`→`REAL`. Reader mapping converts back, being explicit about SQLite returning strings for dates.
- [ ] `ILIKE`→`LIKE` (SQLite `LIKE` is ASCII case-insensitive by default). `= ANY(@ids)`→expanded `IN (@p0,@p1,…)`. `make_interval`→`datetime(...,'+N seconds')` via the dialect. `COUNT(*)` returns `long` — safe.
- [ ] Startup single-node warning when the node is part of a multi-node deployment (heuristic: log the warning unconditionally for SQLite, and loudly if `Rest`/cluster config implies siblings).
- [ ] `sql/sqlite/schema.sql` committed.
- [ ] Full `Test.Shared` suite green against SQLite, including `soak-randomized` at single-node concurrency.

---

## 7. Milestone 3 — MySQL provider

- [ ] `src/Clutch.Core/Database/Mysql/` mirroring the tree; driver uses `MySqlConnector`.
- [ ] **Acquire serialization** — InnoDB `SELECT … FOR UPDATE` works; the unique index on `(tenantid, lockkey)` gives the gap lock needed to serialize acquirers on a not-yet-existing definition row. Verify with the soak test.
- [ ] **`RETURNING`** — MySQL does not support it. Rewrite each `DELETE … RETURNING *` / `UPDATE … RETURNING` as `SELECT` (inside the transaction, under the row lock) then `DELETE`/`UPDATE`, returning the pre-read rows. This touches `ReleaseAsync`, `ReleaseAllForSessionAsync`, `RevokeAsync`, `PurgeExpiredAsync`, `DeleteExpiredForKeyAsync`, `HeartbeatAsync`, and `IncrementFencingAsync` (read-back the incremented counter).
- [ ] Types: `TIMESTAMPTZ`→`DATETIME(6)` (store UTC), `JSONB`→`JSON`, `BOOLEAN`→`TINYINT(1)`, `DOUBLE PRECISION`→`DOUBLE`. `ILIKE`→`LIKE` (case-insensitive under default collation; pin a collation in DDL to be deterministic).
- [ ] `= ANY(@ids)`→`IN (…)`. `make_interval`→`DATE_ADD(…, INTERVAL … MICROSECOND)`. `LEAST` is native. Paging `LIMIT @max OFFSET @skip` — bind as literals if the driver/prepared-statement combo won't bind them.
- [ ] `COUNT(*)` cast: MySQL returns `long`; keep `(long)` but centralize scalar-count reading in a dialect helper that coerces `int`/`long`/`decimal` safely (also fixes the SQL Server `int` case, §11 hazard 12).
- [ ] `CREATE INDEX IF NOT EXISTS` is unsupported on older MySQL — guard index creation (check `information_schema` or catch duplicate-index errors) in the migration.
- [ ] `sql/mysql/schema.sql` committed.
- [ ] Full `Test.Shared` suite green against MySQL 8, including `soak-randomized`.

---

## 8. Milestone 4 — SQL Server provider

The most dialect-divergent engine; budget the most time here.

- [ ] `src/Clutch.Core/Database/SqlServer/` mirroring the tree; driver uses `Microsoft.Data.SqlClient`.
- [ ] **Acquire serialization** — no `FOR UPDATE`. Use `SELECT … WITH (UPDLOCK, ROWLOCK, HOLDLOCK)` inside a `READ COMMITTED` (or `SERIALIZABLE` on the definition read) transaction. `HOLDLOCK` provides the key-range lock that serializes acquirers on a not-yet-inserted definition row. Prove it with the soak test — this is the single highest-risk item in the plan.
- [ ] **`RETURNING`→`OUTPUT`** — SQL Server supports `DELETE … OUTPUT DELETED.*` and `UPDATE … OUTPUT INSERTED.*`, so the deletes/updates can stay single-statement using `OUTPUT` rather than the read-then-write rewrite MySQL needs. Use the `SupportsReturning`/`OutputClause` dialect flag to branch.
- [ ] Types: `TIMESTAMPTZ`→`datetime2(7)` storing UTC (or `datetimeoffset`), `JSONB`→`NVARCHAR(MAX)`, `BOOLEAN`→`BIT` (literals `1`/`0`, not `TRUE`/`FALSE`), `DOUBLE PRECISION`→`FLOAT`.
- [ ] `ILIKE`→`LIKE` with an explicit case-insensitive collation (`COLLATE Latin1_General_CI_AS`) so behavior does not depend on the database's default collation.
- [ ] `= ANY(@ids)`→`IN (…)` or a table-valued parameter; start with expanded `IN`. `make_interval`→`DATEADD(ms, …)`; `LEAST`→`CASE`/`IIF` (no `LEAST` in SQL Server). Paging→`ORDER BY … OFFSET @skip ROWS FETCH NEXT @max ROWS ONLY` (ORDER BY is mandatory — `EnumerationSql` already always emits one, confirm).
- [ ] `SELECT 1 … LIMIT 1`→`SELECT TOP 1 1`. `COUNT(*)` returns `int` — the shared scalar-count helper (§7) must handle it.
- [ ] `CREATE TABLE IF NOT EXISTS` / `CREATE INDEX IF NOT EXISTS` unsupported — wrap each in `IF NOT EXISTS (SELECT … FROM sys.tables/sys.indexes …)` guards in the migration.
- [ ] Schema qualification: honor `Database.Schema` (default `dbo`).
- [ ] `sql/sqlserver/schema.sql` committed.
- [ ] Full `Test.Shared` suite green against SQL Server 2022, including `soak-randomized`.

---

## 9. Milestone 5 — Server, MCP, and coordination wiring

- [ ] **`ClutchServer`** — remove the `PostgresNotificationListener` field, the `database is PostgresqlDatabaseDriver` block, and the `_Listener.StartAsync()`/dispose calls. Nothing else in the host changes; it already depends only on `DatabaseDriverBase`.
- [ ] **`Bootstrapper`** — the DB-init log line already prints `settings.Database.Type`; confirm it reads correctly for all providers. Add the SQLite single-node warning here.
- [ ] **`ManageSchema=false` path** — driver `InitializeAsync` verifies presence of every catalog table and throws a clear, provider-named error if one is missing; it issues no DDL. Bootstrapper surfaces this as a fatal startup error, consistent with today's init-failure handling.
- [ ] **Test-connection endpoint** — add `POST /v1.0/api/settings/database/test` in `SettingsRoutes` (admin-only, consistent with the other settings routes). It builds a driver from the posted `DatabaseSettings`, runs `PingAsync` (and, if `ManageSchema` is false, a table-presence check), disposes it, and returns `{ ok, message }`. Redact the password on the way in exactly as `UpdateAsync` does. This backs the dashboard "Test connection" button (§ dashboard).
- [ ] **MCP** — `ClutchMcpServer` already depends only on `DatabaseDriverBase` and its interface-typed method groups; it needs no provider changes. Verify all four tools (`clutch_server_info`, `clutch_list_tenants`, `clutch_list_locks`, `clutch_lock_audit`) work against each provider (covered by the provider matrix using the same enumeration methods). Optionally enrich `clutch_server_info` to report the active provider.

---

## 10. Milestone 6 — Tests: thorough, per-provider matrix

The requirement is thoroughness for **all** providers. `Test.Shared/ClutchSuites.cs` is the single choke point: all three runners consume `GetSuites()` verbatim, so expanding it into a matrix flows through `Test.Automated`, `Test.Xunit`, and `Test.Nunit` with no runner changes.

- [ ] **Parameterize `InitDatabase` over provider.** Read `CLUTCH_TEST_DB_TYPE` (single provider) or iterate all providers that are reachable. Set `settings.Type` and provider-appropriate connection defaults (today everything defaults to Postgres-shaped `localhost:5544`). Emit one suite per available provider, id-prefixed (`clutch-postgresql`, `clutch-mysql`, `clutch-sqlserver`, `clutch-sqlite`), so the 16 DB-backed cases run once per provider. Unreachable providers skip with a clear reason, as Postgres does today.
- [ ] **The 6 compatibility cases** stay provider-independent (pure `LockCompatibilityEvaluator` logic) — run once, not per provider.
- [ ] **`notify-fires`** currently hard-casts to `PostgresqlDatabaseDriver` and `PostgresNotificationListener`. With `LISTEN/NOTIFY` removed (§1.2), retire it and add in its place a **`waiter-poll-wakeup`** case that asserts a blocked waiter is granted after a release on another logical connection within `WaiterPollMs` — the provider-agnostic behavior that replaces it. Run it per provider.
- [ ] **`soak-randomized` per provider** is the acquire-serialization proof for each engine and the gate for milestones 2–4. It must pass its invariant oracle (`CheckKeyInvariantAsync`) on SQLite (single-writer), MySQL (`FOR UPDATE` gap lock), SQL Server (`UPDLOCK/HOLDLOCK`), and Postgres. Keep `CLUTCH_SOAK_MS/_CLIENTS/_SEED` tunable.
- [ ] **SQLite in-process** needs no container — a temp file or `:memory:` shared-cache database. Add a fixture that creates and tears it down.
- [ ] **Container provisioning for MySQL / SQL Server / Postgres.** Add a `docker/compose.test.yaml` that publishes Postgres, MySQL, and SQL Server on distinct host ports, and document the env vars a developer/CI sets to point the matrix at them. (Testcontainers is an acceptable alternative if preferred; compose keeps it consistent with the rest of the repo and needs no new dependency.) Note explicitly in the test README that the matrix requires these engines running — silent skips must log which providers were exercised and which were skipped.
- [ ] **`Test.Throughput`** talks to a running endpoint over WebSockets and inherits whatever provider the server uses, so it needs no provider logic. Add a `--compose` option (or document one) to point it at per-provider stacks so throughput can be measured on each engine and reported in docs.
- [ ] **Data-driven runners** (`Test.Nunit/ClutchNunitTests.cs`, and an equivalent theory file for `Test.Xunit` if desired) then surface one row per (provider × case) automatically.
- [ ] Every provider suite green in `Test.Automated`, `Test.Xunit`, and `Test.Nunit`.

---

## 11. Reference — Postgres portability hazards and per-provider resolution

Read this before writing any driver. Each row is a Postgres idiom in the current code, where it lives, and how each provider resolves it. The `SqlDialect` (§4) is where most of these are centralized.

| # | Postgres idiom | Where (file:line, current) | SQLite | MySQL | SQL Server |
|---|---|---|---|---|---|
| 1 | `RETURNING *` on DELETE/UPDATE | `LockHolderMethods.cs` 159/200/220/248/276/424/461 | keep (3.35+) | rewrite: SELECT-under-lock then DELETE/UPDATE | `OUTPUT DELETED.*` / `OUTPUT INSERTED.*` |
| 2 | `SELECT … FOR UPDATE` row lock | `LockHolderMethods.cs:382` | `BEGIN IMMEDIATE` (db write lock) | `FOR UPDATE` (gap lock via unique index) | `WITH (UPDLOCK, ROWLOCK, HOLDLOCK)` |
| 3 | `make_interval(secs => …)` + `LEAST` | `LockHolderMethods.cs:191-193` | `datetime(…,'+N seconds')`, `MIN()` | `DATE_ADD(… INTERVAL … MICROSECOND)`, `LEAST` | `DATEADD(ms, …)`, `CASE`/`IIF` (no `LEAST`) |
| 4 | `= ANY(@ids)` array bind | `LockHolderMethods.cs:199`; `LockAuditMethods.cs:91/92/144` | expand to `IN (@p0…)` | expand to `IN (…)` | expand to `IN (…)` or TVP |
| 5 | `pg_notify` / `LISTEN`/`NOTIFY` | `LockHolderMethods.cs:533`; `PostgresNotificationListener.cs` | **removed for all** (§1.2) | removed | removed |
| 6 | `ILIKE` | `LockHolderMethods.cs:316/335`; `LockAuditMethods.cs:90/143`; `RequestHistoryMethods.cs:231` | `LIKE` (ASCII CI default) | `LIKE` (CI collation) | `LIKE … COLLATE …_CI_AS` |
| 7 | `JSONB` column + `NpgsqlDbType.Jsonb` | `SetupQueries.cs:188/192`; `RequestHistoryMethods.cs:66-74` | `TEXT` | `JSON` | `NVARCHAR(MAX)` |
| 8 | `TIMESTAMPTZ` + UTC kind assumption | `SetupQueries.cs` throughout; `Converters.cs:110/122` | `TEXT` ISO-8601 | `DATETIME(6)` | `datetime2(7)` |
| 9 | `OFFSET @skip LIMIT @max` | `EnumerationSql` + 6 call sites | same | `LIMIT @max OFFSET @skip` (literals if unbindable) | `OFFSET … ROWS FETCH NEXT … ROWS ONLY` (needs ORDER BY) |
| 10 | `BOOLEAN` / `TRUE`/`FALSE` | `SetupQueries.cs`; `LockHolderMethods.cs:401/474`; `AuthSessionMethods.cs:101` | `INTEGER` 0/1 | `TINYINT(1)` | `BIT` 1/0 |
| 11 | existence probe `… LIMIT 1` | `TenantMethods.cs:242` | keep | keep | `SELECT TOP 1 1` |
| 12 | `COUNT(*)` unbox to `(long)` | `LockAuditMethods.cs:108`; `LockHolderMethods.cs:346/449/466`; `TenantMethods.cs:107`; `UserMethods.cs:124`; `CredentialMethods.cs:119` | `long` ok | `long` ok | returns `int` — **use shared coercion helper** |
| 13 | `DOUBLE PRECISION` | `SetupQueries.cs:186` | `REAL` | `DOUBLE` | `FLOAT` |
| 14 | `CREATE TABLE/INDEX IF NOT EXISTS` | `SetupQueries.cs` throughout | supported | table ok; index needs guard | both need `IF NOT EXISTS(SELECT … sys.*)` guards |
| 15 | manual multi-statement cascades | `TenantMethods` 157-234; `UserMethods` 181-204; `LockDefinitionMethods` 73-96 | re-implement with provider tx types | same | same |

Non-hazards: plain `INSERT`/`UPDATE`/`SELECT … WHERE`, `GROUP BY` (e.g. `CountHoldersAsync`), and named parameters all port cleanly, though `Microsoft.Data.SqlClient` is stricter about the `@` sigil than Npgsql — the dialect's `ParameterPrefix` handles it.

---

## 12. Reference — the table catalog

Nine tables, each with a configurable name and a fixed column layout Clutch owns. Purpose keys are the identifiers used in `TableNamingSettings`, `clutch.json`, and the dashboard mapping UI.

Every default name follows the `clutch_{purpose}` convention so Clutch's tables are self-identifying and unlikely to collide with tables already in a database the operator owns. A user override replaces the default name for that one purpose; the other eight keep their `clutch_` defaults.

| Purpose key | Default table | On the lock hot path? | Notes |
|---|---|---|---|
| `schemaMigrations` | `clutch_schema_migrations` | no | migration tracking; skipped when `ManageSchema=false` |
| `tenants` | `clutch_tenants` | no | |
| `users` | `clutch_users` | no | |
| `credentials` | `clutch_credentials` | no | |
| `authSessions` | `clutch_auth_sessions` | no | |
| `lockDefinitions` | `clutch_lock_definitions` | **yes** | narrow fixed-width row; `FOR UPDATE` target |
| `lockHolders` | `clutch_lock_holders` | **yes** | narrow fixed-width row; counted per acquire |
| `lockAudit` | `clutch_lock_audit` | no | wide, off the decision path |
| `requestHistory` | `clutch_request_history` | no | wide (JSON headers/bodies), off the decision path |

Resolved identifier = `{schema}.{prefix}{name}`, where `name` defaults to `clutch_{purpose}` and is replaced per-purpose by any user override, each part quoted by the provider dialect. The optional `prefix` stacks on top of the resolved name (default empty), so a deployment can add its own namespace without restating all nine names. Names, prefix, and schema are validated against `^[A-Za-z_][A-Za-z0-9_]*$` at construction (§4). `ManageSchema=false` requires all nine (or eight, if migrations tracking is skipped) to pre-exist with the expected columns.

The `clutch_` defaults rename the v0.1.0 bare tables (`tenants`, `lock_holders`, …). Because Clutch is alpha and the schema is explicitly subject to change, v0.2.0 does not auto-migrate an existing v0.1.0 database — a fresh deploy picks up the new defaults, and an operator keeping an existing database either overrides the nine names back to the bare forms or renames the tables using the shipped `sql/{provider}/schema.sql` as the reference. Call this out in the CHANGELOG (§14).

---

## 13. Milestone 7 — Dashboard, SDKs, and documentation

### Dashboard

The provider dropdown and connection fields already exist in `ServerSettingsView.jsx` (`SECTIONS` group `database`, `DB_TYPES = ['Sqlite','SqlServer','Mysql','Postgresql']`). The gaps are conditional fields, the table-name map, and the test button.

- [ ] **Conditional fields.** The declarative model renders every field unconditionally. Add a per-field `showIf(form)` predicate checked in the `section.fields.map` render so SQLite shows `filePath` and hides host/port/username/password, and vice-versa.
- [ ] **Table-name mapping.** Add a `type: 'map'` field (or a dedicated `renderTableMap` helper) that renders one labeled text input per purpose key (§12) and reads/writes the nested `form.database.tables[key]`. Extend `getValue`/`setValue` to resolve the two-level path — they currently only reach one level deep. Keep it inside the existing database `.panel` (no nested cards, per the style guide).
- [ ] **`ManageSchema` toggle** as a `checkbox` field with a hint explaining the least-privilege behavior.
- [ ] **Test connection.** Add `apiClient.testDatabaseConnection(body)` in `src/utils/api.js` hitting `POST /v1.0/api/settings/database/test`. Special-case the database `.panel` (or add a `section.actions` slot) to render a `button-secondary` "Test connection" with an in-flight disabled state, and surface the result through `useToast`.
- [ ] **Restart semantics** already handled — DB changes set `restartRequired`; the existing restart flow covers it.
- [ ] **i18n.** Add English keys under `views.serverSettings` — `sections.database` already exists; add `fields.database.type/host/port/filePath/schema/tablePrefix/manageSchema`, the `fields.database.tables.<purposeKey>` labels, and the test-connection button/toast strings — in `src/i18n/resources.js`. English is sufficient (other locales deep-merge over `en`); add `de`/`ja` overrides for the high-visibility labels.

### SDKs

The SDKs never send or manage DB config; the database appears only as read-only `ServerInfo.Database` (string) and `HealthResponse.Database` (bool). Changes are additive and small.

- [ ] If `server-info` is enriched to name the active provider more richly, add the property to the C# typed `ServerInfo` model (`sdk/csharp/Clutch.Sdk/ServerInfo.cs`); JS/Python read untyped dicts and need only console-printer updates. Do **not** change `ServerInfo.Database` from string to object — that would break the C# model.
- [ ] Only if a writable settings endpoint is exposed through the SDK (not required by BYOD) add a `DatabaseSettings` DTO and `Get/UpdateServerSettingsAsync` to each admin client — net-new, nothing existing reworked. Default: leave the SDK admin surface unchanged and document that DB config is a server/dashboard concern.
- [ ] Each SDK README (`sdk/csharp`, `sdk/js`, `sdk/python`) gets a short note that Clutch's backing database is server-side configuration and transparent to clients.

### Documentation

- [ ] **`README.md` — full, thorough revision, not a patch.** v0.2.0 changes what Clutch *is* (four backing databases, BYOD), how you *set it up* (provider choice, connection details, table naming, `ManageSchema`, shipped DDL), its *surface area* (the database configuration UI, the test-connection endpoint, the new settings fields), and how it *deploys* (per-provider stacks, the SQLite single-node constraint, polling instead of `LISTEN/NOTIFY`). Revise the README end to end against that new reality rather than editing the one scope note. Concretely: rewrite the opening description and "What it does" so BYOD is a headline capability, not a footnote; replace the "Postgres only" scope note entirely; update the Architecture section and its diagram to show the provider seam and polling coordination (drop the `LISTEN/NOTIFY` claim); revisit the "Schema design and throughput" section so its narrowness/throughput argument reads correctly across all four engines and names the per-engine acquire-serialization strategy (§11); add a "Bring your own database" section covering provider selection, connection configuration, the `clutch_{purpose}` default table names and how to override them, schema/prefix, `ManageSchema`, the `sql/{provider}/schema.sql` scripts, and the SQLite single-node caveat, with a worked example per engine; refresh Getting Started and every doc/link reference; keep the alpha banner and bump to v0.2.0. Then re-read the whole document for human voice per `WRITING_DOCUMENTS.md` — real prose per section, no `This.../These...` lead-ins, varied cadence, no formulaic wrap-up. Confirm every claim in it is still true against the shipped v0.2.0 build (`CODE_STYLE.md` requires README accuracy).
- [ ] **New `BYOD.md`-referenced user guide** (or a "Bring your own database" section in README + a `docs`/dedicated page): how to configure each provider, the table-naming and schema options, `ManageSchema`, the shipped `sql/{provider}/schema.sql` scripts, the SQLite single-node caveat, and a worked example per engine.
- [ ] **`DOCKER.md` + `DOCKERHUB_README.md`** — document per-provider deployment; add example compose overrides.
- [ ] **`REST_API.md`** — document `POST /v1.0/api/settings/database/test` and the new `Database` settings fields.
- [ ] **`Clutch.postman_collection.json`** — add the test-connection request; ensure the settings examples show the new fields.
- [ ] **`WEBSOCKETS_API.md`** — no protocol change; confirm nothing references Postgres specifically.
- [ ] **`CHANGELOG.md`** — new `[0.2.0]` section (see §14).

---

## 14. Milestone 8 — Version stamp, Docker, and final compliance sweep

- [ ] **Version bump 0.1.0 → 0.2.0** everywhere it is stamped: `build-all.bat`, `build-server.bat`, `build-dashboard.bat`, `dashboard/package.json`, `sdk/js/package.json`, all three SDK READMEs, `sdk/README.md`, `docker/compose.yaml`, `docker/factory/templates/compose.yaml`, `DOCKER.md`, `DOCKERHUB_README.md`, `README.md`, `REST_API.md`, `WEBSOCKETS_API.md`, `Clutch.postman_collection.json`, `src/Clutch.DataLoader/Catalogs.cs`, `src/Clutch.Server/Routes/ServerInfoRoutes.cs`, and any `.csproj` version metadata. Docker image tags become `jchristn77/clutch-server:v0.2.0` and `jchristn77/clutch-ui:v0.2.0`.
- [ ] **`CHANGELOG.md`** `[0.2.0]` entry: multi-provider support (SQLite/MySQL/PostgreSQL/SQL Server), configurable table names + schema/prefix with the new `clutch_{purpose}` defaults, `ManageSchema` opt-out and shipped DDL scripts, the coordination change (polling everywhere, `LISTEN/NOTIFY` removed), the test-connection endpoint, dashboard DB configuration UI, and the per-provider test matrix. Flag as a breaking change (alpha, no auto-migration) that the default table names moved from the v0.1.0 bare forms (`tenants`, `lock_holders`, …) to `clutch_`-prefixed names, and note the two ways to keep an existing database (override the names back, or rename via `sql/{provider}/schema.sql`). Written in the established Keep-a-Changelog voice.
- [ ] **Docker** — the reference `docker/compose.yaml` stays the multi-node topology it is today and remains the canonical example of Clutch clustered: **two server nodes (`clutch-node1`, `clutch-node2`) sharing one Postgres backend, with nginx fronting both nodes** (the existing `upstream clutch_backend { server clutch-node1:8080; server clutch-node2:8080; }` load balancer, WebSocket upgrade headers intact). Confirm both nodes still come up healthy against shared Postgres after the `LISTEN/NOTIFY` removal — this is exactly the cross-node case that now relies on polling (§1.2), so it is the deployment that proves polling coordination works end to end. Prometheus/Grafana/OTEL and the dashboard container stay as-is.
- [ ] **Per-provider examples follow the same two-node + nginx shape.** Add MySQL and SQL Server example stacks (compose files or documented overrides) that swap only the backend service and the nodes' `CLUTCH_DB_*` env — two nodes behind nginx against a shared MySQL or SQL Server, mirroring the Postgres reference. SQLite is the single-node exception (§1.4): its example runs exactly one node with a file volume and no nginx, and the docs say why.
- [ ] Add `docker/compose.test.yaml` (§10). Update `.gitignore`/`.dockerignore` for SQLite files and any new engine artifacts.
- [ ] **`sql/` directory** — `sql/postgresql/schema.sql`, `sql/sqlite/schema.sql`, `sql/mysql/schema.sql`, `sql/sqlserver/schema.sql`, each idempotent and matching the default catalog, committed and referenced from the docs.
- [ ] **Compliance sweep** — walk §2 top to bottom and check every box: code style clean build with no warnings across `net8.0` and `net10.0`; full test matrix green on all four providers in all three runners; docs re-read for voice; repository layout and Docker conventions intact.

---

## 15. Risks and open items

The load-bearing risk is acquire serialization on the two engines without `SELECT … FOR UPDATE`. SQL Server's `UPDLOCK/HOLDLOCK` key-range behavior on a not-yet-inserted definition row, and SQLite's `BEGIN IMMEDIATE` write-lock, both need to survive the randomized soak test before their milestones can be called done. If SQL Server range-locking proves flaky under contention, the fallback is a `sp_getapplock`-based mutex keyed on `tenantid|lockkey` — heavier, but a known-correct serialization primitive; note this as the contingency rather than discovering it late.

The second risk is subtle datatype round-tripping — timestamps and booleans especially. SQLite returning dates as strings, SQL Server `COUNT(*)` returning `int`, and UTC-kind handling across drivers are the kind of differences that pass a smoke test and fail the soak or the audit-chart summaries. The shared scalar-count coercion helper and a deliberate per-provider `Converters` review are the mitigations.

Everything above the driver — engine, routes, MCP, SDKs — is genuinely provider-agnostic already, so the blast radius is contained to `Clutch.Core/Database` plus the settings/dashboard surface. That is the reassuring part: the seam was designed for this, and removing `LISTEN/NOTIFY` collapses the one piece that was truly Postgres-shaped into a single polling path every engine can share.
