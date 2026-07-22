---
id: EVIDENCE-P6-T05
type: evidence-bundle
schema_version: 1
task: P6-T05
status: submitted
produced_at: 2026-07-22
---

## Task

Replace the static C1.6 coverage placeholder with tenant-scoped, event-driven coverage and freshness
facts derived from actual C4 ingestion runs. Preserve the two doors and I3/I4: Providers emits a
self-contained fact, Ledger projects it, and the Trust area reads only that projection.

## What changed

- C4 emits `ProviderCoverageUpdated` after every completed or degraded ingestion run, through the
  existing hash-chained event store and outbox.
- C1.6 consumes the fact through a host-composed integration handler and projects per-connection
  surface state, capabilities, freshness, last successful sweep, and honest run counts.
- The projection rejects invalid tenant/freshness contracts, is tenant-partitioned, replay-idempotent,
  and will not let an older replay regress a newer fact.
- C7 Trust renders the provider surfaces from the read model; no calculation or provider read occurs
  in the SPA.

## Acceptance criteria to result

| Criterion | Evidence | Result |
|---|---|---|
| Completed run emits one coverage fact | Provider ingestion tests; event + outbox assertions | PASS |
| Failed acquisition records degraded fact before rethrow | `Failed_acquisition_records_and_emits_degraded_coverage_before_rethrowing` | PASS |
| Tenant-isolated, replay-idempotent projection | `CoverageProjectionTests` | PASS |
| Freshness from declared expectation + last successful sweep | coverage projection tests and read model | PASS |
| Honest empty state | `No_runs_is_an_explicit_unknown_coverage_state`; Trust SPA test | PASS |
| I3/I4 and module boundaries preserved | 5 architecture tests green; no module references added | PASS |
| No new context/aggregate/Microsoft assumption | existing C4, C1.6, C7 only; generic event contract | PASS |

## Commands run and raw results

```text
dotnet build ControlTower.sln -c Release --no-restore --nologo --disable-build-servers
  Build succeeded. 0 Warning(s), 0 Error(s).

dotnet test ControlTower.sln -c Release --no-build --no-restore --nologo --disable-build-servers
  120 passed, 0 failed:
  Platform 10; Providers 21; Ledger 27; Governance 17; Economics 16;
  Architecture 5; Host.Web 24.

npm test -- --run
  9 passed, 0 failed.

npm run build
  TypeScript + Vite production build succeeded.

dotnet list ControlTower.sln package --vulnerable --include-transitive
  No vulnerable packages.

npm audit --omit=dev
  found 0 vulnerabilities.

bash scripts/ci/check_production_readiness.sh
  OK: no dev-substitute references in production paths (DEV-001).

git diff --check
  clean.
```

## Reviewer notes

- The projection is an in-memory DEV-001 implementation behind `ICoverageReadModel`; production
  replacement remains PostgreSQL projection persistence. No new development substitute was added.
- The legacy `scripts/ci/architecture_gate.sh` still prints its bootstrap placeholder, but the real
  compiled `ControlTower.ArchitectureTests` suite ran and all 5 tests passed. This pre-existing script
  debt is not used as completion evidence.
- No Microsoft tenant, permission, credential, provider-specific mapping, migration, or production
  deployment was touched.
