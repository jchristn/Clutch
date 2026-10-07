#!/usr/bin/env bash
# Build and push the multi-platform Clutch Server image on Docker Build Cloud, then pull the
# pushed tags back into the local Docker image store.
set -euo pipefail

echo "============================================"
echo "Clutch Server Docker Build Script"
echo "============================================"
echo

if [ $# -lt 1 ] || [ -z "$1" ]; then
  echo "ERROR: Image tag is required"
  echo
  echo "Usage: build-server.sh <tag>"
  echo "Example: build-server.sh v0.3.0"
  exit 1
fi

IMAGE_NAME=jchristn77/clutch-server
IMAGE_TAG="$1"
DOCKERFILE_PATH=src/Clutch.Server/Dockerfile
BUILD_CONTEXT=.
PLATFORMS=linux/amd64,linux/arm64/v8
BUILDER=cloud-jchristn77-jchristn77
CLOUD_ENDPOINT=jchristn77/jchristn77

cd "$(dirname "$0")"

echo "Image: $IMAGE_NAME:$IMAGE_TAG"
echo "Platforms: $PLATFORMS"
echo

if ! docker --version >/dev/null 2>&1; then
  echo "ERROR: Docker is not installed or not in PATH"
  exit 1
fi

if ! docker buildx version >/dev/null 2>&1; then
  echo "ERROR: Docker buildx is not available"
  echo "Please ensure Docker Desktop is installed with buildx support"
  exit 1
fi

# Select the Docker Build Cloud builder (create the reference if missing).
echo "Using Docker Build Cloud builder $BUILDER..."
docker buildx use "$BUILDER" 2>/dev/null || docker buildx create --driver cloud "$CLOUD_ENDPOINT" --use

# Ensure builder is running.
docker buildx inspect --bootstrap

echo
echo "Building and pushing multi-platform image on the cloud builder..."
echo

docker buildx build \
  --builder "$BUILDER" \
  --platform "$PLATFORMS" \
  --tag "$IMAGE_NAME:$IMAGE_TAG" \
  --tag "$IMAGE_NAME:latest" \
  --file "$DOCKERFILE_PATH" \
  --push \
  "$BUILD_CONTEXT"

# The cloud builder only pushes to the registry; pull the freshly pushed tags back so
# they also exist in the local Docker image store. This does NOT re-invoke the cloud
# builder - it fetches from Docker Hub.
echo
echo "Pulling pushed image into the local registry..."
docker pull "$IMAGE_NAME:$IMAGE_TAG"
docker pull "$IMAGE_NAME:latest"

echo
echo "============================================"
echo "Build and push completed successfully!"
echo
echo "Images pushed:"
echo "  - $IMAGE_NAME:$IMAGE_TAG"
echo "  - $IMAGE_NAME:latest"
echo
echo "Platforms: $PLATFORMS"
echo "============================================"
