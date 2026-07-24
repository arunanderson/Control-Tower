---
id: EVIDENCE-P1-T08
type: evidence-bundle
schema_version: 1
task: P1-T08
status: submitted
produced_at: 2026-07-24T12:26:38Z
---

## Task

Persist the frozen C8 E18 role-assignment and E19 person-key-map contracts behind tenant-isolated
PostgreSQL adapters, author migration 0002, and prove atomic eventing, privileged field protection
and O(1) severance only against disposable PostgreSQL 16.

## Approval and bounded scope

The Product Owner explicitly approved P1-T05 through P1-T08 on 2026-07-23, including the event
backbone and C8 interface changes, Npgsql 10.0.3, disposable
`postgres:16.14-alpine3.24` testing, and authoring plus green-CI merge of migrations 0001/0002.
Shared, staging and production migration execution was explicitly excluded.

P1-T08 changes only the paths allowed by its task contract: the Trust PostgreSQL outer adapter,
Trust-owned stateless E18/E19 validation helpers and their existing in-memory consumers, migration
0002, PostgreSQL/architecture tests, the solution, and build-control evidence. It does not modify
the frozen blueprint, migration 0001, the generic PostgreSQL E20 adapter, Platform, Host,
infrastructure, another bounded context, production composition or a cloud resource.

The approval provenance is explicit, but the exact P1-T08 contract artifact was authored after the
broad P1-T05–P1-T08 approval. `approved_hash` therefore remains `null`; this evidence does not
fabricate a pre-approval digest.

## Blueprint and ADR trace

- Stage 5 E19 and §8 require the bidirectional PersonKeyMap severance point, removal of both
  identity directions and retention of only an opaque tombstone.
- Stage 8 §§3, 9, 11 and 14 require application-tier tenant authority, a privileged identity
  perimeter, external tenant-bound protection keys and first-class administrative evidence.
- Stage 9 §§3 and 4 require PostgreSQL, forced row-level isolation, adapter-owned persistence and
  no provider-specific dependency in the core.
- ADR-014 and ADR-017 fix the opaque identity and tenant-isolation boundaries.
- ADR-015 fixes the event record and privileged-read record as audit evidence.
- ADR-020 and ADR-021 require irreversible severance, no cross-tenant enumeration and
  integrity-verifiable evidence.
- ADR-023 confines PostgreSQL and Npgsql to outer adapters.
- DEC-001 selects Azure Database for PostgreSQL Flexible Server and permits local Docker
  PostgreSQL only as a disposable development substitute.

No blueprint deviation or new bounded context was required.

## Delivered implementation

### Durable E18 role assignments

- `PostgreSqlRoleAssignmentStore` implements the unchanged `IRoleAssignmentStore` port through the
  normal runtime identity.
- Create/read/list/revoke preserve the frozen aggregate, active-grant idempotence and optimistic
  typed outcomes.
- State and the canonical `RoleAssignmentChanged` event commit in one caller-owned transaction.
- A final ambient-tenant recheck occurs immediately before the non-cancellable commit.
- Duplicate assignment IDs and duplicate event IDs are distinguished; database failures expose
  only bounded adapter messages.
- Aggregate and event timestamps must each be exact UTC microseconds, including historical
  `AssignedAt` on revocation.

### Privileged E19 person-key map

- `PostgreSqlPersonKeyMap` implements the unchanged `IPersonKeyMap` port through the separate
  privileged runtime identity and bounded point functions only.
- AES-256-GCM protects the raw directory object ID and optional display snapshot. HMAC-SHA-256
  produces the tenant/reference-bound lookup index outside PostgreSQL.
- Authenticated data binds protection format, tenant, PersonKey, AES reference, index reference and
  blind index. Decryption recomputes and fixed-time verifies the identity index.
- One tenant-keyed database authority row fixes the lookup reference and HMAC-key commitment.
  Changed references or changed key material fail before insertion; lookup is a primary-key plus
  unique-index point operation rather than a tenant scan.
- AES-reference rotation remains supported for new writes. Lookup-key rotation is deliberately
  rejected until a later controlled dual-read/reindex migration.
- Every E19 operation records privileged evidence before protected release. The evidence appender
  owns an independent least-privilege connection pool, preventing state-pool starvation.
- Create/sever state and the canonical `PersonKeyMapChanged` event are atomic. Severance clears
  ciphertext, nonce, tag, blind index and key references in one indexed row update and retains only
  the opaque version-2 tombstone.
- Secret, plaintext, AAD, GUID, Base64 and envelope working buffers are bounded and cleared.

