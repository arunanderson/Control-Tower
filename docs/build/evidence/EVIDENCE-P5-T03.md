---
id: EVIDENCE-P5-T03
type: evidence-bundle
schema_version: 1
task: P5-T03
status: submitted
produced_at: 2026-07-22
---

## Task

Complete C9 Audit & Evidence with the V1 legal-hold capability required by ADR-021 before retention
enforcement exists: tenant-scoped, reason-bound, time-stamped, authorised, audited and releasable only
with approval evidence.

## What changed

- Added legal-hold scope over the Stage 5 retention data classes, optionally narrowed to a resource.
- Added append-only hold lifecycle: placement records actor/reason/time; release records actor,
  reason/time and mandatory approval reference without deleting the marker.
- Added `IsProtectedAsync`, the C9 decision point that makes active matching holds take precedence over
  future retention deletion.
- Added tenant-partitioned development persistence behind `ILegalHoldStore` and registered its Azure
  PostgreSQL replacement in DEV-001.
- Added thin C7 placement, release and history endpoints backed by C9 read models.

## Acceptance criteria to result

| Criterion                         | Evidence                                                         | Result |
| --------------------------------- | ---------------------------------------------------------------- | ------ |
| Tenant/reason/time/actor required | API and aggregate validation tests                               | PASS   |
| Blueprint retention scopes        | fixed `RetentionDataClass` model and matching assertions         | PASS   |
| Hold precedence                   | matching subject protected; unrelated class/resource unprotected | PASS   |
| Approved release                  | missing approval rejected; reference retained in history         | PASS   |
| Marker is never deleted           | released hold remains visible as inactive                        | PASS   |
| Placement/release audited         | both payloads asserted in tenant hash-chained event stream       | PASS   |
| Tenant isolation                  | empty other-tenant history and hidden cross-tenant release       | PASS   |
| C7 uses C9 service/read model     | host integration tests                                           | PASS   |
| No new context/retention engine   | existing C9/C7 only                                              | PASS   |

## Commands run and raw results

```text
dotnet build ControlTower.sln -c Release --no-restore --nologo --disable-build-servers
  Build succeeded. 0 Warning(s), 0 Error(s).

dotnet test ControlTower.sln -c Release --no-build --no-restore --nologo --disable-build-servers
  135 passed, 0 failed:
  Platform 10; Providers 24; Ledger 27; Governance 17; Economics 20;
  Architecture 5; Host.Web 32.

npm test -- --run
  10 passed, 0 failed.

npm run build
  TypeScript + Vite production build succeeded.

dotnet list ControlTower.sln package --vulnerable --include-transitive
  No vulnerable packages.

npm audit --omit=dev
  found 0 vulnerabilities.

python3 scripts/ci/validate_task_contracts.py
  checked 19 task contract(s); 0 error(s), 0 warning(s).

bash scripts/ci/check_production_readiness.sh
  OK: no dev-substitute references in production paths (DEV-001).
```

## Reviewer notes

- `InMemoryLegalHoldStore` is a DEV-001 substitute. Production requires PostgreSQL/RLS persistence
  and transactional event/outbox integration before deployment.
- `X-Operator` and `X-Approval-Reference` are development seams. Production authority and approval
  evidence must come from validated Entra/JIT claims and the approved workflow.
- Retention deletion is deliberately not implemented in this task. P5-T03 establishes the mandatory
  hold-precedence decision it must call.
- No tenant credentials, Microsoft API, migration, production resource or frozen blueprint changed.
