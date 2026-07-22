---
id: EVIDENCE-P6-T03
type: evidence-bundle
schema_version: 1
task: P6-T03
status: submitted
produced_at: 2026-07-22
---

## Task

C1 entity resolution — consume the C4 ObservationIngested event through host-composed delivery, build
identity aliases, match deterministically, link to AIAssets / open MergeCases, roll up lowest-wins,
merge/split without touching observations, sever-not-delete links, complete audit. Providers never
references Ledger. Microsoft rules stay PoC-gated. Proven with the CSV provider — no Microsoft tenant.

## What changed (files)

- **Platform:** `Events/IIntegrationEventHandler.cs` — the host-composed delivery seam (topic + idempotent handler).
- **Providers (C4):** `ObservationIngested` enriched with `Tenant` + provider-suggested `DisplayName`/`AssetType`
  (generic well-known attributes); ingestion service populates them. No Ledger reference.
- **Ledger (C1) domain:** `Primitives` (`LinkStatus`, `AliasProvenance`, `IdentityAlias`); `ResolutionLink`
  (status + `ObservationRef` + sever/supersede, retained never deleted); `AIAsset` (lowest-confidence-wins
  `RollUp` over active links, `SeverResolutionLink`/`SupersedeResolutionLink`, `MarkMergedInto`, `RecordSplit`,
  alias exposure, observation-ref idempotency); `MergeCase` (E8); new events
  (`ResolutionLinkSevered/Superseded`, `AssetMergedInto`, `AssetSplit`, `MergeCaseOpened/Resolved`).
- **Ledger application:** `MatchClassifier` (`IMatchClassifier` + provider-agnostic `DeterministicMatchClassifier`,
  PoC-gated seam); `EntityResolutionService` (resolve/merge/split/manual-link/resolve-case);
  `ObservationIngestedHandler` (consumes the topic, reconstructs its own contract, re-enters the tenant scope).
- **Ledger infra:** `InMemoryMergeCaseStore`; repository reverse alias lookup `FindByNativeIdentifierAsync`;
  read model counts active links only.
- **Host.Worker:** `OutboxDispatcher` routes messages to registered `IIntegrationEventHandler`s per scope;
  `Program` registers the Ledger module so the worker composes the C4→C1 seam.
- **Tests:** `EntityResolutionTests` (11) + updated `AssetAggregateTests` roll-up/sever test.

## Acceptance criteria → result

| Criterion                                                       | Evidence (test)                                                                  | Pass/Fail |
| --------------------------------------------------------------- | -------------------------------------------------------------------------------- | --------- |
| Consume ObservationIngested via host-composed delivery          | `End_to_end_csv_ingestion_resolves_into_the_ledger_without_a_tenant`             | PASS      |
| Providers ⊥ Ledger (no cross-reference)                         | `Ledger_and_Providers_modules_do_not_reference_each_other` + ModuleBoundaryTests | PASS      |
| Deterministic matching (provider-agnostic)                      | `Deterministic_match_links_to_the_existing_asset_and_is_provider_agnostic`       | PASS      |
| No-match asset creation                                         | `No_match_creates_a_new_asset_linked_to_the_observation`                         | PASS      |
| Identifier collision → MergeCase, no auto-link                  | `Identifier_collision_opens_a_merge_case_and_does_not_auto_link`                 | PASS      |
| Low/Medium never auto-link → manual queue                       | `Sub_high_confidence_never_auto_links_and_enters_the_merge_queue` (Low+Medium)   | PASS      |
| Lowest-confidence-wins roll-up                                  | `Resolution_link_confidence_rolls_up_to_the_weakest_material_link`               | PASS      |
| Idempotent replay                                               | `Idempotent_replay...` + end-to-end re-pump                                      | PASS      |
| Tenant isolation                                                | `Resolution_is_tenant_isolated`                                                  | PASS      |
| Merge + split audit completeness                                | `Merge_supersedes...complete_audit`, `Split_moves_links...complete_audit`        | PASS      |
| Links sever/supersede, never delete                             | severed/superseded links retained (merge/split/aggregate tests)                  | PASS      |
| Observations never modified by merge/split                      | end-to-end asserts observation count unchanged after resolution                  | PASS      |
| Microsoft rules provisional/PoC-gated; no assumptions confirmed | deterministic classifier generic; heuristic/Low PoC-gated (documented)           | PASS      |

## Commands run + raw output (local, 2026-07-22)

```
dotnet build ControlTower.sln -c Release → Build succeeded. 0 Warning(s) 0 Error(s)
dotnet test  ControlTower.sln -c Release → 111 passed, 0 failed
  (Ledger now 24: +11 EntityResolution; roll-up test updated to lowest-wins/sever)
dotnet list ControlTower.sln package --vulnerable --include-transitive → NONE
```

## Reviewer notes / technical debt

- `InMemoryMergeCaseStore` is a DEV-001 dev-only substitute (prod = PostgreSQL) — added to the registry.
- **PoC gate held:** the deterministic classifier makes NO Microsoft assumptions (exact native-id equality
  only). The heuristic (Medium) / weak (Low) rule table and cross-surface alias types are **PoC-gated
  (PoC-1/2)**; the engine treats any sub-High match conservatively (review, never auto-link) via the
  pluggable `IMatchClassifier`, so the Microsoft rule set can be dropped in later without touching the
  mechanism. No Microsoft identifier mappings were invented.
- **Behavioral corrections** flagged by the repo survey are included: roll-up is now lowest-confidence-wins
  (was strongest-wins), and links are severed/superseded, not deleted (Stage 5 E6).
- **Dev cross-process caveat:** in-memory stores are per-process, so the web and worker hosts don't share
  state in dev; production shares PostgreSQL/Service Bus. The end-to-end test proves the full pipeline in a
  single process. Operator merge-case/merge/split APIs are exposed on the service; a C7 workbench surface
  for them is a later Experience iteration.
