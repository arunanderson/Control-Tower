---
id: P5-T03
title: Add tenant-scoped legal hold and retention precedence
type: task-contract
schema_version: 1
epic: EPIC-5-2
phase: PHASE-5
status: approved
objective: Complete the existing C9 audit and evidence capability with tenant-scoped, reason-bound, authorised legal holds that are auditable, released only with approval evidence, and expose a retention-protection decision for later enforcement.
blueprint_refs:
  - docs/blueprint/stage-08-security-trust-architecture.md#13
  - docs/blueprint/stage-08-security-trust-architecture.md#15
  - docs/blueprint/implementation-handoff-package.md#6
adr_refs: [ADR-015, ADR-017, ADR-019, ADR-020, ADR-021]
rtm_refs: [BR-15]
allowed_files:
  - src/Modules/ControlTower.Modules.Audit/**
  - src/Host/ControlTower.Host.Web/**
  - tests/ControlTower.Host.Web.Tests/**
  - docs/build/**
  - STATUS.md
forbidden_files: [docs/blueprint/**, docs/build/approvals/**]
preconditions:
  - C9 immutable event backbone and customer-visible Trust log are complete
  - C7 tenant and operator command seams are complete
required_tests:
  - creating a hold requires reason scope and authorised operator
  - active holds protect matching retention subjects and not unrelated subjects
  - release requires reason and approval reference and never deletes the hold
  - placement and release append complete events to the tenant hash chain
  - holds and retention decisions are tenant-isolated
  - C7 exposes active and released hold history through C9 read models
security_checks:
  - tenant context is mandatory
  - no cross-tenant hold lookup or release
  - release approval evidence is mandatory
migration_impact: none
acceptance_criteria:
  - Legal holds are tenant-scoped reason-bound time-stamped authorised and audited
  - Hold scopes cover the blueprint retention data classes and optional resource references
  - Active matching holds take precedence over retention deletion decisions
  - Release is append-only state progression with actor reason time and approval reference
  - C7 commands use the C9 service and reads return C9 read models only
  - Backend build tests architecture formatting and dependency gates pass
  - No new bounded context retention engine or blueprint change
evidence_required: [docs/build/evidence/EVIDENCE-P5-T03.md]
rollback: Revert the PR; no retention deletion engine exists yet, so existing data remains unaffected.
requires_human_approval: false
approved_by: Product Owner autonomous implementation mandate, 2026-07-22
approved_hash: null
---

## Definition of done

An authorised tenant operator can place and inspect a scoped legal hold. Releasing it requires an
approval reference and records rather than erases the transition. A retention caller can ask C9
whether a subject is protected, making legal-hold precedence enforceable before deletion exists.
