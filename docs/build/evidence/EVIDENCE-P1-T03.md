---
id: EVIDENCE-P1-T03
type: evidence-bundle
schema_version: 1
task: P1-T03
status: submitted
produced_at: 2026-07-23T12:00:33Z
---

## Task

Resolve server-controlled C8 role assignments for the validated human and internal tenant, derive
only the four curated V1 role bundles, and require one explicit fine-grained capability on every
existing Experience API route.

## Blueprint and decision trace

- Stage 2 C8 and Stage 8 §6: C8 owns authorization; roles, organisation scope and data granularity
  remain independent dimensions.
- Stage 4 E18 and Stage 5 E18/E19: role assignments are evented and reference people by opaque
  `PersonKey`, while raw Entra identifiers remain inside the severable E19 boundary.
- Stage 6 §9: the V1 roles are exactly Viewer, Operator, Administrator and Executive-scope;
  Executive-scope is restricted to prescribed aggregate and drill paths.
- ADR-001/020: C8 owns the role catalogue and assignment ports; the Host.Web composition root
  bridges C8 authorization to C1 without introducing a module-to-module dependency.
- ADR-015/021: assignments and their canonical audit events are tenant-scoped, fail closed and
  committed through one atomic persistence port.
- ADR-023: ASP.NET Core's built-in authorization primitives are used. No dependency, bounded context,
  infrastructure or frozen-blueprint change was introduced.

## What changed

- Added immutable C8 role, capability and `TenantWide` organisation-scope types with one exhaustive,
  non-hierarchical role-bundle catalogue. Direct capability grants, custom roles, group grants and
  business-unit scopes are not representable.
- Added tenant-scoped E18 `RoleAssignment`, `RoleAssignmentChanged`, resolver and mutation ports.
  Assignment state and audit events contain opaque `PersonKey`, never raw Entra `oid`.
- Added the minimal E19 lookup seam required to keep directory identity out of E18. Its in-memory
  implementation generates distinct opaque keys per tenant and is registered only in Development.
- Added a development E18 store that rejects cross-tenant writes and mismatched event identity,
  subject, role, scope, transition, actor or timestamp. The durable adapter contract requires one
  state/event transaction, active-grant uniqueness and optimistic revocation.
- Production composes deny-all E18/E19 readers and continues to map no Experience API routes.
- Added ASP.NET Core capability requirements and handlers. All 27 current API routes carry exactly
  one explicit capability policy.
- Bound the existing C1 `ILedgerAuthorizer` seam to the same C8 evaluator in Host.Web; the Web host no
  longer resolves the permissive development authorizer.
- `/whoami` now projects only server-resolved roles, capability names and `TenantWide` scope.
  Caller-supplied role/group/capability claims and authorization headers are ignored.

## Exact V1 access model

| Role            | Curated capability bundle                                                                                                                                                                                                                              |
| --------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| Viewer          | Portfolio read; executive, portfolio and detailed economics read; reporting-period read; governance read; trust coverage read; resolution read                                                                                                         |
| Operator        | Viewer read paths plus reporting-period management, resolution management and Ledger management                                                                                                                                                        |
| Administrator   | Trust coverage, privileged-access and legal-hold read; legal-hold management; administration read. No implicit Operator inheritance                                                                                                                    |
| Executive-scope | Portfolio read; executive and portfolio economics read; reporting-period read; trust coverage read. No detailed economics, governance, resolution, administration, privileged-access, legal-hold, reporting-period command or Ledger-management access |

V1 organisation scope is exactly `TenantWide`. Purpose and approval reference remain bounded
business context and never grant access.

## Acceptance criteria → result

