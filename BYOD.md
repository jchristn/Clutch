# Bring Your Own Database

Clutch does not ship its own storage engine. It runs on a database you already operate — PostgreSQL, MySQL, SQL Server, or SQLite — and treats that database as the single source of truth for every lock decision. Nothing about a grant lives in server memory: an acquire and its matching release each run as one transaction, serialized per lock key, so two nodes pointed at the same database can never hand out conflicting locks. That design is what makes "bring your own database" safe rather than merely convenient. You supply the durability and the operational muscle you already know how to run; Clutch supplies the locking semantics on top.

This guide covers how to point Clutch at your database: where the settings live, every knob you can turn, a worked example per engine, the sharp edges worth knowing before you go to production, and how the four supported platforms compare. The last section sketches where the storage layer could go next.

## Where configuration lives

A Clutch node resolves its database configuration from three places, in increasing order of precedence:

1. **The settings file** (`clutch.json`, or the mounted `clutch.node1.json` / `clutch.node2.json` in the reference Docker stack). This is the canonical, version-controllable source. The `Database` block holds everything below.
2. **The dashboard.** Server Settings → Database edits the same block through a form, validates it, and can test the connection before you save. Database changes flag a restart as required; the existing restart flow applies them.
3. **Environment variables.** A handful of `CLUTCH_DB_*` overrides win over the file at startup, which is how the containerized deployments inject per-node values without templating a whole file.

On boot each node rewrites its settings file to include any newly added properties at their defaults, so upgrading Clutch will populate new fields for you rather than failing on their absence.

## The `Database` block

Every field, its default, and where it applies:

