# Enterprise AI Control Tower — Build Status

_Single source of build truth. Updated by the build agent as part of every task's Document step._

| Field             | Value                                                                                                   |
| ----------------- | ------------------------------------------------------------------------------------------------------- |
| **Current phase** | Phase 6 — Provider Integration (C4)                                                                     |
| **Current epic**  | C4→C1 pipeline (ingestion + entity resolution) built tenant-independently; Phase B stays gated          |
| **Current task**  | P6-T03 complete (PR open)                                                                               |
| **Overall state** | **Building tenant-independent C4/C1 work; Microsoft providers await the tenant (external dependency).** |
| **Merge policy**  | Merge trains — agent merges tenant-independent green PRs with a Merge Readiness Report; emergent-first  |
| **Last updated**  | 2026-07-22                                                                                              |
| **Updated by**    | Claude Code (build agent)                                                                               |

## Completed & merged on `main`

- **E0/E1 rails + CI** (#1) · **E2 platform skeleton + tenancy** (#3) · **E3 event backbone** (#4) · **Platform foundation** (#5) · **Asset Ledger C1** (#6) · **Cost & Value Intelligence C3** (#7) · **Governance Orchestration C2** (#8) · **Experience Layer C7** (#9) · **Provider framework C4.5, Phase A** (#10) · **C4 observation ingestion** (#11).
- **Provider framework (C4.5, P6-T01):** the complete provider plug-in framework — `IProvider`, manifest/capability/health/versioning/freshness/coverage/error models, contract validator, registry, diagnostics, negotiation, auth abstraction, watermarking, the Manual CSV provider (ADR-013), test harness. Every provider equal; no provider-specific logic outside a provider.
- **C4 observation ingestion (P6-T02):** the "one door in" (ADR-009/020) — contract-validate → privacy Gate 1 (L1) → delta-suppress → append immutable `ProviderObservation` → emit `ObservationIngested` to the hash-chained stream + outbox. Append-only `IObservationStore` + `IngestionRun` log.

## In progress (PR open)

- **C1 entity resolution (P6-T03) — tenant-independent:** the resolution engine (ADR-012) that consumes `ObservationIngested` through **host-composed delivery** (a Platform `IIntegrationEventHandler` + the worker's outbox dispatcher — **Providers never references Ledger**). It builds provider-scoped **identity aliases**, matches **deterministically** (exact native-id equality → DocumentedJoin/High), **links** observations to `AIAsset`s via `ResolutionLink`s, **creates a new asset only on no match**, and opens a **MergeCase** on identifier collision or ambiguity. The **MatchConfidence taxonomy** is enforced — High auto-links; **sub-High never auto-links** (manual merge queue); Manual = operator-approved. Asset confidence rolls up **lowest-confidence-wins** across active links. **Merge/split** are supported **without modifying `ProviderObservation`s**; links are **severed/superseded, never deleted**. Complete immutable audit events for link/sever/supersede/merge/split/confidence/merge-case; **tenant-isolated and idempotent**. **Microsoft aliases + confidence rules stay provisional and PoC-gated** (the deterministic classifier makes no Microsoft assumptions; the heuristic/weak rule table is a pluggable, PoC-gated seam). Proven end-to-end with the CSV provider + fixtures. **111 backend tests green**; 0 vulnerable production packages. Merge Readiness Report on the PR.

## Blocked (TENANT GATE — Phase B, external dependency)

- **Phase B — Microsoft providers + Gate-1 PoC execution:** a human gate. Requires a provisioned M365 tenant, licences, admin consent, and sample agent archetypes (full list + Gate-1 plan + rollback in **`docs/build/plans/PROVIDER-INTEGRATION-READINESS.md`**). **Wave 0** (zero-cost validation) is the next human step; a **Microsoft dev sandbox** was chosen but **could not be provisioned — Microsoft's provisioning service is currently unavailable** (external dependency, not a project blocker). No Microsoft/Graph/Copilot/Entra code exists and no permissions were requested.

## Required Arun actions

- **Retry Wave 0 provisioning** when Microsoft's service is back (dev sandbox → the three zero-cost checks), then return the results per the readiness plan.
- **Branch protection + CODEOWNERS enforcement + `production` environment** (manual GitHub config) — recommended.

## Test status

- Backend: **111 tests green** (Platform 10, Ledger 24, Governance 17, Economics 16, Providers 20, Architecture 5, Host.Web 19); build 0/0. SPA: **5 vitest green**; `npm run build` clean; production deps 0 vulns.

## Deployment status

- Nothing deployed. No production access/credentials/secrets. No frozen-doc changes.

## Next autonomous train

- **Next tenant-independent capability from the implementation handoff package** — e.g. a **C7 resolution / merge-case workbench** surface over the new read models (open merge cases, operator merge/split, alias graph), or surfacing privileged-read audit. Continue autonomously until a genuine human gate. **Phase B** (Gate-1 PoCs + Microsoft providers + finalizing the PoC-gated confidence rule table) proceeds on tenant availability; Wave 0 provisioning is currently blocked externally (Microsoft dev-sandbox service unavailable).
