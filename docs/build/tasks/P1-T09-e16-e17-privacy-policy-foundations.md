---
id: P1-T09
title: Establish E16 jurisdiction and E17 telemetry-policy foundations
type: task-contract
schema_version: 1
epic: EPIC-1-2
phase: PHASE-1
status: approved
objective: Implement the frozen tenant-scoped E16 JurisdictionProfile and E17 TelemetryPolicy semantics, ports, canonical events and development in-memory adapters needed before either privacy gate can be connected.
blueprint_refs:
  - docs/blueprint/stage-04-domain-model.md#27-orgmodel--jurisdictionprofile-c5
  - docs/blueprint/stage-04-domain-model.md#28-tenantconfiguration-c8--including-telemetrypolicy
  - docs/blueprint/stage-05-conceptual-data-model.md#1-entity-catalogue-20-entities
  - docs/blueprint/stage-05-conceptual-data-model.md#e17-telemetrypolicy--bitemporal-privileged
  - docs/blueprint/stage-08-security-trust-architecture.md#6-authorization-model
  - docs/blueprint/stage-08-security-trust-architecture.md#9-privileged-zone
  - docs/blueprint/stage-08-security-trust-architecture.md#12-privacy-enforcement-architecture-adr-0030140159-operationalised
adr_refs: [ADR-003, ADR-014, ADR-015, ADR-020, ADR-021, ADR-023]
rtm_refs: [BR-09, BR-10, BR-15]
allowed_files:
  - ControlTower.sln
  - src/ControlTower.Platform/Privacy/**
  - src/Modules/ControlTower.Modules.EnterpriseContext/Privacy/**
  - src/Modules/ControlTower.Modules.EnterpriseContext/Infrastructure/InMemoryJurisdictionProfileStore.cs
  - src/Modules/ControlTower.Modules.Trust/Privacy/**
  - src/Modules/ControlTower.Modules.Trust/Infrastructure/InMemoryTelemetryPolicyStore.cs
  - src/Modules/ControlTower.Modules.Providers/Domain/ObservationPrimitives.cs
  - src/Modules/ControlTower.Modules.Providers/Domain/ProviderObservation.cs
  - src/Modules/ControlTower.Modules.Providers/Application/ObservationIngestionService.cs
  - tests/ControlTower.Modules.Providers.Tests/ObservationIngestionTests.cs
  - tests/ControlTower.Modules.EnterpriseContext.Tests/**
  - tests/ControlTower.Modules.Trust.Tests/**
  - tests/ControlTower.ArchitectureTests/DomainEventContractTests.cs
  - tests/ControlTower.ArchitectureTests/PrivacyBoundaryTests.cs
  - docs/build/tasks/P1-T09-e16-e17-privacy-policy-foundations.md
  - docs/build/evidence/EVIDENCE-P1-T09.md
  - docs/build/state/build-state.yaml
  - docs/build/state/dev-substitute-registry.md
  - STATUS.md
forbidden_files:
  - docs/blueprint/**
  - docs/build/approvals/**
  - src/ControlTower.Platform/**
  - src/Adapters/**
  - src/Host/**
  - src/Modules/ControlTower.Modules.Audit/**
  - src/Modules/ControlTower.Modules.Economics/**
  - src/Modules/ControlTower.Modules.Experience/**
  - src/Modules/ControlTower.Modules.Governance/**
  - src/Modules/ControlTower.Modules.Ledger/**
  - src/Modules/ControlTower.Modules.Providers/**
  - db/**
  - infra/**
  - web/**
  - tests/ControlTower.Adapters.PostgreSql.Tests/**
  - tests/ControlTower.Host.Web.Tests/**
  - tests/ControlTower.Modules.Economics.Tests/**
  - tests/ControlTower.Modules.Governance.Tests/**
  - tests/ControlTower.Modules.Ledger.Tests/**
  - tests/ControlTower.Modules.Providers.Tests/**
  - tests/ControlTower.Platform.Tests/**
preconditions:
  - P1-T08 is merged to main and its tenant-independent CI and disposable PostgreSQL evidence are green
  - The frozen blueprint already assigns E16 to C5 and E17 to C8 and requires most-restrictive jurisdiction-aware policy resolution
  - The Product Owner instructed the build agent on 2026-07-24 to identify validate automatically approve and implement the highest-priority approved backlog item after P1-T08
  - The exact allowed Platform Privacy paths are the sole exceptions to the broad Platform forbidden glob
  - The exact four allowed Provider and Provider-test paths are the sole exceptions to their broad forbidden globs and may change only to reuse the shared PrivacyMarking without behavior change
  - This slice accepts opaque applicable jurisdiction and population references but does not invent or connect production population-to-jurisdiction source data HR mapping or real taxonomies
  - No production jurisdiction taxonomy policy value legal interpretation works-council decision tenant activation or cloud action is authorised
required_tests:
  - E16 revisions are immutable tenant-scoped versioned effective-dated and queryable exactly and as-of without foreign-tenant disclosure
  - E16 rejects invalid identity time actor regime and version tuples and concurrent stale writes return bounded conflict outcomes
  - E16 resolution uses every applicable jurisdiction and fails closed to L1 when no authoritative ceiling exists
  - E17 revisions preserve valid time and record time history and exact as-of policy selection by both valid-time and recorded-time cutoffs
  - E17 concurrent or stale writes preserve one authoritative history and return bounded conflict outcomes
  - E17 defaults each unmatched capability to enabled L1 and resolves overlapping applicable rules as disabled when any rule disables it or otherwise at the minimum applicable telemetry level
  - E17 rejects any rule above the effective E16 ceiling instead of accepting and flagging it
  - E17 requires a bounded justification and valid opaque actor for every change and explicit activation purpose approval and retention references for enabled L2 or higher rules
  - E17 requires L4 rules to be explicitly time-limited
  - E16 and E17 reject forged tenant identity version actor timestamp justification reason aggregate or correlation event tuples before append or mutation
  - E16 and E17 state and their canonical events are atomic in the development adapters and append failure leaves history unchanged
  - missing malformed switched or foreign tenant context fails before state mutation event append or cross-tenant enumeration
  - the promoted shared PrivacyMarking remains the single L1 to L4 type and existing C4 ingestion behavior remains L1 by default
  - all full-solution architecture build format dependency secret task-contract protected-path and development-substitute gates remain green
security_checks:
  - C5 remains the sole authority for jurisdiction ceilings and C8 remains the sole authority for telemetry policy
  - E17 consumes only the shared C5 ceiling-resolution port and neither module references another module
  - all public tenant operations capture one ambient TenantId before reading validating appending or mutating
  - ambiguous or absent jurisdiction applicability fails closed and never widens beyond L1
  - policy changes carry required justification actor exact version valid time record time exact event metadata and privileged event classification
  - no raw employee identity jurisdiction taxonomy production policy value or customer activation is introduced
  - no Gate 1 Gate 2 Host API UI provider collector persistence migration cloud resource credential or new bounded context is introduced
  - no new package or infrastructure dependency is introduced
migration_impact: none
acceptance_criteria:
  - Platform owns the one shared PrivacyMarking and opaque jurisdiction population capability and policy-reference primitives required across existing contexts
  - C5 exposes a tenant-scoped E16 history and ceiling-resolution port with an atomic development adapter and canonical standard JurisdictionProfileChanged event
  - C8 exposes a tenant-scoped bitemporal E17 policy history and effective-resolution port with an atomic development adapter and canonical privileged TelemetryPolicyChanged event
  - E17 validation applies the E16 ceiling disable-wins then minimum-level resolution L1 default explicit L2-plus activation and time-limited L4 invariants from the frozen blueprint
  - C4 continues to assign immutable L1 markings by default using the shared type with no ingestion behavior change
  - architecture tests prevent a duplicate privacy-level type cross-module dependencies and incorrect event privilege classification
  - no shared staging production Microsoft tenant or cloud resource is accessed or changed
evidence_required: [docs/build/evidence/EVIDENCE-P1-T09.md]
rollback: Revert the task commit or PR; this slice changes only code contracts and development in-memory state and creates no persistent or external state.
requires_human_approval: true
approved_by: Product Owner explicit automatic-approval instruction on 2026-07-24
approved_hash: "sha256:e3476cbd14dc7c100f573d405f01c3dafce7a7e7353568caa208a6ea52a42a6a"
---

## Objective

Establish the frozen privacy-policy authority before connecting enforcement. C5 owns effective,
versioned jurisdiction ceilings. C8 owns effective-dated, bitemporal telemetry policy and validates
each rule against C5. Both expose tenant-scoped ports, deterministic most-restrictive resolution and
canonical audit events. Existing C4 code reuses the shared privacy marking without changing its L1
default.

This task adds no privacy gate, persistence, application composition, product surface, policy
taxonomy or production configuration.

## Steps (bounded, ordered)

1. Promote the existing L1–L4 `PrivacyMarking` into the Platform shared kernel and add only opaque
   cross-context privacy references plus the C5 ceiling-resolution port. Its input remains an
   opaque set of already-applicable jurisdiction and population references.
2. Implement E16 immutable versioned/effective history, validation, deterministic
   most-restrictive ceiling resolution, canonical event and tenant-partitioned in-memory store in
   C5 without introducing a population source or HR mapping.
3. Implement E17 bitemporal policy revisions, per-capability and jurisdiction/population rules,
   frozen activation invariants, E16 ceiling validation, canonical privileged event and
   tenant-partitioned in-memory store in C8.
4. Add focused domain, concurrency, hostile tenant-isolation, event-atomicity and permanent
   architecture tests.
5. Run every local CI-equivalent gate, perform independent review, capture evidence, reconcile
   build state and submit the tenant-independent PR.

## Definition of done

C5 and C8 own their frozen E16/E17 semantics without depending on each other or duplicating privacy
types. Missing or conflicting applicability fails closed; any disabling rule wins before the
minimum permitted level is selected; policy above the jurisdiction ceiling is rejected; L1 remains
default; L2+ requires explicit bounded activation evidence; L4 is temporary; history and canonical
events are exact and tenant-isolated. No Gate 1/2 consumer, population source, persistence,
production policy or external state is added.

## Rollback

Revert the task commit or PR. The task creates no migration, durable data, production configuration
or external state.
