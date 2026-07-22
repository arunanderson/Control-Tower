# Enterprise AI Control Tower — Build Status

_Single source of build truth. Updated by the build agent as part of every task's Document step._

| Field             | Value                                                                                                  |
| ----------------- | ------------------------------------------------------------------------------------------------------ |
| **Current phase** | Phase 5 — Enterprise readiness foundations                                                             |
| **Current epic**  | Build-state reconciliation after enterprise-control merge trains                                       |
| **Current task**  | P0-T17 complete — repository state reconciled through PR #18                                           |
| **Overall state** | **Development capability slices are green; production readiness remains materially incomplete.**       |
| **Merge policy**  | Merge trains — agent merges tenant-independent green PRs with a Merge Readiness Report; emergent-first |
| **Last updated**  | 2026-07-22                                                                                             |
| **Updated by**    | Claude Code (build agent)                                                                              |

## Completed & merged on `main`

- **E0/E1 rails + CI** (#1) · **E2 platform skeleton + tenancy** (#3) · **E3 event backbone** (#4) · **Platform foundation** (#5) · **Asset Ledger C1** (#6) · **Cost & Value Intelligence C3** (#7) · **Governance Orchestration C2** (#8) · **Experience Layer C7** (#9) · **Provider framework C4.5, Phase A** (#10) · **C4 observation ingestion** (#11) · **C1 entity resolution** (#12) · **Resolution Workbench** (#13) · **coverage/freshness** (#14) · **privileged-read audit** (#15) · **provider sweep execution** (#16) · **reporting snapshots** (#17) · **legal hold** (#18).
- **C4 observation ingestion (P6-T02):** the "one door in" (ADR-009/020) — contract-validate → privacy Gate 1 (L1) → delta-suppress → append immutable `ProviderObservation` → emit `ObservationIngested` to the hash-chained stream + outbox.
- **C1 entity resolution (P6-T03):** consumes `ObservationIngested` via **host-composed delivery** (Providers ⊥ Ledger); identity aliases, **deterministic matching**, link / new-asset / **MergeCase**; **High auto-links, sub-High never auto-links**; **lowest-confidence-wins** roll-up; **merge/split** without touching observations; links **severed/superseded, never deleted**; tenant-isolated + idempotent; Microsoft rules PoC-gated.
- **C4 sweep execution (P6-T07):** secret-free tenant job enters the event/outbox backbone; the worker resolves an existing provider connection and invokes the invariant ingestion pipeline with idempotent completion and retry-safe claims (#16).

## Paused local draft

- **P5-T04 retention enforcement:** incomplete work is preserved outside `main` in a named local Git stash. It is not committed, pushed, active or represented as implemented. Its contract must require an authoritative jurisdiction-policy provider rather than operator-supplied legal bounds.

## Blocked (TENANT GATE — Phase B, external dependency)

- **Phase B — Microsoft providers + Gate-1 PoC execution:** Cursor is waiting at Wave 0 Step 0.3 for a human permission-catalogue check. No permission, consent or credential should be added during that check. Microsoft provider implementation remains gated on real PoC findings.
- **Retention jurisdiction authority:** actual jurisdiction floors/ceilings require an authoritative, versioned Legal-owned policy source. The engine may be built against that fail-closed port, but production policy values cannot be invented by an implementation agent.

## Required Arun actions

- **Complete Cursor's Wave 0 Step 0.3** permission-catalogue check and return the observed permission names or “none found.”
- **Confirm Legal ownership and source** for versioned jurisdiction retention floors/ceilings before production policy activation.
- **Branch protection + CODEOWNERS enforcement + `production` environment** (manual GitHub config) — recommended.

## Test status

- Backend: **135 tests green** (Platform 10, Ledger 27, Governance 17, Economics 20, Providers 24, Architecture 5, Host.Web 32); build 0/0. SPA: **10 vitest green**; `npm run build` clean; production dependencies 0 vulnerabilities.

## Deployment status

- Nothing deployed. The web host registers application modules only in Development; the worker still uses in-memory adapters. There is no production PostgreSQL/RLS, Service Bus, Key Vault, Blob/WORM, Entra authentication, IaC, observability or DR evidence. No production access/credentials/secrets and no frozen-doc changes.

## Next autonomous train

- **Policy-driven retention mechanism**, using a versioned jurisdiction-policy provider and failing closed without an authoritative rule. It must call C9 legal-hold precedence before deletion and event every deletion. Actual legal values remain human-governed. **Phase B** remains tenant-gated.

## Overall Blueprint Completion

| Capability            | Status      | Notes                                                                                 |
| --------------------- | ----------- | ------------------------------------------------------------------------------------- |
| Platform Foundation   | Complete    | DI, tenancy (AsyncLocal, by-construction), event store + hash chain, outbox (#1–#5)   |
| Asset Ledger (C1)     | Complete    | AIAsset aggregate, lifecycle, ownership, aliases, resolution links (#6)               |
| Provider Framework    | Complete    | C4.5 plug-in framework + CSV provider + harness (#10)                                 |
| Observation Pipeline  | Complete    | C4 "one door in": observe → privacy-mark → delta-suppress → append → emit (#11)       |
| Entity Resolution     | Complete    | Deterministic match / MergeCase / lowest-wins / merge-split; MS rules PoC-gated (#12) |
| Governance (C2)       | Complete    | Cases, tiered approvals, waivers, retirement, reuse, debt (#8)                        |
| Economics (C3)        | Complete    | One model, many ROI views; signed immutable snapshots/restatement (#7, P5-T02)        |
| Experience (C7)       | Complete    | Five areas + Asset Record (#9); Resolution Workbench (#13); live coverage projection  |
| Decision Intelligence | Not Started | C6 intentionally vacant (ADR-010) — no separate context planned                       |
| Provider Integrations | Blocked     | Microsoft Graph/Copilot/Entra/PPAC providers — need the tenant (Phase B)              |
| Microsoft PoCs        | Blocked     | Gate-1 PoC-1/2/3 — need the tenant; Wave 0 provisioning externally unavailable        |
| Production Readiness  | In Progress | Development seams only; identity, Gate 2, exports, Azure adapters/IaC and DR remain   |
