---
id: P6-T03
title: C1 entity resolution — consume ObservationIngested, alias graph, deterministic match, links/MergeCase, merge/split
type: task-contract
schema_version: 1
epic: EPIC-6-1
phase: PHASE-6
status: complete
objective: Implement the tenant-independent C1 entity-resolution pipeline (ADR-012) that consumes the C4 ObservationIngested event through host-composed delivery, builds provider-scoped identity aliases, performs deterministic identifier matching, links observations to AIAssets via ResolutionLinks (creating a new asset only on no match), opens a MergeCase on collision/ambiguity, applies the MatchConfidence taxonomy (High auto-links; Medium per the current approved rule; Low never auto-links → manual merge queue; Manual = operator decision), rolls asset confidence up lowest-confidence-wins across material links, supports merge/split without modifying ProviderObservations, severs/supersedes links (never deletes), and keeps complete immutable audit events. Providers never references Ledger. Microsoft-specific aliases and confidence rules stay provisional and PoC-gated. Proven with the CSV provider and deterministic fixtures — no Microsoft tenant.
blueprint_refs:
  - docs/blueprint/stage-04-domain-model.md#2
  - docs/blueprint/stage-05-conceptual-data-model.md
  - docs/blueprint/stage-07-conceptual-architecture.md
adr_refs: [ADR-012, ADR-015, ADR-020, ADR-024, ADR-025]
rtm_refs: [BR-01, BR-05]
allowed_files:
  - src/Modules/ControlTower.Modules.Ledger/**
  - src/Modules/ControlTower.Modules.Providers/**
  - src/ControlTower.Platform/**
  - src/Host/ControlTower.Host.Worker/**
  - tests/ControlTower.Modules.Ledger.Tests/**
  - docs/build/**
  - STATUS.md
forbidden_files: [docs/blueprint/**, docs/build/approvals/**]
migration_impact: none
acceptance_criteria:
  - Consume ObservationIngested via host-composed delivery (Platform IIntegrationEventHandler + worker dispatcher); Providers never references Ledger
  - Provider-scoped identity aliases (Stage 5 E5) built from observations; reverse alias lookup for candidates
  - Deterministic identifier matching (exact native-id equality → DocumentedJoin/High); no-match creates a new asset
  - Collision (identifier maps to >1 asset) or ambiguity opens a MergeCase (Stage 5 E8); never auto-links
  - MatchConfidence taxonomy applied — High auto-links; sub-High never auto-links (manual queue); Manual = operator-approved
  - Lowest-confidence-wins roll-up across active (material) links
  - Merge/split supported without modifying ProviderObservations; links severed/superseded, never deleted or rewritten
  - Complete immutable audit events for link/sever/supersede/merge/split/confidence/merge-case
  - Tenant-isolated and idempotent (replay neither double-links nor duplicates cases)
  - Microsoft aliases + confidence rules provisional/PoC-gated; deterministic classifier is provider-agnostic (no Microsoft assumptions)
  - Full pipeline proven with the CSV provider + fixtures; build 0/0; suite green; 0 vulnerable production packages
  - No provider-specific logic in Ledger; no new bounded context or aggregate; no blueprint change
evidence_required: [docs/build/evidence/EVIDENCE-P6-T03.md]
rollback: Revert the PR; resolution service/handler/MergeCase and the dispatcher routing are removed; C4 ingestion and the rest of the ledger are unaffected.
requires_human_approval: false # tenant-independent mechanism; emergent within C1/C4; merge-train policy. Microsoft rule table stays PoC-gated.
approved_by: Arun (Priority 6 C1 resolution train, 2026-07-22)
---

## Definition of done

The C4→C1 pipeline resolves CSV observations into ledger assets end-to-end (host-composed delivery,
Providers ⊥ Ledger); deterministic match / no-match / collision / sub-High-review all proven;
lowest-wins roll-up; merge/split with links severed-not-deleted and full audit; tenant-isolated +
idempotent; Microsoft rules provisional/PoC-gated. 111 backend tests green; 0 vulnerable production
deps. Merge Readiness Report posted.
