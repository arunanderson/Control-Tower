---
id: P0-T16
title: Execute Gate-1 PoCs (PoC-1/2/3) against a representative tenant
type: task-contract
schema_version: 1
epic: EPIC-0-4
phase: PHASE-0
status: blocked
objective: Run the Gate-1 correlation PoCs and append findings + Stage 3 matrix updates + ADR-012 confidence rules.
blueprint_refs: [docs/blueprint/poc-gate1-specifications.md]
adr_refs: [ADR-012, PD-004]
allowed_files: [poc/**]
forbidden_files: [docs/blueprint/**, docs/build/approvals/**, src/**]
migration_impact: none
blocked_reason: >
  Requires a provisioned representative M365 tenant (Agent 365 licence + four agent archetypes) and
  Entra app registrations with consented scopes. Granting Microsoft tenant permissions/consent is a
  human gate the build agent may not perform. See BLOCKED.md.
awaiting: Arun — provision Gate-1 PoC tenant + app registrations + consent
acceptance_criteria:
  - PoC-1 appId cross-walk result recorded (escalate to Arun if it fails for modern agents)
  - PoC-2 manifest-ID recoverability result recorded
  - PoC-3 Package API app-only access + throttling result recorded
  - Findings appended to poc-gate1-specifications findings notes; Stage 3 matrix rows updated (via PD-006 revision, human)
evidence_required: [poc/findings/]
requires_human_approval: true
---

## Status
**BLOCKED** — see repo-root `BLOCKED.md`. Harness ready (P0-T15). Cannot proceed without tenant access.
