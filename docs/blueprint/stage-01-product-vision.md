# Stage 1 — Product Vision & Strategic Positioning

| | |
|---|---|
| **Version** | 1.1 |
| **Date** | 2026-07-15 |
| **Status** | **FROZEN** — approved by Arun 2026-07-15 after red-team revision. Changes require a new version and an entry in the decision log. |
| **Related** | [decision-log.md](decision-log.md) (PD-001..002, ADR-001..008), [challenge-01-vision-red-team.md](challenge-01-vision-red-team.md), [open-issues-parking-lot.md](open-issues-parking-lot.md), [requirements-traceability-matrix.md](requirements-traceability-matrix.md) (BR-01..14) |

**Revision history**

| Version | Date | Change |
|---|---|---|
| 1.0 | 2026-07-15 | Initial draft from Arun's Stage 1 inputs + market research (July 2026) |
| 1.1 | 2026-07-15 | Red-team decisions applied (ADR-006/007/008): refocus to independent AI portfolio intelligence; three-capability core; pluggable telemetry, native-first V1, no custom collectors; commercial positioning led by portfolio/cost/value/executive intelligence; differentiators re-ranked (independence first, cross-vendor registry demoted); scope cuts moved to parking lot (PL-006..010). **Frozen.** |
| 1.2 | 2026-07-15 | PD-006 revision (Revision Package v1.0, Independent Review 01 finding): §7 success criteria restated against ADR-011 phasing; measurement clock defined. No other changes. |

Claim labelling: **[Confirmed]** verified against current sources; **[Likely]** multiple secondary sources; **[Assumption]** working assumption; **[Unknown]** requires validation.

---

## 1. Product vision

**Vision statement.** Every enterprise running AI at scale needs one place where AI is *known, governed, and proven*. The Enterprise AI Control Tower (working name — see OI-001) is the **independent ledger of enterprise AI**: every asset, every euro, every outcome — governed, attributed, and provable to the board, across every vendor. It is the intelligence platform for the AI portfolio (cost, value, executive decision support), the system of record for AI assets, and the operational backbone for AI governance (ADR-006, ADR-008).

**Three-year ambition.** The platform becomes for enterprise AI what the CMDB became for IT service management and what the ERP became for finance: the layer without which the domain cannot be managed. It manages the full portfolio — agents, Copilot deployments, flows, models, MCP servers, connectors, prompts, knowledge sources, and external AI services — across Microsoft first, then other vendors, for any multinational organisation.

**What it is not.** It does not build, run, or secure AI workloads. Execution, enforcement, and data protection remain in the native platforms (ADR-005). It is the layer *above* them.

## 2. Strategic positioning

One sentence: **the independent AI portfolio-intelligence and governance layer above the control planes AI vendors ship — measuring what no vendor can credibly measure about itself.**

Commercial positioning (ADR-008): the product leads with **AI portfolio intelligence, cost intelligence, business value, and executive decision support**. Governance is a core capability — the operational backbone the intelligence stands on — but not the primary commercial message. The structural moat is independence: Microsoft will never ship a feature recommending Copilot licence cuts, and no CFO accepts a vendor's own dashboard as evidence for reducing that vendor's spend.

The market context that forces this positioning [Confirmed, July 2026 research]:

- Microsoft has largely closed the raw *inventory-and-control* gap in its own stack: Agent 365 (GA May 2026) provides a unified agent registry, RBAC, activity monitoring, and cross-cloud registry sync (AWS Bedrock, Google Cloud); Entra Agent ID (GA) provides agent identity, ownership, and lifecycle to prevent orphaned agents; Copilot Control System provides tenant-wide agent approval/blocking; the M365 admin center provides per-agent/per-user Copilot Credits consumption reporting; Power Platform admin center Inventory replaced the (end-of-life) CoE Starter Kit.
- Therefore a product whose core value proposition is "we inventory your Microsoft agents" is already obsolete. The durable position is the layer Microsoft is structurally unlikely to build: **cross-vendor portfolio management, business-capability and value mapping, governance orchestration across organisational (not technical) processes, cost allocation and ROI intelligence, and executive decision support** — consuming Microsoft's registries and telemetry as feeds rather than replicating them (ADR-004).

Positioning against the three adjacent categories (full analysis §10): we are not an AI *security* product (Zenity, SentinelOne/Prompt), not a *compliance documentation* product (Credo AI, Holistic AI), and not a *platform-captive* control tower (ServiceNow, Microsoft). We are the independent management layer that connects governance to business value.

