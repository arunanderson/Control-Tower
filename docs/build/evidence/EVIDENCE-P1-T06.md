---
id: EVIDENCE-P1-T06
type: evidence-bundle
schema_version: 1
task: P1-T06
status: submitted
produced_at: 2026-07-24T09:12:13Z
---

## Task

Complete the frozen blueprint's E20 semantic event record and the existing C8 E18/E19 contracts
before their first permanent PostgreSQL schemas are authored.

## Approval and bounded scope

The Product Owner explicitly approved P1-T05 through P1-T08 on 2026-07-23, including event-backbone
and C8 interface changes, Npgsql 10.0.3, ephemeral `postgres:16.14-alpine3.24` testing, and authoring
plus green-CI merge of migrations 0001/0002. Shared, staging and production migration execution was
explicitly excluded.

P1-T06 introduced no package, database, migration, container, infrastructure, cloud resource,
credential, secret, tenant configuration or deployment. Its base is merged `main` commit
`7154f442db7dfe220fff451412ffadc56189a9f4`.

The approval provenance is explicit, but no P1-T06 contract artifact was committed before that
approval. `approved_hash` therefore remains `null`; this evidence does not fabricate a
pre-approval digest.

## Blueprint and decision trace

- Stage 4 §§3, 5 and 9 require shared typed value objects, the canonical event set and one event
  record as the audit trail.
- Stage 5 E19 makes `PersonKeyMap` the sole raw-directory-identity and GDPR severance point; E20
  requires aggregate, opaque actor, optional reason/correlation and immutable privileged-read
  evidence.
- Stage 7 §§7 and 10 keep C8/C9 behind Platform contracts inside the modular monolith and reject
  duplicated event or identity models.
- Stage 8 §§3, 9, 11, 13 and 14 require tenant isolation, an internal privileged perimeter,
  pseudonymous person references, integrity-covered administrative evidence and fail-closed
  release.
- ADR-015 keeps one append-only audit stream; ADR-020 keeps existing bounded contexts and Host as
  composition root; ADR-021 supplies tenant/privacy/integrity constraints; ADR-023 remains
  unchanged because no technology or infrastructure was introduced.

## Completed E20 contract

- `EventAppendMetadata` requires one bounded aggregate reference, one typed `AuditActor`, and
  explicitly present-or-absent bounded reason and correlation reference.
- `IEventStore` exposes only the complete metadata append operation and tenant-scoped read; no
  metadata-free overload, update or delete path exists.
- `StoredEvent` persists aggregate, actor, reason and correlation, and integrity format 2 covers
  them independently of caller-owned payload bytes.
- The canonical format uses fixed field order, network-order scalars, RFC 4122 GUID bytes, strict
  UTF-8, UTC microseconds, explicit nullable-presence bytes and bounded length prefixes. Formats
  0, 1 and unknown fail closed.
- Every existing producer now supplies an explicit aggregate, actor, reason and correlation
  mapping. Invalid producer evidence is constructed before state mutation or append.
- Provider observation ingestion rejects a raw observation whose surface identity differs from
  the admitted manifest before observation or delta state changes.

The executable format-2 vector is:

```text
Envelope:
00000002000000000000000100112233445566778899AABBCCDDEEFF
FFEEDDCCBBAA9988776655443322110000000009546573744576656E74
0000000E746573742D616767726567617465
0000000D6167677265676174652D303031
030000000B70726F76696465722D3031
000000000012D687FFFFFFFFFFF0BDC1
0100000003776879
010000000772657175657374000000076162632D313233
000000000300FF41

Genesis hash:
A3490E9C641477F7869E10F8A01710F4E1F51E11F0A9BA86EA27C79F0F8FAA5B

Same envelope chained after that binary digest:
A2B40DB499E3559CEC0BE68F161D29ED524BCE0F5C82510D869D1D5C7D48087C
```

## Shared opaque identity

- Platform owns the only `PersonKey` and `AuditActor` value objects.
- A human actor structurally requires `PersonKey`; the public raw
  `(AuditActorKind, string)` construction path no longer exists.
