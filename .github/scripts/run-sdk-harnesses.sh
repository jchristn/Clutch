#!/usr/bin/env bash
# Starts a Clutch node (JIT build, SQLite) and runs the C#, JavaScript, and Python SDK test harnesses
# against it. Each harness exits non-zero on any failed check.
#
# Usage: run-sdk-harnesses.sh <framework>   (net8.0 or net10.0; run from the repository root after a Release build)
set -euo pipefail

framework="$1"
root="$(pwd)"
port=8090
endpoint="http://127.0.0.1:$port"
accessKey="clutch-default-access-key"
work="$(mktemp -d)"

# On Windows (Git Bash), hand native Windows paths to dotnet.
native() {
  if command -v cygpath > /dev/null 2>&1; then cygpath -w "$1"; else echo "$1"; fi
}

cleanup() {
  if [ -n "${server_pid:-}" ]; then kill "$server_pid" 2>/dev/null || true; fi
}
trap cleanup EXIT

echo "Starting Clutch.Server ($framework) on $endpoint"
(
  cd "$work"
  CLUTCH_DB_TYPE=Sqlite CLUTCH_DB_FILEPATH="$(native "$work/clutch.db")" CLUTCH_REST_PORT=$port CLUTCH_MCP_PORT=8091 \
    exec dotnet "$(native "$root/src/Clutch.Server/bin/Release/$framework/Clutch.Server.dll")" > "$work/server.log" 2>&1
) &
server_pid=$!

for attempt in $(seq 1 60); do
  if curl -sf "$endpoint/v1.0/api/health" > /dev/null; then break; fi
  if [ "$attempt" -eq 60 ]; then
    echo "::error::Clutch.Server did not become healthy"
    cat "$work/server.log"
    exit 1
  fi
  sleep 1
done

echo "== C# SDK"
dotnet "sdk/csharp/Clutch.Sdk.Test/bin/Release/$framework/Clutch.Sdk.Test.dll" "$endpoint" "$accessKey"

echo "== JavaScript SDK"
(cd sdk/js && npm ci --no-audit --no-fund && node test-harness.js "$endpoint" "$accessKey")

echo "== Python SDK"
python -m pip install --quiet -r sdk/python/requirements.txt
(cd sdk/python && python test_harness.py "$endpoint" "$accessKey")