## 3. Product boundaries

**Product core (ADR-006) — three capabilities; every feature must be a view of, feed into, or component of these:**

1. **Federated AI asset ledger** — authoritative record (ownership, purpose, lifecycle, risk) built by consuming native registries, never rebuilding them; includes discovery orchestration and an explicit discovery-coverage map.
2. **Governance orchestration** — registration, approval, risk profiling, lifecycle gates, attestations; audit-grade evidence; invokes native controls for enforcement.
3. **Cost & value intelligence** — cross-vendor cost allocation, portfolio economics, ROI with confidence labelling, executive and operational reporting.

Supporting layers: AI activity and usage intelligence consumed through **pluggable telemetry providers** — native Microsoft and supported third-party sources in V1, no custom collectors in V1 (ADR-007) — within configured privacy levels (ADR-003); integration and API layer.

**Out of scope (the platform is not):** an LLM or model provider; an agent builder or workflow designer; a SIEM; a data governance platform; an endpoint security product. It will not replace Copilot Studio, Power Automate, Purview, Entra, Defender, Microsoft Foundry, Power BI, Azure Monitor, or Fabric (ADR-005). In V1 it ships no telemetry collectors of its own (browser extension, desktop agent, endpoint service); collection is consumed via the pluggable provider architecture, and custom collectors remain a deliberate future option, not a default (ADR-007).

**Consume, orchestrate, or build** — the boundary test applied to every future capability:

| If Microsoft (or another vendor) provides… | We… | Example |
|---|---|---|
| Data (inventories, telemetry, cost) | **Consume** via API | Agent 365 registry, Graph Copilot usage API [Confirmed GA], PPAC Inventory API |
| Controls (block, disable, restrict) | **Orchestrate** — trigger native controls from our workflows | CCS agent blocking, Entra Conditional Access for agents [Confirmed] |
| Nothing (gap) | **Build** | Cross-vendor portfolio model, value measurement, governance workflows spanning org processes |

## 4. Problems we are solving

1. **No single source of truth.** AI assets are scattered across at least six Microsoft admin surfaces plus non-Microsoft tools; nobody can answer "what AI do we run, who owns it, and why does it exist?" from one place.
2. **Governance is manual.** Policies exist; operational governance (registration, approval, risk assessment, lifecycle) runs on spreadsheets and goodwill.
3. **Unknown cost.** AI spend (licences, consumption billing, Azure workloads, third-party subscriptions) is not attributable to business units or outcomes.
4. **Unknown value.** 3,000+ Copilot licences with no defensible answer to "what are we getting for this?" Executives demand transparency.
5. **Unowned and orphaned assets.** Citizen-built agents outlive their creators' roles; no lifecycle management. (Native mitigations now exist per asset type — Entra Agent ID ownership [Confirmed] — but nothing spans the portfolio.)
6. **Shadow AI blind spot.** Employees use external AI tools with no enterprise visibility; existing controls (Purview DSPM for AI third-party detection [Confirmed]) are security-oriented, not governance/value-oriented.
7. **Fragmented executive view.** CIO, CISO, CDO, CRO, CFO, and Audit each need different, currently unavailable, views of the same AI estate.
8. **No cross-vendor picture.** Microsoft's native governance stops at (or near) Microsoft's edge; enterprises will run multi-vendor AI.

## 5. Problems we are intentionally not solving

- Runtime security of AI (prompt injection, jailbreak defence, agent runtime inspection) — security vendors' lane; we consume their findings where useful.
- Data loss prevention and information protection — Purview's lane.
- Model quality: evaluation, red-teaming, bias auditing, drift monitoring — Foundry/watsonx/Credo lane; we register that these activities happened and their outcomes, not perform them.
- Building or hosting agents, models, or workflows.
- Employee performance management. Telemetry exists to govern AI and prove value, never to rate individuals. This is a product-philosophy commitment (§9), not just a legal posture.
- General SaaS management or non-AI non-human identity governance (see PL-005).
- Business process mining / workflow discovery — process-intelligence vendors' lane; an AI asset links to a named business process, nothing more (PL-006).
- Constructing data lineage — Purview/Fabric's lane; we consume lineage and add only the AI-asset→knowledge-source→business-capability linkage (PL-007).
- Managing prompt or knowledge-source *content* — builder tooling's lane; registered as assets only (PL-008).
- Managing APIs, connectors, Dataverse/Fabric artefacts as first-class citizens — they appear only as *dependencies of* AI assets.

