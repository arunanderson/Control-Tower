---
id: EVIDENCE-P1-T09
type: evidence-bundle
schema_version: 1
task: P1-T09
status: submitted
produced_at: 2026-07-24T23:41:52Z
---

## Task

Establish the frozen E16 jurisdiction-profile and E17 telemetry-policy authorities before either
privacy gate is connected. E16 is simple versioned event history owned by C5. E17 is bitemporal,
privileged policy owned by C8. Existing C4 ingestion continues to assign immutable L1 markings by
default through the one shared Platform type.

This is a tenant-independent code-contract and development-adapter slice. It creates no Gate 1 or
Gate 2 enforcement, Host composition, persistence, migration, production policy, taxonomy, HR
mapping, tenant action or cloud resource.

## Approval and contract correction

The original contract was automatically approved under the Product Owner's 2026-07-24 instruction
with draft SHA-256
`e3476cbd14dc7c100f573d405f01c3dafce7a7e7353568caa208a6ea52a42a6a`.

Independent review then found that its requested E16 valid-time/record-time model contradicted the
frozen blueprint. Stage 4 §8 limits bitemporality to a named set that excludes E16, and the Stage 5
catalogue classifies E16 as **Versioned** while E17 is **Bitemporal**. The Product Owner approved the
exact correction on 2026-07-25 and gave standing authority to resolve task-contract/blueprint
conflicts in favor of the frozen blueprint without modifying it.

The final amended contract:

- makes E16 simple versioned event history with no valid-time, record-time or as-of API;
- keeps E17 bitemporal and privileged;
- allows exactly
  `docs/build/deviations/DEV-003-p1-t09-e16-temporal-contract-correction.md` to satisfy the
  constitution's anti-silent-change record; and
- has reproducible draft/null SHA-256
  `3a1068c5a4272648c42a2a84d7e6d50891958c954dd22df86a12e771a984f0df`.

DEV-003 narrows the incorrect task contract to the frozen model. It is not a blueprint or ADR
deviation and requires no PD-006 revision.

## Blueprint, ADR and RTM trace

- Stage 4 §§2.7 and 8 assign `JurisdictionProfile` to C5 as simple versioned event history.
- Stage 4 §2.8 assigns effective-dated full-history `TelemetryPolicy` to C8 and requires privileged
  policy-change events.
- Stage 5 E16/E17 catalogue and E17 detail require versioned E16, bitemporal E17, jurisdiction and
  population scopes, capability toggles, exact change evidence and rejection above the E16 ceiling.
- Stage 8 §§6, 9 and 12 require L1 default, jurisdiction/population-aware most-restrictive
  resolution, the C8 privileged boundary, tenant isolation and the two privacy gates.
- ADR-003 and ADR-014 define L1-L4 and read-time re-masking.
- ADR-015 makes canonical domain events the audit trail.
- ADR-017 supports the opaque retention-policy evidence required for enabled L2+ rules.
- ADR-020 and ADR-023 preserve module boundaries and keep implementation dependencies outside the
  core.
- ADR-021 requires L1 default, explicit L2+ activation, time-limited L4, no cross-tenant
  enumeration, privacy storage refusal and verifiable evidence.
- BR-09, BR-10 and BR-15 trace configurable telemetry privacy, evidence-grade audit and tenant
  isolation.

No frozen file changed and no new bounded context or architecture was introduced.

## Delivered implementation

### Shared Platform privacy contracts

- Platform now owns the sole `PrivacyMarking` enum with the original integer values L1=0 through
  L4=3.
- Opaque bounded `JurisdictionRef`, `PopulationRef`, `TelemetryCapabilityRef` and
  `RetentionPolicyRef` value types contain no production taxonomy or identity.
- `PrivacyApplicability` defensively owns canonical jurisdiction and population sets.
- `IJurisdictionCeilingResolver` is a tenant-ambient, current-only C5 port. Missing authority can
  return only non-authoritative L1 with bounded partial version evidence.
- The exact C4 Provider paths only import the shared type. Ingestion behavior and immutable L1
  default are unchanged.

### C5 E16 jurisdiction profiles

- `JurisdictionProfile` is an immutable tenant, jurisdiction and version revision with a current
  telemetry ceiling, opaque regime markers, one normalized change/event time, actor and bounded
  reason.
