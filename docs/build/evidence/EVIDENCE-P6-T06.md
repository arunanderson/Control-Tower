---
id: EVIDENCE-P6-T06
type: evidence-bundle
schema_version: 1
task: P6-T06
status: submitted
produced_at: 2026-07-22
---

## Task

Complete the existing privileged-read audit seam without inventing an L2 data surface or duplicating
the platform audit port. C7 explicitly marks privileged endpoints; C9 records the read through the
existing `IPrivilegedReadAuditor`, appends an immutable domain event, and projects a tenant-scoped
customer-visible Trust log.

## What changed

- Reused `IPrivilegedReadAuditor` and `PrivilegedReadRecord` from the Phase 0 event backbone.
- Added C9 `PrivilegedAccessService`, `PrivilegedReadRecorded`, and a disposable tenant-partitioned
  read projection for the Trust experience.
- Added reusable C7 endpoint metadata/filter enforcement requiring actor and purpose.
- Marked only `/api/trust/privileged-access` as privileged. Existing L1 aggregate views remain
  deliberately unmarked and do not generate false audit claims.
- Added the customer-visible Trust log to the SPA and thin API client.

## Acceptance criteria to result

| Criterion                                   | Evidence                                                            | Result |
| ------------------------------------------- | ------------------------------------------------------------------- | ------ |
| Explicit privileged endpoint filter         | `PrivilegedReadAuditFilter`; missing actor/purpose test             | PASS   |
| Existing audit capability reused            | service depends on `IPrivilegedReadAuditor`; no second auditor port | PASS   |
| Actor, purpose, resource, time, correlation | API integration assertions                                          | PASS   |
| Immutable event backbone record             | integration test reads tenant event stream                          | PASS   |
| Tenant-isolated customer log                | separate-tenant integration test and partitioned projection         | PASS   |
| L1 reads not misclassified                  | coverage read followed by empty audit-log response                  | PASS   |
| Trust API and SPA surface                   | API tests plus `TrustArea` test                                     | PASS   |
| No new bounded context or L2 surface        | existing C9/C7 only                                                 | PASS   |

## Commands run and raw results

```text
dotnet build ControlTower.sln -c Release --no-restore --nologo --disable-build-servers
  Build succeeded. 0 Warning(s), 0 Error(s).

dotnet test ControlTower.sln -c Release --no-build --no-restore --nologo --disable-build-servers
  123 passed, 0 failed:
  Platform 10; Providers 21; Ledger 27; Governance 17; Economics 16;
  Architecture 5; Host.Web 27.

npm test -- --run
  10 passed, 0 failed.

npm run build
  TypeScript + Vite production build succeeded.

dotnet list ControlTower.sln package --vulnerable --include-transitive
  No vulnerable packages.

npm audit --omit=dev
  found 0 vulnerabilities.

python3 scripts/ci/validate_task_contracts.py
  checked 16 task contract(s); 0 error(s), 0 warning(s).

bash scripts/ci/check_production_readiness.sh
  OK: no dev-substitute references in production paths (DEV-001).

npx --yes prettier@3 --check "**/*.{md,yml,yaml,json}"
  All matched files use Prettier code style.
```

## Reviewer notes

- The existing `InMemoryPrivilegedReadAuditor` remains the canonical DEV-001 audit port substitute.
  `InMemoryPrivilegedAccessProjection` is a new disposable read projection, registered in the
  substitute registry; production replacement is PostgreSQL.
- The development SPA supplies placeholder actor/purpose headers. Production identity must come from
  validated Entra claims and JIT context; no production-auth claim is made here.
- No tenant credentials, migrations, Microsoft API calls, or frozen-blueprint changes occurred.