## 6. Target users

**Primary personas (v1 is built for them):** Enterprise AI Centre of Excellence (platform owner); AI Governance Team; Platform Administrators.

**Secondary personas (served through purpose-built experiences, progressively):** CIO (initial executive sponsor), CISO, Business Unit Leaders, Finance, Risk, Privacy, Security, Internal Audit, Agent Owners, Citizen Developers, Professional Developers. Long-term executive stakeholders additionally include the Chief Data Officer, Chief Digital Officer, and CFO.

**Design implication.** One platform, multiple experiences: an auditor gets evidence trails; a CFO gets cost/value; an agent owner gets their assets' status and obligations; the CoE gets the whole control tower. Persona-experience architecture is Stage 3. Ownership model: AI CoE owns the platform with delegated responsibilities across Security, Data Governance, Risk, Privacy, Finance, and Business Units — the platform must support *delegated administration* as a first-class concept.

## 7. Success criteria (12 months) — restated in v1.2

**Measurement clock (defined per Revision Package v1.0):** the 12-month period begins at **V1 production go-live of the internal deployment**, not at project kickoff or blueprint approval. Criteria are phased consistently with ADR-011: items 1–5 and 8 are V1 obligations measured across the period; items 6–7 depend on V1.5 (governance engine and full value methodology), which the covenant (OI-013) requires within two quarters of go-live — making them month-12 obligations *provided the covenant holds*. A covenant breach (R-14) makes items 6–7 formally unachievable and must be escalated as such, not absorbed silently.

Arun's criteria, with two refinements (flagged, not silently changed):

1. **Registration coverage:** 100% of *known* production AI assets registered with owner, business purpose, lifecycle status, and assigned risk profile. *(Refinement of "100% enterprise inventory" — completeness against unknown assets is unfalsifiable; see OI-005.)*
2. **Discovery coverage:** automated discovery connected to every technically available inventory surface (Agent 365, Entra Agent ID, PPAC Inventory, Purview, Foundry, plus validated others), with an explicit, reported *discovery coverage map* showing what the platform can and cannot see.
3. Every production AI asset has an owner, business purpose, lifecycle status, and risk profile.
4. Executive dashboards live for adoption, cost, governance posture, and business value.
5. AI spend attributable by business unit.
6. AI business value measured via agreed methodologies, with every figure labelled Measured / Estimated / Self-reported / Inferred / Unknown. *(Refinement: credibility of the ROI number is itself a success criterion — an unlabelled ROI dashboard would damage trust.)*
7. Governance processes operational in-platform; zero spreadsheet-based governance for AI.
8. The platform is the authoritative source of truth for enterprise AI governance, evidenced by: executive reporting sourced solely from it, and audit/risk functions accepting its records as evidence.

## 8. Product principles

1. **Complement, never compete with Microsoft.** Consume native capability; add value only in gaps (ADR-004).
2. **Consume > orchestrate > build**, in that order, for every capability (§3 boundary test).
3. **Three-capability core.** Federated ledger, governance orchestration, cost & value intelligence; every proposed feature is a view of, feed into, or component of these three — or it is parked (ADR-006).
4. **Observe and orchestrate; delegate enforcement** to native controls (ADR-002).
5. **Privacy by design, configurable by jurisdiction.** Four telemetry levels; prompt/response content never collected by default; every telemetry capability independently configurable, audited, transparent (ADR-003).
6. **Pluggable telemetry providers.** V1 consumes native Microsoft and supported third-party telemetry; new providers — including custom collectors, if ever justified — plug in without architectural redesign (ADR-007).
7. **Honest data.** Every metric labelled Measured / Estimated / Self-reported / Inferred / Unknown. Never overstate confidence.
8. **Multi-tenant SaaS from day one**; modular, configurable governance; no fundamental redesign for commercialisation (ADR-001).
9. **Not hardcoded to any customer.** Quadient is the reference implementation, not the design.
10. **Persona-appropriate experiences** over one-size-fits-all admin UI; V1 builds three (governance operator, executive, agent owner), the rest are report recipients until proven otherwise.
11. **Every asset has an owner.** Ownerless AI is a governance failure the platform must surface, not tolerate.
12. **Evidence-grade audit.** Governance actions are recorded such that Internal Audit can rely on them.
13. **Phased delivery, modular architecture, enterprise readiness** (scalability, extensibility) as standing constraints.

