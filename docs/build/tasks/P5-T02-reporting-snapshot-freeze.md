---
id: P5-T02
title: Operationalize immutable reporting-period snapshots and restatement
type: task-contract
schema_version: 1
epic: EPIC-5-2
phase: PHASE-5
status: approved
objective: Complete the existing C3 ReportingPeriod and ReportSnapshot capability with tenant-scoped persistence, signed freeze, pinned input basis, immutable version history, restatement by new version, domain events, and C7 operator APIs.
blueprint_refs:
  - docs/blueprint/stage-04-domain-model.md#11.4
  - docs/blueprint/stage-05-conceptual-data-model.md#E13/E14
  - docs/blueprint/stage-08-security-trust-architecture.md#13
  - docs/blueprint/implementation-handoff-package.md#6
adr_refs: [ADR-015, ADR-016, ADR-019, ADR-020, ADR-025]
rtm_refs: [BR-03, BR-04]
allowed_files:
  - src/Modules/ControlTower.Modules.Economics/**
  - src/Host/ControlTower.Host.Web/**
  - tests/ControlTower.Modules.Economics.Tests/**
  - tests/ControlTower.Host.Web.Tests/**
  - docs/build/**
  - STATUS.md
forbidden_files: [docs/blueprint/**, docs/build/approvals/**]
preconditions:
  - C3 economics semantic model and read projections are complete
  - Event backbone and tenant context are complete
required_tests:
  - freeze persists immutable version 1 with payload and complete input basis
  - repeat freeze is rejected
  - restatement creates a new version that supersedes but never mutates version 1
  - events record signer reason version and snapshot identity
  - periods and snapshots are tenant-isolated
  - C7 APIs expose period lifecycle and immutable history
security_checks:
  - signer/operator is required
  - tenant context is mandatory
  - no cross-tenant snapshot enumeration
migration_impact: none
acceptance_criteria:
  - ReportingPeriod supports Open Closing Frozen and Restated lifecycle
  - ReportSnapshot carries version immutable output payload complete input basis signedBy and supersedes reference
  - Freeze and restatement append domain events to the hash-chained stream
  - Historical snapshot versions remain byte-for-byte unchanged after restatement
  - C7 APIs use the C3 service and return read models only
  - Backend build tests architecture formatting and dependency gates pass
  - No new bounded context reporting engine or blueprint change
evidence_required: [docs/build/evidence/EVIDENCE-P5-T02.md]
rollback: Revert the PR; the pre-existing disconnected period/snapshot domain stubs remain and economics observations/projections are unaffected.
requires_human_approval: false
approved_by: Product Owner autonomous implementation mandate, 2026-07-22
approved_hash: null
---

## Definition of done

Finance can create and close a period, freeze signed immutable output with a pinned reproducibility
basis, inspect every version, and restate by creating a new superseding snapshot. C3 remains the one
economics model; C7 exposes operator commands and read models only.