- It deliberately has no `ValidFrom`, `ValidTo`, `RecordedAt`, `IsEffectiveAt` or as-of query.
- The store supports exact-version, current and complete immutable history reads, one aggregate per
  tenant/jurisdiction, monotonic versions/change times and bounded optimistic conflicts.
- Current ceiling resolution requires every applicable jurisdiction, uses the minimum current
  ceiling and fails closed to L1 when any authority is missing.
- `JurisdictionProfileChanged` is a standard canonical event containing the complete revision.

### C8 E17 telemetry policy

- `TelemetryPolicyRevision` preserves independent valid and record time, exact history, actor,
  bounded justification, deterministic fingerprint and immutable canonical rule order.
- Rules support capability, jurisdiction and population scopes. Enabled L2+ requires explicit
  purpose, approval and retention evidence. L4 additionally requires an explicit time limit inside
  the policy interval.
- Every rule is rejected at commit when it exceeds the current E16 ceiling, including disabled and
  non-first rules. Missing E16 authority is L1.
- Stale/concurrent writes return a bounded authoritative conflict before any ceiling dependency is
  called.
- Resolution evaluates the conservative jurisdiction × population applicability product. Every
  uncovered cell contributes enabled L1; global scopes cover all cells; any applicable disabled
  rule wins; otherwise the minimum rule level is selected and then clamped by current E16.
- `TelemetryPolicyChanged` is privileged and defensively owns the complete canonical policy-rule
  snapshot as its E20 payload, plus the fingerprint and rule count.

### Atomic development adapters

Both stores:

- capture and validate one non-default ambient tenant on every public operation;
- reject missing, malformed, foreign or pre-append switched tenant context;
- perform tuple, version, ceiling and concurrency validation before append;
- prepare a complete copy-on-write replacement state, serialized payload and result before append;
- publish state only after a successful event append through a non-throwing reference swap; and
- treat successful `IEventStore.AppendAsync` as the irreversible development commit point, so even
  a hostile dependency that changes ambient context after storing the event cannot create an
  event-without-state divergence.

Append failure leaves the prior state snapshot unchanged. No distributed-transaction claim is made;
future durable adapters must use their database transaction behind the same ports.

## Verification results

| Gate                                   | Result                                                 |
| -------------------------------------- | ------------------------------------------------------ |
| Release restore/build                  | Passed; 0 warnings, 0 errors                           |
| Full backend solution                  | 303/303 passed; 0 failed/skipped                       |
| C5 E16 focused suite                   | 14/14 passed                                           |
| C8 E17 focused suite                   | 34/34 passed                                           |
| Provider regression suite              | 29/29 passed                                           |
| Permanent architecture tests           | 17/17 passed                                           |
| Exact disposable PostgreSQL suite      | 26/26 passed against fresh `postgres:16.14-alpine3.24` |
| SPA build                              | Passed; 187 modules                                    |
| SPA tests                              | 114/114 passed across 13 files                         |
| NuGet vulnerable-package scan          | 0 vulnerable packages                                  |
| npm shipped-production audit           | 0 vulnerabilities                                      |
| P1-T09 C# format verification          | Passed for every changed C# path                       |
| Prettier Markdown/YAML/JSON            | Passed                                                 |
| Task-contract validation               | 29 contracts; 0 errors, 0 warnings                     |
| Protected-path guard                   | Passed; no blueprint/approval changes                  |
| DEV-001 production-readiness guard     | Passed                                                 |
| Diff whitespace checks                 | Passed                                                 |
| Independent semantic/security review   | No remaining production finding                        |
| Independent test-contract review       | All reported gaps closed                               |
| Independent final scope/process review | 25/25 paths authorized; no remaining finding           |
| GitHub PR #29 workflows                | 9/9 passed against commit `1522c24`                    |

The full backend count is the sum of the ten test assemblies:

- Platform 31, Architecture 17, Host 79, Ledger 31, Governance 19, Economics 23, Providers 29,
  PostgreSQL 26, EnterpriseContext 14 and Trust 34.

The PostgreSQL suite used only a fresh loopback container with a generated disposable password. It
re-exercised migrations 0001/0002 and the existing hostile durable-store suite; P1-T09 authors or
executes no migration. No shared, staging or production database was contacted.