## 9. Product philosophy

Governance that slows builders down gets bypassed; governance that is invisible gets ignored by regulators and auditors. The philosophy is **governance as an enabler**: registration, approval, and lifecycle management should be faster than the spreadsheet they replace, and should give makers something in return (visibility, legitimacy, support, budget). The platform treats trust as its core asset in both directions — executives must trust its numbers (hence honest-data labelling), and employees must trust its telemetry (hence privacy by design and transparency about what is collected). And it is built on humility about its own visibility: it always shows what it *cannot* see, because a control tower that overstates its coverage is more dangerous than no control tower at all.

## 10. Competitive positioning

Market category [Confirmed]: Gartner tracks "AI Governance Platforms" (spend forecast $492M in 2026, >$1B by 2030) and AI TRiSM. The space is crowded and consolidating (Prompt Security→SentinelOne; Traceloop→ServiceNow).

| Cluster | Players | Their position | Our differentiation |
|---|---|---|---|
| Platform-captive control planes | **Microsoft** (Agent 365, Entra Agent ID, CCS, Purview, Foundry Control Plane) [Confirmed GA] | Deep, native, single-vendor; licensing-gated (E7 / ~$15 user/mo for Agent 365) | We are the cross-vendor layer above; we consume their registries; we serve orgs and assets outside their licence walls |
| Enterprise-platform control tower | **ServiceNow AI Control Tower** [Confirmed — expanded May 2026, much of it GA Aug 2026] | CMDB-anchored discover/observe/govern/secure/measure; strongest where ServiceNow already runs the enterprise | Independence from the ServiceNow platform and pricing model; Microsoft-ecosystem depth; governance-orchestration designed for the M365/Power Platform citizen-developer reality |
| Value/adoption intelligence | **Larridin** [Confirmed active; ~$17M seed Likely] | AI adoption, fluency, impact, token spend; Microsoft-specific depth Unverified | We pair value intelligence with an authoritative registry and operational governance — value claims traceable to governed assets |
| RAI compliance/documentation | **Credo AI** (Microsoft-aligned), **Holistic AI**, **IBM watsonx.governance** [Likely] | Policy packs, audits, EU AI Act documentation | We are operational, not documentary; we can feed them evidence, or embed compliance mapping later |
| Agent/AI security | **Zenity**, **SentinelOne/Prompt**, **Nudge**, **Knostic** [Confirmed/Likely] | Runtime security, shadow-AI detection from a security lens | Explicit non-goal (§5); integration candidates, not competitors |

**White space we occupy:** the intersection of (a) authoritative cross-vendor AI portfolio/registry, (b) governance *orchestration* across organisational processes, (c) cost allocation + ROI intelligence with honest confidence labelling, (d) executive decision support across CIO/CISO/CDO/CRO/CFO/Audit — delivered Microsoft-first but vendor-neutral. No researched player holds all four; Microsoft and ServiceNow each hold parts but are structurally platform-captive.

**Category strategy note (OI-008):** commercial positioning leads with portfolio/cost/value intelligence (ADR-008), while the recognised analyst category remains Gartner's "AI Governance Platforms." This straddle carries procurement-classification risk and needs an explicit analyst strategy before commercialisation.

**Regulatory tailwind, recalibrated [Confirmed]:** EU AI Act GPAI obligations and Art. 50 transparency are live (Aug 2025 / Aug 2026), but the Digital Omnibus delayed high-risk obligations to Dec 2027 (Annex III) and Aug 2028 (Annex I). Compliance urgency is real but not a 2026 cliff — the near-term buying driver is *operational control and value transparency*, with compliance readiness as the 2027 wave.

## 11. Key differentiators

Re-ranked in v1.1 (ADR-006/008): independence leads; cross-vendor registry sync is demoted to table stakes — Agent 365 already syncs with AWS Bedrock and Google Cloud [Confirmed].

1. **Independence.** Cross-vendor spend/value arbitration is structurally impossible for any vendor selling the AI being measured. The only moat no platform roadmap can close.
2. **Business value and ROI intelligence** with explicit confidence labelling — the honest-data stance is itself a differentiator in a market of inflated ROI claims.
3. **Cross-vendor cost allocation and portfolio economics** — blending licences, consumption billing, cloud spend, and third-party AI contracts, attributable by business unit; licence rationalisation no vendor will ever recommend.
4. Enterprise AI **portfolio management** — assets mapped to business capabilities, processes, and strategy, not just listed.
5. **Executive decision support** — persona-specific views for seven-plus stakeholder functions.
6. **Governance orchestration** — approval, risk, lifecycle, attestation workflows that operate across the org and *invoke* native controls; the organisational process layer vendors don't ship.
7. **AI lifecycle management and operating model.**
8. **Configurable, jurisdiction-aware privacy architecture** (ADR-003) — a genuine differentiator for multinationals versus US-centric telemetry products.
9. **Vendor neutrality with Microsoft-first depth** — deep enough to matter in a Microsoft estate, independent enough to outlive any one vendor's roadmap.

