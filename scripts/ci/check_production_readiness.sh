#!/usr/bin/env bash
# DEV-001 enforcement: no development-only substitute may leak into a production path.
# Scans production config + IaC for dev-substitute markers. Passes when those paths are absent
# or clean (as in the bootstrap PR, before any /infra exists).
set -euo pipefail

# Production paths (created in later phases). Dev-only markers that must never appear here.
PROD_GLOBS=("infra" "src/**/appsettings.Production.json" "src/**/appsettings.json")
DEV_MARKERS='localhost|127\.0\.0\.1|azurite|supabase\.co|host\.docker\.internal|DEV-ONLY|dev-substitute'

found=0
for g in "${PROD_GLOBS[@]}"; do
  # shellcheck disable=SC2086
  for path in $g; do
    [ -e "$path" ] || continue
    hits=$(grep -RInE "$DEV_MARKERS" "$path" 2>/dev/null || true)
    if [ -n "$hits" ]; then
      echo "PRODUCTION-READINESS VIOLATION in $path:"
      printf '%s\n' "$hits"
      found=1
    fi
  done
done

if [ "$found" -ne 0 ]; then
  echo "A dev-only substitute reference reached a production path (DEV-001). Fix before merge."
  exit 1
fi
echo "OK: no dev-substitute references in production paths (DEV-001)."
