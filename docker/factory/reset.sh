#!/usr/bin/env bash
# Reset the local Clutch stack to factory state: stop everything, drop the data
# volumes (Postgres, Prometheus, Grafana), and restore pristine settings.
set -euo pipefail

cd "$(dirname "$0")/.."

echo "This will STOP the Clutch stack and DELETE all local data (Postgres, Prometheus, Grafana)."
read -r -p "Type 'RESET' to confirm: " CONFIRM
if [ "$CONFIRM" != "RESET" ]; then
  echo "Aborted."
  exit 1
fi

echo "Stopping stack and removing volumes..."
docker compose down -v || true

echo "Restoring pristine settings from factory/templates ..."
cp factory/templates/clutch.node1.json server/clutch.node1.json
cp factory/templates/clutch.node2.json server/clutch.node2.json

echo "Factory reset complete. Start again with: docker compose up -d"