## 12. Risks with the current vision

| ID | Risk | Severity | Notes / mitigation direction |
|---|---|---|---|
| R-01 | **Microsoft absorption.** Agent 365 shipped faster than most expected; Microsoft may extend into portfolio/value territory. | High | Anchor durable value in cross-vendor, org-process, and value layers; re-run Microsoft-gap analysis every stage (ADR-004) |
| R-02 | **Name collision.** "AI Control Tower" is ServiceNow's flagship branding. | Medium | Rename before commercialisation (OI-001, DD-001) |
| R-03 | **Crowded, consolidating market.** Well-funded players on every side; category may be absorbed into security/ITSM suites. | High | Speed to internal value at Quadient; differentiate on the four-way white space (§10) |
| R-04 | **SaaS vs Microsoft-first tension.** Multi-tenant SaaS reading customer tenants needs multi-tenant Entra consent, least-privilege Graph scopes, and answers on data residency. | High | OI-002; Stage 9 must design consent + residency architecture explicitly |
| R-05 | **Licence dependency.** Best native feeds (Agent 365, Copilot analytics APIs) sit behind customer licensing tiers; platform value varies by customer's Microsoft spend. | Medium | OI-004; define degradation tiers; value must not collapse without E7 |
| R-06 | **API coverage risk.** The vision assumes Microsoft surfaces expose enough via API. Partially confirmed (Graph Copilot usage API GA, PPAC Inventory API, Agent 365 registry) but per-capability validation is outstanding. | High | Stage 4 dedicated to API validation; never assume (project rule) |
| R-07 | **ROI credibility.** Business value measurement is methodologically hard; a dashboard of soft numbers destroys executive trust. | High | Honest-data labelling (§8.5); Stage 10 methodology with Finance as co-owner |
| R-08 | **Privacy/works-council backlash.** Individual telemetry (L2+) in EU without consultation could halt the programme reputationally and legally. | High → Medium | Reduced by ADR-007 (native signals only in V1, no own collectors); OI-003 consultation still required for L2+ display of native data; jurisdiction-scoped levels; transparency by design |
| R-09 | **Scope breadth.** The capability list spans six products' worth of ambition; v1 diffusion is the classic failure mode. | High | Phased delivery principle; Stage 2 must ruthlessly sequence capabilities; Stage 12 roadmap |
| R-10 | **Single-reference-org bias.** Designing from Quadient's estate may bake in assumptions (e.g., Microsoft-only, 6k-employee scale) that break for other tenants. | Medium | "Not hardcoded" principle (§8.7); test major decisions against a hypothetical second customer |
| R-11 | **Two-masters risk.** Serving internal Quadient deadlines and commercial-product generality simultaneously slows both. | Medium | Explicit per-stage call-outs when internal need and product generality diverge |

---

## Stage-end review

### Summary
Stage 1 (v1.1, post red-team) defines the platform as the **independent AI portfolio-intelligence and governance layer** above native AI control planes — Microsoft-first, multi-tenant SaaS from day one, observe-and-orchestrate posture, pluggable native-first telemetry, three-capability core (federated ledger, governance orchestration, cost & value intelligence). Market research (July 2026) confirms the raw inventory/control layer is being commoditised by Microsoft itself (Agent 365 GA); the durable product is independence and judgment above the vendors' control planes.

