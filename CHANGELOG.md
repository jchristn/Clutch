# Changelog

All notable changes to Clutch are documented here. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project uses [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.1.0] - 2026-08-08

Initial alpha release. Everything is subject to change.

### Added
- Project plan and repository scaffolding.
- Distributed lock platform design: Postgres-authoritative lock engine (read/write/delete modes, MRSW + exclusive delete, first-acquirer policy), WebSocket lock protocol with session-bound ownership, TTL leases, heartbeats, and fencing tokens.
- Multi-tenant administration model (tenants, users, application keys) with system-admin / tenant-admin / regular-user tiers.
- Planned: REST API with OpenAPI, React dashboard, C#/JavaScript/Python SDKs, Prometheus/Grafana telemetry, and Docker deployment with two nodes behind nginx.

[0.1.0]: https://github.com/jchristn/Clutch/releases/tag/v0.1.0
