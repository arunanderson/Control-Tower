# Challenge 01 — Red-Team of the Product Vision

| | |
|---|---|
| **Version** | 1.0 |
| **Date** | 2026-07-15 |
| **Status** | **Resolved 2026-07-15** — ADR-006/007/008 adopted in amended form (see decision-log): ADR-007 as adopted defers rather than permanently excludes custom collectors (pluggable providers); ADR-008 as adopted defines commercial positioning, leaving analyst-category strategy open (OI-008). Stage 1 revised to v1.1 and frozen. |
| **Related** | [stage-01-product-vision.md](stage-01-product-vision.md), [decision-log.md](decision-log.md), [research-annex-2026-07.md](research-annex-2026-07.md) |

Mandate: challenge the vision, not validate it. Based on the July 2026 landscape (Agent 365 GA, Entra Agent ID GA, CCS, Purview DSPM for AI, Foundry Control Plane, ServiceNow AI Control Tower, Zenity/SentinelOne, Credo/Holistic, Larridin).

---

## 1. If we started this company today, what product would I build?

Not the product in Stage 1. Stage 1 describes a broad governance platform with value intelligence as one of four pillars. Starting cold today, I would invert that.

I would build **the enterprise's independent AI ledger**: the system that knows every AI asset, every euro of AI spend, and every claimed outcome — across all vendors — and can defend those numbers to a CFO and an auditor. The wedge product is brutally specific: **Copilot and agent portfolio economics**. "You pay for 3,000+ Copilot licences and run N hundred agents. Here is who uses what, what each costs, which are zombies, what to cut, and what the survivors return." That lands with money, creates its own mandate, and requires the registry as a by-product — you cannot allocate cost to assets you haven't inventoried, and you cannot trust value claims about assets nobody owns.

The reasoning: governance-first products need a mandate that mostly doesn't exist yet and produce value the buyer can't feel monthly. Cost-and-value-first products create their own justification, then *earn* the governance role. The registry is still the foundation — but as plumbing, not as the pitch.

One structural argument makes this durable: **Microsoft will never ship a feature that recommends cancelling Copilot licences.** ServiceNow will never audit ServiceNow AI consumption against value. An independent measurement layer has a conflict-of-interest moat that no platform vendor can cross. That is the company I would start.

## 2. Capabilities we should never build (platforms will own them)

| Capability (in current vision) | Who owns it | Verdict |
|---|---|---|
| Agent inventory/discovery inside the Microsoft estate | Agent 365 registry, Entra Agent ID, PPAC Inventory — all GA [Confirmed] | Never build. Federate. |
| Agent blocking, quarantine, conditional access | CCS, Entra (Conditional Access for agents GA) | Never build (already ADR-002 — correct). |
| Shadow-AI detection via endpoint/browser collection | Purview browser extension + Endpoint DLP [Confirmed]; Nudge, Zenity | **Never build our own collectors.** This contradicts part of the original brief (browser extension, desktop app, endpoint service) — see §6. |
| Runtime observability, tracing, agent telemetry | Foundry Control Plane (GA Mar 2026), ServiceNow/Traceloop | Never build. Consume. |
| Prompt/response content inspection, DLP | Purview | Never build (already excluded). |
| Agent identity lifecycle | Entra Agent ID | Never build. |
| MCP gateway / transaction governance | Microsoft, ServiceNow AI Gateway | Never build the gateway; registering MCP servers as assets is fine. |
| Generic workflow engine | ServiceNow, Power Automate | Never build; embed a workflow *model*, not a workflow *platform*. |
| Compliance policy packs / framework libraries as core IP | Credo AI, Holistic AI | Don't lead with it; partner or add later as content. |
| Data lineage construction | Purview, Fabric | Never build lineage crawling. Consume lineage; add only the AI-asset-to-business-capability linkage they lack. |

## 3. Durable advantages (unlikely to ever be Microsoft features)

