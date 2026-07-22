---
type: blocked
schema_version: 1
task: P0-T16
blocked_at: 2026-07-22
severity: high
awaiting: Arun — complete Wave 0 catalogue checks, then Gate-1 tenant prerequisites and consent
---

## Summary

Gate-1 PoC **execution** (P0-T16) is blocked on Microsoft tenant actions. Cursor is currently at Wave
0 Step 0.3: a human must inspect the Entra application-permission catalogue without selecting a
permission, granting consent or creating credentials. Full execution later requires the representative
tenant prerequisites and consent that only a human can provide.

## What I was doing

Completing Phase 0. The PoC **harness** (P0-T15) is authored and committed under `/poc` (quarantined).
The database-engine decision (DEC-001) is recorded. The bootstrap rails + CI are built and in the PR.

## The blocker

The permission-catalogue result is not yet recorded. The frozen spec (`poc-gate1-specifications.md`)
also requires an Agent 365 licence, four agent archetypes, a Foundry project, and app registrations
with `CopilotPackages.Read.All` / PPAC RBAC Reader / an `AgentIdentity.Read.All`-family permission /
ARM Reader, plus interactive admin consent. None can be assumed or granted autonomously.

## What I need to proceed

Arun to: (1) complete Wave 0 Step 0.3 and report the observed permission names or “none found”;
(2) when the readiness plan permits, provision/point to the representative tenant prerequisites and
approve the required registration/consent actions; (3) supply tokens to the human operator who runs
the quarantined PoCs and appends findings.

## What I did NOT do (to avoid guessing)

- Did not fabricate PoC results or a confidence table.
- Did not request or store any tenant credentials.
- Did not finalise Stage 5 (it is gated on real PoC-1/2/3 results; PoC-1 failure requires escalation
  before finalisation) — Stage 5 finalisation is a human-led PD-006 revision regardless.

## Parallel human gate — retention jurisdiction authority

P5-T04 retention enforcement must not accept jurisdiction floors and ceilings from the same tenant
command that chooses a retention duration. The safe mechanism is an authoritative, versioned
jurisdiction-policy provider owned through the Legal/Privacy governance process; tenant configuration
may choose a duration only within those externally governed bounds. The engine can fail closed behind
that port, but production policy values cannot be invented or activated by an implementation agent.