`npm ci` reports five advisories in the pre-existing development/build-tool graph. The shipped
production graph is the repository gate and passed with zero vulnerabilities. P1-T09 changes no
web or npm file.

The repository's `format` workflow is the Prettier gate above. An additional changed-path
`dotnet format --verify-no-changes` passed. A full-solution dotnet-format diagnostic, which is not a
repository CI workflow, still reports pre-existing indentation in
`PostgreSqlPersonKeyMap.cs`; that file is byte-identical to `origin/main`, outside P1-T09 scope and
does not affect the warning-clean Release build.

The architecture shell wrapper still prints its historical placeholder message, while the actual
NetArchTest/xUnit architecture assembly is included in the full build workflow and passed 17/17.
GitHub PR #29 passed all nine required workflows against implementation/evidence commit `1522c24`:
architecture `30134737709`, build-test `30134737723`, dependency-scan `30134737716`, format
`30134737714`, production-readiness `30134737692`, protected-paths `30134737708`, secret-scan
`30134737696`, task-contract-validation `30134737699` and web `30134737745`. The final
documentation-only closeout commit must pass the same workflows before merge.

## Independent review findings closed

The final read-only reviews confirmed no remaining production, test, scope or architecture finding.
Closed findings include:

- original E16 bitemporality conflicting with the frozen model;
- fingerprint collisions between absent scopes and a literal `-`;
- tenant switching through C5, C8, E16 resolver and event dependencies;
- event-without-state divergence after successful append;
- malformed/default tenant acceptance;
- stale E17 writes calling E16 before returning conflict;
- incomplete hostile tuple and temporal-boundary coverage;
- minimum-rule tests masked by a lower ceiling;
- missing jurisdiction/population scope and uncovered-cell L1 behavior;
- checking only the first policy rule against E16; and
- privileged `TelemetryPolicyChanged` payload omitting the complete policy rules.

## Change-to-contract accounting

Every change is required by the final approved contract:

- `ControlTower.sln` and the two test projects include the required C5/C8 suites.
- Platform Privacy provides the sole shared marking, opaque references and C5 resolver port.
- The exact three Provider files and one Provider test only reuse the promoted type.
- C5 Privacy plus its in-memory store implement E16 and its standard event.
- C8 Privacy plus its in-memory store implement E17 and its privileged event.
- Architecture tests make type ownership, module boundaries and event privilege permanent.
- DEV-003 records the approved contract correction required by the build constitution.
- This evidence, contract status, build state, substitute registry and `STATUS.md` close the
  mandatory build-control record.

No dependency, persistence adapter, migration, Host, API, UI, gate, provider collector, production
security control, taxonomy, HR mapping, credential, tenant action, cloud resource or unrelated
refactor was added.

## Merge Readiness Report

- Blueprint alignment: **PASS**; E16 is versioned simple history, E17 is bitemporal, and no frozen
  file changed.
- ADR compliance: **PASS**; privacy defaults, event audit, tenant isolation, bounded contexts and
  dependency direction remain intact.
- Tests: **PASS**; 303/303 backend, 26/26 real disposable PostgreSQL, 17/17 architecture and 114/114
  SPA.
- CI status: local equivalents **PASS**; all nine GitHub PR #29 workflows **PASS** on `1522c24`;
  the final documentation-only head must rerun green.
- Security status: **PASS locally**; fail-closed applicability, exact privileged payload, dependency
  and shipped-package scans are green; GitHub gitleaks passed.
- Architecture status: **PASS**; no new context or cross-module dependency.
- Technical debt introduced: none.
- Known risks: E16/E17 are domain and development-adapter foundations only. Durable repositories,
  authoritative applicability data, Host composition, production policy values and both privacy
  gates remain backlog work.
- Deviations requested: none. DEV-003 is an approved task-contract correction, not a blueprint
  deviation.
- Merge recommendation: **MERGE** after the final documentation-only head passes every required
  GitHub workflow; PR #29 is tenant-independent, mergeable and has no reviewer finding.

## External-state statement

P1-T09 accessed no Microsoft tenant, production credential, cloud resource, shared database,
staging environment or production environment. No production or shared deployment was attempted.
