---
id: EVIDENCE-P1-T07
type: evidence-bundle
schema_version: 1
task: P1-T07
status: submitted
produced_at: 2026-07-24T10:30:08Z
---

## Task

Persist the frozen E20 event contract behind the unchanged `IEventStore` port, author migration
0001, and prove the durable kernel's atomicity, tenant isolation and append-only behavior only
against an approved disposable PostgreSQL container.

## Approval and bounded scope

The Product Owner explicitly approved P1-T05 through P1-T08 on 2026-07-23, including event-backbone
and C8 interface changes, Npgsql 10.0.3, ephemeral `postgres:16.14-alpine3.24` testing, and authoring
plus green-CI merge of migrations 0001/0002. Shared, staging and production migration execution was
explicitly excluded.

P1-T07 changes only the PostgreSQL adapter, migration 0001 and its guarded rollback/verification,
the disposable database test harness, permanent adapter-boundary tests, the build workflow and
build-control records. It does not change Platform, a bounded context, Host composition, tenant
configuration, infrastructure, cloud resources or production deployment.

The approval provenance is explicit, but no exact P1-T07 contract artifact existed when the broad
P1-T05–P1-T08 approval was given. `approved_hash` therefore remains `null`; this evidence does not
fabricate a pre-approval digest.

## Blueprint and decision trace

- Stage 5 E20 defines the immutable domain-event record and its complete semantic metadata.
- Stage 8 §§3, 13 and 14 require tenant scoping by construction, verifiable evidence integrity and
  first-class administrative/security events.
- Stage 9 §§3.1, 3.3 and 4.2 select PostgreSQL, an append-only hash-chained event table and forced
  row-level tenant isolation.
- ADR-015 makes the event record the audit trail; ADR-021 requires no cross-tenant enumeration and
  integrity-verifiable evidence; ADR-023 keeps PostgreSQL-specific behavior inside an adapter.
- DEC-001 resolves the database target to Azure Database for PostgreSQL Flexible Server and permits
  disposable local Docker PostgreSQL as a development-only substitute.

RLS and immutable-row triggers are measurable security controls expressly required by Stage 9 and
remain confined to the PostgreSQL adapter/migration. No blueprint deviation or new bounded context
is introduced.

## Durable E20 adapter

- `ControlTower.Adapters.PostgreSql` references only Platform and Npgsql 10.0.3. Platform and all
  modules remain free of Npgsql and concrete-adapter dependencies.
- `PostgreSqlEventStore` captures opaque tenant authority only from `ITenantContextAccessor` before
  caller-controlled event getters or database I/O, binds it through transaction-local `set_config`,
  and commits only after a complete event plus stream-head advance succeeds.
- The adapter snapshots event identity, contract, occurred time, metadata and owned payload bytes
  once before its first database wait. A hostile event getter cannot retarget the append: any
  ambient change is rejected before tenant SQL and both candidate tenant streams remain unchanged.
  Store-controlled recorded time is captured only after obtaining the tenant stream lock, so
  position order and recorded order cannot invert under contention.
- Commit becomes non-cancellable after one final cancellation check. A cancellation exception can
  therefore never ambiguously accompany an event that the method committed.
- Server integrity/RLS failures are translated to one bounded `EventIntegrityException` without a
  PostgreSQL constraint, relation, tenant, event or provider-detail inner exception.
- Complete E20 format-2 rows rehydrate actor, aggregate, reason, correlation, privilege, timestamps,
  hashes and exact binary payload bytes. The production `HashChainVerifier` validates the reopened
  stream against a separately retained test checkpoint.
- The public adapter-only composition seam is bounded by an opaque `PostgreSqlTenantCapture`, a
  non-owning `PostgreSqlTenantTransaction`, an immutable `PostgreSqlEventAppendRequest` and
  `PostgreSqlEventTransactionAppender`. It accepts no caller-supplied tenant ID, commit or rollback
  authority. P1-T08 can therefore persist E18/E19 state and its E20 evidence atomically from a
  separate Trust PostgreSQL adapter without changing Platform or duplicating event SQL/hash logic.

## Migration 0001

`0001_event_kernel.sql` creates:

- `event_store.domain_events`, with a global event-ID primary key, tenant-local position uniqueness,
  complete bounded E20 columns, format/UUID/actor/correlation/privilege/time/hash checks and exact
  `bytea` payload storage;
- `event_store.stream_heads`, with one tenant row whose next position and last hash are internally
  consistent;
- forced RLS on both tenant-bearing tables;
- seven policies covering the trusted read/append path and the owner-only mutation path needed to
  prove trigger defense in depth;
- row-level update/delete and statement-level truncate rejection for committed event rows;
- fixed-search-path, migration-owner `SECURITY DEFINER` functions that lock a tenant head and
  atomically insert one event plus advance that head;