| Field | Type | Default | Applies to | Notes |
|---|---|---|---|---|
| `Type` | enum | `Postgresql` | all | One of `Postgresql`, `Mysql`, `SqlServer`, `Sqlite`. |
| `Host` | string | `localhost` | networked | Hostname or IP. Ignored by SQLite. |
| `Port` | int (1–65535) | `5432` | networked | Set it per engine — MySQL is usually `3306`, SQL Server `1433`. Ignored by SQLite. |
| `DatabaseName` | string | `clutch` | networked | The database/catalog Clutch connects to. It must already exist. Ignored by SQLite. |
| `Username` | string | `postgres` | networked | Login user. Ignored by SQLite. |
| `Password` | string | `postgres` | networked | Login password. Ignored by SQLite. |
| `FilePath` | string | `clutch.db` | SQLite | Path to the database file. Only used when `Type` is `Sqlite`. |
| `Schema` | string \| null | `null` | PostgreSQL, SQL Server | Schema/namespace that qualifies every table. MySQL and SQLite ignore it. Null means the provider default (`public` on PostgreSQL, `dbo` on SQL Server). |
| `ManageSchema` | bool | `true` | all | Whether Clutch may create and migrate its tables. See below. |
| `Tables` | object | all defaults | all | Per-purpose table-name overrides plus an optional `Prefix`. See [Table naming](#table-naming-schema-and-prefix). |
| `AdditionalOptions` | string \| null | `null` | all | Appended verbatim to the built connection string for knobs Clutch does not model. |
| `MaxPoolSize` | int (1–1024) | `100` | networked | Maximum pooled connections. Ignored by SQLite. |

Clutch builds the provider-appropriate connection string from these fields, so you set `Host`/`Port`/`DatabaseName` rather than a raw connection string. When you do need something Clutch does not expose — a specific SSL mode, an application name, a command timeout — put it in `AdditionalOptions` and it is appended as-is. A few provider defaults are baked in: SQL Server connects with `Encrypt=False;TrustServerCertificate=True` (override via `AdditionalOptions` for a properly certified server), MySQL forces `DateTimeKind=Utc`, and SQLite disables foreign-key enforcement.

## A worked example per engine

### PostgreSQL

The default and the reference clustering backend.

```json
"Database": {
  "Type": "Postgresql",
  "Host": "db.internal",
  "Port": 5432,
  "DatabaseName": "clutch",
  "Username": "clutch_app",
  "Password": "…",
  "Schema": null,
  "ManageSchema": true,
  "MaxPoolSize": 100
}
```

### MySQL

Set the port to 3306 and give the login a user that can create tables (or pre-create them, below). MySQL treats the database and the schema as the same thing, so leave `Schema` null and put the target in `DatabaseName`.

```json
"Database": {
  "Type": "Mysql",
  "Host": "mysql.internal",
  "Port": 3306,
  "DatabaseName": "clutch",
  "Username": "clutch_app",
  "Password": "…",
  "ManageSchema": true
}
```

### SQL Server

Port 1433, and `Schema` is honored (defaults to `dbo`). The connection trusts the server certificate by default; for a server with a real certificate, add `"AdditionalOptions": "Encrypt=True"`.

```json
"Database": {
  "Type": "SqlServer",
  "Host": "mssql.internal",
  "Port": 1433,
  "DatabaseName": "clutch",
  "Schema": "dbo",
  "Username": "clutch_app",
  "Password": "…",
  "ManageSchema": true
}
```

### SQLite

Just a file path. Host, port, user, and password are ignored. SQLite is single-node only (see the gotchas), so this is for development, tests, and single-server or embedded deployments.

```json
"Database": {
  "Type": "Sqlite",
  "FilePath": "/var/lib/clutch/clutch.db",
  "ManageSchema": true
}
```

## Table naming, schema, and prefix

Clutch owns the column layout of its tables — narrow, fixed-width rows are what keep the lock hot path resident in the buffer cache — but it does not insist on owning the table *names*. It uses nine tables, each with a `clutch_`-prefixed default so they are self-identifying and unlikely to collide with tables already in a database you own:

| Purpose key | Default name | On the lock hot path? |
|---|---|---|
| `schemaMigrations` | `clutch_schema_migrations` | no (migration tracking) |
| `tenants` | `clutch_tenants` | no |
| `users` | `clutch_users` | no |
| `credentials` | `clutch_credentials` | no |
| `authSessions` | `clutch_auth_sessions` | no |
| `lockDefinitions` | `clutch_lock_definitions` | **yes** |
| `lockHolders` | `clutch_lock_holders` | **yes** |
| `lockAudit` | `clutch_lock_audit` | no |
| `requestHistory` | `clutch_request_history` | no |

You can reshape the names three ways, and they stack:

- **Per-purpose override.** Set any of the nine names under `Tables` to replace just that one; the other eight keep their defaults. A blank or omitted value means "use the default."
- **Global prefix.** `Tables.Prefix` is prepended to every resolved name, so you can namespace all nine without restating them — `"Prefix": "svc_"` yields `svc_clutch_tenants` and so on.
- **Schema.** `Schema` qualifies every table on PostgreSQL and SQL Server.

The resolved reference for each table is `schema.(prefix + name)`, quoted for the provider. Example:

```json
"Database": {
  "Type": "SqlServer",
  "Schema": "locking",
  "Tables": {
    "Prefix": "app_",
    "LockHolders": "holders",
    "LockDefinitions": "definitions"
  }
}
```

resolves the two hot-path tables to `[locking].[app_holders]` and `[locking].[app_definitions]`, and the remaining seven to `[locking].[app_clutch_tenants]`, etc.

**Every name, the prefix, and the schema are validated against `^[A-Za-z_][A-Za-z0-9_]*$` when the driver starts.** Anything else — a hyphen, a dot, a space, a quote — is rejected with a clear error. This allowlist is deliberately strict because these identifiers are composed into SQL text; it is the sole defense against injection through a configured name, so there is no way to relax it. Keep names to letters, digits, and underscores, starting with a letter or underscore.

## Letting Clutch create the tables — or not

`ManageSchema` decides who owns the DDL.

**`ManageSchema: true` (default).** On startup Clutch runs its tracked, idempotent migrations against the target database and creates any missing tables and indexes under the configured names, using `CREATE TABLE IF NOT EXISTS` semantics (and the provider equivalents where that syntax does not exist). This is the easy path: grant the login table-creation rights, point Clutch at an empty database, and it sets itself up.

**`ManageSchema: false` (least privilege).** Clutch issues no DDL at all. Instead it verifies at startup that every required table already exists and refuses to start — with a specific, provider-named error — if one is missing. The migration-tracking table is not required in this mode; the eight data tables are. Use this when your DBA, not the application login, owns schema changes.

For that hand-off, Clutch ships a reviewable, idempotent DDL script per provider under `sql/{provider}/schema.sql` (`postgresql`, `mysql`, `sqlserver`, `sqlite`). The scripts are generated from the same schema builder the runtime uses and reflect the `clutch_` default names, so a DBA can create the tables by hand, or you can adapt them if you have overridden names or a prefix. Re-running a script is safe.

## Test before you trust

Before saving a configuration you can validate it. The dashboard's **Test connection** button — and the `POST /v1.0/api/settings/database/test` endpoint behind it (system-admin only) — builds a driver from the posted settings, opens a connection, and, when `ManageSchema` is false, also checks that the tables are present. It returns `{ ok, message, provider }` and never persists anything. A blank or redacted (`***`) password reuses the running one, so you can test a host or port change without re-entering the secret. This is the fastest way to catch a wrong port, a missing database, a firewall rule, or an absent table before a node tries to boot against it.

## Environment overrides for containers

Nine variables override the file at startup, which is how the reference stack gives two otherwise-identical nodes their shared-database coordinates:

| Variable | Overrides |
|---|---|
| `CLUTCH_DB_TYPE` | `Type` (`Postgresql` / `Mysql` / `SqlServer` / `Sqlite`) |
| `CLUTCH_DB_HOST` | `Host` |
| `CLUTCH_DB_PORT` | `Port` |
| `CLUTCH_DB_DATABASE` | `DatabaseName` |
| `CLUTCH_DB_USERNAME` | `Username` |
| `CLUTCH_DB_PASSWORD` | `Password` |
| `CLUTCH_DB_FILEPATH` | `FilePath` (SQLite) |
| `CLUTCH_DB_SCHEMA` | `Schema` |
| `CLUTCH_DB_MANAGE_SCHEMA` | `ManageSchema` |

Table names and the prefix have no environment override by design — they are structural, not per-environment, so they belong in the settings file or the dashboard.

## Gotchas worth knowing

- **SQLite is single-node, full stop.** One file cannot safely back multiple server nodes writing concurrently, and Clutch will not pretend otherwise. It logs a warning at startup whenever SQLite is selected. Run exactly one node against a SQLite file; for a cluster, choose one of the networked engines.
- **The database must already exist.** Clutch creates *tables*, not the database itself. Create the `clutch` database (or whatever you named it) first, then let Clutch populate it. On SQL Server in particular, the container image does not create it for you.
- **How the per-key lock is taken differs by engine, and it matters under contention.** PostgreSQL and MySQL serialize acquirers with `SELECT … FOR UPDATE` (MySQL leaning on the gap lock from the unique key); SQL Server uses an `UPDLOCK, ROWLOCK, HOLDLOCK` key-range lock; SQLite takes the database write lock with an `IMMEDIATE` transaction. All four are correct. MySQL and SQL Server can raise a transient deadlock or serialization failure under heavy contention, which Clutch retries automatically rather than surfacing to the caller — so a busy cluster is fine, but do not be alarmed to see those retries in engine metrics.
- **Cross-node wakeup is polling, not push.** Clutch removed the PostgreSQL `LISTEN/NOTIFY` path so that one coordination model runs on all four engines. A blocked waiter re-runs its acquire on a bounded interval (`WaiterPollMs`, default one second, shortened to fit the caller's remaining timeout). Correctness is unaffected — the transaction is still the only authority — but a cross-node waiter's *wakeup latency* is bounded by that interval. Same-node waiters are signaled immediately in-process. Lower `WaiterPollMs` if you need tighter cross-node latency at the cost of more idle queries.
- **Everything stored is UTC.** Timestamps are written and read as UTC across every provider. SQLite stores them as ISO-8601 text; the others use their native timestamp types. If you query the tables directly, treat the times as UTC.
- **Collation on SQL Server and MySQL.** Case-insensitive lookups (the `ILIKE`-equivalent searches) rely on a case-insensitive collation. Clutch pins one where it matters, but if you pre-create tables from the shipped script into a database with an unusual default collation, keep the collation case-insensitive so search behaves as expected.
- **No auto-migration from v0.1.0's bare names.** The default table names moved from the old bare forms (`tenants`, `lock_holders`, …) to `clutch_`-prefixed names. A fresh deploy simply uses the new defaults. To keep an existing v0.1.0 database, either override the nine names back to the bare forms, or rename the tables using `sql/{provider}/schema.sql` as the reference. Because Clutch is alpha, there is no automatic rename.
- **Connection pool sizing.** `MaxPoolSize` defaults to 100 per node. Multiply by node count against your database's connection ceiling — several nodes at 100 each can exhaust a small PostgreSQL `max_connections` or a modest SQL Server. Size the pool and the server together.

## Choosing a platform

| | PostgreSQL | MySQL | SQL Server | SQLite |
|---|---|---|---|---|
| **Multi-node cluster** | yes | yes | yes | no (single node) |
| **Best when** | you want the reference path and the widest operational track record | MySQL/MariaDB is your house standard | you are a SQL Server shop or need AD auth on the DB tier | dev, tests, embedded, or a single server |
| **Serialization primitive** | `FOR UPDATE` row lock | `FOR UPDATE` gap lock | `UPDLOCK/HOLDLOCK` range lock | database write lock (`IMMEDIATE`) |
| **Contention behavior** | clean row-level locking | occasional deadlock retries | occasional deadlock retries | coarse but correct; one writer at a time |
| **Schema/namespace** | `Schema` (`public`) | database == schema | `Schema` (`dbo`) | n/a |
| **Operational weight** | light–moderate | light–moderate | moderate–heavy | negligible |

Guidance in a sentence each: **PostgreSQL** is the default for a reason — it is the reference clustering backend, the widest-tested path, and the one the example Docker topology runs. **MySQL** is an equal-footing choice if it is already what you operate; its gap-lock serialization is proven, just expect the odd automatic retry under load. **SQL Server** fits enterprises standardized on it, with the caveat that it is the heaviest to run and the most dialect-divergent engine under the hood (so it gets the most scrutiny in the test matrix). **SQLite** is the right answer precisely when you do not need a cluster — a laptop, a CI run, an appliance, a single box — and the wrong answer the moment you need a second node.

A practical rule: pick the database your team already runs well. Clutch's whole premise is that the lock service should not force a new datastore into your stack. The one hard constraint is the single-node limit on SQLite; everything else is a preference between engines you can operate confidently.

## Roadmap: persistence platforms beyond the four

The four relational engines cover most of the field, but the driver seam was built to be provider-neutral — the lock engine, routes, MCP server, and sweeper only ever see `DatabaseDriverBase` and the eight `I*Methods` contracts. Anything that can serialize writers on a single key and store a handful of narrow rows can, in principle, back Clutch. Two broad directions are worth pursuing.

**Tier 1 — reuse the existing SQL seam (low effort, high certainty).** These speak a dialect Clutch already models, so they slot into the `SqlDialect` / `TableCatalog` machinery with little more than a dialect entry and a connection string:

- **CockroachDB** and **Google Cloud Spanner (PostgreSQL interface)** — distributed SQL over the PostgreSQL wire protocol. They would give Clutch a horizontally-scalable, geo-distributed backend while reusing most of the PostgreSQL driver, with attention paid to their serializable-isolation retry semantics (which Clutch's automatic retry already anticipates).
- **MariaDB** — MySQL-compatible; effectively free to support and worth naming explicitly.
- **Amazon Aurora (PostgreSQL/MySQL editions)** — wire-compatible with engines already supported; largely a documentation-and-testing exercise.
- **Oracle Database** — an enterprise SQL target with `SELECT … FOR UPDATE`, for shops standardized on it. More dialect work than the others, but conceptually the same shape.

**Tier 2 — a new driver over a non-relational primitive (more work, new capabilities).** These need their own driver implementing the eight contracts, but each brings something the relational engines do not:

- **Redis / Valkey / KeyDB** — the hot-path speed play. Atomic Lua scripts (or a single-instance `SET NX` / compare-and-set) provide per-key serialization in memory, with the definition and holder rows kept as hashes. The trade-off is durability and consistency semantics: single-instance is simple and fast; multi-node correctness pushes you toward a Redlock-style protocol whose guarantees are debated, so this suits latency-sensitive deployments that accept Redis's durability model.
- **Amazon DynamoDB** — arguably the cleanest cloud-native fit. Conditional writes give exactly the compare-and-set needed for an acquire, TTL attributes model lease expiry natively, and strongly-consistent reads are available. Serverless, multi-node by default, no connection pool to size.
- **etcd** (and the coordination family — **Consul**, **ZooKeeper**) — purpose-built for distributed coordination. Native leases map almost one-to-one onto Clutch's lease model, compare-and-swap on a key gives serialization, and **watches could reintroduce push-based cross-node wakeup** as a per-provider optimization over today's polling baseline — bringing back the low-latency wakeup that removing `LISTEN/NOTIFY` traded away, but this time on a store designed for it. The constraint is capacity: these stores hold modest key counts and would suit deployments with bounded lock cardinality.
- **MongoDB** — `findAndModify` gives atomic conditional updates, TTL indexes handle expiry, and multi-document transactions cover the cascade operations. A natural fit for teams already running Mongo.
- **FoundationDB** — ordered, strictly-serializable key-value transactions; a strong technical match for the lock model if the operational commitment is acceptable.
- **Azure Cosmos DB** — ETag-based optimistic concurrency and TTL, for Azure-centric stacks wanting multi-region writes.

If and when a coordination-native backend like etcd lands, the most interesting follow-on is optional push-based wakeup: keep polling as the universal floor, and let providers that support watches signal cross-node waiters immediately. That would let Clutch offer near-zero cross-node latency where the backend allows it, without giving up the single coordination model everywhere else.

For the full engineering history of how the abstraction was generalized from Postgres-only to four providers, see [`archive/BYOD_PLAN.md`](archive/BYOD_PLAN.md).
