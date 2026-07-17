# Enterprise AI Control Tower — Build Status

_Single source of build truth. Updated by the build agent as part of every task's Document step._

| Field             | Value                                                                                                  |
| ----------------- | ------------------------------------------------------------------------------------------------------ |
| **Current phase** | Phase 2 — Asset Ledger (C1)                                                                            |
| **Current epic**  | Asset Ledger complete (PR open); Cost & Value Intelligence (C3) next                                   |
| **Current task**  | P2-T01 complete                                                                                        |
| **Overall state** | **Building autonomously via merge trains. No blocking gate.**                                          |
| **Merge policy**  | Merge trains — agent merges tenant-independent green PRs with a Merge Readiness Report; emergent-first |
| **Last updated**  | 2026-07-17                                                                                             |
| **Updated by**    | Claude Code (build agent)                                                                              |

## Completed & merged on `main`

- **E0/E1 rails + CI** (PR #1) · **E2 platform skeleton + tenancy + NetArchTest** (PR #3) · **E3 event backbone** (PR #4) · **Platform foundation** — host DI, tenancy middleware, outbox dispatcher, dev adapters (PR #5).

## In progress (PR open)

- **Asset Ledger (C1), P2-T01:** `AIAsset` aggregate (one aggregate, all types) · `RegistrationStatus` + `OperationalLifecycle` state machines (guarded) · temporal ownership with **Ownerless/Lapsed** first-class · provider identifier aliases + `ResolutionLink` foundations + **MatchConfidence** roll-up · domain events appended to the immutable stream · `TaxonomyScheme` · tenant-scoped repository + read model via ports (dev in-memory; PostgreSQL later) · registration workflow with an authorization seam · dev-only `/assets` read endpoint. **31 tests green** (Platform 10, Architecture 5, Ledger 13, Host.Web 3); 0 vulnerable packages. Emergent within C1 — no new context/aggregate. Merge Readiness Report on the PR.

## Blocked (parallel workstream, not the critical path)

- **Gate-1 PoC execution** + **Phase-2 provider adapters** — need a provisioned M365 tenant + consent.

## Required Arun actions

- **Branch protection + CODEOWNERS enforcement + `production` environment** (manual GitHub config) — recommended.
- **Provision the Gate-1 tenant** when convenient (unblocks the parallel provider/PoC workstream).
- Otherwise none — tenant-independent trains merge on green + Merge Readiness Report.

## Test status

- Local & CI: **31 tests green**; build 0/0; 0 vulnerable packages; module + adapter boundaries enforced.

## Deployment status

- Nothing deployed. No production access/credentials/secrets. No frozen-doc changes.

## Next autonomous train

- **Priority 3 — Cost & Value Intelligence (C3):** cost observations, cost allocation + Unattributed, usage/adoption facts, value declarations with the six evidence classes, Finance-validation states, ROI calculation projections, **Agent ROI as an emergent read model over `AIAsset`** (no separate module), and portfolio/department/agent economics projections — with tests proving no economic figure is exposed without evidence class, source, methodology, and as-of date. Tenant-independent. Provider integrations remain the parallel tenant workstream.
