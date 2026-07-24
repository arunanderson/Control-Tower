---
id: P1-T06
title: Complete E20 audit metadata and C8 E18/E19 contracts
type: task-contract
schema_version: 1
epic: EPIC-1-2
phase: PHASE-1
status: complete
objective: Complete the pre-persistence E20 semantic event record, consolidate the shared opaque actor and person-reference contracts, and make the existing E18/E19 C8 seams audited, severable, tenant-safe and concurrency-ready before any permanent PostgreSQL schema is authored.
blueprint_refs:
  - docs/blueprint/stage-04-domain-model.md#3-value-objects-shared-kernel
  - docs/blueprint/stage-04-domain-model.md#5-domain-events-the-canonical-set
  - docs/blueprint/stage-04-domain-model.md#9-audit-model-the-event-record-is-the-audit-trail
  - docs/blueprint/stage-05-conceptual-data-model.md#e19-personkeymap-the-gdpr-severance-point
  - docs/blueprint/stage-05-conceptual-data-model.md#e20-domaineventrecord--privilegedreadrecord
  - docs/blueprint/stage-07-conceptual-architecture.md#7-trust-and-security-boundaries
  - docs/blueprint/stage-07-conceptual-architecture.md#10-simplifications-chosen-challenge-applied
  - docs/blueprint/stage-08-security-trust-architecture.md#3-tenant-isolation-model
  - docs/blueprint/stage-08-security-trust-architecture.md#9-privileged-zone
  - docs/blueprint/stage-08-security-trust-architecture.md#11-encryption--secrets-boundaries-conceptual
  - docs/blueprint/stage-08-security-trust-architecture.md#13-evidence-integrity
  - docs/blueprint/stage-08-security-trust-architecture.md#14-security-event-model--administrative-audit
