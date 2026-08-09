<p align="center">
  <img src="https://raw.githubusercontent.com/jchristn/Clutch/main/assets/logo.png" alt="Clutch" width="128" height="128" />
</p>

# Clutch

> ⚠️ **Alpha (v0.1.0).** Everything — APIs, WebSocket protocol, schema, settings, SDKs, and behavior — is subject to change. Not yet recommended for production.

Clutch is a distributed lock management platform. Client applications connect over WebSockets with a tenant application key and acquire **read**, **write**, or **delete** locks on named keys. Clutch decides — safely and consistently across every node in the fleet — whether a caller may proceed. Postgres is the single source of truth for every lock decision, so two nodes can never hand out conflicting locks.

## Images

- `jchristn77/clutch-server:v0.1.0` — the server node (REST + WebSocket on 8080, Prometheus metrics on 9464).
- `jchristn77/clutch-ui:v0.1.0` — the React operator dashboard.

## Use cases

- Coordinate mutating vs. non-mutating access to a shared resource across many worker nodes.
- Serialize writers while allowing concurrent readers, with per-key policies set by the first acquirer.
- Fence off stale holders with a monotonic token so a slow client can't corrupt state after its lease lapses.

## Architecture

Stateless server nodes sit behind an nginx load balancer and share one Postgres. Each acquire/release is a single Postgres transaction serialized per key; blocked waiters on any node are woken via `LISTEN/NOTIFY`. A lock is owned by the WebSocket connection that holds it and is released when the connection closes; a TTL lease renewed by heartbeat backstops half-open connections.

## Quick start

The full stack (Postgres, two nodes, nginx, dashboard, Prometheus, Grafana) is defined in the repository's `docker/compose.yaml`:

```bash
git clone https://github.com/jchristn/Clutch
cd Clutch/docker
docker compose -f compose.yaml -f compose.build.yaml up --build -d
```

Then open the dashboard at `http://localhost:3000` and connect to `http://localhost:8080` with the seeded default access key (`clutch-default-access-key`). The access key is the sole credential — there is no secret key.

## Documentation

Full documentation, the REST and WebSocket API references, and SDKs for C#, JavaScript, and Python are in the [GitHub repository](https://github.com/jchristn/Clutch).

## License

MIT.
