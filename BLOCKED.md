---
type: blocked
schema_version: 1
task: P0-T16
blocked_at: 2026-07-16
severity: high
awaiting: Arun — provision Gate-1 PoC tenant + Entra app registrations + consent
---

## Summary

Gate-1 PoC **execution** (P0-T16) is blocked: it requires a representative Microsoft 365 tenant and
consented Entra app registrations that only a human can provision. Granting Microsoft tenant
permissions/consent is a human gate the build agent may not perform.

## What I was doing

Completing Phase 0. The PoC **harness** (P0-T15) is authored and committed under `/poc` (quarantined).
The database-engine decision (DEC-001) is recorded. The bootstrap rails + CI are built and in the PR.

## The blocker

No representative tenant is available. The frozen spec (`poc-gate1-specifications.md`) requires an
Agent 365 licence, four agent archetypes, a Foundry project, and app registrations with
`CopilotPackages.Read.All` / PPAC RBAC Reader / `AgentIdentity.Read.All`-family / ARM Reader —
plus interactive admin consent. None can be created autonomously.

## What I need to proceed

Arun to: (1) provision/point to a representative M365 tenant meeting the spec preconditions;
(2) create the Entra app registration + grant consent; (3) supply tokens to the human operator who
runs `poc/poc1_appid_crosswalk.sh`, `poc2_manifest_id.sh`, `poc3_package_api.sh` and appends findings.

## What I did NOT do (to avoid guessing)

- Did not fabricate PoC results or a confidence table.
- Did not request or store any tenant credentials.
- Did not finalise Stage 5 (it is gated on real PoC-1/2/3 results; PoC-1 failure requires escalation
  before finalisation) — Stage 5 finalisation is a human-led PD-006 revision regardless.