1. **Independence itself.** Cross-vendor spend/value arbitration is structurally impossible for Microsoft, ServiceNow, or any vendor selling the AI being measured. This is the only moat in the list that *cannot* be closed by a platform roadmap.
2. **Cross-vendor cost allocation and chargeback** — blending M365 licences, Copilot Credits, Azure consumption, OpenAI/Anthropic/Google contracts, and embedded-AI SaaS uplifts into one BU-attributable model. Each vendor shows its own meter; nobody blends them.
3. **Value measurement methodology with confidence labelling** (Measured/Estimated/Self-reported/Inferred/Unknown) — methodology and trust, not telemetry. Microsoft's incentive is to inflate Copilot ROI, not to grade it honestly.
4. **Governance orchestration over *organisational* process** — your intake, approval, risk, attestation, and audit-evidence flows spanning committees and jurisdictions. Vendors ship controls; nobody ships your operating model.
5. **Portfolio management against business capabilities and strategy** — "which business capabilities are over/under-invested in AI" is an enterprise-architecture judgment, not platform telemetry.
6. **Cross-customer benchmarking** once multi-tenant (PL-003) — Microsoft's per-tenant data boundary makes this hard for them to offer credibly.
7. **Licence-tier independence** — value for customers who won't pay E7/Agent 365 uplift.

Note what is *not* on this list: cross-vendor *registry sync* — Agent 365 already syncs with Bedrock and Google Cloud [Confirmed]. Stage 1 §11.2 overweights this. Aggregation is table stakes; *judgment over the aggregate* is the moat.

## 4. The product in one sentence

**The independent ledger of enterprise AI: every asset, every euro, every outcome — governed, attributed, and provable to the board, across every vendor.**

## 5. The product in three core capabilities

1. **Federated AI asset ledger** — the authoritative record (ownership, purpose, lifecycle, risk) built by consuming native registries, never by rebuilding them.
2. **Governance orchestration** — intake→approval→risk→lifecycle→attestation workflows producing audit-grade evidence, invoking native controls for enforcement.
3. **Cost & value intelligence** — cross-vendor cost allocation, portfolio economics, ROI with confidence labelling, executive decision support.

Everything else in the vision is a *view* of these three (dashboards, persona experiences), a *feed* into them (integrations, discovery), or creep (§6).

## 6. Feature creep in the current vision

