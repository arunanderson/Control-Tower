---
id: P1-T07
title: Persist the E20 event kernel in PostgreSQL
type: task-contract
schema_version: 1
epic: EPIC-1-2
phase: PHASE-1
status: complete
objective: Implement the frozen IEventStore contract as a tenant-isolated append-only PostgreSQL adapter, author migration 0001, and prove its integrity and isolation properties only against disposable PostgreSQL 16 containers.
blueprint_refs:
  - docs/blueprint/stage-04-domain-model.md#9-audit-model-the-event-record-is-the-audit-trail
  - docs/blueprint/stage-05-conceptual-data-model.md#e20-domaineventrecord--privilegedreadrecord
  - docs/blueprint/stage-08-security-trust-architecture.md#3-tenant-isolation-model
  - docs/blueprint/stage-08-security-trust-architecture.md#13-evidence-integrity
  - docs/blueprint/stage-08-security-trust-architecture.md#14-security-event-model--administrative-audit
  - docs/blueprint/stage-09-technology-deployment.md#3-data-platform-decisions
adr_refs: [ADR-015, ADR-017, ADR-020, ADR-021, ADR-023]
rtm_refs: [BR-09, BR-10, BR-15]
allowed_files:
  - ControlTower.sln
  - src/Adapters/ControlTower.Adapters.PostgreSql/**
  - tests/ControlTower.Adapters.PostgreSql.Tests/**
  - tests/ControlTower.ArchitectureTests/AdapterBoundaryTests.cs
  - tests/ControlTower.ArchitectureTests/ControlTower.ArchitectureTests.csproj
  - db/CLAUDE.md
  - db/migrations/0001_event_kernel.sql
  - db/migrations/0001_event_kernel.down.sql
  - db/migrations/0001_event_kernel.verify.sql
  - db/migrations/0001_event_kernel.validation.md
  - .github/workflows/build-test.yml
  - docs/build/tasks/P1-T07-durable-e20-postgresql-event-kernel.md
  - docs/build/evidence/EVIDENCE-P1-T07.md
  - docs/build/state/build-state.yaml
  - docs/build/state/dev-substitute-registry.md
  - STATUS.md
forbidden_files:
  - docs/blueprint/**
  - docs/build/approvals/**
  - src/ControlTower.Platform/**
  - src/Modules/**
  - src/Adapters/ControlTower.Adapters.InMemory/**
  - src/Host/**
  - tests/ControlTower.Platform.Tests/**
  - tests/ControlTower.Host.Web.Tests/**
  - tests/ControlTower.Modules.*/**
  - db/migrations/0002*
  - infra/**
  - web/**
preconditions:
  - P1-T06 is merged to main and its complete E20 integrity format 2 and IEventStore contract are green
  - DEC-001 selects Azure Database for PostgreSQL Flexible Server behind an adapter and permits local Docker PostgreSQL only as a disposable development substitute
  - The Product Owner explicitly approved P1-T05 through P1-T08 including Npgsql 10.0.3 PostgreSQL 16.14 Alpine 3.24 ephemeral testing and authoring plus green-CI merge of migrations 0001 and 0002 on 2026-07-23
  - No shared staging or production migration execution is authorised
required_tests:
  - migration 0001 applies from an empty database under a distinct migration owner against postgres 16.14-alpine3.24
  - migration 0001 passes apply verify rollback re-apply and verify with an equivalent catalog fingerprint before and after rollback
  - the runtime role has only the schema and table privileges required by IEventStore and cannot bypass row-level security
  - append and read round-trip every E20 format 2 field byte-for-byte or value-for-value including opaque actor optional reason optional correlation privilege and payload
  - the first event uses genesis and concurrent same-tenant appends receive contiguous tenant-local positions and one valid hash chain
  - concurrent different-tenant appends remain independent and each tenant reads only its own ordered stream
  - a globally duplicate event ID fails generically and leaves both streams positions and integrity links unchanged
  - malformed metadata empty event IDs unsupported event contracts and cancelled operations leave no committed event or consumed position
  - database update delete and truncate attempts against immutable event records fail and leave the chain intact
  - direct cross-tenant select returns no rows and a cross-tenant append function call is rejected by row-level security and function guards
  - missing and malformed database tenant context fail closed without returning or mutating a tenant row
  - transaction-local tenant context is cleared after commit rollback cancellation and pooled-connection reuse
  - persisted timestamps are normalized UTC microseconds and persisted hashes verify through the production HashChainVerifier with a trusted checkpoint
  - the full backend architecture build format dependency secret task-contract protected-path and development-substitute gates remain green
security_checks:
  - tenant identity is obtained only from ITenantContextAccessor and bound transaction-locally before every SQL statement that can access tenant rows
  - all commands are parameterized and no caller-controlled identifier or SQL fragment is interpolated
  - the database runtime role has no table ownership superuser BYPASSRLS update delete truncate or DDL capability
  - row-level security is enabled and forced on every tenant-bearing migration 0001 table
  - event records are immutable under both privilege revocation and database-enforced update delete and truncate rejection
  - global event-ID collisions and cross-tenant failures reveal no other tenant identifier event metadata or existence detail
  - recorded time stream position tenant event contract privilege previous hash and final hash remain store-controlled
  - database failures roll back event insertion and stream-head mutation as one transaction
  - no connection string credential secret tenant data cloud resource or production configuration is committed
  - the ephemeral harness refuses non-loopback PostgreSQL endpoints and cannot execute migration 0001 against shared staging or production databases
  - Npgsql is isolated to the PostgreSQL adapter and test project and no module or Platform dependency direction changes
  - permanent architecture tests load the PostgreSQL adapter and reject any Platform or module dependency on Npgsql or the concrete adapter
  - no outbox WORM anchor E18 E19 provider repository host composition infrastructure or deployment capability is introduced
  - PostgreSQL RLS and append-only triggers are confined to the adapter and migration as measurable security controls explicitly required by Stage 9 and do not change the domain architecture
migration_impact: authored-not-executed # no shared, staging or production execution; disposable local/CI validation only
acceptance_criteria:
  - ControlTower.Adapters.PostgreSql implements the unchanged IEventStore port using Npgsql 10.0.3
  - migration 0001 creates the minimum durable event-kernel tables constraints indexes RLS policies grants and append-only enforcement required by the frozen blueprint
  - migration 0001 has a guarded rollback and validation note and its apply verify rollback re-apply drift cycle is green
  - a non-owner non-BYPASSRLS runtime role can append and read only the ambient tenant through the adapter
  - all failure and concurrency cases are atomic and leave a contiguous verifiable per-tenant stream
  - CI provisions only the approved disposable PostgreSQL image and runs the integration suite on every pull request and main build
  - build state records migration 0001 as authored and ephemeral-only with shared staging and production execution absent
  - backend build tests architecture formatting dependency secret task-contract protected-path and readiness gates pass
  - no shared staging production Microsoft tenant or Azure resource is accessed or changed
evidence_required: [docs/build/evidence/EVIDENCE-P1-T07.md]
rollback: Revert the PR; migration 0001 has been exercised only in disposable containers, so no persistent datastore rollback or external action is required.
requires_human_approval: true
approved_by: Product Owner explicit approval on 2026-07-23
approved_hash: null
---

## Objective

Add a production-grade PostgreSQL implementation beside the still development-only event-store
substitute for the already-frozen E20 contract. The work persists the complete integrity format 2
envelope, binds every operation to the ambient tenant, serializes each tenant's append position, and
makes committed event rows database-enforced append-only. Host composition remains unchanged and
out of scope; the existing Worker development-substitute registration is an explicit production
composition hardening gap for a later task.

This is an infrastructure adapter for the existing Platform port. It adds no bounded context,
product capability, event semantic, tenant surface, provider, telemetry level or deployment.

## Steps (bounded, ordered)

1. Add the PostgreSQL adapter and integration-test projects to the solution with Npgsql 10.0.3
   isolated to the adapter boundary.
2. Author migration `0001_event_kernel.sql` with the minimum E20 event stream state, constraints,
   forced RLS, least-privilege grants and immutable-row enforcement, paired with guarded rollback,
   executable verification and validation notes.
3. Implement transaction-local tenant binding, tenant-serialized atomic append, complete row
   rehydration and generic integrity-safe database error mapping behind `IEventStore`.
4. Build an ephemeral-only fixture that refuses remote endpoints, creates distinct migration and
   runtime roles, executes apply, verify, rollback, re-apply and catalog-drift comparison on a fresh
   database, and destroys the database and roles after the suite.
5. Add hostile concurrency, atomic rollback, missing/malformed tenant, pooled-context leakage, RLS,
   direct-SQL immutability, exact round-trip and production hash-verifier integration tests.
6. Pin the approved PostgreSQL container image in CI and run the durable adapter suite without
   adding any shared, staging or production connection.
7. Run all local gates, perform independent architecture/security/test review, capture evidence,
   reconcile build state and open the tenant-independent PR.

## Definition of done

The unchanged `IEventStore` contract has a durable PostgreSQL implementation whose complete E20
records are atomic, tenant-isolated, append-only and verifiably hash-chained. Migration 0001 and all
hostile database tests pass against only the approved disposable PostgreSQL container, every
repository gate is green, and no shared, staging, production, tenant or cloud resource has changed.

## Rollback

Revert the PR. All migration execution performed by this task is against disposable containers
whose databases and roles are destroyed after testing.
