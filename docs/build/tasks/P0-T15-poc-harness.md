---
id: P0-T15
title: Author Gate-1 PoC harness (quarantined in /poc)
type: task-contract
schema_version: 1
epic: EPIC-0-4
phase: PHASE-0
status: complete
objective: Author runnable-but-uncredentialed PoC-1/2/3 harness scripts + commissioning guide in /poc, never referenced by /src.
blueprint_refs:
  - docs/blueprint/poc-gate1-specifications.md
  - docs/blueprint/stage-03-microsoft-validation.md#7
adr_refs: [ADR-012, PD-004, PD-005]
allowed_files: [poc/**]
forbidden_files: [docs/blueprint/**, docs/build/approvals/**, src/**]
migration_impact: none
acceptance_criteria:
  - PoC-1/2/3 harness scripts present in /poc, parameterised by tenant/app-registration config
  - /poc README with commissioning preconditions + result protocol
  - No credentials embedded; /poc not referenced by any /src (none exists yet; arch test will enforce)
evidence_required: [docs/build/evidence/EVIDENCE-P0-BOOTSTRAP-rails.md]
rollback: Delete /poc.
requires_human_approval: true
approved_by: Arun (Phase-0 plan approval, 2026-07-16)
---

## Objective

Provide the exact PoC harness so a human with a provisioned tenant can execute Gate-1 and append findings.

## Definition of done

Scripts + README present; execution deferred to P0-T16 (needs tenant).
