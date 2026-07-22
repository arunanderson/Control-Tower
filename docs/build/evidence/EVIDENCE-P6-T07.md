---
id: EVIDENCE-P6-T07
type: evidence-bundle
schema_version: 1
task: P6-T07
status: submitted
produced_at: 2026-07-22
---

## Task

Complete the C4 provider sweep job and worker execution seam without introducing the in-app durable
scheduler rejected by Stage 9 §5.1. A tenant-scoped, secret-free request enters the event store and
outbox; the worker resolves the existing provider connection and executes the existing invariant
ingestion pipeline. Azure Service Bus remains the production schedule/retry/DLQ adapter.

## What changed

- Added the blueprint-defined `ProviderConnection` with credential reference, surface, capabilities,
  cadence and enabled state; credentials never enter job payloads.
- Added `ProviderSweepRequested`, request service, outbox topic and host-composed worker handler.
- Added tenant-partitioned development connection and job-receipt stores behind ports.
- Added idempotent job claims: completed replay is ignored; failure releases the claim for external
  bounded retry and eventual DLQ.
- Composed the Provider module into the existing worker; no timer or provider-specific worker path.

## Acceptance criteria to result

| Criterion                          | Evidence                                                             | Result |
| ---------------------------------- | -------------------------------------------------------------------- | ------ |
| Secret-free tenant-scoped job      | payload test excludes credential reference and private configuration | PASS   |
| Event store and outbox             | test verifies event id/hash and sweep topic                          | PASS   |
| Worker executes invariant pipeline | CSV job produces two observations and one ingestion run              | PASS   |
| Completed replay idempotent        | replay leaves ingestion-run count at one                             | PASS   |
| Failure releases for retry         | same failed job throws on both attempts                              | PASS   |
| Tenant isolation                   | cross-tenant connection lookup rejected                              | PASS   |
| No in-process durable scheduler    | request/handler only; production timing remains Service Bus          | PASS   |
| No new context/provider rule       | existing C4 framework and generic manifest capability                | PASS   |

## Commands run and raw results

```text
dotnet build ControlTower.sln -c Release --no-restore --nologo --disable-build-servers
  Build succeeded. 0 Warning(s), 0 Error(s).

dotnet test ControlTower.sln -c Release --no-build --no-restore --nologo --disable-build-servers
  126 passed, 0 failed:
  Platform 10; Providers 24; Ledger 27; Governance 17; Economics 16;
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
  checked 17 task contract(s); 0 error(s), 0 warning(s).

bash scripts/ci/check_production_readiness.sh
  OK: no dev-substitute references in production paths (DEV-001).

npx --yes prettier@3 --check "**/*.{md,yml,yaml,json}"
  All matched files use Prettier code style.
```

## Reviewer notes

- `InMemoryProviderConnectionStore` and `InMemoryProviderJobReceiptStore` are DEV-001 substitutes;
  both are registered. Production replacements are PostgreSQL/RLS connection metadata plus Service
  Bus delivery state and a durable idempotency receipt.
- This train deliberately does not implement cadence timers, rate-limit budgets or DLQ infrastructure
  in-process. Those belong to the Azure Service Bus production adapter and Gate-3 measured limits.
- No tenant credential, Microsoft API, migration, production resource or frozen blueprint was touched.
