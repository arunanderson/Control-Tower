---
id: P6-T02
title: C4 observation ingestion — the "one door in" (observe → privacy-mark → delta-suppress → append → emit)
type: task-contract
schema_version: 1
epic: EPIC-6-1
phase: PHASE-6
status: complete
objective: Build the C4 invariant ingestion segment (ADR-009/020, the one door in) that turns provider RawObservations into immutable, append-only, pre-resolution ProviderObservations — contract-validate → privacy-mark (Gate 1, L1 default) → delta-suppress (watermark) → append the observation → emit ObservationIngested to the hash-chained stream and stage it on the outbox. Provable end-to-end with the manual CSV provider, tenant-independent, no Microsoft API or tenant resource. Stops exactly at the ObservationIngested event boundary; the C1 resolution segment (PoC-gated confidence rule table) is a later train.
blueprint_refs:
  - docs/blueprint/stage-07-conceptual-architecture.md
  - docs/blueprint/stage-04-domain-model.md#2
  - docs/blueprint/stage-05-conceptual-data-model.md
adr_refs: [ADR-009, ADR-020, ADR-015, ADR-014, ADR-007, ADR-013]
rtm_refs: [BR-05]
allowed_files:
  - src/Modules/ControlTower.Modules.Providers/**
  - tests/ControlTower.Modules.Providers.Tests/**
  - docs/build/**
  - STATUS.md
forbidden_files: [docs/blueprint/**, docs/build/approvals/**]
migration_impact: none
acceptance_criteria:
  - Immutable ProviderObservation (Stage 5 E2) — append-only, privacy-marked (set once), delta-classified; carries native identifiers, kind, evidence label, content hash
  - IObservationStore port (append-only; no update/delete) + dev-only in-memory substitute; IngestionRun log (Stage 5 E3)
  - ObservationIngestionService runs the invariant pipeline for any provider — contract-validate, privacy Gate 1 (L1), delta-suppress via watermark, append, emit ObservationIngested to IEventStore + IOutbox
  - Delta suppression proven — identical re-sweep fully suppressed; a changed attribute recorded as Changed
  - ObservationIngested is a self-contained serialization contract (no shared types cross the module boundary); nothing reads/writes the ledger (I3)
  - Tenant-scoped (ADR-021); cross-tenant writes rejected; ingestion requires a tenant scope
  - Build 0/0; full suite green; 0 vulnerable production packages
  - No Microsoft/Graph/Entra/PPAC code; no resolution rule table (PoC-gated); no new bounded context; no blueprint change
evidence_required: [docs/build/evidence/EVIDENCE-P6-T02.md]
rollback: Revert the PR; the observation store, ingestion service, and events are removed from the Providers module; no other module is affected.
requires_human_approval: false # tenant-independent; emergent within C4; merge-train policy
approved_by: Arun (continue tenant-independent work while Microsoft provisioning is externally blocked, 2026-07-22)
---

## Definition of done

The C4 ingestion door turns CSV provider output into immutable, privacy-marked, delta-suppressed
observations and emits ObservationIngested on the hash-chained stream + outbox — 100 backend tests
green, 0 vulnerable production deps. Stops at the event boundary; C1 resolution (PoC-gated) follows.
Merge Readiness Report posted.
