---
id: P2-T01
title: Asset Ledger (C1) — AIAsset aggregate, state machines, ownership, aliases, events, read model, registration workflow
type: task-contract
schema_version: 1
epic: EPIC-2-1
phase: PHASE-2
status: complete
objective: Implement the C1 Asset Ledger within the existing Ledger module — the AIAsset aggregate, both state machines, ownership (incl. Ownerless/Lapsed), provider aliases + ResolutionLink foundations, match-confidence taxonomy, domain events, a tenant-scoped read model, and the registration workflow — with no new bounded context or aggregate beyond the blueprint.
blueprint_refs:
  - docs/blueprint/stage-04-domain-model.md#2
  - docs/blueprint/stage-04-domain-model.md#4
  - docs/blueprint/stage-04-domain-model.md#5
  - docs/blueprint/stage-02-capability-model.md#53
adr_refs: [ADR-012, ADR-015, ADR-018, ADR-020, ADR-021]
rtm_refs: [BR-01, BR-02]
allowed_files:
  - src/Modules/ControlTower.Modules.Ledger/**
  - src/Host/ControlTower.Host.Web/**
  - tests/ControlTower.Modules.Ledger.Tests/**
  - docs/build/**
  - STATUS.md
forbidden_files: [docs/blueprint/**, docs/build/approvals/**]
migration_impact: none
acceptance_criteria:
  - AIAsset aggregate (one aggregate for all types, Stage 4 §11.1)
  - RegistrationStatus + OperationalLifecycle state machines with guarded transitions
  - OwnershipAssignment temporal; Ownerless + Lapsed first-class; reassignment never overwrites history
  - Provider identifier aliases (NativeIdentifierSet) + ResolutionLink foundations + MatchConfidence roll-up
  - Domain events for all transitions; appended to the immutable event stream
  - TaxonomyScheme (C1) validates asset types; new types are values, not aggregates
  - Tenant-scoped repository + read model through ports (dev in-memory; PostgreSQL later)
  - Registration workflow service with an authorization seam
  - Unit + integration + tenancy + authorization tests green; architecture boundaries hold
  - No live Microsoft tenant; no new bounded context/aggregate; no blueprint change
evidence_required: [docs/build/evidence/EVIDENCE-P2-T01.md]
rollback: Revert the PR; the Ledger module falls back to its marker.
requires_human_approval: false # tenant-independent; emergent within C1; merge-train policy
approved_by: Arun (merge-train standing approval, 2026-07-17)
---

## Definition of done

C1 ledger implemented within the existing module; 31 tests green; CI green; Merge Readiness Report posted.
