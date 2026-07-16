# Stage 3 Charter — Microsoft Platform Validation

| | |
|---|---|
| **Version** | 1.0 |
| **Date** | 2026-07-15 |
| **Status** | Charter approved in principle (PD-003); **execution awaiting Arun's go** |
| **Related** | [stage-02-capability-model.md](stage-02-capability-model.md), [challenge-02-v1-minimization.md](challenge-02-v1-minimization.md), [decision-log.md](decision-log.md) (PD-003), [research-annex-2026-07.md](research-annex-2026-07.md) |

## Objective

Prove the platform is technically feasible before any UX or implementation design. For every consumed or orchestrated capability, validate what Microsoft actually provides — never assume, never invent APIs (project rule).

## Validation matrix — attributes per capability/surface pair

Every row of the Stage 3 deliverable answers, per capability:

1. Does Microsoft already provide it?
2. Which Microsoft service owns it?
3. Which API or data source provides it?
4. Officially supported? (GA / preview / undocumented-internal)
5. Documented? (link)
6. Authentication model
7. Permissions required (least privilege)
8. Rate limits
9. Data freshness (latency, refresh cadence)
10. Known limitations
11. Missing data (what the capability needs that the surface does not expose)
12. **Confidence: Confirmed / Likely / Unknown / Requires proof of concept**

## Scope — surfaces to validate (priority order per ADR-011: V1 capabilities first)

**First pass (V1 feeds):**

| Surface | Feeds capability | Existence |
|---|---|---|
| Microsoft Agent 365 registry & monitoring | C4.1/C4.2 → C1, C3.7 | [Confirmed product; API surface unvalidated] |
| Microsoft Entra Agent ID (agent identities, owners/sponsors) | C4.1 → C1.4 | [Confirmed product] |
| Power Platform admin center Inventory + Inventory API | C4.1 → C1 | [Confirmed, incl. API claim — validate depth] |
| Microsoft Graph Copilot usage reports | C4.2 → C3.3/C3.7 | [Confirmed GA] |
| M365 admin center Cost Management / Copilot Credits consumption (per user/group/service/agent) | C4.3 → C3.1/C3.2 | [Confirmed product; API/export path unvalidated] |
| Azure Cost Management (AI workload spend) | C4.3 → C3.1 | [Confirmed product] |
| Microsoft Purview DSPM for AI signals (incl. third-party AI site detection) | C4.2 → C3.7 | [Confirmed product; extractability unvalidated] |
| Microsoft Foundry Control Plane (deployments, agents, cost/token governance) | C4.1/C4.3 → C1, C3 | [Confirmed product] |
| Entra identity for our own platform (auth, multi-tenant consent model) | C8.1 | [Confirmed pattern; scopes to be enumerated] |

**Critical cross-cutting question (R-12/OI-009), answered first:** do these surfaces expose **stable, correlatable identifiers** for the same asset (e.g., an agent visible in Agent 365, Entra Agent ID, and PPAC)? This is the go/no-go for C1.3 and the ledger concept.

**Second pass (V1.5/V2 feeds):** CCS agent management actions (block/approve — write APIs?), Entra Agent ID lifecycle/Conditional Access invocation (C2.8/C4.6), Purview audit/eDiscovery exports (C9.2 evidence), Viva Insights/Copilot Dashboard (adoption depth), M365 admin centralized agent dashboard reporting.

## Method

- Primary: official Microsoft Learn documentation, Graph API references, release notes. Secondary: reputable practitioner sources (flagged Likely at best).
- Anything not confirmable from documentation → **Requires proof of concept**, with the PoC question stated precisely. No PoC code in planning (PD-001) — PoCs are listed, not executed.
- Licensing dependencies recorded per surface (E7/Agent 365, Copilot licences, Viva Insights, Purview SKUs) → feeds OI-004 degradation tiers.
- Every claim dated; this market moves quarterly (R-01).

## Deliverables

1. `stage-03-microsoft-validation.md` — the full validation matrix.
2. Go/no-go assessment on C1.3 reconciliation (R-12).
3. Licence-dependency map per feed (OI-004).
4. Revised C4 provider list: V1 feeds that survive validation; gaps flagged with fallback options.
5. PoC backlog (questions only).
6. Updates to RTM (API column), risks, parking lot.

## Exit criteria

Stage 3 is done when every V1 capability's feeds carry a confidence label, R-12 has a verdict, and the V1 scope (ADR-011) is confirmed feasible, amended, or escalated to Arun with options.
