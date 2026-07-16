# Stage 3 — Microsoft Platform Validation

| | |
|---|---|
| **Version** | 1.0 |
| **Date** | 2026-07-15 (all findings dated; this market moves quarterly — re-verify before build) |
| **Status** | Draft — awaiting Arun's approval |
| **Related** | [stage-03-microsoft-validation-charter.md](stage-03-microsoft-validation-charter.md), [stage-02-capability-model.md](stage-02-capability-model.md) v1.1, [decision-log.md](decision-log.md) (ADR-012 proposed here), [open-issues-parking-lot.md](open-issues-parking-lot.md) |

**Method:** four parallel evidence streams over official Microsoft Learn/Graph documentation (pages fetched, not just searched). Labels: **[C]** Confirmed (fetched official doc) · **[L]** Likely (official doc located/secondary corroboration) · **[U]** Unknown · **[PoC]** Requires proof of concept. Nothing below is guessed; where documentation is silent, that silence is stated as the finding.

---

## 1. Headline verdicts

1. **The federated ledger (C1) is feasible — with an architecture change.** Deterministic cross-surface correlation is documented for a *subset* of assets; universal deterministic correlation is **not possible today**. Proposed ADR-012 (§4) adapts C1.3 accordingly. **R-12 verdict: AMBER — proceed with the alias-graph pattern; not a no-go.**
2. **The biggest V1 gap is cost, not inventory:** per-user/per-agent **Copilot Credits consumption has no API** — admin-center only [C]. This partially degrades the C3.2 wedge at launch and needs an explicit mitigation decision (§6, Question 2).
3. **Agent-level usage has no API** (admin-center preview report only) [C]; however, **Purview audit records** (`CopilotInteraction` with `AppIdentity` fields) are API-retrievable and may yield per-agent interaction counts [C schema / PoC end-to-end].
4. **The Package Management API — our M365 agent catalog feed — requires an Agent 365 licence** [C]. The licence dependency risk (R-05/OI-004) is now confirmed fact, not assumption.
5. **Microsoft deprecated the one API that solved correlation** (`agentInstance.sourceAgentId`, deprecated May 2026) with its replacement not yet fully shipped [C] — evidence that building on Microsoft's newest surfaces carries churn risk (R-01) and that our alias-graph must not depend on any single join.

## 2. Validation matrix — inventory surfaces (feeds C4.1 → C1)

