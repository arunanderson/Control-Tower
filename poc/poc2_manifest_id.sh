#!/usr/bin/env bash
# PoC-2 — manifest ID hole. Frozen spec §PoC-2. Is the M365 manifest ID recoverable from Dataverse
# botcomponent rows for the published channel, closing the bot-GUID<->manifestId hole without appId?
set -euo pipefail
: "${GRAPH_TOKEN:?}"; : "${DATAVERSE_ENV_URL:?}"; : "${TEST_BOT_ID:?}"
OUT="poc/findings/poc2-run.md"; mkdir -p poc/findings
echo "# PoC-2 findings (fill on run)" > "$OUT"
# Dataverse Web API needs a Dataverse-scoped token; the human supplies it.
echo "TODO(human): sweep bot/botcomponent rows for TEST_BOT_ID at $DATAVERSE_ENV_URL/api/data/v9.2" >> "$OUT"
echo "  and search component payloads for the manifestId seen in copilotPackage.manifestId." >> "$OUT"
echo "Outcome: documented retrieval path (redundant join) OR confirmed dead end." >> "$OUT"
echo "Wrote $OUT."
