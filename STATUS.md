# Enterprise AI Control Tower — Build Status

_Single source of build truth. Updated by the build agent as part of every task's Document step._

| Field             | Value                                                                                                   |
| ----------------- | ------------------------------------------------------------------------------------------------------- |
| **Current phase** | Phase 6 — Provider Integration (C4)                                                                     |
| **Current epic**  | Tenant-independent C4 pipeline including queued sweep-job worker execution; Phase B stays gated         |
| **Current task**  | P6-T07 complete — secret-free sweep request and idempotent worker execution                             |
| **Overall state** | **Building tenant-independent C4/C1 work; Microsoft providers await the tenant (external dependency).** |
| **Merge policy**  | Merge trains — agent merges tenant-independent green PRs with a Merge Readiness Report; emergent-first  |
| **Last updated**  | 2026-07-22                                                                                              |
| **Updated by**    | Claude Code (build agent)                                                                               |

## Completed & merged on `main`

- **E0/E1 rails + CI** (#1) · **E2 platform skeleton + tenancy** (#3) · **E3 event backbone** (#4) · **Platform foundation** (#5) · **Asset Ledger C1** (#6) · **Cost & Value Intelligence C3** (#7) · **Governance Orchestration C2** (#8) · **Experience Layer C7** (#9) · **Provider framework C4.5, Phase A** (#10) · **C4 observation ingestion** (#11) · **C1 entity resolution** (#12) · **Resolution Workbench** (#13).
- **C4 observation ingestion (P6-T02):** the "one door in" (ADR-009/020) — contract-validate → privacy Gate 1 (L1) → delta-suppress → append immutable `ProviderObservation` → emit `ObservationIngested` to the hash-chained stream + outbox.
- **C1 entity resolution (P6-T03):** consumes `ObservationIngested` via **host-composed delivery** (Providers ⊥ Ledger); identity aliases, **deterministic matching**, link / new-asset / **MergeCase**; **High auto-links, sub-High never auto-links**; **lowest-confidence-wins** roll-up; **merge/split** without touching observations; links **severed/superseded, never deleted**; tenant-isolated + idempotent; Microsoft rules PoC-gated.

## In progress (current merge train)

- **C4 provider sweep jobs (P6-T07) — tenant-independent:** secret-free tenant job enters the hash-chained event store and outbox; the worker resolves the configured provider and calls the established ingestion pipeline. Completed replay is idempotent; failure releases for bounded Service Bus retry/DLQ. No in-process durable scheduler. **126 backend + 10 SPA tests green**; 0 vulnerable production packages.

## Blocked (TENANT GATE — Phase B, external dependency)

- **Phase B — Microsoft providers + Gate-1 PoC execution:** a human gate. Requires a provisioned M365 tenant, licences, admin consent, and sample agent archetypes (full list + Gate-1 plan + rollback in **`docs/build/plans/PROVIDER-INTEGRATION-READINESS.md`**). **Wave 0** (zero-cost validation) is the next human step; a **Microsoft dev sandbox** was chosen but **could not be provisioned — Microsoft's provisioning service is currently unavailable** (external dependency, not a project blocker). No Microsoft/Graph/Copilot/Entra code exists and no permissions were requested.

## Required Arun actions

- **Retry Wave 0 provisioning** when Microsoft's service is back (dev sandbox → the three zero-cost checks), then return the results per the readiness plan.
- **Branch protection + CODEOWNERS enforcement + `production` environment** (manual GitHub config) — recommended.

## Test status

- Backend: **126 tests green** (Platform 10, Ledger 27, Governance 17, Economics 16, Providers 24, Architecture 5, Host.Web 27); build 0/0. SPA: **10 vitest green**; `npm run build` clean; production dependencies 0 vulnerabilities.

## Deployment status

- Nothing deployed. No production access/credentials/secrets. No frozen-doc changes.

## Next autonomous train

- **Next tenant-independent capability from the implementation handoff package** — enterprise-readiness foundations (legal hold, export/deletion, snapshot freeze, retention) that need no production credentials. Decision Intelligence (C6) is intentionally vacant (ADR-010). **Phase B** remains tenant-gated.

## Overall Blueprint Completion

| Capability            | Status      | Notes                                                                                 |
| --------------------- | ----------- | ------------------------------------------------------------------------------------- |
| Platform Foundation   | Complete    | DI, tenancy (AsyncLocal, by-construction), event store + hash chain, outbox (#1–#5)   |
| Asset Ledger (C1)     | Complete    | AIAsset aggregate, lifecycle, ownership, aliases, resolution links (#6)               |
| Provider Framework    | Complete    | C4.5 plug-in framework + CSV provider + harness (#10)                                 |
| Observation Pipeline  | Complete    | C4 "one door in": observe → privacy-mark → delta-suppress → append → emit (#11)       |
| Entity Resolution     | Complete    | Deterministic match / MergeCase / lowest-wins / merge-split; MS rules PoC-gated (#12) |
| Governance (C2)       | Complete    | Cases, tiered approvals, waivers, retirement, reuse, debt (#8)                        |
| Economics (C3)        | Complete    | One model, many ROI read models; evidence on every figure (#7)                        |
| Experience (C7)       | Complete    | Five areas + Asset Record (#9); Resolution Workbench (#13); live coverage projection  |
| Decision Intelligence | Not Started | C6 intentionally vacant (ADR-010) — no separate context planned                       |
| Provider Integrations | Blocked     | Microsoft Graph/Copilot/Entra/PPAC providers — need the tenant (Phase B)              |
| Microsoft PoCs        | Blocked     | Gate-1 PoC-1/2/3 — need the tenant; Wave 0 provisioning externally unavailable        |
| Production Readiness  | Not Started | Azure adapters, IaC, deployment — needs production credentials + PO deploy decision   |