| Surface | Exists / status | API | Auth / permissions | Licensing | Freshness | Key limitations | Confidence |
|---|---|---|---|---|---|---|---|
| **PPAC Inventory + API** | GA (Feb 2026; connector inventory preview) | `POST api.powerplatform.com/resourcequery/resources/query` (KQL-translated); also via Azure Resource Graph | Entra app; scope `api.powerplatform.com/.default`; **delegated-only permissions — service principals use PPAC RBAC roles** (Reader) | PP admin/D365 admin role; **no Managed Environments prerequisite stated** | ≤15 min (agents ~20 min) | Classic (V1) bots excluded; flow owner = creator; no GCC/DoD/21Vianet; rate limits undocumented [PoC] | **Confirmed** — [inventory](https://learn.microsoft.com/en-us/power-platform/admin/power-platform-inventory), [API](https://learn.microsoft.com/en-us/power-platform/admin/inventory-api), [schema](https://learn.microsoft.com/en-us/power-platform/admin/inventory-schema) |
| — coverage | Copilot Studio agents (incl. drafts), Agent Builder agents, canvas/model-driven/code apps, cloud/agent/M365 flows, environments | Rich per-agent schema: `botId`, `environmentId`, `schemaName`, `ownerId`, `createdBy`, **`entraAgentId`, `entraAgentBlueprintId`, `entraAppId`**, channels (names only, preview), model, orchestration, sharing counts, quarantine state | | | | Published version only; **no M365 manifest/title ID field**; Agent Builder agents carry no Entra identity props | **Confirmed** — [agent inventory](https://learn.microsoft.com/en-us/microsoft-copilot-studio/admin-agent-inventory) |
| **Graph Package Management API** (M365 agent catalog / Agent 365 registry API) | v1.0 reads; writes preview; **doc inconsistency: one page says API "in preview"** [PoC] | `GET /v1.0/copilot/admin/catalog/packages` (+`{id}`); filters: platform, elementTypes, supportedHosts | Delegated + **app-only**; `CopilotPackages.Read.All`; delegated also needs AI Admin role | **Requires Microsoft Agent 365 licence** [C] | Undocumented [U] | Returns *packages* (catalog entities), **no owner field, no Entra identity link, no bot/environment ID**; third-party registry-sync agents **not in API**; no activity/risk API; registry sync itself preview "not for production"; Global cloud only | **Confirmed** — [overview](https://learn.microsoft.com/en-us/microsoft-365/copilot/extensibility/api/admin-settings/package/overview), [list](https://learn.microsoft.com/en-us/microsoft-365/copilot/extensibility/api/admin-settings/package/copilotpackages-list), [resource](https://learn.microsoft.com/en-us/microsoft-365/copilot/extensibility/api/admin-settings/package/resources/copilotpackage) |
| **Entra Agent ID (Graph)** | **GA on v1.0** (agentIdentity, blueprints, agentUser); risk APIs beta; admin wizard preview | Full CRUD + list owners / **list sponsors**; sign-in & audit logs via standard Graph reports | Standard Graph; `AgentIdentity.*` family; roles AI Reader / Agent ID Administrator | Agent ID "part of Agent 365" — features gated on Agent 365/E7; CA for agents = P1; ID Protection = P2 | Directory reads ≈ real-time; sign-in log latency = standard Entra | **No documented property carrying source-platform agent ID**; pre-Mar-2026 Copilot Studio agents are plain SPs (tags only, e.g. `power-virtual-agents-{agent-id}`); no in-place migration; unpublished Foundry agents share one project identity | **Confirmed** — [platform overview v1.0](https://learn.microsoft.com/en-us/graph/api/resources/agentid-platform-overview?view=graph-rest-1.0), [what's new](https://learn.microsoft.com/en-us/entra/agent-id/whats-new-agent-id) |
| **Entra agentRegistry (beta)** — `agentInstance` with **`sourceAgentId` + `originatingStore`** | **Deprecated May 2026**; replacement (Agent 365 APIs) not fully shipped | beta only | — | — | — | The only documented Entra↔source-platform join, being retired — do not build on it | **Confirmed** — [beta overview](https://learn.microsoft.com/en-us/graph/api/resources/agentid-platform-overview?view=graph-rest-beta), [convergence](https://learn.microsoft.com/en-us/entra/agent-id/agent-registry-convergence) |
| **Foundry control plane (ARM)** | Projects List GA (2025-06-01); deployments GA; hosted-agent `agentDeployment` ARM **preview**; agents data-plane REST | ARM + `GET {endpoint}/api/projects/{p}/agents` (scope `ai.azure.com/.default`) | Entra RBAC (Foundry roles; Reader for ARM enumeration) | Azure consumption | ARM ≈ real-time | Rate limits for agents API undocumented [PoC] | **Confirmed/Likely** — [REST](https://learn.microsoft.com/en-us/rest/api/aifoundry/), [agent identity](https://learn.microsoft.com/en-us/azure/foundry/agents/concepts/agent-identity) |
| **Azure OpenAI (ARM)** | GA | Accounts + `Deployments - List` (model name/version/SKU) | Reader role sufficient [L] | Azure consumption | Real-time | — | **Confirmed/Likely** — [deployments list](https://learn.microsoft.com/en-us/rest/api/aiservices/accountmanagement/deployments/list?view=rest-aiservices-accountmanagement-2024-10-01) |
| **AI Builder models** | **Not in PPAC Inventory API** [C by omission]; no tenant-wide API found | Per-environment Dataverse tables possible [U] | — | — | — | **Coverage gap** — feeds the discovery coverage map (C1.6) | **Unknown / PoC** |

## 3. Validation matrix — telemetry & cost surfaces (feeds C4.2/C4.3 → C3)

| Surface | Exists / status | API | Key metrics | Licensing | Freshness | Critical limitations | Confidence |
|---|---|---|---|---|---|---|---|
| **Graph Copilot usage reports** | **GA v1.0** (`/copilot/reports/...UsageUserDetail`, UserCountSummary/Trend) | Yes; app-only `Reports.Read.All`; JSON/CSV | **Per-user last-activity per app only.** "Per-user prompt counts… isn't supported" (verbatim) [C] | Copilot licences (data scope) | 24–72 h | **Pseudonymized by default** — un-concealing is a tenant-wide, audited setting (`adminReportSettings`); licensed users only; Global cloud only | **Confirmed** — [copilotReportRoot](https://learn.microsoft.com/en-us/microsoft-365/copilot/extensibility/api/admin-settings/reports/resources/copilotreportroot), [activity reports](https://learn.microsoft.com/en-us/microsoft-365/admin/activity-reports/activity-reports) |
| **Agent-level usage** (M365 admin center agents usage report) | Report GA + richer version **preview** (roadmap 497999) | **No API** [C by omission — copilotReportRoot has exactly 3 methods, none agent-level] | Per-agent users/responses; per user–agent pair | — | ~1 h | **Admin-center only**; 7/30-day windows | **Confirmed** — [agents report](https://learn.microsoft.com/en-us/microsoft-365/admin/activity-reports/microsoft-365-copilot-agents-new) |
| **Copilot Credits consumption** (Cost Management + Credits report) | GA dashboard; report elements preview | **No API** [C by omission] | Per-user, per-agent, per user–agent credits; budgets/caps | PAYG/P3 setup | 2–4 h (dashboard); ~1 h (report) | **Admin-center only — the single biggest V1 gap**; 30-day history cap (preview) | **Confirmed** — [manage credits](https://learn.microsoft.com/en-us/microsoft-365/copilot/usage-based-billing-manage-copilot-credits), [credits report](https://learn.microsoft.com/en-us/microsoft-365/admin/activity-reports/microsoft-365-copilot-credits) |
| **PAYG cost (currency)** | GA | Bills to Azure subscription → **Azure Cost Management APIs** should expose service/meter-level cost (tag `m365copilotchat`) | Service-level cost, not per-agent | Azure | Daily, ≤24 h lag | Granularity depth unverified → **[PoC]** | **Likely** — [view cost](https://learn.microsoft.com/en-us/microsoft-365/copilot/pay-as-you-go/view-cost) |
| **Azure Cost Management** | GA | Cost Details API (EA/MCA), Exports, Query API (QPU-throttled: 12/10 s) | Line items per resource ID + meter + tags; actual & amortized | Azure | 4 h refresh; query ≤1×/day/scope | **No per-deployment cost line for AOAI** — reconstruct via meter × token metrics or tagging [PoC] | **Confirmed** — [automation](https://learn.microsoft.com/en-us/azure/cost-management-billing/costs/manage-automation) |
| **Azure Monitor metrics (AOAI/Foundry)** | GA | Metrics REST API | **Per-deployment token metrics** (prompt/completion/total), requests, TTFT — PT1M grain, dimensions incl. ModelDeploymentName/ModelName/ModelVersion | Azure | ~1–3 min | — | **Confirmed** — [monitoring reference](https://learn.microsoft.com/en-us/azure/foundry/openai/monitor-openai-reference) |
| **Licence assignment (Graph)** | GA | `subscribedSkus`, `licenseDetails`; app-only; `LicenseAssignment.Read.All` | Assigned vs purchased per SKU; per-user assignment | none | Real-time | Join to activity requires concealment off | **Confirmed** — [subscribedSkus](https://learn.microsoft.com/en-us/graph/api/subscribedsku-list?view=graph-rest-1.0), [throttling](https://learn.microsoft.com/en-us/graph/throttling-limits) |
| **Purview audit — AI interactions** | GA (audit); collection policies for enterprise AI apps; third-party sites via browser extension + endpoint onboarding | **Yes** — Office 365 Management Activity API (Copilot schema) + Graph audit log query [C/L] | `CopilotInteraction`/`AIAppInteraction` records: RecordType 261, **AppIdentity** (e.g., `Copilot.Studio.AppId`, `ConnectedAIApp.AzureAI.AzureResourceName`), message IDs (not content by default), accessed resources + sensitivity labels | E5/E5 Compliance for Copilot records; **Purview PAYG mandatory for third-party/enterprise AI app records** | Ingestion latency [PoC] | Prompts/responses sometimes absent (by design, fine for us); classic DSPM being replaced by unified DSPM — feature drift risk | **Confirmed** — [audit-copilot](https://learn.microsoft.com/en-us/purview/audit-copilot), [Copilot schema](https://learn.microsoft.com/en-us/office/office-365-management-api/copilot-schema), [DSPM considerations](https://learn.microsoft.com/en-us/purview/dspm-for-ai-considerations) |
| **Viva Insights Copilot Dashboard** | GA dashboard; **export preview** | **No REST API**; browser export (de-identified weekly user-level, incl. the only per-user **prompt counts** anywhere) + analyst Power BI connector | Prompt counts per app | ≥50 Copilot/Viva licences; premium for depth | Weekly | Manual/UI-gated; not schedulable | **Confirmed** — [export](https://learn.microsoft.com/en-us/viva/insights/org-team-insights/export-copilot-metrics) |
| **Graph `aiInteractionHistory`** | **beta — "not supported for production"** | beta only | Full prompt/response content | Copilot add-on | — | Content collection also violates ADR-003 defaults — noted and set aside | **Confirmed** — [resource](https://learn.microsoft.com/en-us/microsoft-365/copilot/extensibility/api/ai-services/interaction-export/resources/aiinteractionhistory) |

## 4. The correlation question (R-12) — verdict and proposed ADR-012

**Question:** can a single AI asset be deterministically correlated across Agent 365, Entra Agent ID, Copilot Studio, PPAC, Graph, Foundry, Azure OpenAI, and Purview?

**Verdict: NO — not universally. YES — for a well-defined modern subset. The spine exists but has documented holes, and Microsoft deprecated the one API that closed them.**

**What is deterministically joinable today [C]:**

| Join | Mechanism | Status |
|---|---|---|
| Copilot Studio agent ↔ Entra Agent ID | PPAC Inventory exposes `entraAgentId` + `entraAgentBlueprintId` + `entraAppId` per agent | **Documented, GA** — strongest join in the estate |
| Foundry published agent ↔ Entra Agent ID | Agent-application ARM resource exposes `agentIdentityId` | **Documented** (ARM→Entra direction) |
| Purview audit record ↔ source app | `AppIdentity` (`Copilot.Studio.AppId`, `ConnectedAIApp.AzureAI.AzureResourceName`) | **Documented schema** |
| M365 package ↔ Entra app registration | `copilotPackage.appId` | Documented field; join semantics to `entraAppId` **[PoC]** |
| Azure OpenAI/Foundry resources ↔ cost | ARM resource ID in Cost Management line items | **Documented** (account-level) |

**Documented holes [C]:** Copilot Studio bot GUID ↔ M365 manifest/title ID (neither side exposes the other; Microsoft's own docs note the two surfaces' counts don't reconcile); Agent Builder agents (no Entra identity properties); pre-March-2026 Copilot Studio agents (tag-based identification only); unpublished Foundry agents (shared project identity — project-level correlation only); third-party registry-synced agents (no API at all); AI Builder (no inventory); `agentInstance.sourceAgentId` — the only documented universal join — deprecated May 2026 with no shipped replacement.

### Proposed ADR-012 — Ledger correlation: identity-spine + alias graph + confidence tiers

Since deterministic correlation is partial, C1.3 is redesigned as an **entity-resolution model**, not a key join:

1. **Alias graph.** Each ledger asset owns a set of provider-scoped identifiers (botId+environmentId, manifestId, appId, agentIdentityId, ARM ID, package ID, audit AppIdentity…). No single "master key" is assumed — resilient to Microsoft's API churn by design.
2. **Entra Agent ID + appId as the preferred spine** where present — never as a requirement.
3. **Match-confidence tiers on every asset:** *Deterministic* (documented key join) / *Strong heuristic* (name+owner+environment+timestamps) / *Manual* (merged by operator) / *Unmatched*. The tier is **visible in the product** — the honest-data principle applied to the ledger itself.
4. **Manual merge/split queue** (already in C1.3 V1 scope) for heuristic and unmatched assets.
5. **Registration-time binding** (V1.5+): governance intake captures the asset's surface IDs at the gate — the gate becomes the correlator, converting heuristic matches to deterministic over time.
6. **Coverage map (C1.6) reports correlation quality per surface**, not just connection status.

This pattern turns the documented gaps from a product-killer into a visible, improving property of the ledger. **R-12 → AMBER, managed.**

## 5. Licence-dependency map (OI-004 — now evidence-based)

| Feed | Licence gate | Without it |
|---|---|---|
| Package Management API (M365 agent catalog) | **Agent 365 licence** [C] | No M365 catalog feed; ledger covers Power Platform (PPAC), Azure (ARM), Entra identities — coverage map shows the hole |
| Entra Agent ID features | Agent 365 / E7; CA-for-agents P1; ID Protection P2 [C] | Core Graph objects readable; registry viewing role-gated free |
| Graph usage reports | Copilot licences (data scope) [C] | Report exists but empty of Copilot data |
| Viva prompt-count export | ≥50 Copilot/Viva licences [C] | No prompt-intensity data at all |
| Purview third-party AI records | Purview PAYG [C]; Copilot records need E5/E5 Compliance | No shadow-AI telemetry feed |
| PPAC Inventory, ARM, Azure Monitor, Cost Mgmt, licence APIs | Admin roles / Azure only — no premium licence [C] | Always available — the guaranteed baseline |

**Product implication:** the guaranteed baseline (PPAC + ARM + Entra core + licence + Azure cost) already delivers a sellable ledger + partial economics. Premium Microsoft licensing widens coverage; the coverage map makes each customer's blind spots explicit. This *strengthens* the degradation-tier story rather than undermining it.

## 6. V1 feasibility assessment (against ADR-011's 25 capabilities)

| Capability cluster | Verdict | Evidence summary |
|---|---|---|
| C4.1 → C1 inventory | **GREEN** (with coverage map caveats) | PPAC API GA-rich; ARM GA; Entra Graph v1.0 GA; Package API conditional (licence + preview inconsistency [PoC]); gaps: AI Builder, classic bots, third-party sync |
| C1.3 reconciliation | **AMBER — redesigned** | ADR-012 alias-graph; deterministic subset documented; PoCs 1–3 gate the build |
| C1.4 ownership | **GREEN** | PPAC `ownerId`/`createdBy` GA; Entra owners+sponsors GA v1.0; package owner absent (write preview) |
| C4.2 → C3.7 usage | **AMBER** | User-level GA (24–72 h, pseudonymized default); **agent-level: no API** — Purview audit counts as candidate workaround [PoC 5]; prompt counts: Viva manual export only |
| C4.3 → C3.1/C3.2 cost | **AMBER/RED for Copilot Credits** | Credits per-user/per-agent: **no API** [C]. Azure cost: API GA. Licence cost: API GA. Mitigation options — Question 2 |
| C3.3/C3.4 utilisation & zombies | **GREEN-AMBER** | Assigned-vs-active feasible (GA APIs) *if concealment off*; dormancy from last-activity + audit counts + Entra sign-ins — multiple corroborating signals |
| C4.4/C8.4 privacy levels | **GREEN, with a design finding** | Pseudonymization-by-default actually *implements* L1 natively; **L2+ requires tenant-wide de-concealment (audited)** — jurisdiction-scoped levels within one tenant need our own re-masking layer, since Microsoft's toggle is all-or-nothing [C] |
| C8.1–C8.3, C9.1 | **GREEN** | Standard Entra/Graph patterns, GA |

**Overall: V1 is technically feasible.** Two capabilities need scope honesty at launch: C3.2 (Copilot Credits attribution — pending API or manual feed) and agent-level usage depth in C3.7 (workaround via audit records, pending PoC).

## 7. Consolidated PoC backlog (questions only — no code in planning, PD-001)

**Gate-1 (before any build commitment) — correlation:**
1. For one Copilot Studio agent published to M365: verify `copilotPackage.appId` ↔ PPAC `entraAppId`/`entraAgentId` join fidelity end-to-end.
2. Does Dataverse `botcomponent` store the generated M365 manifest ID (closing the bot-GUID↔manifestId hole)?
3. Package API in practice: does it work app-only with `CopilotPackages.Read.All` + Agent 365 licence outside preview programs; do registry-synced third-party agents appear; v1.0/beta field parity.

**Gate-2 — economics:**
4. Azure Cost Management API depth for Copilot PAYG (`m365copilotchat` meters): daily granularity? any agent dimension?
5. Purview Management Activity API end-to-end for `CopilotInteraction`/`AIAppInteraction`: per-agent interaction counts derivable? ingestion latency? PAYG metering cost at 6,000-employee scale?
6. Credits report CSV export fidelity (user/agent-pair level) as an interim manual feed.

**Gate-3 — operational:**
7. Effective throttling on PPAC resourcequery and `/copilot/admin/catalog/packages` at inventory-scan volumes.
8. Concealment toggle end-to-end: de-hash latency, audit trail, join fidelity to `licenseDetails`.
9. Viva export schema stability + Power BI connector as a scheduled prompt-metrics feed.
10. AOAI per-deployment cost reconstruction: meter names vs deployment disambiguation; tag strategy.
11. Foundry agents data-plane list API: rate limits; reverse mapping Graph `agentIdentity` → Foundry agent ID.
12. AI Builder tenant-wide enumeration options (Dataverse per-environment sweep?).

## 8. Roadmap-watch register (absence of API today ≠ forever; recheck quarterly, R-01)

Agent-level usage API (roadmap ID 497999); Copilot Credits consumption API; Agent 365 replacement for deprecated `agentRegistry` APIs; registry-sync GA + API; unified DSPM programmatic surface; hosted-agent `agentDeployment` ARM GA.

---

## Stage-end review

### Summary
Every V1 feed is validated with cited evidence. The ledger is feasible via an entity-resolution redesign of C1.3 (proposed ADR-012); the guaranteed no-premium-licence baseline (PPAC + ARM + Entra + licence + Azure cost APIs) is itself sellable; the two material launch gaps are Copilot Credits attribution (no API) and agent-level usage (no API, audit-record workaround plausible). No V1 capability is infeasible; two are scope-degraded pending PoCs or Microsoft roadmap.

### Assumptions
- Microsoft will ship the announced Agent 365 registry API replacement (not relied upon — alias graph works without it).
- Purview audit-based per-agent counts will survive the classic→unified DSPM transition (watch item).
- PoC access to a representative tenant (with Agent 365 licence) will be available before build.

### Confirmed facts
See matrices §2–§3 (30+ cited findings). Most consequential: PPAC Inventory API GA with `entraAgentId` fields; Entra Agent ID GA on Graph v1.0 with owners/sponsors; **no API for Copilot Credits consumption**; **no API for agent-level usage**; prompt counts explicitly unsupported in Graph; `sourceAgentId` join deprecated; Package API licence-gated on Agent 365; Purview AI audit records API-retrievable; per-deployment AOAI token metrics GA.

### Unknowns
All §7 PoC items; Package API preview/GA contradiction; Purview unified-DSPM feature drift; effective rate limits on the two newest APIs.

### Risks
- **R-12 → AMBER (managed)** via ADR-012; residual: heuristic-match quality at real-tenant scale [PoC 1–2].
- **R-16 (new):** Copilot Credits API absence persists → V1 cost attribution depends on manual/CSV feeds for the credits component; wedge messaging must not overpromise (interacts with R-07 honesty).
- **R-17 (new):** API churn on newest surfaces (one deprecation already observed mid-2026); mitigation: alias-graph independence + quarterly re-validation + provider-contract isolation (C4.5).
- R-05/OI-004 now factual: sales motion must qualify customer licensing early.

### Alternative approaches considered
- **Wait for Microsoft's registry API replacement** before designing C1.3 — rejected: timing unknown, alias graph is strictly more robust and absorbs the replacement when it ships.
- **Screen-scraping/UI automation for Credits data** — rejected: unsupported, fragile, likely ToS-adverse; CSV import (human-operated) is the acceptable interim.
- **Building on beta APIs** (`aiInteractionHistory`, risk APIs) — rejected: explicitly non-production; also content-collection conflicts with ADR-003.

### Questions for Arun
1. **Approve ADR-012** (alias-graph correlation with visible confidence tiers)?
2. **Copilot Credits gap — choose the V1 posture:** (a) ship with licence+Azure cost only and label credits "manual feed (self-reported)" via CSV import; (b) hold the cost-attribution claim until the API ships; (c) both markets differ — internal Quadient uses CSV, commercial launch waits. My recommendation: (a) — the honest-data labelling absorbs it, and the CSV pipeline reuses the C4.5 provider contract.
3. **Concealment interaction:** L2+ telemetry requires tenant-wide de-concealment plus our own jurisdiction-scoped re-masking (Microsoft's toggle is all-or-nothing). Accept this as a C4.4 design requirement (it strengthens our privacy differentiator but adds build weight)?
4. **PoC gate:** do you want the Gate-1 PoCs (correlation) executed/commissioned before Stage 4 (personas/UX), or run Stage 4 in parallel and gate only the data-model stage on PoC results?

### Recommendations
1. Adopt ADR-012; make correlation-confidence a visible product feature, not internal plumbing.
2. Take posture (a) on credits: manual-feed provider with Self-reported labelling; revisit quarterly against the roadmap-watch register.
3. Add the quarterly **Microsoft re-validation ritual** to the operating model — this stage's findings have a shelf life.
4. Lead sales qualification with the licence-dependency map (§5): it converts R-05 from risk into a consultative selling tool.
5. Proceed to Stage 4 (personas/UX) in parallel with Gate-1 PoCs; hard-gate Stage 8 (data model) on PoC 1–3 results.