- System/provider actors are bounded, strict-Unicode workload identities and reject email, Entra
  prefixes, embedded D/N GUIDs and other obvious directory-identity forms.
- Provider surface identifiers such as `provider:custom/surface` retain their exact canonical
  value while raw directory-shaped actor values fail closed.
- Permanent architecture tests scan all public production actor use sites, reject alternate
  actor-shaped types, require typed `*PersonKey` state and permit raw directory/display identity
  only in the explicit E19 boundary.
- `RoleAssignmentChanged`, `PersonKeyMapChanged`, `RoleAssignment` and `PersonRef` now carry the
  shared `PersonKey` type rather than an untyped person GUID.

## E19 privileged identity boundary

- `DirectoryIdentitySnapshot` is the only model that carries raw directory object identity and an
  optional bounded display snapshot.
- Every find, get, create, existing-create, sever, repeated-sever, conflict and cross-tenant
  operation records immutable C9 evidence before returning protected data.
- Successful create and sever mutations append one privileged `PersonKeyMapChanged` event before
  state mutation.
- The bidirectional development map serializes operations, yields one key/event under concurrent
  creation, removes both raw-identity directions in O(1), retains only a non-personal tombstone and
  creates a different key on remap.
- Tenant A and tenant B receive unrelated keys for the same raw identity; foreign lookup/severance
  returns the same generic absence as a nonexistent mapping.
- Purpose, actor, correlation and policy context reject raw directory GUID/display injection before
  evidence or protected-value release.
- Tests search serialized audit records, projections, API responses, event payload/metadata and the
  canonical integrity byte spans for raw D/N directory IDs and display snapshots.

## One non-bypassable C9 evidence path

- `PrivilegedReadRecord` preserves access ID, tenant, typed actor, resource, purpose, explicit
  policy applicability/version, correlation and normalized occurrence time.
- `PrivilegedReadEvidenceAuditor` appends `PrivilegedReadRecorded`, then stores the immutable
  record, then updates the customer-visible projection. A failure at an earlier step releases or
  projects nothing.
- E19 depends only on the high-level `IPrivilegedReadAuditor`; the lower-level record sink is
  adapter-owned and used only by C9 composition.
- The generic in-memory high-level registration deliberately throws. It cannot become an
  evidence-less fallback. Development Web replaces it with the complete C9 auditor and uses the
  in-memory type only as the post-event sink.

## E18 authorization lifecycle

- `RoleAssignment` is tenant-scoped and rehydrates only valid active version 1 or revoked version 2
  state.
- Assignment and revocation commits are typed, optimistic and atomic at the development adapter
  seam; there is no service-level check-then-write race.
- Concurrent identical grants return one authoritative active assignment and append one event.
  Concurrent/stale revocations append at most one event and return an authoritative idempotent or
  typed conflict result.
- Assignment state, event body, actor, aggregate reference, correlation and expected version must
  agree before append. Event failure leaves assignment state unchanged.
- Host resolves the request's raw directory identity to `PersonKey` once, carries that key in
  effective access and derives every persisted human actor from the server-resolved key.

## Fail-before-mutation producer review

Hostile invalid metadata tests cover Ledger registration/resolution/merge, Governance commands,
Economics cost/value/reporting, C9 legal holds and C4 observation/sweep paths. Aggregate,
correlation, actor, reason and event objects are validated before domain/repository mutation;
event-store failure retains the prior authoritative state.

## Acceptance criteria to result

| Criterion                                                                    | Result |
| ---------------------------------------------------------------------------- | ------ |
| Complete E20 metadata is persisted and independently integrity-covered       | PASS   |
| Null/present fields and all direct size boundaries are unambiguous           | PASS   |
| One typed actor and PersonKey model; raw Entra actor shapes fail closed      | PASS   |
| E19 lifecycle is tenant-safe, audited, severable, idempotent and concurrent  | PASS   |
| Audit/event failure releases no protected identity or partial E19 mutation   | PASS   |
| Raw directory identity is absent from all tested evidence/output forms       | PASS   |
| E18 versioning, authoritative idempotence and optimistic conflict are proved | PASS   |
| Existing module, Host, architecture and SPA behavior remains green           | PASS   |
| No database, migration, dependency, tenant or infrastructure action occurred | PASS   |