adr_refs: [ADR-015, ADR-020, ADR-021, ADR-023]
rtm_refs: [BR-09, BR-10, BR-15]
allowed_files:
  - src/ControlTower.Platform/Events/**
  - src/ControlTower.Platform/Audit/PrivilegedRead.cs
  - src/ControlTower.Platform/Identity/**
  - src/Adapters/ControlTower.Adapters.InMemory/InMemoryEventStore.cs
  - src/Adapters/ControlTower.Adapters.InMemory/InMemoryPrivilegedReadAuditor.cs
  - src/Modules/ControlTower.Modules.Trust/Authorization/**
  - src/Modules/ControlTower.Modules.Trust/Infrastructure/**
  - src/Modules/ControlTower.Modules.Audit/PrivilegedAccess.cs
  - src/Modules/ControlTower.Modules.Audit/InMemoryPrivilegedAccessStore.cs
  - src/Modules/ControlTower.Modules.Audit/LegalHolds.cs
  - src/Modules/ControlTower.Modules.Ledger/Application/AssetRegistrationService.cs
  - src/Modules/ControlTower.Modules.Ledger/Application/EntityResolutionService.cs
  - src/Modules/ControlTower.Modules.Ledger/Application/ResolutionWorkbenchReadModel.cs
  - src/Modules/ControlTower.Modules.Ledger/Domain/**
  - src/Modules/ControlTower.Modules.Ledger/Infrastructure/InMemoryAssetLedgerReadModel.cs
  - src/Modules/ControlTower.Modules.Governance/Application/GovernanceService.cs
  - src/Modules/ControlTower.Modules.Governance/Domain/**
  - src/Modules/ControlTower.Modules.Economics/Application/EconomicsIngestionService.cs
  - src/Modules/ControlTower.Modules.Economics/Application/ReportingSnapshotService.cs
  - src/Modules/ControlTower.Modules.Economics/Domain/**
  - src/Modules/ControlTower.Modules.Providers/Application/ObservationIngestionService.cs
  - src/Modules/ControlTower.Modules.Providers/Application/ProviderSweepJobs.cs
  - src/Host/ControlTower.Host.Web/Authentication/ControlTowerAuthentication.cs
  - src/Host/ControlTower.Host.Web/Authorization/ControlTowerAuthorization.cs
  - src/Host/ControlTower.Host.Web/PrivilegedReadAuditFilter.cs
  - src/Host/ControlTower.Host.Web/ExperienceApi.cs
  - src/Host/ControlTower.Host.Web/Program.cs
  - tests/ControlTower.Platform.Tests/**
  - tests/ControlTower.ArchitectureTests/**
  - tests/ControlTower.Host.Web.Tests/**
  - tests/ControlTower.Modules.Ledger.Tests/**
  - tests/ControlTower.Modules.Governance.Tests/**
  - tests/ControlTower.Modules.Economics.Tests/**
  - tests/ControlTower.Modules.Providers.Tests/**
  - docs/build/**
  - STATUS.md
forbidden_files:
  - docs/blueprint/**
  - docs/build/approvals/**
  - src/ControlTower.Platform/Tenancy/**
  - src/ControlTower.Platform/Ports/**
  - src/Adapters/ControlTower.Adapters.PostgreSql/**
  - src/Host/ControlTower.Host.Worker/**
  - web/**
  - db/**
  - infra/**
  - poc/**
  - .github/**
preconditions:
  - P1-T05 is merged and main is green
  - No durable event store or production event data exists, so integrity format 1 can be replaced without migration or compatibility loss
  - P1-T07 will not author migration 0001 until the completed E20 format and append contract are frozen by this task
  - P1-T08 will not author migration 0002 until the E18 versioning and E19 access and severance contracts are frozen by this task
  - The Product Owner explicitly approved P1-T05 through P1-T08 including event-backbone and C8 interface changes on 2026-07-23
required_tests:
  - integrity format 2 has byte-exact cross-platform golden bytes and hash output while formats 0 1 and unknown fail closed
  - aggregate reference opaque actor optional reason and optional correlation are persisted and changing any one breaks verification at the affected position
  - nullable fields use unambiguous presence encoding and blank-present or unbounded values fail before append
  - the event-store contract exposes no metadata-free append overload and every current append path supplies explicit semantic metadata
  - one shared AuditActor accepts only bounded person system and provider forms and raw Entra actors are rejected
  - every person reference outside E19 contains only an opaque PersonKey and no directory object ID or display snapshot
  - E19 find get create existing-create sever repeated-sever and remap operations are tenant-bound and privileged-audited
  - an E19 audit or event failure releases no protected value and leaves create or sever state unchanged
  - concurrent E19 get-or-create yields one PersonKey and one creation event
  - E19 severance removes both raw identity directions in constant time retains only a non-personal tombstone and remapping creates a different PersonKey
  - same raw directory identity in two tenants yields unrelated PersonKeys and cross-tenant lookup or severance returns generic absence
  - serialized events audit records read projections API responses and integrity material contain no raw directory identity or display snapshot
  - effective access carries the resolved PersonKey and Host derives every persisted human actor from that server-resolved value
  - privileged-read records preserve record ID opaque actor purpose policy applicability policy version correlation and occurred time
  - E18 initial assignment is version 1 revocation is version 2 and rehydration rejects invalid state
  - concurrent identical grants produce one active assignment one event and the same authoritative assignment ID
  - concurrent or stale revocations produce at most one event and return a typed conflict or authoritative idempotent result
  - forged assignment event metadata or expected-version mismatches fail before append and event failure leaves E18 state unchanged
  - existing authentication role capability tenant isolation event integrity module and SPA suites remain green
security_checks:
  - AuditActor is the only audit-actor value object and arbitrary strings cannot enter the E20 envelope
  - a human actor is person plus opaque PersonKey only and can never contain Entra tenant object subject display or email identity
  - E20 aggregate actor reason correlation and privilege metadata are integrity-covered independently of opaque payload bytes
  - event metadata is supplied explicitly by trusted modules and never inferred by parsing caller JSON
  - no implicit actor fallback fabricated aggregate runtime type name or assembly identity exists
  - every E19 read is audited before protected data is returned and every E19 write is represented by one privileged domain event
  - E19 evidence may contain PersonKey action version and opaque context only never raw identity display snapshot ciphertext blind index key material or secret
  - severance and cross-tenant failures disclose no existence detail
  - production composition remains fail-closed while in-memory adapters remain development-only
  - no database SQL package migration container cloud resource tenant action credential secret or deployment is introduced
  - no telemetry-policy engine L2 surface role redesign delegated administration staff JIT break-glass outbox redesign WORM anchor or general aggregate persistence rewrite is introduced
migration_impact: none
acceptance_criteria:
  - StoredEvent and its canonical integrity format 2 fully represent E20 event metadata
  - every current event producer appends through the one complete E20 contract with reviewed aggregate actor reason and correlation semantics
  - PersonKey and AuditActor are shared-kernel values and duplicate raw or module-local actor representations are removed from affected paths
  - E19 is one auditable bidirectional and severable boundary with typed idempotent outcomes and no raw identity leakage
  - E18 exposes versioned optimistic-concurrency semantics and the in-memory adapter proves hostile concurrent behavior before its durable implementation
  - Host resolves a request human to PersonKey once and all downstream persisted actor references are opaque
  - privileged-read records include the blueprint-required policy context and correlation without inventing a policy version when not applicable
  - backend build tests architecture formatting dependency and secret gates pass
evidence_required: [docs/build/evidence/EVIDENCE-P1-T06.md]
rollback: Revert the PR; no persistent datastore migration tenant configuration infrastructure external system or production environment is changed.
requires_human_approval: true
approved_by: Product Owner explicit approval on 2026-07-23
approved_hash: null
---

## Objective

Finish the existing E20/C8 contracts before the first permanent schema records them. P1-T05 made
the provisional storage frame tamper-evident; P1-T06 adds the semantic audit fields the blueprint
already requires, removes raw directory identity from every affected persisted path, completes the
E19 severance perimeter, and gives E18 the concurrency contract its durable adapter will enforce.

This is completion of existing Platform, C8 and C9 seams. It adds no bounded context, product
capability, persistence technology, provider, telemetry level or deployment surface.

## Steps (bounded, ordered)

1. Introduce shared opaque `PersonKey` and `AuditActor` values; replace affected module-local or raw
   actor/person representations without changing the role catalogue.
2. Define bounded aggregate and correlation references plus the complete append metadata contract.
3. Issue integrity format 2, persist and hash all E20 metadata, and remove the incomplete append
   overload.
4. Give every current producer an explicit aggregate, actor, reason and correlation mapping while
   leaving integration-event/outbox payloads unchanged.
5. Complete the immutable privileged-read record and E19 access context.
6. Implement the development E19 adapter's audited bidirectional lookup, typed creation and O(1)
   severance lifecycle with fail-closed evidence behavior.
7. Carry the server-resolved PersonKey through effective access and remove raw Entra actors from
   Host commands, audit records, projections and responses.
8. Add versioned E18 rehydration, consistency validation, expected-version commit outcomes and
   hostile-concurrency behavior to the development adapter.
9. Run adversarial and full gates, perform read-only architecture/security review, capture evidence,
   reconcile build state and open the tenant-independent PR.

## Definition of done

The repository has one complete, integrity-covered E20 event contract; one opaque audit actor; one
E19 raw-identity boundary whose reads and writes cannot evade evidence; and one E18 version/commit
contract ready for durable implementation. No raw Entra identity exists in an affected durable
payload, audit record or human actor. No database, migration, package, tenant, cloud or deployment
action has occurred.

## Rollback

Revert the PR.
