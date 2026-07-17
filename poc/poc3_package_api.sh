#!/usr/bin/env bash
# PoC-3 — Package API access reality. Frozen spec §PoC-3. Does the packages API work app-only with
# CopilotPackages.Read.All + an Agent 365 licence outside preview? Coverage + v1.0/beta + throttling.
set -euo pipefail
: "${GRAPH_TOKEN:?}"
OUT="poc/findings/poc3-run.md"; mkdir -p poc/findings
echo "# PoC-3 findings (fill on run)" > "$OUT"
for ver in v1.0 beta; do
  echo "## GET /$ver/copilot/admin/catalog/packages" >> "$OUT"
  curl -sS -D - -o /dev/null -H "Authorization: Bearer $GRAPH_TOKEN" \
    "https://graph.microsoft.com/$ver/copilot/admin/catalog/packages" >> "$OUT" 2>&1 || true
done
echo "TODO(human): record auth recipe (app-only vs delegated), licence prerequisite, coverage of the" >> "$OUT"
echo "  four agent types + registry-synced third parties, v1.0-vs-beta field diffs, and observed throttling." >> "$OUT"
echo "Wrote $OUT."