## Verification

```text
dotnet restore ControlTower.sln
  All projects are up-to-date for restore.

dotnet build ControlTower.sln -c Release --no-restore
  Build succeeded. 0 Warning(s). 0 Error(s).

dotnet test ControlTower.sln -c Release --no-build --no-restore --verbosity minimal
  Platform 31/31; Ledger 31/31; Governance 19/19; Economics 23/23;
  Providers 29/29; Architecture 12/12; Host.Web 79/79.
  Total backend: 224 passed, 0 failed.

npm --prefix web run typecheck
  TypeScript completed with no errors.

npm --prefix web test -- --run
  13 test files passed; 114 tests passed; 0 failed.

npm --prefix web run build
  187 modules transformed; production bundle built successfully.

PATH=/Users/arunanderson/.dotnet:$PATH bash scripts/ci/architecture_gate.sh
  12/12 passed.

python3 scripts/ci/validate_task_contracts.py
  checked 26 task contracts; 0 errors, 0 warnings.

bash scripts/ci/check_production_readiness.sh
  OK: no dev-substitute references in production paths (DEV-001).

bash scripts/ci/validate_protected_paths.sh origin/main
  OK: no protected-path modifications.

dotnet list ControlTower.sln package --vulnerable --include-transitive
  No vulnerable NuGet packages.

npm --prefix web audit --omit=dev --audit-level=high
  found 0 vulnerabilities.

dotnet format ControlTower.sln --verify-no-changes --no-restore
  [no output; exit 0]

npx --no-install prettier --check "**/*.{md,yml,yaml,json}"
  All matched files use Prettier code style.

git diff --check
  [no output; exit 0]

targeted credential/private-key scan across changed files
  [no sensitive-value match; gitleaks remains the authoritative PR check]
```

## Independent review

- Architecture reviewer: clean after the generic C9 adapter was made fail-closed and structural
  actor/person-reference gates replaced name-only assertions.
- Security reviewer: clean after provider manifest attribution and the stale raw actor construction
  paths were closed.
- Test reviewer: clean after nullable presence, exact workload/reference/reason boundaries, full
  privileged-record/API preservation and direct canonical-byte identity checks were added.

## Explicitly deferred

P1-T06 freezes the tenant-independent contracts that P1-T07/P1-T08 will persist. It does not claim a
durable event/E18/E19 store, RLS, field protection, transactional outbox, WORM anchor, production
composition or tenant activation. No migration exists or was executed in this task.

## Merge readiness report

- Blueprint alignment: PASS; E18, E19 and E20 now match the frozen model without a new context.
- ADR compliance: PASS; one event/audit path, one actor/person model and Host composition are
  preserved.
- Tests passed: PASS; 224 backend and 114 SPA.
- CI status: pending branch publication.
- Security status: PASS locally; PR gitleaks remains authoritative.
- Architecture status: PASS; 12/12 architecture tests and independent review clean.
- Technical debt introduced: the generic in-memory auditor retains a legacy high-level
  registration, but it is permanently fail-closed and cannot release protected data. A later
  approved adapter-registration cleanup may remove that compatibility seam.
- Known risks: no pre-approval P1-T06 contract hash exists; approval scope is evidenced, and no
  provenance was fabricated. Durable security properties remain explicitly deferred to P1-T07/T08.
- Deviations requested: none.
- Merge recommendation: merge when all required PR checks are green.

## CI

Pull request checks: https://github.com/arunanderson/Control-Tower/pull/26/checks

## Rollback

Revert the P1-T06 PR. No persistent data, migration, tenant, infrastructure or production
configuration changed.
