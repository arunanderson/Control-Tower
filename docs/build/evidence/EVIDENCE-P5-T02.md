---
id: EVIDENCE-P5-T02
type: evidence-bundle
schema_version: 1
task: P5-T02
status: submitted
produced_at: 2026-07-22
---

## Task

Operationalize the existing C3 `ReportingPeriod` and `ReportSnapshot` model as the ADR-016
reproducibility anchor: signed, tenant-scoped, immutable frozen outputs with complete pinned input
bases and append-only restatement versions exposed through C7.

## What changed

- Completed the `Open -> Closing -> Frozen -> Restated` period lifecycle with signer and freeze time.
- Added structured input basis covering as-of time, source references, rule versions, organisation
  model version and observation watermark; its canonical serialized form receives a SHA-256 digest.
- Added append-only tenant persistence for reporting periods and snapshot versions.
- Added signed freeze and reasoned restatement services. Restatement creates a superseding version and
  never updates or deletes an earlier snapshot.
- Appended freeze/restatement events containing snapshot identity, version, basis hash, signer,
  supersedes reference and reason to the existing tenant hash chain.
- Added thin C7 lifecycle commands and read models; no reporting engine or bounded context was added.

## Acceptance criteria to result

| Criterion                          | Evidence                                                   | Result |
| ---------------------------------- | ---------------------------------------------------------- | ------ |
| Complete lifecycle                 | domain and API tests traverse Open/Closing/Frozen/Restated | PASS   |
| Signed immutable version 1         | freeze assertions cover signer, payload, basis and version | PASS   |
| Complete reproducibility basis     | validation and API round trip cover all Stage 5 E14 fields | PASS   |
| Repeat freeze rejected             | focused economics test                                     | PASS   |
| Restatement is a new version       | version 2 supersedes version 1 with mandatory reason       | PASS   |
| Historical bytes unchanged         | serialized version 1 is byte-equal after restatement       | PASS   |
| Hash-chained domain events         | event payload and previous-hash assertions                 | PASS   |
| Tenant isolation                   | period/snapshot cross-tenant lookup assertions             | PASS   |
| C7 uses C3 service/read models     | host API integration test; no domain aggregate returned    | PASS   |
| No new context or reporting engine | existing Economics module/store/event backbone only        | PASS   |

## Commands run and raw results

```text
dotnet build ControlTower.sln -c Release --no-restore --nologo --disable-build-servers
  Build succeeded. 0 Warning(s), 0 Error(s).

dotnet test ControlTower.sln -c Release --no-build --no-restore --nologo --disable-build-servers
  132 passed, 0 failed:
  Platform 10; Providers 24; Ledger 27; Governance 17; Economics 20;
  Architecture 5; Host.Web 29.

npm test -- --run
  10 passed, 0 failed.

npm run build
  TypeScript + Vite production build succeeded.

dotnet list ControlTower.sln package --vulnerable --include-transitive
  No vulnerable packages.

npm audit --omit=dev
  found 0 vulnerabilities.

python3 scripts/ci/validate_task_contracts.py
  checked 18 task contract(s); 0 error(s), 0 warning(s).

bash scripts/ci/check_production_readiness.sh
  OK: no dev-substitute references in production paths (DEV-001).

npx --yes prettier@3 --check "**/*.{md,yml,yaml,json}"
  All matched files use Prettier code style.
```

## Reviewer notes

- `InMemoryEconomicsStore` remains the registered DEV-001 substitute. The new data uses its existing
  tenant partition; production replacement remains Azure PostgreSQL with RLS and append-only snapshot
  version constraints.
- The API requires an explicit `X-Operator` only as a development identity seam. Production signer
  identity must be supplied from validated Entra claims and JIT context.
- No tenant credentials, Microsoft API, migration, production resource or frozen blueprint changed.
