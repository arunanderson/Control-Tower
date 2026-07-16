---
id: EVIDENCE-P0-T09
type: evidence-bundle
schema_version: 1
task: P0-T09
status: submitted
produced_at: 2026-07-16
---

## Task

Platform skeleton, tenancy context, and architecture-boundary tests (E2).

## What changed (files)

- `ControlTower.sln`, `global.json`
- `src/ControlTower.Platform/` — `IModule`, `Tenancy/{TenantId,ITenantContextAccessor,TenantContextAccessor}`, `Ports/Ports.cs`, `Events/Events.cs`
- `src/Modules/ControlTower.Modules.{Ledger,Governance,Economics,Providers,EnterpriseContext,Experience,Trust,Audit}/` — module markers (C1–C9; C6 vacant per ADR-010)
- `src/Host/ControlTower.Host.{Web,Worker}/`
- `tests/ControlTower.Platform.Tests/TenantContextTests.cs`, `tests/ControlTower.ArchitectureTests/ModuleBoundaryTests.cs`
- CI: `build-test.yml`, `architecture-gate.yml` (now runs NetArchTest), `dependency-scan.yml` (now `dotnet list --vulnerable`); `scripts/ci/architecture_gate.sh`

## Acceptance criteria → result

| Criterion                                 | Evidence                                                                   | Pass/Fail |
| ----------------------------------------- | -------------------------------------------------------------------------- | --------- |
| Modular-monolith skeleton (13 projects)   | `dotnet sln list` shows Platform + 8 modules + 2 hosts + 2 tests           | PASS      |
| Build clean                               | `Build succeeded. 0 Warning(s) 0 Error(s)`                                 | PASS      |
| Tenancy context unforgeable + unit-tested | Platform.Tests 4/4 (throws without scope; reverts; nested; empty rejected) | PASS      |
| Module-boundary rules enforced            | ArchitectureTests 3/3 (kernel→no module; module→no module; module→no host) | PASS      |
| No vulnerable packages                    | `dotnet list package --vulnerable` → none (after test-tooling upgrade)     | PASS      |
| No live Microsoft tenant used             | skeleton only; providers module is an empty boundary                       | PASS      |

## Commands run + raw output (local, 2026-07-16; .NET 8.0.423)

```
dotnet build ControlTower.sln -c Release   → Build succeeded. 0 Warning(s) 0 Error(s)
dotnet test  ControlTower.sln -c Release   → Platform.Tests: Passed 4/4 ; ArchitectureTests: Passed 3/3
dotnet list  ControlTower.sln package --vulnerable --include-transitive → NO VULNERABLE PACKAGES
bash scripts/ci/architecture_gate.sh       → NetArchTest boundary rules: Passed 3/3
```

Repair note: the initial `dotnet list --vulnerable` flagged transitive `System.Net.Http 4.3.0` and
`System.Text.RegularExpressions 4.3.0` (High) pulled by the old xunit template. Remediated by
upgrading the test tooling (Microsoft.NET.Test.Sdk / xunit / xunit.runner.visualstudio /
coverlet.collector) to current versions; re-scan clean; build + tests re-run green. (Repair attempt 1.)

## CI run link

Populated by the E2 PR's Actions run (build-test / architecture-gate / dependency-scan) on push.

## Reviewer notes

Architecture keystone (R-23) is now live and machine-enforced. Two-doors / I3-I4 / "no provider SDK
outside Providers" / "/poc never referenced by /src" rules are the next extension of the arch-test
suite (added as those surfaces gain real code).
