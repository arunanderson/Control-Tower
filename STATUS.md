# Enterprise AI Control Tower — Build Status

_Single source of build truth. Updated by the build agent as part of every task's Document step._

| Field             | Value                                                                                                  |
| ----------------- | ------------------------------------------------------------------------------------------------------ |
| **Current phase** | Phase 5 — Experience Layer (C7)                                                                        |
| **Current epic**  | Experience complete (PR open); Provider Integration abstraction (C4) next                              |
| **Current task**  | P5-T01 complete                                                                                        |
| **Overall state** | **Building autonomously via merge trains. No blocking gate.**                                          |
| **Merge policy**  | Merge trains — agent merges tenant-independent green PRs with a Merge Readiness Report; emergent-first |
| **Last updated**  | 2026-07-17                                                                                             |
| **Updated by**    | Claude Code (build agent)                                                                              |

## Completed & merged on `main`

- **E0/E1 rails + CI** (#1) · **E2 platform skeleton + tenancy** (#3) · **E3 event backbone** (#4) · **Platform foundation** (#5) · **Asset Ledger C1** (#6) · **Cost & Value Intelligence C3** (#7) · **Governance Orchestration C2** (#8).

## In progress (PR open)

- **Experience Layer (C7), P5-T01:** the read-model-only **API contract** (`/api/*`, tenant-gated) exposing Portfolio + the single polymorphic **Asset Record**, Economics (executive/portfolio/departments/agents), Governance (cases/debt), **Trust (honest coverage, C1.6)**, Administration — plus a **React/TS SPA** (Vite) with the five areas: Executive Dashboard, Portfolio + Asset Record, Economics views, Governance workbench, Trust & Coverage, Administration. UI **consumes read models only** (no domain access, no calculations, no contract bypass); **evidence/confidence/validation/as-of shown everywhere**; coverage/freshness honest; governance debt + recommendation outcomes shown; tenant-isolated. **80 backend tests + 5 SPA tests green**; 0 vulnerable production packages; web CI wired. Merge Readiness Report on the PR.

## Blocked (parallel workstream, not the critical path)

- **Gate-1 PoC execution** + **Microsoft provider adapters** — need a provisioned M365 tenant + consent.

## Required Arun actions

- **Branch protection + CODEOWNERS enforcement + `production` environment** (manual GitHub config) — recommended.
- **Provision the Gate-1 tenant** when convenient (unblocks the parallel provider/PoC workstream).
- Otherwise none — tenant-independent trains merge on green + Merge Readiness Report.

## Test status

- Backend: **80 tests green** (Platform 10, Ledger 13, Governance 17, Economics 16, Architecture 5, Host.Web 19); build 0/0. SPA: **5 vitest green**; `npm run build` clean; production deps 0 vulns.

## Deployment status

- Nothing deployed. No production access/credentials/secrets. No frozen-doc changes.

## Next autonomous train

- **Priority 6 — Provider Integration (C4):** implement the **approved provider abstraction only** — the C4.5 provider manifest + plug-in contract + registry (the ADR-007 promise), tenant-independent, no provider-specific code. **Microsoft provider adapters and the Gate-1 PoCs remain the parallel, tenant-gated workstream** — they proceed behind the provider interfaces once a tenant + consent are provisioned (a human gate).
