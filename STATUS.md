# Enterprise AI Control Tower — Build Status

_Single source of build truth. Updated by the build agent as part of every task's Document step._

| Field             | Value                                                                                                   |
| ----------------- | ------------------------------------------------------------------------------------------------------- |
| **Current phase** | Phase 6 — Provider Integration (C4)                                                                     |
| **Current epic**  | C4 ingestion (the one door in) built tenant-independently; Phase B (Microsoft providers) stays gated    |
| **Current task**  | P6-T02 complete (PR open)                                                                               |
| **Overall state** | **Building tenant-independent C4/C1 work; Microsoft providers await the tenant (external dependency).** |
| **Merge policy**  | Merge trains — agent merges tenant-independent green PRs with a Merge Readiness Report; emergent-first  |
| **Last updated**  | 2026-07-22                                                                                              |
| **Updated by**    | Claude Code (build agent)                                                                               |

## Completed & merged on `main`

- **E0/E1 rails + CI** (#1) · **E2 platform skeleton + tenancy** (#3) · **E3 event backbone** (#4) · **Platform foundation** (#5) · **Asset Ledger C1** (#6) · **Cost & Value Intelligence C3** (#7) · **Governance Orchestration C2** (#8) · **Experience Layer C7** (#9) · **Provider framework C4.5, Phase A** (#10).
- **Provider Integration (C4.5), P6-T01 — Phase A (tenant-independent):** the complete provider **framework** — `IProvider` contract, `ProviderManifest`, capability/health/versioning/freshness/coverage/error models, `ProviderContractValidator`, `IProviderRegistry` (validate-on-register, reject dupes/non-conforming), discovery, `ProviderDiagnostics`, native-identifier mapping, capability negotiation, auth/authz abstraction, `SyncSchedule` + `IWatermarkStore`, the **Manual CSV provider** (ADR-013, "Self-reported / Manual Import"), the **provider test harness** (any provider through identical contract checks — conforming passes, broken fails), and the integration-test framework. Host-wired (Development) with a read-only tenant-gated `/api/admin/providers` discovery endpoint. **Every provider is equal** — Microsoft, CSV, OpenAI, Anthropic, Google, ServiceNow implement the same contracts; no provider-specific logic outside a provider. **93 backend tests green**; 0 vulnerable production packages.

## In progress (PR open)

- **C4 observation ingestion (the "one door in"), P6-T02 — tenant-independent:** the C4 invariant pipeline (ADR-009/020) that turns provider `RawObservation`s into **immutable, append-only, pre-resolution** `ProviderObservation`s — contract-validate → **privacy Gate 1 (L1, set once)** → **delta-suppress** (watermark) → append → emit **`ObservationIngested`** to the hash-chained stream + outbox. Append-only `IObservationStore` (+ `IngestionRun` log), dev-only in-memory substitute. **Proven end-to-end with the manual CSV provider** (identical re-sweep fully suppressed; changed attribute recorded as Changed; each append emits one event + one outbox message), tenant-scoped, no ledger read/write (I3). Stops exactly at the event boundary. **100 backend tests green**; 0 vulnerable production packages. Merge Readiness Report on the PR.

## Blocked (TENANT GATE — Phase B, external dependency)

- **Phase B — Microsoft providers + Gate-1 PoC execution:** a human gate. Requires a provisioned M365 tenant, licences, admin consent, and sample agent archetypes (full list + Gate-1 plan + rollback in **`docs/build/plans/PROVIDER-INTEGRATION-READINESS.md`**). **Wave 0** (zero-cost validation) is the next human step; a **Microsoft dev sandbox** was chosen but **could not be provisioned — Microsoft's provisioning service is currently unavailable** (external dependency, not a project blocker). No Microsoft/Graph/Copilot/Entra code exists and no permissions were requested.

## Required Arun actions

- **Retry Wave 0 provisioning** when Microsoft's service is back (dev sandbox → the three zero-cost checks), then return the results per the readiness plan.
- **Branch protection + CODEOWNERS enforcement + `production` environment** (manual GitHub config) — recommended.

## Test status

- Backend: **100 tests green** (Platform 10, Ledger 13, Governance 17, Economics 16, Providers 20, Architecture 5, Host.Web 19); build 0/0. SPA: **5 vitest green**; `npm run build` clean; production deps 0 vulns.

## Deployment status

- Nothing deployed. No production access/credentials/secrets. No frozen-doc changes.

## Next autonomous train

- **C1 resolution segment** (tenant-independent mechanism): consume `ObservationIngested` → deterministic identifier join → `AIAsset` link / MergeCase → confidence roll-up, with **host-composed cross-module event delivery** (Providers never references Ledger). The **confidence rule table and alias types are PoC-gated (PoC-1/2)** and stay provisional until the Microsoft tenant is provisioned — the mechanism is built and proven with the CSV path; the Microsoft-specific join rules wait for Gate-1. Then **Phase B** on tenant availability.
