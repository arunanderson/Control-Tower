---
id: EVIDENCE-P6-T02
type: evidence-bundle
schema_version: 1
task: P6-T02
status: submitted
produced_at: 2026-07-22
---

## Task

C4 observation ingestion — the "one door in": turn provider RawObservations into immutable, append-only,
pre-resolution ProviderObservations and emit ObservationIngested. Proven end-to-end with the manual CSV
provider; no Microsoft API or tenant resource. Stops at the event boundary (C1 resolution is a later,
PoC-gated train).

## What changed (files)

- `src/Modules/ControlTower.Modules.Providers/Domain/ObservationPrimitives.cs` — `ObservationKind`,
  `PrivacyMarking`, `DeltaStatus` enums + `ObservationNormalization` (capability→kind map, delta key,
  content hash).
- `.../Domain/ProviderObservation.cs` — immutable append-only observation (Stage 5 E2): identifiers,
  kind, payload, observed/ingested times, privacy marking (set once), delta status, evidence label,
  content hash, natural key.
- `.../Domain/ProviderEvents.cs` — `ProviderEvent` base + `ObservationIngested` (self-contained
  serialization contract; primary identifier flattened — no shared types cross the boundary).
- `.../Domain/IngestionRun.cs` — immutable sweep log (Stage 5 E3): observed/new/changed/suppressed counts.
- `.../Application/IObservationStore.cs` — append-only port (no update/delete); `ObservationIngestionService`
  (the invariant pipeline) + `IngestionResult`.
- `.../Infrastructure/InMemoryObservationStore.cs` — DEV-001 dev-only, tenant-partitioned, append-only.
- `.../ProvidersModuleServiceCollectionExtensions.cs` — registers `IObservationStore` + `ObservationIngestionService`.
- `tests/.../ObservationIngestionTests.cs` (7) + test csproj references the in-memory adapters.

## Acceptance criteria → result

| Criterion                                                                       | Evidence                                                                                          | Pass/Fail |
| ------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------- | --------- |
| Immutable, append-only, privacy-marked, delta-classified observation            | `It_appends_immutable_observations...`: kind Inventory, L1, evidence label, New                   | PASS      |
| Append-only store + IngestionRun log                                            | `IObservationStore` has no update/delete; `It_records_an_honest_ingestion_run`                    | PASS      |
| Invariant pipeline for any provider (validate/mark/suppress/append/emit)        | CSV run through `ObservationIngestionService`; contract-validate + capability check               | PASS      |
| Delta suppression                                                               | `Re_ingesting_identical_data...` (2 suppressed, 0 appended); `A_changed_attribute...` (1 Changed) | PASS      |
| ObservationIngested emitted to stream + outbox                                  | `Each_appended_observation_emits_one_ingested_event_and_one_outbox_message`                       | PASS      |
| No ledger read/write (I3); self-contained event contract                        | Providers references Platform only; event carries flattened identifier, no shared type            | PASS      |
| Tenant-scoped; cross-tenant rejected; scope required                            | `Ingestion_requires_a_tenant_scope`; store rejects cross-tenant writes                            | PASS      |
| Build clean; suite green; production deps clean                                 | build 0/0; 100 passed; `dotnet list --vulnerable` NONE                                            | PASS      |
| No Microsoft code; no PoC-gated rule table; no new context; no blueprint change | Providers module only; resolution deferred                                                        | PASS      |

## Commands run + raw output (local, 2026-07-22)

```
dotnet build ControlTower.sln -c Release → Build succeeded. 0 Warning(s) 0 Error(s)
dotnet test  ControlTower.sln -c Release → Passed: 100  Failed: 0
  (Providers project now 20: framework 6 + CSV 3 + harness 3 + observation ingestion 7 + [prior 1])
dotnet list ControlTower.sln package --vulnerable --include-transitive → NONE
```

## Reviewer notes / technical debt

- `InMemoryObservationStore` is a DEV-001 dev-only substitute (Development-only; production =
  PostgreSQL append-only partitions) — added to the dev-substitute registry.
- **Scope boundary held deliberately:** this train stops at `ObservationIngested`. The C1 resolution
  engine that consumes it — deterministic-join → link, low-confidence → MergeCase, confidence roll-up —
  is the next train. Its **confidence rule table and alias types are PoC-gated (PoC-1/2)** and cannot be
  finalized until the Microsoft tenant is provisioned; the ingestion segment carries no such dependency.
- **Cross-module event delivery is not wired yet** (no subscriber consumes the outbox topic). That
  wiring belongs to the resolution train (composed at the host, so Providers never references Ledger).
- Observation `RollUp`/link-severing reconciliation noted by the survey lives in the Ledger and is
  out of scope here.