### Migration 0002

Migration 0002 creates only the required `trust_store` objects:

- forced-RLS `role_assignments`, `person_key_map` and `person_key_index_authority` tables;
- active E18 uniqueness, E18 history index and keyed E19 lookup;
- fixed-search-path security-definer point functions;
- distinct normal and privileged runtime grants with no direct table DML or bulk E19 read;
- deferred state-event guards for E18/E19 mutations; and
- a P1-T08-only guarded rollback.

The normal and privileged database logins are trusted workload identities, not tenant
credentials. The frozen application tier authenticates and fixes request/job tenancy; the
transaction-local tenant setting and forced RLS are the persistence defence in depth.

Migration artifact SHA-256:

- forward:
  `183f1fa0d38686efc03c77f20999fdb210a08b1e936ec4c3e10d49bd49c0e32f`
- guarded rollback:
  `12a1452a43981d519a81493dd5832b461a6838286b10bbf0197755426fcc8e5e`
- executable verifier:
  `10bf38bfd494cc5034e9c969be3596e17785fb4a8ead22cecc09393d9a055912`
- validation notes:
  `cfb186abba24f959f3fec29daa937dd527ba942a5af1b296df94dfa7acf30d65`

## Migration lifecycle evidence

Only generated loopback databases and roles in disposable
`postgres:16.14-alpine3.24` containers were used.

The fixture and an independent read-only database review both passed this exact cycle:

1. create generated non-owner normal/privileged roles, a migration owner and a
   `ct_p1_t08_<hex>` database;
2. apply immutable migration 0001 and its verifier;
3. capture the exact post-0001 catalog fingerprint;
4. apply migration 0002 and run its executable verifier;
5. capture the combined event/trust fingerprint;
6. execute migration 0002's rollback only with the P1-T08 ephemeral guard;
7. verify migration 0001 remains intact and the catalog is byte-identical to the baseline;
8. reapply migration 0002, rerun both verifiers and prove the combined fingerprint is
   byte-identical to the first application; and
9. destroy only the generated database and roles and stop the disposable container.

The fingerprint includes constraint definitions, exact index definitions and validity/readiness
flags, policy commands/roles/permissiveness, trigger enabled state, function definitions/ACLs,
grants, ownership and RLS flags.

No shared, staging or production database was contacted or changed.

## Hostile and regression evidence

The 12-test P1-T08 hostile class passed against a fresh exact-version container. It covers:

- apply/verify/rollback/reapply and runtime-role separation;
- exact E18 round trips, concurrent grant/revoke, stale outcomes and remap non-transfer;
- forged state/event metadata, duplicate event IDs, canonical timestamps and event-append rollback;
- concurrent E19 create/sever, create-existing, remap and O(1) tombstoning;
- audit/event failures, early bounded database failures and a saturated one-connection state pool;
- tenant switching immediately before commit, pooled/missing tenant context and cross-tenant reads;
- same raw identity across tenants even when test key bytes are equal;
- AES rotation plus immutable lookup-reference/key authority;
- wrong-tenant secret metadata, missing and unknown persisted references, wrong/replaced key bytes,
  a cross-tenant/cross-row protected envelope, malformed envelope shape and tag tampering; and
- absence of raw GUID forms/bytes, display UTF-8, raw keys, Base64 keys and complete secret material
  across protected database byte fields, payloads, canonical E20 envelopes, audit sink records,
  customer-visible projections, serialized evidence and exception text.

All failed application operations returned no protected identity, created no additional PersonKey,
appended no `PersonKeyMapChanged` event and left the authoritative row unchanged.

## Verification results

| Gate                                     | Result                                                                   |
| ---------------------------------------- | ------------------------------------------------------------------------ |
| Release restore/build                    | Passed; 0 warnings, 0 errors                                             |
| Expanded P1-T08 hostile PostgreSQL suite | 12/12 passed                                                             |
| Full PostgreSQL adapter suite            | 26/26 passed                                                             |
| Full backend solution                    | 253/253 passed                                                           |
| Standalone architecture gate             | 15/15 passed                                                             |
| SPA build                                | Passed                                                                   |
| SPA tests                                | 114/114 passed across 13 files                                           |
| NuGet vulnerable-package scan            | 0 vulnerable packages                                                    |
| npm shipped-production audit             | 0 vulnerabilities                                                        |
| Prettier Markdown/YAML/JSON check        | Passed                                                                   |
| Task-contract validation                 | 28 contracts; 0 errors, 0 warnings                                       |
| Protected-path guard                     | Passed; no blueprint/approval changes                                    |
| DEV-001 production-readiness guard       | Passed                                                                   |
| Secret scans                             | 51-commit history and 6.32 MB final working tree scanned; no leaks found |
| Diff whitespace check                    | Passed                                                                   |