### Assumptions
- Quadient's estate is representative enough to be the reference implementation without distorting the product (guarded by R-10).
- The AI CoE has, or will get, the mandate to operate cross-functional governance (the platform cannot create a mandate that doesn't exist).
- Customers will grant a third-party SaaS read access (and limited write access for orchestration) to their Microsoft tenants — common pattern, but consent friction is real (R-04).
- Microsoft's API surfaces will remain stable enough to build on (guarded by Stage 4 validation).

### Confirmed facts (from July 2026 research; sources in [research-annex-2026-07.md](research-annex-2026-07.md))
- Microsoft Agent 365 GA (May 2026): unified agent registry, RBAC, monitoring, cross-cloud registry sync; E7 or ~$15/user/month.
- Entra Agent ID GA: agent identity, ownership, lifecycle, Conditional Access for agents.
- Copilot Control System: tenant-wide agent approval/blocking; per-agent/per-user Copilot Credits consumption reporting; Graph Copilot usage report API GA (Oct 2025).
- Purview DSPM for AI: captures Copilot/agent interactions; third-party AI site detection via browser extension + Endpoint DLP.
- CoE Starter Kit end-of-life (2026); PPAC native Inventory (incl. Copilot Studio agents) with API.
- ServiceNow AI Control Tower expanded (May 2026, GA ~Aug 2026); Gartner sizes AI governance platforms at $492M (2026).
- EU AI Act: GPAI + transparency obligations live in 2026; high-risk delayed to Dec 2027 / Aug 2028.

### Unknowns
- Depth and stability of each Microsoft API (per-capability validation pending — Stage 4).
- Feasibility of enterprise-vs-personal account detection and prompt-sophistication estimation without content collection (OI-006/007 — Stage 5).
- Larridin's actual Microsoft-ecosystem depth (Unverified in research).
- Whether Microsoft will offer cross-*tenant* (multi-org) governance — nothing confirmed found.
- Quadient's works-council landscape and legal posture on L2+ telemetry (needs input — question 4 below).

### Risks
See §12 (R-01–R-11). Highest severity: Microsoft absorption (R-01), SaaS/tenant-access architecture (R-04), API coverage (R-06), ROI credibility (R-07), privacy backlash (R-08), scope breadth (R-09).

### Alternative approaches considered
- **In-tenant product** (Power Platform/Dataverse app per customer) instead of SaaS: lower consent/residency friction, faster internal v1; rejected by ADR-001, but a hybrid (in-tenant data plane + SaaS control plane) remains open (OI-002).
- **Security-led positioning** (compete with Zenity): larger current budgets, but crowded and off-vision; rejected (§5).
- **Compliance-led positioning** (compete with Credo/Holistic): regulatory tailwind but documentary, not operational; rejected as the lead, kept as a later capability.
- **Pure value-intelligence positioning** (compete with Larridin): abandons the governance backbone objective; rejected as sole focus. *(v1.1 note: ADR-008 subsequently elevated portfolio/value intelligence to lead the commercial positioning — but atop the governed ledger, not instead of it, which is what distinguishes this from the rejected pure-play.)*

### Questions for Arun (status at freeze, v1.1)
1. **Success criteria refinements** — *resolved by freeze*: registration coverage + discovery coverage reframing accepted (OI-005 closed).
2. **Positioning weight** — *resolved by ADR-008*: portfolio/cost/value/executive intelligence leads; governance core but not the primary commercial message.
3. **Hybrid architecture** (in-tenant data plane + SaaS control plane, OI-002) — **open**, carried to Stage 7/9.
4. **Works councils** — **open**; urgency reduced by ADR-007 (no own collectors in V1) but consultation may still be needed to *display* individual-level native data (L2+); needed before Stage 5 concludes.
5. **Naming** — deferred to commercialisation (DD-001) — working name retained.
6. **Stage 2 scope** — **open**: proposed Stage 2 = capability model & domain map organised around the three-capability core; awaiting confirmation.

### Recommendations
1. *(Revised in v1.1 per ADR-008.)* Commercial wedge: **federated ledger + portfolio economics** (spend attribution, licence utilisation, zombie assets, ROI) — governance orchestration is what the money buys and follows within the first year. Internal Quadient rollout may sequence governance-first given the existing CoE mandate; the divergence is tracked (R-11) and must be reconciled explicitly in the Stage 12 roadmap. The registry remains the credibility base in either sequence: value claims are only trusted when traceable to governed, owned assets.
2. Treat **Stage 4 (Microsoft integration/API validation)** as a go/no-go checkpoint for several Stage 1 claims; schedule it early (after Stage 2), before deep UX or data-model work.
3. Adopt the **honest-data labelling** principle as a brand-level commitment, not just a UX detail.
4. Begin **legal/works-council scoping** in parallel with planning stages — it has the longest lead time of anything in this blueprint (R-08).
5. Re-run the **Microsoft-gap analysis at every stage boundary** — the May 2026 Agent 365 release proves the ground moves fast (R-01).
