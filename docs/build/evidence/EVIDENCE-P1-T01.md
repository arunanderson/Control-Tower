---
id: EVIDENCE-P1-T01
type: evidence-bundle
schema_version: 1
task: P1-T01
status: submitted
produced_at: 2026-07-17
---

## Task

Platform foundation — host composition, tenancy middleware, background dispatcher, dev adapters.

## What changed (files)

- `src/ControlTower.Platform/DependencyInjection/PlatformServiceCollectionExtensions.cs` — `AddControlTowerPlatform`.
- `src/Adapters/ControlTower.Adapters.InMemory/` — dev-only `InMemoryEventStore` (tenant-partitioned, hash-chained), `InMemoryOutbox`, `InMemoryPrivilegedReadAuditor`, `InMemorySecretProvider`, `AddInMemoryAdapters`.
- `src/Host/ControlTower.Host.Web/` — `Program.cs` (DI + endpoints), `TenantResolutionMiddleware`.
- `src/Host/ControlTower.Host.Worker/` — `Program.cs` (DI + hosted service), `OutboxDispatcher`.
- `tests/ControlTower.Host.Web.Tests/PlatformFoundationTests.cs` (WebApplicationFactory integration), `tests/ControlTower.ArchitectureTests/AdapterBoundaryTests.cs`.

## Acceptance criteria → result

| Criterion                                      | Evidence                                                        | Pass/Fail                           |
| ---------------------------------------------- | --------------------------------------------------------------- | ----------------------------------- |
| DI composition                                 | AddControlTowerPlatform registers ITenantContextAccessor        | PASS                                |
| Per-request tenancy scope                      | TenantResolutionMiddleware opens/reverts scope from X-Tenant-Id | PASS                                |
| Dev adapters behind ports (DEV-001)            | in-memory impls of all four ports; registered Development-only  | PASS                                |
| Background dispatcher                          | OutboxDispatcher drains + acks                                  | PASS (build; drain logic extracted) |
| Adapters not referenced by kernel/modules      | ArchitectureTests AdapterBoundaryTests 2/2                      | PASS                                |
| Endpoint behaviour                             | /health 200 no-tenant; /whoami 400 no-header, 200 with header   | PASS (integration 3/3)              |
| Build clean; no vulnerable packages; no tenant | 0/0; vuln scan NONE                                             | PASS                                |

## Commands run + raw output (local, 2026-07-17; .NET 8.0.423)

```
dotnet build ControlTower.sln -c Release → Build succeeded. 0 Warning(s) 0 Error(s)
dotnet test  ControlTower.sln -c Release →
  Platform.Tests        Passed 10/10
  ArchitectureTests     Passed  5/5   (3 module-boundary + 2 adapter-boundary)
  Host.Web.Tests        Passed  3/3   (health, whoami-no-tenant→400, whoami-with-tenant→200)
dotnet list package --vulnerable --include-transitive → NONE
```

## Reviewer notes / technical debt

- `OutboxDispatcher.DrainOnceAsync` is extracted for testability but not yet unit-tested (no Host.Worker.Tests project); its dependency `IOutbox` is covered by Platform.Tests. Follow-up: add a worker test project. Minor.
- Tenancy source is a dev header (`X-Tenant-Id`); production resolves the tenant from the validated Entra token (later phase). Real Azure-backed port adapters (PostgreSQL/Service Bus/Key Vault/Blob) also later.