- **Self-built activity collection** (browser extension, desktop app, endpoint service from the original brief). The heaviest creep. It duplicates Purview/Defender/Nudge territory, drags the product into endpoint software distribution, works-council exposure (R-08), and security-vendor competition — to collect signals Purview already collects. **Proposal: ADR-003's privacy levels stay (they govern what we *store and show*), but collection is via native/partner signals only. We are a consumer of telemetry, never a collector agent.** This changes Stage 5's question from "how do we collect?" to "what can we responsibly consume?"
- **Business process mapping / workflow intelligence** — process mining (Celonis, ServiceNow). Creep; cut or reduce to "an asset links to a named business process."
- **Data lineage** as a platform capability — consume Purview/Fabric lineage; build only AI-asset→knowledge-source→business-capability linkage.
- **Prompt libraries and knowledge-source management** — builder-adjacent tooling (Copilot Studio's lane). Register them as assets; don't manage their content.
- **Managing APIs, connectors, Dataverse and Fabric assets as first-class citizens** — CMDB ambition. Scope to AI assets; other artefacts appear only as *dependencies of* AI assets.
- **AI Fluency** — already parked (PL-001). Correct.
- **Twelve-plus persona experiences** — v1 needs three (CoE/governance operator, executive, agent owner). The rest are report recipients until proven otherwise.
- The 30+ capability-area list in the project brief is six products. The three-capability core (§5) is one product.

## 7. Gartner category in three years

Most probable: **AI Governance Platforms** (Gartner already runs this Market Guide; $492M 2026 → >$1B 2030 [Confirmed]), likely absorbed by then into a broader "AI governance and management" category within AI TRiSM's governance layer. If the value pillar leads, analysts may instead file it near "FinOps for AI" — and *category straddle is a commercial risk*: procurement can't shortlist what it can't classify. The positioning answer: claim **AI governance platform** as the category, and use value intelligence as the differentiator *within* it, not as a second category. Three years out, the winning slot is "the governance platform that proves value," not "the FinOps tool that does governance."

## 8. What prevents shelfware?

Governance tools become shelfware when they demand manual feeding and give operators nothing daily. Five design commitments, in priority order:

1. **Self-populating or dead.** The ledger fills from native APIs (Agent 365, Entra, PPAC, Graph, billing). If inventory requires data entry, the product has already failed.
2. **Be the gate, not the survey.** Registration must be *in the path* to production (agent publishing, licence assignment, budget release), enforced through native controls — not a voluntary census. This requires the AI CoE's mandate to exist organisationally; the platform cannot conjure it (Stage 1 assumption, now elevated to adoption risk #1).
3. **Answer a recurring executive question.** The quarterly "AI spend and value by BU" report the CFO already asks for must be one click. A tool that feeds an existing executive ritual gets renewed.
4. **Give makers something back.** Faster approval than the spreadsheet, visibility, legitimacy, budget. Governance that only takes gets bypassed.
5. **Show what it cannot see.** The discovery-coverage map (Stage 1 §7.2) sustains trust; silent blind spots kill governance platforms the first time an unregistered agent causes an incident.

## 9. If Microsoft announced ten major governance features tomorrow, what survives?

Assume the worst case: deeper inventory, richer analytics, portfolio views, even ROI dashboards. What survives is exactly §3: independence (Microsoft grading Microsoft is the fox auditing the henhouse), cross-vendor blending beyond registry sync, the organisational process layer, honest-methodology value measurement, cross-customer benchmarking, and value below the E7 licence wall. The one-line test that survives any Microsoft announcement: **"Would you accept the vendor's own dashboard as evidence for cutting that vendor's licences?"** No CFO says yes. That answer is the company.

Corollary: any capability whose survival depends on Microsoft *not* shipping something is already dead — §2 is the list of those, and none of them should be built.

## 10. Recommendation

**Refocus and significantly simplify. Do not continue unchanged; do not fundamentally reposition.**

- **Keep** (the vision got these right): system-of-record ambition, complement-don't-compete (ADR-004), observe+orchestrate (ADR-002), privacy-by-design levels (ADR-003, narrowed per §6), multi-tenant SaaS (ADR-001), honest-data labelling, Quadient-as-reference-not-design.
- **Refocus:** elevate cost & value intelligence from "one of four pillars" to co-lead with the ledger. Lead the narrative with independence and money; let governance orchestration be what the money buys. Revise Stage 1 Recommendation 1 accordingly: land with **ledger + portfolio economics**, earn the mandate, then operationalise governance workflows — rather than governance-first with value as fast-follow. (Internal Quadient sequencing may differ from the commercial wedge — the CoE mandate exists internally; flag this two-masters divergence explicitly in Stage 12.)
- **Simplify:** adopt the three-capability core (§5) as the product definition. Cut from the active scope: self-built collectors, process mining, lineage construction, prompt/knowledge content management, non-AI asset management, 12-persona v1. Move all to the parking lot with revisit criteria.
- **Reposition only the telemetry stance:** from "collector with privacy controls" to "consumer of native signals with privacy controls." This is the single largest change to the original brief.

### Proposed decisions for Arun (become ADRs if accepted)

1. **ADR-006 (proposed):** Product core = three capabilities (federated ledger, governance orchestration, cost & value intelligence); everything else is view, feed, or parked.
2. **ADR-007 (proposed):** The platform never builds its own collection agents (browser/desktop/endpoint); AI Activity Intelligence consumes native and partner signals only. Amends the original project brief's Activity Intelligence scope.
3. **ADR-008 (proposed):** Category claim = AI Governance Platform; value intelligence is the differentiator within the category, not a second category.
4. **Stage 1 v1.1** to be issued reflecting whichever of the above are accepted (changes: §2 positioning weight, §7 sequencing, §11 differentiator ranking — cross-vendor registry demoted, independence elevated).

### Questions for Arun

1. Accept, reject, or amend ADR-006/007/008?
2. The wedge question: for the *commercial* product, do you accept "portfolio economics first, governance earned second"? (Internal Quadient rollout can still sequence governance-first given the CoE mandate.)
3. Does dropping self-built collectors conflict with any non-negotiable expectation you hold for Activity Intelligence (e.g., visibility into personal-account ChatGPT use beyond what Purview/partners expose)? If that visibility is non-negotiable, we must consciously re-accept endpoint-software costs and works-council risk — it should be a deliberate exception, not a default.
