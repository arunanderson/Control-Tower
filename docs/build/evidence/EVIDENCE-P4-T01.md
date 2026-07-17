---
id: EVIDENCE-P4-T01
type: evidence-bundle
schema_version: 1
task: P4-T01
status: submitted
produced_at: 2026-07-17
---

## Task

Governance Orchestration (C2) — GovernanceCase lifecycle, tiered approvals, reviews, waivers, retirement, reuse decisions.

## What changed (files)

- `Domain/` — `Primitives` (ids, enums, `ActorRef`), `ApprovalRouting` (risk-tier routing + SLA + auto-approve), `GovernanceEvents`, `GovernanceCase` (aggregate: intake/approval/decision/waiver/recert/retire/reuse/native-intent/notification-intent), `GovernanceDebtItem`.
- `Application/` — `IGovernanceStore`, `INativeControlOrchestrator` (contract) + receipt, read models (`GovernanceCaseView`, `GovernanceDebtView`), `GovernanceService`.
- `Infrastructure/` — dev-only `InMemoryGovernanceStore`, `RecordingNativeControlOrchestrator` (Enforced=false). `AddGovernanceModule`; dev-only `/governance/cases` + `/governance/debt`.
- `tests/` — `GovernanceCaseTests` (12), `GovernanceIntegrationTests` (5).

## Acceptance criteria → result

| Criterion                                   | Evidence                                                                                                | Pass/Fail |
| ------------------------------------------- | ------------------------------------------------------------------------------------------------------- | --------- |
| Tenant isolation                            | Integration: open in A → empty in B                                                                     | PASS      |
| Valid + invalid state transitions           | Unit: low auto-approve; medium/high routing; decision on closed case + non-required role throw          | PASS      |
| Approval routing by risk tier               | Unit: Low auto; Medium=Governance; High=Governance+Security+Business                                    | PASS      |
| Waiver expiry (time-bound)                  | Unit: not expired before, Expired after                                                                 | PASS      |
| Recertification expiry                      | Unit: Expired when overdue                                                                              | PASS      |
| Retirement flow                             | Unit: Retired + RetirementRequested event                                                               | PASS      |
| Reuse decision recording                    | Unit + integration: action/justification/outcome preserved; BuildNew never blocked                      | PASS      |
| Governance debt creation                    | Integration: Ownerless + LapsedOwner debt                                                               | PASS      |
| Audit completeness                          | Integration: CaseOpened + DecisionRecorded + CaseApproved on the stream                                 | PASS      |
| No native enforcement by C2                 | Integration: receipt Recorded=true, Enforced=false                                                      | PASS      |
| No duplicate lifecycle state outside Ledger | GovernanceCase stores CaseStatus + AssetId only; module has no dependency on Ledger (ArchitectureTests) | PASS      |

## Commands run + raw output (local, 2026-07-17; .NET 8.0.423)

```
dotnet build ControlTower.sln -c Release → Build succeeded. 0 Warning(s) 0 Error(s)
dotnet test  ControlTower.sln -c Release → 64 passed
  Platform 10 · Ledger 13 · Governance 17 · Economics 16 · Architecture 5 · Host.Web 3
dotnet list package --vulnerable --include-transitive → NO VULNERABLE PACKAGES
```

## Reviewer notes / technical debt

- `GovernanceCase` records the target Ledger transition as intent/events; the actual C1 transition is
  driven via the event stream (wiring in a later train). Governance debt is populated via a service
  command; in production C1 ownership events (OwnershipLapsed) drive it. Native-control invocation is a
  recording contract only (C4.6 adapters are V2). Notifications are domain intents; delivery via
  Teams/email (C7.4) is later. Azure PostgreSQL store replaces the in-memory dev substitute.
- No workflow engine, no security enforcement, no model gateway, no new bounded context were introduced
  (constraints honoured); the "no module depends on another module" architecture test proves C2 shares
  no lifecycle type with C1.