- runtime grants limited to schema usage, event selection and those functions. Database-default
  `PUBLIC` temporary-object creation is revoked. The runtime owns no object, belongs to no upward
  role and has no table insert/update/delete/truncate, DDL, `SUPERUSER`, `BYPASSRLS`, role, database
  or replication capability.

The schema intentionally avoids an arbitrary fixed hash-partition count and a second event-ID
registry. Physical partitioning/archival is deferred until the R-28 volume and retention work can
choose a measured time/tenant strategy without weakening global identity or direct-child
immutability.

## Migration lifecycle and safety

- The CI service image is pinned exactly to `postgres:16.14-alpine3.24` and authenticates with a
  per-run ephemeral password. Trust authentication is forbidden by a permanent test.
- The fixture refuses every non-loopback host, multiple hosts, a non-`postgres` admin database,
  missing credentials or a missing/incorrect P1-T07 ephemeral sentinel.
- It verifies server version 16.14 and refuses a pre-existing fixed runtime role before creating
  one generated non-privileged migration-owner role, the run-owned fixed runtime role and one
  generated `ct_p1_t07_<hex>` database. It never logs a connection string or parameter value.
- It applies migration 0001, runs the executable catalog/privilege verification, captures a
  canonical schema fingerprint, sets an explicit rollback guard, rolls back by dropping the schema,
  proves absence, re-applies the same forward bytes, re-verifies and compares the fingerprint
  exactly.
- The rollback script rejects every database outside the generated P1-T07 name pattern and every
  session lacking `control_tower.ephemeral_migration_guard=P1-T07-EPHEMERAL-ONLY`.
- Teardown disposes pools, terminates only connections to the generated database and drops only the
  database and roles whose creation this fixture completed. Partial initialization or a name
  collision therefore cannot delete a pre-existing cluster object. No shared, staging or
  production endpoint is accepted.

Migration ledger state at merge:

| Environment                    | 0001 state                                         |
| ------------------------------ | -------------------------------------------------- |
| Disposable CI PostgreSQL 16.14 | apply/verify/rollback/reapply passed               |
| Local Docker                   | not executed; daemon awaited macOS helper approval |
| Shared development/test        | never executed                                     |
| Staging                        | never executed                                     |
| Production                     | never executed                                     |

## Adversarial verification

The PostgreSQL suite proves:

- exact E20 field and binary-payload round trip across a reopened store;
- event identity and occurrence getters are read once, payload ownership is detached before a
  blocked connection wait, and a getter-triggered ambient tenant switch fails with zero cross-tenant
  state;
- Human, System and Provider opaque actors all rehydrate through Platform validation;
- 40 simultaneous same-tenant writers receive positions 1–40 with one valid chain;
- simultaneous two-tenant writers each begin at position 1 and can read only their own chain;
- a cross-tenant global event-ID collision returns one generic error and consumes no position;
- a failure injected after event insertion but during stream-head update rolls back both;
- empty IDs, undeclared contracts, null metadata and pre-cancellation mutate no row;
- cancellation while blocked on the tenant head rolls back and the next success remains position 1;
- missing/malformed database tenant context, cross-tenant selection and a forged function tenant
  fail closed;
- a transaction scope can be rebound idempotently only to the same captured tenant; ambient
  switching, tenant rebinding, missing ambient scope and connection/transaction mismatch all fail,
  and the caller-owned rollback removes the earlier uncommitted append;
- transaction-local tenant context does not survive commit, rollback, cancellation or forced
  single-connection pool reuse;
- runtime update/delete/truncate/trigger-disable and temporary-table creation attempts fail by ACL,
  while migration-owner update/delete/truncate attempts reach and fail the immutable triggers;
- direct inserts prove the database rejects eight E20 shapes that cannot be rehydrated by Platform;
- SQL-looking legal event/reference/provider/payload values remain literal parameter data;
- a separately retained checkpoint upgrades the production verifier result from internally intact
  unanchored to checkpoint-bound in the test. No WORM-anchor claim is made.

## Acceptance criteria to result

| Criterion                                                               | Result |
| ----------------------------------------------------------------------- | ------ |
| Unchanged `IEventStore` has a durable Npgsql 10.0.3 implementation      | PASS   |
| Complete E20 format 2 persists and rehydrates exactly                   | PASS   |
| Same-tenant concurrency is contiguous, atomic and hash-verifiable       | PASS   |
| Different tenants cannot enumerate or mutate each other's rows          | PASS   |
| Runtime is non-owner, least-privilege and cannot mutate tables directly | PASS   |
| Event rows reject update, delete and truncate even for the owner        | PASS   |
| Migration apply/verify/rollback/reapply has no catalog drift            | PASS   |
| Unsafe endpoints and missing/malformed tenant context fail closed       | PASS   |
| Npgsql remains outside Platform/modules and adapter direction is green  | PASS   |
| No shared, staging, production, tenant or Azure action occurred         | PASS   |

