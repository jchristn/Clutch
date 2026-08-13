<p align="center">
  <img src="https://raw.githubusercontent.com/jchristn/Clutch/main/assets/logo.png" alt="Clutch" width="128" height="128" />
</p>

# Clutch

> ⚠️ **Alpha (v0.2.0).** Everything — APIs, WebSocket protocol, schema, settings, SDKs, and behavior — is subject to change. Not yet recommended for production.

Clutch is a distributed lock management platform. Client applications connect over WebSockets with a tenant application key and acquire **read**, **write**, or **delete** locks on named keys. Clutch decides — safely and consistently across every node in the fleet — whether a caller may proceed. You bring the database: Clutch runs on **PostgreSQL, MySQL, SQL Server, or SQLite**, and the database you choose is the single source of truth for every lock decision, so two nodes can never hand out conflicting locks.

## Images

- `jchristn77/clutch-server:v0.2.0` — the server node (REST + WebSocket on 8080, Prometheus metrics on 9464).
- `jchristn77/clutch-ui:v0.2.0` — the React operator dashboard.

## Use cases

- Coordinate mutating vs. non-mutating access to a shared resource across many worker nodes.
- Serialize writers while allowing concurrent readers, with per-key policies set by the first acquirer.
- Fence off stale holders with a monotonic token so a slow client can't corrupt state after its lease lapses.

## Architecture

Stateless server nodes sit behind an nginx load balancer and share one database — PostgreSQL, MySQL, or SQL Server for a multi-node cluster, or SQLite for a single node. Each acquire/release is a single database transaction serialized per key; blocked waiters on any node are woken by bounded polling, so one coordination path runs identically on every provider. A lock is owned by the WebSocket connection that holds it and is released when the connection closes; a TTL lease renewed by heartbeat backstops half-open connections.

## Quick start

The reference stack (PostgreSQL, two nodes, nginx, dashboard, Prometheus, Grafana) is defined in the repository's `docker/compose.yaml`:

```bash
git clone https://github.com/jchristn/Clutch
cd Clutch/docker
docker compose up -d
```

`compose.yaml` pulls the published `jchristn77/clutch-server:v0.2.0` and `jchristn77/clutch-ui:v0.2.0` tags directly — no local build step.

Then open the dashboard at `http://localhost:3000` and connect to `http://localhost:8080` with the seeded default access key (`clutch-default-access-key`). The access key is the sole credential — there is no secret key.

## Documentation

To point Clutch at your own database — provider choice, connection details, table naming, and whether Clutch manages the schema — see the [Bring Your Own Database guide](https://github.com/jchristn/Clutch/blob/main/BYOD.md). Full documentation, the REST and WebSocket API references, and SDKs for C#, JavaScript, and Python are in the [GitHub repository](https://github.com/jchristn/Clutch).

## License

MIT.
