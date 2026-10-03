#!/usr/bin/env bash
# Build and push the Clutch server and dashboard images with the given tag and "latest".
set -euo pipefail

if [ $# -lt 1 ] || [ -z "$1" ]; then
  echo "Usage: build-all.sh <docker-image-tag>"
  echo "Example: build-all.sh v0.2.0"
  exit 1
fi

IMAGE_TAG="$1"
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"

"$SCRIPT_DIR/build-server.sh" "$IMAGE_TAG"
"$SCRIPT_DIR/build-dashboard.sh" "$IMAGE_TAG"

echo
echo "============================================"
echo "Clutch Docker build-all completed successfully!"
echo
echo "Components built and pushed:"
echo "  - Clutch Server: jchristn77/clutch-server:$IMAGE_TAG"
echo "  - Clutch Server: jchristn77/clutch-server:latest"
echo "  - Clutch Dashboard: jchristn77/clutch-ui:$IMAGE_TAG"
echo "  - Clutch Dashboard: jchristn77/clutch-ui:latest"
echo "============================================"
