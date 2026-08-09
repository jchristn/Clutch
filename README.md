# Clutch

> ⚠️ **Alpha (v0.1.0).** This is an early alpha release. Everything — the REST API, the WebSocket protocol, the database schema, settings, SDK surfaces, and runtime behavior — is subject to change without notice. It is not yet recommended for production use.

Clutch is a distributed lock management platform for coordinating access to shared resources across many nodes. A client acquires a **read** (non-mutating), **write** (mutating), or **delete** lock on a named key, and Clutch decides — safely, and consistently across every node in the fleet — whether that client may proceed.

The design goal is safety over cleverness. Postgres is the single source of truth for every lock decision; each acquire and release runs as an atomic transaction serialized per key, so two nodes can never hand out conflicting locks. Server memory holds only observational and bookkeeping state (who is waiting, which locks a connection holds, a cache for the dashboard) — never the grant decision itself.

## What it does

- **Three lock modes.** Read locks are shared, write locks are exclusive among writers, and delete locks are fully exclusive and drain everything. The first client to acquire a given key fixes its policy — how many concurrent readers are allowed, whether a write blocks reads, and the lease lifespan — and every later client is held to it.
- **Two acquisition behaviors.** Fail fast when the lock is unavailable, or wait for it up to a caller-supplied timeout. Waiters on any node are woken the moment a lock frees, via Postgres `LISTEN/NOTIFY`, with a polling fallback so a missed notification never hangs a caller.
- **Safe ownership.** A lock is owned by the WebSocket session that holds it; closing the socket releases it. Every hold also carries a TTL lease renewed by heartbeat, so a crashed or half-open client's locks expire on their own. Each grant returns a monotonic fencing token so a downstream resource can reject a stale holder.
- **Multi-tenant administration.** Tenants isolate lock namespaces, users, and application keys. Users are system administrators, tenant administrators, or regular members. A React dashboard shows live locks, audit history, and a lock-activity chart; a REST API and Postman collection cover the same surface.

## Architecture

Clients connect over WebSockets with a tenant application key and drive locks through a persistent connection. Administration and observability run over a versioned REST API with OpenAPI metadata. Multiple stateless server nodes sit behind an nginx load balancer and share one Postgres database; scaling out is a matter of adding nodes. Telemetry follows the [Radiant](https://github.com/jchristn) OpenTelemetry model, exposing metrics for both the web transport and the lock data path, scraped by Prometheus and rendered in preconfigured Grafana dashboards.

```
clients ──ws──▶ nginx ──▶ [ node1 | node2 ] ──▶ Postgres (authority + LISTEN/NOTIFY)
dashboard ─rest─▶            │                        ▲
                            └── /metrics ─▶ Prometheus ─▶ Grafana
```

## Getting started

The supported deployment is Docker: two server nodes, nginx, Postgres, Prometheus, and Grafana.

```bash
cd docker
docker compose up -d
```

See [`CLUTCH_PLAN.md`](CLUTCH_PLAN.md) for the full build-out plan, [`REST_API.md`](REST_API.md) and [`WEBSOCKETS_API.md`](WEBSOCKETS_API.md) for the protocols, and the `sdk/` directory for C#, JavaScript, and Python clients.

## Scope notes

Clutch deliberately narrows two things from the reference architecture it is built against:

- **Postgres only.** The database abstraction is provider-neutral, but only the Postgres provider is implemented — cross-node coordination relies on `LISTEN/NOTIFY`.
- **No RBAC.** Authorization is the three-tier model — system admin, tenant admin, regular user — without roles and permissions.

## License

MIT. See [`LICENSE.md`](LICENSE.md).
