# Running Clutch with Docker

> Alpha (v0.1.0).

The supported deployment runs Clutch as a small cluster: Postgres, two stateless server nodes behind an nginx load balancer, the dashboard, and Prometheus + Grafana for observability. Everything is in `docker/compose.yaml`.

## Topology

```
                    ┌────────────┐
      clients ─ws─▶ │            │──▶ clutch-node1 ─┐
   dashboard ─rest▶ │   nginx    │                  ├─▶ postgres
                    │  (8080 LB) │──▶ clutch-node2 ─┘
                    └────────────┘         │
                                           └─ /metrics:9464 ─▶ prometheus ─▶ grafana
```

Both nodes are stateless and share one Postgres, which is the sole authority for lock state. nginx uses `ip_hash` so a client's WebSocket stays pinned to one node; correctness does not depend on it.

## Ports

| Service | URL |
|---|---|
| API load balancer (REST + WebSocket) | http://localhost:8080 |
| Dashboard | http://localhost:3000 |
| Prometheus | http://localhost:9090 |
| Grafana (anonymous admin) | http://localhost:3001 |

## First run

`compose.yaml` references published image tags (`jchristn77/clutch-server:v0.1.0`, `jchristn77/clutch-ui:v0.1.0`). Since this is an unpublished alpha, build them locally first — either with the build script or the build override.

Build and run in one step (local build):

```bash
cd docker
docker compose -f compose.yaml -f compose.build.yaml up --build -d
```

Or build/tag/push explicitly, then run against the tags:

```bash
cd docker
./build.sh           # or: build.bat   (add --push to push to the registry)
docker compose up -d
```

Watch it come up:

```bash
docker compose ps
curl http://localhost:8080/v1.0/api/health
```

## Logging in

Open the dashboard at http://localhost:3000 and log in with the seeded default application key:

- Server URL: `http://localhost:8080`
- Access key: `clutch-default-access-key`

The access key is the sole credential — there is no secret key.

Change the default admin credentials and the `CLUTCH_AUTH_SIGNING_KEY` before exposing Clutch anywhere real.

## Configuration

Each node reads a mounted settings file (`docker/server/clutch.node1.json`, `clutch.node2.json`) and is further overridden by environment variables in the compose file (`CLUTCH_DB_HOST`, `CLUTCH_DB_PASSWORD`, `CLUTCH_NODE_ID`, `CLUTCH_AUTH_SIGNING_KEY`, …). On startup each node rewrites its settings file to pick up any newly added properties.

## Observability

Each node pushes OTLP metrics to an `otel-collector`, which re-exposes them in Prometheus format for Prometheus to scrape (this path is used because .NET's in-process Prometheus `HttpListener` exporter is not hostable on Linux). Grafana loads a provisioned **Clutch Overview** dashboard (acquires/denials by outcome, release rate, acquire-latency p95, HTTP request rate, active WebSocket connections, blocked waiters, process memory).

For a standalone (non-Docker) node, set `Telemetry.PrometheusEnable=true` to host `/metrics` directly on `PrometheusPort` (9464) instead of pushing OTLP.

## Factory reset

To wipe all local data and restore pristine settings:

```bash
cd docker
./factory/reset.sh     # or: factory\reset.bat  (type RESET to confirm)
```

This stops the stack, removes the Postgres/Prometheus/Grafana volumes, and restores the node settings from `docker/factory/templates/`.