| Criterion                                                           | Evidence                                                                                       | Result |
| ------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------- | ------ |
| Missing assignments fail with a generic 403                         | No-assignment integration test                                                                 | PASS   |
| Caller claims and headers cannot grant access                       | Forged claims/headers against unassigned and assigned Viewer identities                        | PASS   |
| Exactly four roles and their public names/bundles are accepted      | Exhaustive role, scope, bundle and capability-name contract test                               | PASS   |
| Every API route has one exact capability policy                     | Executable 27-route endpoint metadata map                                                      | PASS   |
| Viewer and Executive-scope are restricted read-only bundles         | Role-matrix integration tests                                                                  | PASS   |
| Operator and Administrator have no implicit inheritance             | Positive and negative command/area tests                                                       | PASS   |
| Multiple assignments union only curated bundles                     | Multi-role integration test                                                                    | PASS   |
| E18 resolution and mutation remain tenant/subject/record isolated   | Hostile reader/store tests, same-oid two-tenant test and returned-ID mismatch test             | PASS   |
| E19 pseudonyms do not correlate the same oid across tenants         | Direct person-key map isolation test                                                           | PASS   |
| Empty/default person keys fail closed                               | Resolver, service, aggregate and actor-boundary test                                           | PASS   |
| Role changes produce canonical opaque E18 audit events              | Assign/revoke test plus event ID, subject, transition, actor, timestamp and raw-oid assertions | PASS   |
| State is not written when event append or event consistency fails   | Development-store append failure and adversarial mismatch tests                                | PASS   |
| Authorization completes before privileged purpose/audit behavior    | Denied read proves neither privileged projection nor audit event executes                      | PASS   |
| Ledger commands use C8 access and bind tenant plus subject          | Positive/negative bridge test and Web dependency-injection replacement test                    | PASS   |
| Production exposes no Experience API with development authorization | Existing production endpoint inspection                                                        | PASS   |

## Verification

```text
dotnet restore ControlTower.sln
  All projects are up-to-date for restore.

dotnet build ControlTower.sln -c Release --no-restore
  Build succeeded. 0 Warning(s). 0 Error(s).

dotnet test ControlTower.sln -c Release --no-build --verbosity normal
  Platform 10/10; Ledger 27/27; Governance 17/17; Economics 20/20;
  Providers 24/24; Architecture 5/5; Host.Web 67/67.
  Total backend: 170 passed, 0 failed.

PATH=/Users/arunanderson/.dotnet:$PATH bash scripts/ci/architecture_gate.sh
  5/5 passed.

python3 scripts/ci/validate_task_contracts.py
  checked 23 task contract(s); 0 error(s), 0 warning(s).

bash scripts/ci/check_production_readiness.sh
  OK: no dev-substitute references in production paths (DEV-001).

dotnet list ControlTower.sln package --vulnerable --include-transitive
  No vulnerable NuGet packages.

npm run build
  SPA production build passed.

npm test
  6 files and 10 tests passed.

npm audit --omit=dev --audit-level=high
  found 0 vulnerabilities.

dotnet format ControlTower.sln --verify-no-changes --no-restore --include <changed C# files>
  [no output; exit 0]

npx --yes prettier@3 --check "**/*.{md,yml,yaml,json}"
  All matched files use Prettier code style.

git diff --check
  [no output; exit 0]

targeted credential/private-key pattern scan across every changed file
  [no matches; gitleaks is not installed locally and remains a required PR check]
```

## Independent review

- Architecture reviewer: no P0, P1 or P2 findings after the opaque-person-key, tenant/subject,
  returned-record, cache-binding and event-integrity hardening.
- Security reviewer: no P0, P1 or P2 findings; production remains deny-all and Development-only
  substitutes are correctly registered.
- Test reviewer: no P0, P1 or P2 gaps after adversarial claims/header, E18/E19 isolation, default-key,
  event-integrity, privileged-ordering and exact public-contract coverage.

## Deliberately deferred production obligations

- This task establishes only the E19 indirection seam. The durable E19 implementation must add field
  protection, O(1) sever/anonymise lifecycle and privileged audit for every map read/write.
- The durable E18 adapter must add PostgreSQL/RLS persistence, one transactional state/event commit,
  a partial unique constraint for active tenant/person/role grants and optimistic revocation.
- SPA bearer acquisition, real Entra assignment administration, platform-staff JIT/break-glass,
  privacy Gate 2 and delegated administration remain separate blueprint tasks.
- Existing development data outside this new E18 boundary may still contain legacy raw identity
  fields; the durable privacy train must remediate them before production activation.

## CI

Pull request checks: https://github.com/arunanderson/Control-Tower/pull/23/checks

## Rollback

Revert the P1-T03 PR. There is no datastore migration, tenant permission, infrastructure or
production configuration change.
