---
id: P1-T08
title: Persist the C8 E18 and E19 foundations in PostgreSQL
type: task-contract
schema_version: 1
epic: EPIC-1-2
phase: PHASE-1
status: complete
objective: Implement the frozen E18 RoleAssignment and E19 PersonKeyMap ports as tenant-isolated PostgreSQL adapters, author migration 0002, and prove atomic eventing, privileged field protection and O(1) severance only against disposable PostgreSQL 16 containers.
blueprint_refs:
  - docs/blueprint/stage-05-conceptual-data-model.md#e19-personkeymap--the-gdpr-severance-point
  - docs/blueprint/stage-05-conceptual-data-model.md#8-privacy--erasure-mechanics
  - docs/blueprint/stage-08-security-trust-architecture.md#3-tenant-isolation-model
  - docs/blueprint/stage-08-security-trust-architecture.md#9-privileged-zone
  - docs/blueprint/stage-08-security-trust-architecture.md#11-encryption--secrets-boundaries-conceptual
  - docs/blueprint/stage-08-security-trust-architecture.md#14-security-event-model--administrative-audit
  - docs/blueprint/stage-09-technology-deployment.md#3-data-platform-decisions
adr_refs: [ADR-014, ADR-015, ADR-017, ADR-020, ADR-021, ADR-023]
rtm_refs: [BR-09, BR-10, BR-15]
allowed_files:
  - ControlTower.sln
  - src/Adapters/ControlTower.Adapters.PostgreSql.Trust/**
  - src/Modules/ControlTower.Modules.Trust/Authorization/PersonKeys.cs
  - src/Modules/ControlTower.Modules.Trust/Authorization/RoleAssignments.cs
  - src/Modules/ControlTower.Modules.Trust/Infrastructure/InMemoryPersonKeyMap.cs
  - src/Modules/ControlTower.Modules.Trust/Infrastructure/InMemoryRoleAssignmentStore.cs
  - tests/ControlTower.Adapters.PostgreSql.Tests/**
  - tests/ControlTower.ArchitectureTests/AdapterBoundaryTests.cs
  - tests/ControlTower.ArchitectureTests/ControlTower.ArchitectureTests.csproj
  - tests/ControlTower.Host.Web.Tests/RoleAuthorizationTests.cs
  - db/migrations/0002_c8_identity_authorization.sql
  - db/migrations/0002_c8_identity_authorization.down.sql
  - db/migrations/0002_c8_identity_authorization.verify.sql
  - db/migrations/0002_c8_identity_authorization.validation.md
  - docs/build/tasks/P1-T08-durable-c8-postgresql-foundations.md
  - docs/build/evidence/EVIDENCE-P1-T08.md
  - docs/build/state/build-state.yaml
  - docs/build/state/dev-substitute-registry.md
  - STATUS.md
forbidden_files:
  - docs/blueprint/**
  - docs/build/approvals/**
  - src/ControlTower.Platform/**
  - src/Modules/ControlTower.Modules.Audit/**
  - src/Modules/ControlTower.Modules.Economics/**
  - src/Modules/ControlTower.Modules.EnterpriseContext/**
  - src/Modules/ControlTower.Modules.Experience/**
  - src/Modules/ControlTower.Modules.Governance/**
  - src/Modules/ControlTower.Modules.Ledger/**
  - src/Modules/ControlTower.Modules.Providers/**
  - src/Modules/ControlTower.Modules.Trust/ControlTower.Modules.Trust.csproj
  - src/Modules/ControlTower.Modules.Trust/TrustModule.cs
  - src/Adapters/ControlTower.Adapters.PostgreSql/**
  - src/Adapters/ControlTower.Adapters.InMemory/**
  - src/Host/**
  - tests/ControlTower.Platform.Tests/**
  - tests/ControlTower.Host.Web.Tests/**
  - tests/ControlTower.Modules.*/**
  - db/migrations/0001*
  - db/migrations/0003*
  - infra/**
  - web/**
preconditions:
  - P1-T07 is merged to main and its durable E20 transaction appender migration 0001 and PostgreSQL isolation evidence are green
  - P1-T06 has frozen the E18 versioning and E19 privileged-access severance contracts that this task persists unchanged
  - DEC-001 selects Azure Database for PostgreSQL Flexible Server behind an adapter and permits local Docker PostgreSQL only as a disposable development substitute
  - The Product Owner explicitly approved P1-T05 through P1-T08 including C8 interface and event-backbone changes Npgsql 10.0.3 PostgreSQL 16.14 Alpine 3.24 ephemeral testing and authoring plus green-CI merge of migrations 0001 and 0002 on 2026-07-23
  - The Product Owner explicitly approved adding only tests/ControlTower.Host.Web.Tests/RoleAuthorizationTests.cs to the P1-T08 file scope solely to normalize the stale DateTimeOffset fixture on 2026-07-24
  - No shared staging or production migration execution is authorised
required_tests:
  - migration 0002 applies after migration 0001 under a distinct migration owner against postgres 16.14-alpine3.24
  - migration 0002 passes apply verify rollback re-apply and verify with an equivalent catalog fingerprint before and after rollback while migration 0001 remains intact
  - normal and privileged runtime roles are distinct non-owner non-BYPASSRLS identities with no upward memberships temporary objects table DML DDL or bulk E19 read surface
  - every E18 and E19 operation captures one ambient tenant before protection audit or database I/O and transaction-locally binds that tenant before each SQL access
  - E18 create read list and revoke round-trip the frozen aggregate exactly and forced RLS hides every foreign-tenant row
  - concurrent identical E18 grants produce one active assignment and one canonical RoleAssignmentChanged event
  - concurrent or stale E18 revocations append at most one version 2 event and return only the frozen authoritative idempotent or conflict outcomes
  - forged E18 event state metadata actor aggregate correlation or expected version fails before mutation and event append failure leaves state unchanged
  - E19 stores directory object IDs and display snapshots only as authenticated ciphertext plus a tenant-scoped keyed lookup index with a bounded non-secret key reference
  - E19 find get create existing-create sever repeated-sever and remap operations preserve the frozen typed outcomes and audit before protected release
  - concurrent E19 get-or-create produces one PersonKey one active mapping and one PersonKeyMapChanged creation event
  - E19 severance nulls ciphertext lookup index and key reference in one indexed row update retains only a non-personal tombstone and permits a different-key remap
  - E19 audit or event failure returns no protected value and leaves create or sever state unchanged
  - the same raw directory object ID in two tenants has unrelated PersonKeys ciphertext and blind indexes and cannot be correlated through runtime database access
  - missing malformed switched or pooled tenant context generic cross-tenant lookup and direct SQL bypass attempts fail without identity assignment or existence disclosure
  - ciphertext tampering wrong-tenant protection keys unknown key references and malformed protected envelopes fail closed without returning raw identity
  - event evidence database rows logs exceptions and serialized test outputs contain no raw directory ID display snapshot plaintext key material protection key or secret
  - the full backend architecture build format dependency secret task-contract protected-path and development-substitute gates remain green
security_checks:
  - E18 remains an outer adapter for the existing C8 port and the generic PostgreSQL E20 adapter remains module-independent
  - stateless Trust-owned validation helpers are shared by development and PostgreSQL adapters without changing either frozen C8 port
  - E19 uses a separate privileged PostgreSQL login and only bounded point-lookup and mutation functions with no list export or direct table grant
  - field protection uses authenticated encryption and a keyed tenant-scoped lookup index outside PostgreSQL while only a bounded key reference is persisted
  - protection-key acquisition is an injected tenant-bound adapter seam with no key secret credential or connection string committed or logged
  - migration 0002 enables and forces RLS on every tenant-bearing table and its fixed-search-path security-definer functions reject a missing malformed or different tenant
  - database runtime roles own no table or function and have no superuser BYPASSRLS create update delete truncate direct insert or upward-role capability
  - state rows carry the canonical event ID and deferred database guards reject a committed E18 or E19 mutation whose privileged E20 event is absent
  - E18 active tenant person role uniqueness and optimistic revocation are database-constrained and state plus E20 append use one caller-owned transaction
  - E19 lookup is indexed and severance removes both raw-identity directions with one constant-time row update without deleting the opaque PersonKey tombstone
  - raw directory identity display plaintext ciphertext blind indexes key references and key material never enter event or privileged-read evidence
  - all SQL values are parameterized and database failures are mapped to bounded messages without identifiers ciphertext indexes key references SQL or cross-tenant existence detail
  - the ephemeral harness refuses non-loopback PostgreSQL endpoints and cannot execute migration 0002 against shared staging or production databases
  - Npgsql remains isolated to PostgreSQL adapters and tests and no Platform module Host provider telemetry policy or deployment dependency direction changes
  - no production key provider Key Vault resource credential tenant activation host composition outbox WORM anchor provider repository or new bounded context is introduced
migration_impact: authored-not-executed # no shared, staging or production execution; disposable local/CI validation only
acceptance_criteria:
  - ControlTower.Adapters.PostgreSql.Trust implements the unchanged IRoleAssignmentStore and IPersonKeyMap ports using the approved Npgsql dependency only at the outer adapter boundary
  - migration 0002 creates the minimum E18 and E19 tables constraints indexes forced-RLS policies grants bounded functions and state-event commit guards required by the frozen blueprint
  - the E18 adapter provides exact durable active-grant idempotence optimistic revocation and atomic canonical event persistence
  - the E19 adapter provides tenant-separated authenticated field protection privileged-audited bidirectional point lookup and O(1) irreversible severance
  - normal runtime identity cannot access E19 and privileged runtime identity has no bulk export direct table access or unrelated C8 authority
  - migration 0002 and all hostile database tests pass only against the approved disposable PostgreSQL container while migration 0001 remains green and unchanged
  - build state records migration 0002 as authored and ephemeral-only with shared staging and production execution absent
  - backend build tests architecture formatting dependency secret task-contract protected-path and readiness gates pass
  - no shared staging production Microsoft tenant Azure resource credential or production key is accessed or changed
evidence_required: [docs/build/evidence/EVIDENCE-P1-T08.md]
rollback: Revert the PR; migration 0002 has been exercised only in disposable containers, so no persistent datastore rollback or external action is required.
requires_human_approval: true
approved_by: Product Owner explicit approval on 2026-07-23
approved_hash: null
---

## Objective

Persist the already-frozen C8 E18 authorization and E19 privacy foundations beside the durable E20
kernel. E18 uses the normal runtime identity and retains one active tenant/person/role grant with
optimistic revocation. E19 is a smaller privileged database perimeter: raw directory identity is
field-protected before persistence, every access follows the existing privileged-audit port, there
is no bulk-read path, and severance removes both identity directions in one indexed update while
retaining only the opaque tombstone.

This task adds outer PostgreSQL adapters for existing C8 ports. It adds no context, user-facing
capability, provider, telemetry level, production composition or cloud resource.

## Steps (bounded, ordered)

1. Extract only the existing stateless E18 commit and E19 evidence-context checks into shared
   Trust-owned helpers without changing either frozen port, then add a Trust-specific PostgreSQL
   outer-adapter project that references those ports and the generic P1-T07 transaction appender
   without changing Platform or the E20 adapter.
2. Author migration `0002_c8_identity_authorization.sql` with minimum E18/E19 tables, constraints,
   indexes, forced RLS, distinct normal/privileged identities, bounded security-definer functions,
   state-event commit guards and least-privilege grants, paired with guarded rollback, executable
   verification and validation notes.
3. Implement durable E18 exact rehydration, bounded reads, active-grant idempotence, optimistic
   revocation and state-plus-event atomicity.
4. Implement tenant-bound E19 authenticated field protection, keyed lookup, audit-before-release
   reads, concurrent get-or-create and indexed irreversible severance behind the unchanged map port.
5. Add a second non-parallel ephemeral-only PostgreSQL fixture with a generated `ct_p1_t08_*`
   database. It applies and verifies migration 0001 as its immutable baseline, cycles only migration
   0002, proves rollback restores the exact post-0001 catalog, then runs hostile isolation,
   concurrency, atomicity, evidence-leak and ciphertext-tamper tests.
6. Add permanent dependency tests for the Trust-specific outer adapter and prove that Npgsql and
   concrete adapters remain outside Platform and all modules.
7. Run every local gate, perform independent architecture/security/test review, capture evidence,
   reconcile build state and open the tenant-independent PR.

## Definition of done

The unchanged E18 and E19 C8 ports have durable PostgreSQL implementations. E18 state and its event
are atomic and tenant-isolated. E19 raw identity is field-protected behind a separate privileged
identity, every operation is audited through the existing C9 seam, severance is constant-time and
irreversible, and no bulk export path exists. Migration 0002 and hostile tests are green only in
approved disposable PostgreSQL containers; migration 0001 remains immutable; no shared, staging,
production, tenant or cloud resource changes.

## Rollback

Revert the PR. All migration execution performed by this task is against disposable containers
whose databases and roles are destroyed after testing.
