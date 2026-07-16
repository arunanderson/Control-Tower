# Gate-1 PoC harness — Correlation feasibility (QUARANTINED)

**This folder is quarantined.** Nothing here is product code; `/src` must never reference `/poc`
(an architecture test will enforce this once `/src` exists). PoCs are pre-build validation, run by a
human against a representative tenant. See `docs/blueprint/poc-gate1-specifications.md` (frozen spec).

## Commissioning preconditions (human — see spec §preconditions)

A representative M365 tenant with: ≥1 Agent 365 licence; several Copilot Studio agents incl. one
pre-March-2026 (legacy SP), one modern (Entra Agent ID), one Agent Builder, one published to M365/Teams;
a Foundry project with one published agent; PPAC admin + AI Administrator + Global Reader; an Entra app
registration with `CopilotPackages.Read.All`, PPAC RBAC Reader, `AgentIdentity.Read.All`-family, ARM Reader.

## Configuration (no secrets in git)

Copy `config.example.env` → `config.local.env` (gitignored) and fill it after provisioning.
Provide the bearer token via the environment; never commit tokens.

## Run

```
set -a; . ./config.local.env; set +a
bash poc1_appid_crosswalk.sh
bash poc2_manifest_id.sh
bash poc3_package_api.sh
```

## Result protocol (frozen spec)

Each PoC appends a findings note under `poc/findings/`, updates the Stage 3 matrix rows it touches
(via the PD-006 revision process — human), and adjusts ADR-012 confidence rules if needed.
**If PoC-1 fails for modern agents, escalate to Arun before Stage 5 finalisation** (scope conversation,
not silent workaround).

## Status

**Execution BLOCKED** — no tenant provisioned. See repo-root `BLOCKED.md` and task `P0-T16`.