## Verification

```text
dotnet build ControlTower.sln -c Release
  Build succeeded. 0 Warning(s). 0 Error(s).

dotnet test ControlTower.sln -c Release --no-build --verbosity normal
  The local guard-inactive run reports 240 passed, but 13 database facts return before assertions
  without the explicit ephemeral marker. Therefore 227 tests actively execute locally; CI below is
  the authoritative database proof.

GitHub Actions build-test run 30086233960
  PostgreSQL adapter: 14/14 passed against postgres:16.14-alpine3.24, comprising 13
  real-database integration tests and one permanent workflow guard.
  Platform 31/31; Ledger 31/31; Governance 19/19; Economics 23/23;
  Providers 29/29; Architecture 14/14; Host.Web 79/79.
  Total backend: 240 passed, 0 failed.
  Build succeeded. 0 Warning(s). 0 Error(s).

GitHub Actions web run 30086233944
  Typecheck and production build PASS; 13 files and 114 tests passed.

python3 scripts/ci/validate_task_contracts.py
  checked 27 task contracts; 0 errors, 0 warnings.

bash scripts/ci/check_production_readiness.sh
  OK: no dev-substitute references in production paths (DEV-001).

bash scripts/ci/validate_protected_paths.sh
  OK: no protected-path modifications.

dotnet list ControlTower.sln package --vulnerable --include-transitive
  No vulnerable NuGet packages, including Npgsql 10.0.3.

dotnet format ControlTower.sln --verify-no-changes --no-restore
  [no output; exit 0]

npx --yes prettier@3 --check "**/*.{md,yml,yaml,json}"
  All matched files use Prettier code style.

git diff --check
  [no output; exit 0]

GitHub Actions pull request checks
  architecture PASS; build-test PASS; dev-substitute-guard PASS; format PASS;
  gitleaks PASS; protected-paths PASS; validate PASS; vulnerable-packages PASS; web PASS.
```

Earlier pre-green branch runs identified Markdown formatting, migration-parser and
catalog-fingerprint issues. Subsequent green review exposed snapshot, tenant-switch, cleanup and
least-privilege gaps, all of which were closed and exercised by the authoritative run above. No
shared database was involved in any iteration.

## Independent review

- Architecture: PASS. The opaque tenant-capture/request/transaction/appender seam preserves
  dependency direction, snapshot-before-I/O semantics and a separate P1-T08 outer adapter without
  a new Platform abstraction or duplicated E20 behavior.
- Security/database: PASS. Tenant-switch attribution, database/ambient rebind, runtime `TEMPORARY`,
  upward membership, forced RLS, security-definer paths, immutable rows and fixture-owned cleanup
  have no remaining P0/P1 blocker.
- Test/evidence: PASS. The final suite covers all contract and adversarial cases; local guard-inactive
  versus CI real-database counts are distinguished explicitly.

## Explicitly deferred

P1-T07 does not wire the adapter into Web or Worker, persist an outbox, anchor checkpoints to WORM
Blob, implement event retention/archival partitions, persist E18/E19, configure Azure PostgreSQL,
create infrastructure or execute any shared migration. The existing Worker's unconditional
development-adapter registration remains an explicit production-composition hardening gap.

## Merge readiness report

- Blueprint alignment: PASS; this is the durable adapter for the existing C9/E20 capability.
- ADR compliance: PASS; one event model/port is preserved, RLS and triggers stay in the adapter,
  and no bounded context or topology changes.
- Tests passed: PASS; 240 backend including 13 real PostgreSQL integration tests and one workflow
  guard, plus 114 SPA tests.
- CI status: PASS; all nine pull-request checks green on run set headed by build-test 30086233960.
- Security status: PASS; least privilege, forced RLS, immutable rows, context reset and generic
  failure behavior are executable.
- Architecture status: PASS; 14/14 permanent architecture tests and Npgsql boundary checks.
- Technical debt introduced: physical event partitioning and WORM anchoring remain later measured
  durability work; no substitute or compatibility shortcut was added to a production path.
- Known risks: no exact pre-approval P1-T07 contract hash exists; approval is explicit and no digest
  was fabricated. Host production composition remains incomplete and unchanged.
- Deviations requested: none.
- Merge recommendation: merge after this evidence/state completion commit retains all nine green
  checks.

## CI

Pull request: https://github.com/arunanderson/Control-Tower/pull/27

Authoritative database run:
https://github.com/arunanderson/Control-Tower/actions/runs/30086233960

Authoritative web run:
https://github.com/arunanderson/Control-Tower/actions/runs/30086233944

## Rollback

Revert the P1-T07 PR. Migration 0001 has been executed only inside disposable CI databases that
were dropped by the fixture; there is no persistent datastore or external state to roll back.
