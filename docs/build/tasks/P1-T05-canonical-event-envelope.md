---
id: P1-T05
title: Harden the canonical event envelope and integrity verifier
type: task-contract
schema_version: 1
epic: EPIC-1-2
phase: PHASE-1
status: complete
objective: Version and harden the existing event backbone's storage-integrity frame before P1-T06 completes the E20 audit metadata and any permanent PostgreSQL event record is authored.
blueprint_refs:
  - docs/blueprint/stage-04-domain-model.md#5-domain-events-the-canonical-set
  - docs/blueprint/stage-05-conceptual-data-model.md#e20-domaineventrecord--privilegedreadrecord
  - docs/blueprint/stage-07-conceptual-architecture.md#7-trust-and-security-boundaries
  - docs/blueprint/stage-07-conceptual-architecture.md#9-failure-handling-philosophy--resiliency-principles
  - docs/blueprint/stage-07-conceptual-architecture.md#10-simplifications-chosen-challenge-applied
  - docs/blueprint/stage-08-security-trust-architecture.md#13-evidence-integrity
  - docs/blueprint/stage-08-security-trust-architecture.md#14-security-event-model--administrative-audit
  - docs/blueprint/stage-09-technology-deployment.md#3-data-platform-decisions
adr_refs: [ADR-015, ADR-020, ADR-021, ADR-023]
rtm_refs: [BR-15]
allowed_files:
  - src/ControlTower.Platform/Events/**
  - src/ControlTower.Platform/DependencyInjection/**
  - src/Adapters/ControlTower.Adapters.InMemory/InMemoryEventStore.cs
  - src/Modules/ControlTower.Modules.Audit/PrivilegedAccess.cs
  - src/Modules/ControlTower.Modules.Audit/LegalHolds.cs
  - src/Modules/ControlTower.Modules.Trust/Authorization/RoleAssignments.cs
  - src/Modules/ControlTower.Modules.Ledger/Domain/LedgerEvents.cs
  - src/Modules/ControlTower.Modules.Governance/Domain/GovernanceEvents.cs
  - src/Modules/ControlTower.Modules.Economics/Domain/EconomicsEvents.cs
  - src/Modules/ControlTower.Modules.Providers/Domain/ProviderEvents.cs
  - tests/ControlTower.Platform.Tests/**
  - tests/ControlTower.ArchitectureTests/**
  - docs/build/**
  - STATUS.md
forbidden_files:
  - docs/blueprint/**
  - docs/build/approvals/**
  - src/ControlTower.Platform/Tenancy/**
  - src/ControlTower.Platform/Audit/**
  - src/Modules/ControlTower.Modules.Trust/Infrastructure/**
  - src/Modules/ControlTower.Modules.Ledger/Application/**
  - src/Modules/ControlTower.Modules.Ledger/Infrastructure/**
  - src/Modules/ControlTower.Modules.Governance/Application/**
  - src/Modules/ControlTower.Modules.Governance/Infrastructure/**
  - src/Modules/ControlTower.Modules.Economics/Application/**
  - src/Modules/ControlTower.Modules.Economics/Infrastructure/**
  - src/Modules/ControlTower.Modules.Providers/Application/**
  - src/Modules/ControlTower.Modules.Providers/Infrastructure/**
  - src/Host/**
  - web/**
  - db/**
  - infra/**
  - .github/**
preconditions:
  - P0-T12 and P1-T04 are merged and main is green
  - No durable event store or production event data exists, so the integrity format can be corrected without migration or compatibility loss
  - P1-T05 is a provisional integrity format for the current skeleton and does not claim a complete E20 audit record
  - P1-T06 will add aggregateRef opaque actor reason and correlationRef issue the completed integrity format and close E19 privacy semantics before the first permanent event migration
  - Product Owner approval is required because this task changes the event-backbone integrity contract
required_tests:
  - canonical envelope encoding is byte-for-byte deterministic for identical stored-event fields
  - integrity format version 1 is persisted and hashed while zero unsupported or unknown versions fail closed
  - changing position tenant event ID event type occurred time recorded time privilege classification or payload breaks verification at the affected position
  - changing the previous-hash link or stored hash breaks verification at the affected position
  - mutating or reusing a caller-owned payload buffer after append cannot alter the stored event
  - reading a stored payload cannot expose a mutable buffer that changes the retained record
  - reordering duplicating or removing an interior event breaks verification at the first affected position
  - an internally valid prefix without a trusted checkpoint is reported as unanchored and never as proof that the stream tail is complete
  - a trusted checkpoint binds the expected final tenant position and hash so suffix truncation or tail substitution fails verification
  - a valid multi-event tenant stream verifies successfully and remains isolated from another tenant stream
  - every concrete domain event has exactly one explicit bounded canonical name and privilege classification
  - canonical event names are unique across all concrete domain-event types and never use assembly-qualified runtime identities
  - recorded time comes from an injected TimeProvider and is captured exactly once during append
  - equivalent DateTimeOffset instants with different offsets normalize to identical UTC microsecond values and canonical bytes
  - timestamp normalization round-trips at PostgreSQL timestamptz microsecond precision without hash drift
  - privilege classification is explicit and security-sensitive privileged read role-assignment and legal-hold events receive reviewed classifications
  - invalid empty unbounded duplicate-ID or unsupported-version event metadata is rejected before append
  - the event-store contract still exposes append and tenant-scoped read only with no update or delete path
  - the existing backend and SPA suites remain green
security_checks:
  - StoredEvent persists IntegrityFormatVersion and hash material includes that version in a length-delimited canonical envelope plus the previous hash
  - envelope encoding is culture locale and architecture independent and length-delimits the stored payload bytes without parsing or reserializing JSON
  - integers use fixed-width network byte order GUIDs use RFC 4122 network byte order and timestamps use normalized signed UTC microseconds
  - StoredEvent owns an immutable payload value rather than exposing a caller-owned mutable byte array
  - tenant ID and stream position remain store-controlled and cannot be supplied by a module caller
  - recorded time remains store-controlled and uses TimeProvider rather than caller input
  - event type derives from the bounded canonical domain-event name and never includes an assembly name
  - every concrete event declares its canonical name and privilege classification with no implicit default
  - verifier checks supported integrity version contiguous one-based positions one tenant per stream and unique non-empty event IDs
  - verifier discloses only the first broken position and a bounded generic integrity reason
  - verifier distinguishes internally intact from trusted-checkpoint-bound and never claims suffix completeness without an external checkpoint
  - no raw Entra identifier secret credential key connection string infrastructure or tenant action is introduced
  - no outbox transaction persistence WORM anchoring PostgreSQL adapter or second event model is introduced
migration_impact: none
acceptance_criteria:
  - StoredEvent is deeply immutable and includes integrity format version stable event type normalized recorded time and privilege classification in addition to the existing position event ID occurred time tenant links and payload
  - one production EventEnvelopeCanonicalizer is used by both append and verification
  - Sha256HashChain hashes the previous hash and the canonical envelope bytes rather than payload alone
  - HashChainVerifier detects tampering with every stored field covered by the envelope
  - HashChainVerifier accepts an optional immutable trusted checkpoint and reports unanchored integrity when none is supplied
  - the in-memory adapter uses the same production canonicalizer and injected clock as the later durable adapter will use
  - existing domain-event payloads and module behavior are unchanged
  - the task records that aggregateRef actor reason correlationRef trusted checkpoint persistence and WORM anchoring remain mandatory before production evidence claims
  - P0-T12 evidence remains historical while P1-T05 evidence records the corrected integrity guarantee
  - backend build tests architecture formatting dependency and secret gates pass
evidence_required: [docs/build/evidence/EVIDENCE-P1-T05.md]
rollback: Revert the PR; no persistent datastore migration tenant configuration external system or production environment is changed.
requires_human_approval: true
approved_by: Product Owner explicit approval on 2026-07-23
approved_hash: "sha256:5915e8e3c10e8d018fd36fa23e5da9472b9053b1560c206ca3bc92631de2d7f1"
---

## Objective

Correct the existing event-backbone skeleton before it becomes durable. The current chain covers only
the payload bytes; a datastore attacker could alter the stored tenant, position, event identity or
timestamps without invalidating verification. P1-T05 introduces one deterministic, explicitly
versioned storage-integrity frame and makes both append and verification use it.

This task does not create a new event model. It strengthens E20 and the existing `IEventStore`,
`StoredEvent`, `Sha256HashChain` and `HashChainVerifier` capabilities established by P0-T12. It does
not yet claim the full E20 semantic record: P1-T06 must add aggregate reference, opaque actor, reason
and correlation reference and complete the format before any permanent migration.

## Steps (bounded, ordered)

1. Define integrity format v1 with fixed scalar encodings, UTC microsecond timestamp normalization
   and a length-delimited canonical envelope for every stored field except the hash links themselves.
2. Extend `StoredEvent` with persisted integrity version, stable event type, store-recorded time,
   explicit privilege classification and deeply immutable payload ownership.
3. Capture store-controlled tenant, position and recorded time once, canonicalize the prospective
   record, and append its hash-linked immutable representation.
4. Make verification validate structural stream invariants and recompute the same canonical envelope.
5. Add one non-serialized event-contract declaration and explicitly classify every current concrete
   domain event; reject missing, duplicate or unbounded canonical names.
6. Add an optional trusted-checkpoint input so a later WORM anchor can bind the expected final tenant,
   position and hash; report internally intact but unanchored streams honestly.
7. Add field-by-field tamper, hash-link, tail-truncation, structural, determinism, precision,
   immutability and clock tests plus architecture assertions that there remains one platform event
   model and one exhaustive event-contract registry.
8. Run all gates, capture evidence, reconcile build state and open the tenant-independent PR.

## Definition of done

Changing any persisted field covered by integrity format v1 invalidates the chain at a deterministic
position; a supplied trusted checkpoint also detects suffix loss, while an unanchored prefix is
reported honestly. P1-T05 makes no complete-E20 or production-evidence claim. No database, migration,
package, tenant, cloud or deployment action has occurred.

## Rollback

Revert the PR.
