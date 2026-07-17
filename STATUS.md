# Enterprise AI Control Tower — Build Status

_Single source of build truth. Updated by the build agent as part of every task's Document step._

| Field             | Value                                                                                                  |
| ----------------- | ------------------------------------------------------------------------------------------------------ |
| **Current phase** | Phase 6 — Provider Integration (C4)                                                                    |
| **Current epic**  | Provider framework (C4.5) complete, Phase A (PR open); Phase B stops at the Microsoft-tenant gate      |
| **Current task**  | P6-T01 complete                                                                                        |
| **Overall state** | **Phase A merged-ready. Phase B (Microsoft providers + Gate-1 PoCs) STOPS at the tenant gate.**        |
| **Merge policy**  | Merge trains — agent merges tenant-independent green PRs with a Merge Readiness Report; emergent-first |
| **Last updated**  | 2026-07-17                                                                                             |
| **Updated by**    | Claude Code (build agent)                                                                              |

## Completed & merged on `main`

- **E0/E1 rails + CI** (#1) · **E2 platform skeleton + tenancy** (#3) · **E3 event backbone** (#4) · **Platform foundation** (#5) · **Asset Ledger C1** (#6) · **Cost & Value Intelligence C3** (#7) · **Governance Orchestration C2** (#8) · **Experience Layer C7** (#9).

## In progress (PR open)

- **Provider Integration (C4.5), P6-T01 — Phase A (tenant-independent):** the complete provider **framework** — `IProvider` contract, `ProviderManifest`, capability/health/versioning/freshness/coverage/error models, `ProviderContractValidator`, `IProviderRegistry` (validate-on-register, reject dupes/non-conforming), discovery, `ProviderDiagnostics`, native-identifier mapping, capability negotiation, auth/authz abstraction, `SyncSchedule` + `IWatermarkStore`, the **Manual CSV provider** (ADR-013, "Self-reported / Manual Import"), the **provider test harness** (any provider through identical contract checks — conforming passes, broken fails), and the integration-test framework. Wired into the host (Development) with a read-only tenant-gated `/api/admin/providers` discovery endpoint. **Every provider is equal** — Microsoft, CSV, OpenAI, Anthropic, Google, ServiceNow implement the same contracts; no provider-specific logic outside a provider. **93 backend tests green**; 0 vulnerable production packages. Merge Readiness Report on the PR.

## Blocked (TENANT GATE — Phase B)

- **Phase B — Microsoft providers + Gate-1 PoC execution:** STOPPED at the human gate. Requires a provisioned M365 tenant, licences, admin consent, and sample agent archetypes. The full provisioning list, Gate-1 execution plan, validation criteria, estimated time, and rollback are in **`docs/build/plans/PROVIDER-INTEGRATION-READINESS.md`**. No Microsoft/Graph/Copilot/Entra code exists and no permissions were requested.

## Required Arun actions

- **Provision the Gate-1 Microsoft tenant** (permissions, Entra app registration, licences, admin consent, sample archetypes, test users) per `PROVIDER-INTEGRATION-READINESS.md` — unblocks Phase B.
- **Branch protection + CODEOWNERS enforcement + `production` environment** (manual GitHub config) — recommended.

## Test status

- Backend: **93 tests green** (Platform 10, Ledger 13, Governance 17, Economics 16, Providers 13, Architecture 5, Host.Web 19); build 0/0. SPA: **5 vitest green**; `npm run build` clean; production deps 0 vulns.

## Deployment status

- Nothing deployed. No production access/credentials/secrets. No frozen-doc changes.

## Next autonomous train

- **None without the tenant.** All tenant-independent priorities (1–6 Phase A) are implemented. The next step — **Phase B (Gate-1 PoCs + Microsoft providers behind the existing contracts)** — is a **hard human gate** awaiting tenant provisioning. On provisioning, execution proceeds autonomously per the readiness plan; no blueprint change.
