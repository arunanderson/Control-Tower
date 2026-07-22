---
id: P6-T04
title: Resolution & Merge Workbench (C7) — read-model APIs for aliases/links/merge-cases + operator actions
type: task-contract
schema_version: 1
epic: EPIC-6-1
phase: PHASE-6
status: complete
objective: Complete the Resolution & Merge Workbench within the existing Experience layer (C7). Expose the alias graph, ResolutionLinks, MergeCases, confidence labels and coverage through read-model-only APIs (I4), plus operator actions (merge/split/manual-link/resolve-case) routed to the C1 resolution service so every mutation is event-driven and fully auditable. No business logic in the UI; I3 preserved (Providers never references Ledger). Tenant-independent.
blueprint_refs:
  - docs/blueprint/stage-06-experience-architecture.md
  - docs/blueprint/stage-07-conceptual-architecture.md#2
  - docs/blueprint/stage-04-domain-model.md#2
adr_refs: [ADR-019, ADR-020, ADR-012, ADR-015]
rtm_refs: [BR-04, BR-01]
allowed_files:
  - src/Modules/ControlTower.Modules.Ledger/**
  - src/Host/ControlTower.Host.Web/**
  - tests/ControlTower.Host.Web.Tests/**
  - web/**
  - docs/build/**
  - STATUS.md
forbidden_files: [docs/blueprint/**, docs/build/approvals/**]
migration_impact: none
acceptance_criteria:
  - Read-model-only APIs (I4) for open merge cases and per-asset resolution (alias graph + link history + confidence labels)
  - Operator actions (merge/split/manual-link/resolve-case) routed to EntityResolutionService — event-driven, auditable; DomainException → 400
  - Coverage already exposed at /trust/coverage; surfaced in the workbench UI
  - React/TS ResolutionWorkbench in the existing five-area SPA (Trust); read models only; no business logic in the UI
  - Merge/split leave ProviderObservations untouched; links severed/superseded, never deleted (proven upstream + shown as link status)
  - Tenant-isolated; I3 preserved (Providers ⊥ Ledger)
  - Backend integration tests + SPA tests green; build 0/0; 0 vulnerable production packages
  - No new bounded context/aggregate; no blueprint change
evidence_required: [docs/build/evidence/EVIDENCE-P6-T04.md]
rollback: Revert the PR; the workbench read model, endpoints and SPA view are removed; resolution engine and ledger are unaffected.
requires_human_approval: false # tenant-independent; emergent within C7; merge-train policy
approved_by: Arun (Resolution Workbench + continue tenant-independent capabilities, 2026-07-22)
---

## Definition of done

Read-model-only workbench APIs (merge cases, per-asset alias graph + link history) + operator actions
(merge/split/manual-link/resolve-case) that emit audit events; a Trust-area SPA workbench that reads
models and invokes the actions with no business logic. 116 backend + 8 SPA tests green; 0 vulnerable
production deps. Merge Readiness Report + Completion Matrix posted.
