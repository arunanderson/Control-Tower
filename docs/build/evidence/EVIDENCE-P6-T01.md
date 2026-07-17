---
id: EVIDENCE-P6-T01
type: evidence-bundle
schema_version: 1
task: P6-T01
status: submitted
produced_at: 2026-07-17
---

## Task

Provider Integration (C4.5) — tenant-independent provider framework, contracts, registry, harness,
Manual CSV provider (Phase A). Phase B (Microsoft providers + Gate-1 PoCs) stops at the tenant gate.

## What changed (files)

- `src/Modules/ControlTower.Modules.Providers/Domain/` — `ProviderPrimitives` (capability/auth/health/state
  enums, `NativeIdentifier`, `ProviderError`), `ProviderManifest` (+ `ProviderAuthRequirement`,
  `ProviderHealth`, `ProviderFreshness.IsStale`, `ProviderCoverage`), `IProvider` (+ connection context +
  capability-negotiation extension), `RawObservation`, `ProviderContractValidator` (SemVer + manifest/provider
  validation).
- `.../Application/` — `Ports` (`IProviderRegistry`, `SyncSchedule.ForManifest`, `IWatermarkStore`),
  `ProviderDiagnostics` (validate-all + health), `ProviderTestHarness` (contract-conformance suite).
- `.../Infrastructure/` — `ProviderRegistry` (validates on register, rejects duplicates),
  `CsvManualImportProvider` (ADR-013, "Self-reported / Manual Import"), `InMemoryWatermarkStore` (dev-only).
- `.../ProvidersModuleServiceCollectionExtensions.cs` — `AddProviderFramework()`.
- `src/Host/ControlTower.Host.Web/` — `Program.cs` registers the framework (Development); `ExperienceApi.cs`
  exposes read-only `/api/admin/providers` (manifests only, tenant-gated).
- `tests/ControlTower.Modules.Providers.Tests/` — framework, CSV provider, and harness tests.
- `docs/build/plans/PROVIDER-INTEGRATION-READINESS.md` — the Phase B deliverable (readiness + Gate-1 plan).

## Acceptance criteria → result

| Criterion                                                                | Evidence                                                                                                                          | Pass/Fail |
| ------------------------------------------------------------------------ | --------------------------------------------------------------------------------------------------------------------------------- | --------- |
| Contracts + validation + discovery + lifecycle + diagnostics             | `IProvider`/`ProviderManifest`/`ProviderContractValidator`/`IProviderRegistry`/`ProviderDiagnostics`                              | PASS      |
| Identifier mapping, negotiation, auth/authz, scheduling, models          | `NativeIdentifier`, `ProviderCapabilityNegotiation.Supports`, `ProviderAuthRequirement`, `SyncSchedule`, freshness/coverage/error | PASS      |
| Registry validates on register; rejects dupes/non-conforming             | ProviderFrameworkTests (register/duplicate/invalid-manifest)                                                                      | PASS      |
| Manual CSV provider (ADR-013 label)                                      | CsvManualImportProviderTests: `ManualImportLabel` on every row; capability gating; health                                         | PASS      |
| Test harness: conforming passes, broken fails                            | ProviderTestHarnessTests (Fake passes, CSV passes, Broken fails)                                                                  | PASS      |
| Framework provider-agnostic; no provider-specific logic outside          | domain has no Microsoft/OpenAI/etc.; all live behind `IProvider`                                                                  | PASS      |
| Read-only discovery endpoint, tenant-gated                               | `/api/admin/providers` → `IProviderRegistry.Discover()` inside the tenant-gated `/api` group                                      | PASS      |
| Build clean; tests green; production deps clean                          | build 0/0; 93 passed; `dotnet list --vulnerable` NONE                                                                             | PASS      |
| No Microsoft code; no PoC execution; no permissions; no blueprint change | only framework + CSV; Phase B documented, not executed                                                                            | PASS      |
| Phase B deliverable produced                                             | `docs/build/plans/PROVIDER-INTEGRATION-READINESS.md`                                                                              | PASS      |

## Commands run + raw output (local, 2026-07-17)

```
dotnet build ControlTower.sln -c Release → Build succeeded. 0 Warning(s) 0 Error(s)
dotnet test  ControlTower.sln -c Release → 93 passed
  (new: Providers 13 = framework + CSV + harness)
dotnet list ControlTower.sln package --vulnerable --include-transitive → NONE
```

## Reviewer notes / technical debt

- `InMemoryWatermarkStore` is a DEV-001 dev-only substitute (registered Development-only; production
  replacement: PostgreSQL/Blob watermark store) — added to the dev-substitute registry.
- Phase B (Microsoft Graph/Copilot/Entra/PPAC providers + Gate-1 PoC execution) is a **hard human gate**:
  it requires a provisioned M365 tenant, licences, admin consent, and sample archetypes
  (see `PROVIDER-INTEGRATION-READINESS.md`). No such code exists and no permissions were requested.
- Microsoft provider adapters, when built, are ordinary providers behind these contracts — no domain
  change, revertible by PR.
