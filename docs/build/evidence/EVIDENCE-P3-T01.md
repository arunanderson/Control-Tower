---
id: EVIDENCE-P3-T01
type: evidence-bundle
schema_version: 1
task: P3-T01
status: submitted
produced_at: 2026-07-17
---

## Task

Cost & Value Intelligence (C3) — one economics semantic model, multiple ROI read models.

## What changed (files)

- `Domain/` — `Money`, `Evidence` (six `EvidenceClass` + four `ValidationState` + `Evidence` + `EconomicFigure`, all-fields-required), `Observations` (Cost/Usage), `ValueDeclaration` (revision chain + forward-only validation), `PeriodsAndAllocation` (AllocationRule, ReportingPeriod, ReportSnapshot), `EconomicsMath` (`RoiResult`, weakest-link, ROI honesty), `EconomicsEvents`.
- `Application/` — `EconomicAmount` (evidence-carrying) + read models (`AssetEconomicsView`, `RoiView`, `ExecutiveEconomicsView`), `IEconomicsStore`, `EconomicsIngestionService` (write + events), `EconomicsProjectionService` (one model → all read models; Agent ROI = filter).
- `Infrastructure/` — dev-only `InMemoryEconomicsStore`. `AddEconomicsModule`; dev-only `/economics/portfolio` + `/economics/executive` endpoints.
- `tests/` — `EvidenceTests`, `EconomicsMathTests`, `EconomicsProjectionTests` (16 total).

## Acceptance criteria → result

| Criterion                                                    | Evidence                                                                               | Pass/Fail |
| ------------------------------------------------------------ | -------------------------------------------------------------------------------------- | --------- |
| No figure without evidence (structural)                      | EvidenceTests: Evidence/EconomicFigure/Money ctors throw without fields                | PASS      |
| Six evidence classes                                         | `EvidenceClass` (Unknown…FinanciallyValidated)                                         | PASS      |
| Finance validation ladder, forward-only                      | ProjectionTests: Estimated→…→FinanceVerified; 3 revisions kept; backward throws        | PASS      |
| Cost allocation + Unattributed never spread                  | ProjectionTests: Sales cost excludes the untagged 50; Unattributed=50                  | PASS      |
| ROI honesty (range + mix, >25% soft suppresses single point) | EconomicsMathTests: suppressed at 50% soft; validated-only ROI; payback                | PASS      |
| One model → all read models; Agent ROI = filter              | ProjectionTests: asset/agent/dept/BU/portfolio/executive from one store; no ROI module | PASS      |
| Reproducible for a given as-of                               | ProjectionTests: two computations equal                                                | PASS      |
| Tenant-scoped                                                | ProjectionTests: seed in A → empty in B                                                | PASS      |
| No figure exposed without evidence fields                    | ProjectionTests: every EconomicAmount has class/source/methodology/as-of/validation    | PASS      |

## Commands run + raw output (local, 2026-07-17; .NET 8.0.423)

```
dotnet build ControlTower.sln -c Release → Build succeeded. 0 Warning(s) 0 Error(s)
dotnet test  ControlTower.sln -c Release → 47 passed
  Platform 10 · Ledger 13 · Architecture 5 · Economics 16 · Host.Web 3
dotnet list package --vulnerable --include-transitive → NO VULNERABLE PACKAGES
```

## Reviewer notes / technical debt

- Allocation implements the DirectTag driver; headcount/usage-share drivers and full AllocationRule
  versioning are follow-ups. FX conversion and ContractCommitment (E21) are modelled at the edges but
  deferred; multi-currency aggregation currently assumes a single reporting currency. ReportSnapshot
  persistence/restatement is modelled but not yet exercised end-to-end. Department/BU attribution for
  value is joined from the asset's cost attribution (C5 OrgModel replaces this later). Real Azure
  PostgreSQL store replaces the in-memory dev substitute.
