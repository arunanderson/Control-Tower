---
id: P6-T05
title: Project honest provider coverage and freshness from ingestion runs
type: task-contract
schema_version: 1
epic: EPIC-6-1
phase: PHASE-6
status: complete
objective: Replace the static C1.6 coverage placeholder with an event-driven, tenant-scoped projection of actual C4 ingestion-run facts, including surface state, last successful sweep, freshness, covered capabilities, counts, and an honest empty state.
blueprint_refs:
  - docs/blueprint/stage-02-capability-model.md#C1.6
  - docs/blueprint/stage-04-domain-model.md#2.2
  - docs/blueprint/stage-06-experience-architecture.md#8
  - docs/blueprint/stage-07-conceptual-architecture.md#2
  - docs/blueprint/implementation-handoff-package.md#6
adr_refs: [ADR-012, ADR-015, ADR-019, ADR-020]
rtm_refs: [BR-01, BR-04]
allowed_files:
  - src/ControlTower.Platform/**
  - src/Modules/ControlTower.Modules.Providers/**
  - src/Modules/ControlTower.Modules.Ledger/**
  - src/Host/ControlTower.Host.Worker/**
  - tests/ControlTower.Modules.Providers.Tests/**
  - tests/ControlTower.Modules.Ledger.Tests/**
  - tests/ControlTower.Host.Web.Tests/**
  - web/**
  - docs/build/**
  - STATUS.md
forbidden_files: [docs/blueprint/**, docs/build/approvals/**]
preconditions:
  - P6-T02 observation ingestion is complete
  - P6-T03 host-composed integration-event delivery is complete
required_tests:
  - provider ingestion emits one self-contained coverage fact per completed run
  - projection is tenant-isolated and replay-idempotent
  - freshness is derived from provider expectation and last successful sweep
  - degraded and empty states remain explicit
  - Trust API and SPA render the projected facts without business logic
security_checks:
  - no cross-tenant reads or writes
  - Providers and Ledger retain no project reference to each other
  - C7 reads only the coverage projection
migration_impact: none
acceptance_criteria:
  - Every completed ingestion run emits a self-contained provider coverage fact through the event backbone and outbox
  - C1.6 projects per-surface state, covered capabilities, last successful sweep, freshness, run counts and an honest note
  - Failed or never-run surfaces are not presented as covered or fresh
  - Replay does not duplicate or regress newer coverage facts
  - Existing Trust API and SPA display the projection with a truthful no-coverage state
  - Tenant isolation, I3, I4 and module boundaries remain green
  - Backend and SPA tests, builds, formatting and production dependency checks pass
  - No new bounded context, aggregate, provider-specific rule, or blueprint change
evidence_required: [docs/build/evidence/EVIDENCE-P6-T05.md]
rollback: Revert the PR; the prior static coverage placeholder returns and the ingestion/resolution pipeline is otherwise unchanged.
requires_human_approval: false
approved_by: Product Owner autonomous implementation mandate, 2026-07-22
approved_hash: null
---

## Definition of done

Actual C4 ingestion outcomes drive the existing C1.6/C7 Trust coverage view through host-composed,
idempotent event delivery. Coverage and freshness remain honest under empty, stale, degraded, replay,
and multi-tenant cases; no provider SDK or Microsoft assumption enters the domain.
