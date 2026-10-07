#!/usr/bin/env bash
# Fails when a Native AOT publish log contains trimming or AOT warnings outside the allow-list.
#
# Usage: check-aot-warnings.sh <publish-log>
#
# Clutch, Voltaic, and Watson are trimming and AOT clean, and their projects treat those warnings as build
# errors. Microsoft.Data.SqlClient (with its logging assembly and System.Configuration.ConfigurationManager,
# which it pulls in) still reports one summary warning per assembly (IL2104 trim, IL3053 AOT) that Clutch
# cannot fix; those assemblies are allowed. Any other assembly with a summary warning, or any detailed
# trimming/AOT warning, fails the check.
set -euo pipefail

log="$1"
if [ ! -f "$log" ]; then
  echo "::error::Publish log not found: $log"
  exit 1
fi
allowed='^(Microsoft\.Data\.SqlClient|Microsoft\.Data\.SqlClient\.Internal\.Logging|System\.Configuration\.ConfigurationManager)$'
status=0

detailed=$(grep -E "warning IL[0-9]{4}" "$log" | grep -vE "warning IL(2104|3053):" | sed -E 's/ \[[^]]*\]$//' | sort -u || true)
if [ -n "$detailed" ]; then
  echo "::error::Trimming or AOT warnings in the publish output:"
  echo "$detailed"
  status=1
fi

assemblies=$(grep -oE "warning IL(2104|3053): Assembly '[^']+'" "$log" | sed -E "s/.*Assembly '([^']+)'/\1/" | sort -u || true)
for assembly in $assemblies; do
  if [[ "$assembly" =~ $allowed ]]; then
    echo "allowed: $assembly"
  else
    echo "::error::Assembly '$assembly' produced trimming or AOT warnings"
    status=1
  fi
done

if [ $status -eq 0 ]; then
  echo "AOT warning check passed."
fi
exit $status
