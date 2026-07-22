---
id: EVIDENCE-P6-T04
type: evidence-bundle
schema_version: 1
task: P6-T04
status: submitted
produced_at: 2026-07-22
---

## Task

Resolution & Merge Workbench (C7): read-model-only APIs for the alias graph, ResolutionLinks, MergeCases
and confidence labels, plus operator actions (merge/split/manual-link/resolve-case) routed to the C1
resolution service (event-driven, auditable). No UI business logic; I3/I4 preserved. Tenant-independent.

## What changed (files)

- `src/Modules/ControlTower.Modules.Ledger/Application/ResolutionWorkbenchReadModel.cs` — read-model-only
  projection: `MergeCaseView`, `AssetResolutionView` (aliases + full link history), `ResolutionLinkView`,
  `AliasView`, `IdentifierView`. Registered in `AddLedgerModule` (scoped).
- `src/Host/ControlTower.Host.Web/ExperienceApi.cs` — workbench read endpoints
  (`/api/resolution/merge-cases`, `/api/resolution/assets/{id}`) and operator-action commands
  (`/api/resolution/merge|split|manual-link`, `/api/resolution/merge-cases/{id}/resolve`) routed to
  `EntityResolutionService`; `DomainException` → 400; operator from `X-Operator` header (default "operator").
- `web/` — `api/types.ts` + `api/client.ts` (read methods + POST operator actions), `areas/ResolutionWorkbench.tsx`
  (merge-case queue with confidence labels + resolve action), wired into the Trust area in `App.tsx`,
  `ResolutionWorkbench.test.tsx`.
- `tests/ControlTower.Host.Web.Tests/ExperienceApiTests.cs` — workbench integration tests.

## Acceptance criteria → result

| Criterion                                                       | Evidence                                                                                  | Pass/Fail |
| --------------------------------------------------------------- | ----------------------------------------------------------------------------------------- | --------- |
| Read-model-only merge-case + resolution APIs, tenant-gated      | `Api_requires_a_tenant` / `Api_returns_200...` (merge-cases); `Merge_case_queue_lists...` | PASS      |
| Operator actions event-driven + auditable (merge/split/resolve) | `Operator_merge_supersedes_source_links...`; `..._operator_can_resolve_them`              | PASS      |
| Per-asset alias graph + link history + confidence               | `AssetResolutionView` (aliases + all links w/ status); `Resolution_view_is_404...`        | PASS      |
| Merge leaves links superseded (retained), not deleted           | source view contains "Superseded"; target view "Active"                                   | PASS      |
| SPA workbench reads models, no business logic; empty stated     | `ResolutionWorkbench.test.tsx` (queue, confidence label, resolve callback, empty)         | PASS      |
| I3 preserved (Providers ⊥ Ledger); tenant-isolated              | endpoints use Ledger services only; tenant via `X-Tenant-Id`; ModuleBoundaryTests green   | PASS      |
| Build clean; suite green; production deps clean                 | build 0/0; 116 backend + 8 SPA; `dotnet list --vulnerable` NONE                           | PASS      |
| No new context/aggregate; no blueprint change                   | read model + endpoints + UI only                                                          | PASS      |

## Commands run + raw output (local, 2026-07-22)

```
dotnet build ControlTower.sln -c Release → Build succeeded. 0 Warning(s) 0 Error(s)
dotnet test  ControlTower.sln -c Release → Passed: 116  Failed: 0
  (Host.Web now 24: +5 workbench: 2 tenant-gate theory rows + merge-queue/resolve + merge-supersede + 404)
web: npm run build → built; npm test → 8 passed (ResolutionWorkbench +3)
dotnet list ControlTower.sln package --vulnerable --include-transitive → NONE
```

## Reviewer notes / technical debt

- No new dev substitute (reuses the merge-case/asset stores from P6-T03).
- **Operator actions are commands, not reads:** they are routed to `EntityResolutionService`, which emits
  the immutable audit events (ADR-015). The read endpoints remain strictly read-model-only (I4); the UI
  only renders read models and invokes the actions.
- **Dev cross-process caveat (unchanged):** event-driven ingestion→resolution runs in the worker; the web
  host performs operator actions synchronously in-process. The integration tests seed via the resolution
  service in a tenant scope, then exercise the HTTP read/command endpoints.
- Auth is the permissive dev authorizer; the operator identity is a header placeholder pending the C8.2
  role model. A production Entra-authenticated operator identity replaces it later.
