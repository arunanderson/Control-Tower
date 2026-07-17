---
id: EVIDENCE-P2-T01
type: evidence-bundle
schema_version: 1
task: P2-T01
status: submitted
produced_at: 2026-07-17
---

## Task

Asset Ledger (C1) — aggregate, state machines, ownership, aliases, events, read model, registration workflow.

## What changed (files)

- `src/Modules/ControlTower.Modules.Ledger/Domain/` — `Primitives` (ids, enums, `AssetType`, `PersonRef`, `NativeIdentifier(Set)`, `DomainException`), `TaxonomyScheme`, `OwnershipAssignment`, `ResolutionLink`, `LedgerEvents`, `AIAsset` (aggregate + both state machines + roll-up).
- `Application/` — `IAssetRepository`, `IAssetLedgerReadModel` + `AssetLedgerView`, `ILedgerAuthorizer`/`LedgerCapability`, `AssetRegistrationService`.
- `Infrastructure/` — dev-only `InMemoryAssetRepository`, `InMemoryAssetLedgerReadModel`, `AllowAllLedgerAuthorizer`.
- `LedgerModuleServiceCollectionExtensions` (`AddLedgerModule`); dev-only `/assets` read endpoint in `Host.Web`.
- `tests/ControlTower.Modules.Ledger.Tests/` — `AssetAggregateTests` (unit), `AssetLedgerWorkflowTests` (integration + tenancy + authorization).

## Acceptance criteria → result

| Criterion                                                | Evidence                                                      | Pass/Fail |
| -------------------------------------------------------- | ------------------------------------------------------------- | --------- |
| One AIAsset aggregate, all types                         | AIAsset + TaxonomyScheme (types are values)                   | PASS      |
| Registration + lifecycle state machines guarded          | unit tests: illegal transitions throw                         | PASS      |
| Ownership Ownerless/Lapsed, history never overwritten    | unit test: lapse→ownerless→reassign; 2 assignments kept       | PASS      |
| Aliases + ResolutionLink + confidence roll-up            | unit test: Low→High roll-up; remove reverts                   | PASS      |
| Domain events appended to immutable stream               | integration: event stream ≥4 after register                   | PASS      |
| Tenant-scoped persistence via ports                      | integration: tenant isolation (A visible, B empty)            | PASS      |
| Authorization seam enforced                              | integration: missing capability → Unauthorized, stays Triaged | PASS      |
| Architecture boundaries hold                             | ArchitectureTests 5/5 (module + adapter)                      | PASS      |
| No new context/aggregate; no tenant; no blueprint change | Ledger module only; no live tenant used                       | PASS      |

## Commands run + raw output (local, 2026-07-17; .NET 8.0.423)

```
dotnet build ControlTower.sln -c Release → Build succeeded. 0 Warning(s) 0 Error(s)
dotnet test  ControlTower.sln -c Release →
  Platform.Tests            10/10
  ArchitectureTests          5/5
  Modules.Ledger.Tests      13/13  (10 aggregate + 3 workflow/tenancy/authz)
  Host.Web.Tests             3/3
  TOTAL                      31 passed
dotnet list package --vulnerable --include-transitive → NONE
```

## Reviewer notes / technical debt

- Persistence + read model are dev-only in-memory (behind ports; PostgreSQL + RLS impls later). Match-confidence roll-up is provisional (strongest-link) pending the Stage 5 ⛔PoC confidence table. `AllowAllLedgerAuthorizer` is a dev seam replaced by the C8.2 role model. MergeCase (V2) and DependencyRef (V2) intentionally not implemented in this train.
