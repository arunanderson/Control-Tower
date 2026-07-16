#!/usr/bin/env bash
# PoC-1 — appId cross-walk (the load-bearing join). Frozen spec: poc-gate1-specifications.md §PoC-1.
# Question: does copilotPackage.appId == PPAC entraAppId, and does PPAC entraAgentId resolve to a
# Graph agentIdentity, giving PPAC bot GUID <-> Entra identity <-> M365 package for a published agent?
set -euo pipefail
: "${GRAPH_TOKEN:?set via config.local.env}"; : "${PPAC_TOKEN:?}"; : "${TEST_BOT_ID:?}"
OUT="poc/findings/poc1-$(printf 'run').md"   # timestamp added by the human on completion
mkdir -p poc/findings

echo "# PoC-1 findings (fill on run)" > "$OUT"
echo "## Graph package management — catalog packages" >> "$OUT"
curl -sS -H "Authorization: Bearer $GRAPH_TOKEN" \
  "https://graph.microsoft.com/v1.0/copilot/admin/catalog/packages" >> "$OUT" || true
echo "## PPAC inventory record for TEST_BOT_ID=$TEST_BOT_ID (fill endpoint per tenant)" >> "$OUT"
# NOTE: PPAC inventory endpoint is delegated-auth + tenant-specific; the human completes the call.
echo "TODO(human): record entraAppId / entraAgentId and attempt the joins per archetype." >> "$OUT"
echo "Wrote $OUT — complete the join analysis and the per-archetype confidence table."