`npm ci` reported five advisories in the pre-existing development/build-tool graph. The repository's
shipping gate intentionally runs `npm audit --omit=dev --audit-level=high`; that exact gate passed
with zero production vulnerabilities. P1-T08 changes no npm dependency or web file.

## Independent final reviews

Three final read-only reviews were performed after the fixes:

- migration/verifier/fixture and PostgreSQL security boundary: no remaining finding;
- E18/E19 adapter semantics, transactions and task-contract scope: no remaining finding; and
- field protection, buffer hygiene and hostile leakage coverage: no remaining finding.

Previously reported findings for tenant-wide lookup scans, mutable lookup-key authority,
catalog-fingerprint gaps, pre-commit tenant switching, duplicate event-ID classification, early
audit error leakage, shared-pool starvation, timestamp representation, mutable/uncleared protection
buffers, create-before-lookup ordering and hostile/leak-test gaps are closed.

## Change-to-contract accounting

Every P1-T08 change is required by the approved task contract:

- the solution and Trust PostgreSQL project provide the required outer adapters;
- the four Trust/in-memory files share only the required frozen E18/E19 validation semantics;
- migration 0002 and its rollback/verifier/validation files provide the required durable objects
  and safe ephemeral lifecycle;
- PostgreSQL and architecture tests prove the required migration, isolation, concurrency,
  atomicity, protection, severance and dependency boundaries; and
- this evidence, build state, substitute registry, task status and `STATUS.md` reconcile the
  required build-control record.

There are no new capabilities, bounded contexts, production security controls, dependencies or
refactors outside that contract. Npgsql remains exactly 10.0.3 and isolated to PostgreSQL adapters
and tests.

## Approval boundary

P1-T08 is complete and green. There are no implementation failures or technical blockers.

No pull request was opened or merged. No later task started. No frozen blueprint, Microsoft tenant,
cloud resource, production credential, shared database, staging database or production database was
accessed or changed.

Product Owner approval is now required before any pull request, merge or subsequent task.

## GitHub CI trust-boundary result

PR #28 was opened from commit `935a9fa` on 2026-07-24. Eight of nine CI workflows passed:
architecture, dependency scan, format, production readiness, protected paths, secret scan,
task-contract validation and SPA build/tests.

The combined `build-test` workflow failed with 252 of 253 backend tests passing. Migration 0002 and
all 26 PostgreSQL adapter tests passed in the approved disposable PostgreSQL service. The sole
failure was:

`ControlTower.Host.Web.Tests.RoleAuthorizationTests.Development_store_rejects_mismatched_assignment_audit_evidence`

The test constructs E18 state/event time directly from `DateTimeOffset.UtcNow`. Linux supplied
sub-microsecond ticks, so the P1-T08 shared semantic validator correctly rejected the tuple before
the test's injected event-store failure. The test expected the older `NotSupportedException` path.
Local macOS clock resolution did not expose the stale fixture.

The correct production invariant is already implemented and directly required by P1-T08: every E18
aggregate and event timestamp must be exact UTC microseconds. Weakening or bypassing that invariant
would violate the contract. The minimum correction is to normalize the timestamp fixture in
`tests/ControlTower.Host.Web.Tests/RoleAuthorizationTests.cs`.

That file is explicitly forbidden by the approved P1-T08 contract. No allowed-file change can
correct the stale test without weakening required production behavior. This is therefore the
mandatory human gate defined by the build constitution: the task cannot complete without an exact
scope amendment.

### Merge Readiness Report

- Blueprint alignment: aligned; no frozen file changed.
- ADR compliance: aligned with ADR-015/021 timestamp-integrity and evidence requirements.
- Tests: local 253/253 backend and 114/114 SPA; PR CI 252/253 backend.
- CI status: eight workflows green; `build-test` red on the single Host test above.
- Security status: production fail-closed timestamp validation is correct and must remain.
- Architecture status: 15/15 architecture tests and the architecture CI workflow are green.
- Technical debt introduced: none.
- Known risk: merging while CI is red would bypass the repository trust boundary.
- Deviation requested: none. Exact task-scope amendment required for one test file.
- Merge recommendation: **do not merge** until the one-file scope amendment is approved, corrected
  and all CI gates are green.
