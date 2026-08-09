#!/usr/bin/env bash
# Build and push the Clutch server and dashboard images.
#   ./build.sh            build only
#   ./build.sh --push     build and push to the registry
set -euo pipefail

cd "$(dirname "$0")/.."

SERVER_IMAGE="jchristn77/clutch-server:v0.1.0"
UI_IMAGE="jchristn77/clutch-ui:v0.1.0"

echo "Building ${SERVER_IMAGE} ..."
docker build -f src/Clutch.Server/Dockerfile -t "${SERVER_IMAGE}" .

echo "Building ${UI_IMAGE} ..."
docker build -f dashboard/Dockerfile -t "${UI_IMAGE}" dashboard

if [ "${1:-}" = "--push" ]; then
  echo "Pushing images ..."
  docker push "${SERVER_IMAGE}"
  docker push "${UI_IMAGE}"
fi

echo "Done."
