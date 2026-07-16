# Architecture Decision Log

Version 2.1 — 2026-07-15 (PD-007: Revision Package v1.0 applied; final planning revision). **FROZEN with the blueprint (PD-006).** Changes only via the formal revision process: proposed revision → impact analysis across affected documents → Arun's approval → new versions with change identification. Every major decision, its rationale, alternatives considered, and downstream impacts.

Status values: **Accepted** | **Proposed** | **Superseded** | **Deferred**

---

## PD-001 — Stage-by-stage planning with explicit approval gates
- **Status:** Accepted (2026-07-15)
- **Decision:** The blueprint is produced in stages. No stage proceeds without Arun's explicit approval. No implementation until the full blueprint is approved. No application code is generated during planning.
- **Impact:** All documents versioned; changes to prior stages must be identified explicitly.

## PD-002 — Living Markdown knowledge base as the deliverable format
- **Status:** Accepted (2026-07-15)
- **Decision:** One versioned Markdown document per stage; separate ADR log, Open Issues/Parking Lot, and Requirements Traceability Matrix; cross-referenced; hand-off-ready for implementation tooling.
- **Alternatives considered:** Single master document (rejected — unmaintainable); Word documents (rejected — poor versioning/diffing).

---

## ADR-001 — Commercial-grade multi-tenant SaaS architecture from day one
- **Status:** Accepted (2026-07-15)
- **Decision:** Design as a commercial-grade SaaS platform from day one: multi-tenancy, modular deployment, configurable governance, future commercialisation without fundamental redesign. Initial deployment internal to Quadient (reference organisation, ~6,000 employees, NA/EU/APAC; not hardcoded to Quadient).
- **Evolution note:** Initial answer in the same session was "internal first, product later"; upgraded same day to SaaS-from-day-one by explicit instruction from Arun.
- **Alternatives considered:** In-tenant build (e.g., Power Platform/Dataverse app inside the customer tenant) — simpler, but forecloses commercialisation and multi-tenant economics.
- **Downstream impacts / tensions:** (a) A SaaS control plane accessing customer Microsoft tenants requires a multi-tenant Entra app registration, admin consent, and least-privilege Graph permissions per customer — this becomes a foundational security architecture concern (Stage 9). (b) Data residency for telemetry held outside the customer tenant conflicts with some buyers' expectations; may require in-tenant data plane / SaaS control plane split. Logged as OI-002.

## ADR-002 — Enforcement posture: observe + orchestrate, delegate enforcement
- **Status:** Accepted (2026-07-15)
- **Decision:** The platform observes, records, and orchestrates governance (approval workflows, lifecycle gates, risk profiles). Technical enforcement (blocking, quarantine, access revocation) is delegated to native Microsoft controls (e.g., Copilot Control System agent blocking, Entra Conditional Access, Purview policies), which the platform may *invoke* but does not replicate.
- **Alternatives considered:** Observe-only (too weak for "operational backbone" objective); full native enforcement (rejected — duplicates Microsoft controls, high risk, conflicts with ADR-004).
- **Impact:** Platform needs write-scope integration to trigger native controls — API feasibility to be validated in Stage 4.

## ADR-003 — Privacy by Design with configurable telemetry levels
- **Status:** Accepted (2026-07-15)
- **Decision:** Four independently configurable telemetry levels: L1 Aggregate-only (org/dept/BU analytics, no individual visibility); L2 Individual metadata (app, session duration, model, estimated tokens, enterprise-vs-personal account where technically possible, feature usage — no prompt/response content); L3 Enhanced governance (additional metadata, explicit organisational approval); L4 Diagnostic mode (temporary, auditable, time-limited, explicitly authorised). Prompt and response content are never collected by default. Every telemetry capability independently configurable, audited, transparent.
- **Rationale:** Multinational deployments face divergent legal/cultural requirements (GDPR, UK GDPR, EU AI Act, works councils in FR/DE).
- **Impact:** Telemetry architecture (Stage 5/8) must enforce level boundaries technically, not just by policy. Works council consultation likely required for L2+ in parts of the EU — logged as OI-003.

## ADR-004 — Complement Microsoft, never compete; consume native capabilities first
- **Status:** Accepted (2026-07-15)
- **Decision:** Where Microsoft provides native capability, the platform consumes it (data and controls). Where Microsoft has gaps, the platform adds value. The platform positions as the orchestration/intelligence layer *above* Microsoft's control planes — explicitly including Microsoft Agent 365, Entra Agent ID, Copilot Control System, Purview DSPM for AI, Power Platform admin center Inventory, Microsoft Foundry Control Plane, and Copilot analytics APIs (capability status per Stage 1 §10).
- **Rationale:** Microsoft closed much of the raw agent inventory/blocking/cost-reporting gap between late 2025 and May 2026 (Agent 365 GA). Competing on those primitives is a losing race; durable value is portfolio management, cross-vendor aggregation, business value/ROI intelligence, governance orchestration, and executive decision support.
- **Impact:** Stage 4 must produce a consume-vs-build matrix with validated API coverage. Platform value depends partly on customers' Microsoft licensing tiers (Agent 365 is E7 or ~$15/user/month standalone — Confirmed) — logged as risk R-05.

## ADR-005 — The platform is a system of record and orchestrator, not a replacement for execution tools
- **Status:** Accepted (2026-07-15)
- **Decision:** The platform will not replace Copilot Studio, Power Automate, Purview, Entra, Defender, Microsoft Foundry, Power BI, Azure Monitor, or Fabric. It is not an LLM/model provider, agent builder, workflow designer, SIEM, data governance platform, or endpoint security product.
- **Impact:** Defines hard product boundaries (Stage 1 §3); prevents scope creep in later stages.

## ADR-006 — Product core: independent AI portfolio intelligence, three-capability core
- **Status:** Accepted (2026-07-15, from Challenge 01)
- **Decision:** The product is refocused around **independent AI portfolio intelligence** rather than an agent registry. The product core is three capabilities: (1) federated AI asset ledger, (2) governance orchestration, (3) cost & value intelligence. The registry is foundational plumbing, not the product identity. Every proposed feature must be a view of, a feed into, or a component of these three — otherwise it is parked.
- **Rationale:** Microsoft commoditised the registry layer (Agent 365, Entra Agent ID, PPAC — all GA); the durable moat is independence — cross-vendor measurement no platform vendor can credibly perform on itself.
- **Alternatives considered:** Continue broad 30-area vision (rejected — six products' worth of scope, v1 diffusion risk R-09); fundamental repositioning to pure FinOps-for-AI (rejected — abandons governance backbone objective).
- **Impact:** Stage 1 revised to v1.1; scope cuts moved to parking lot (PL-006..PL-010); Stage 2 capability model must be organised around the three-capability core.

## ADR-007 — Telemetry strategy: pluggable providers, native-first V1, no custom collectors in V1
- **Status:** Accepted (2026-07-15, amended from Challenge 01 proposal)
- **Decision:** V1 consumes native Microsoft telemetry and supported third-party telemetry only. The architecture must support **pluggable telemetry providers** so additional sources — including custom collectors (browser extension, desktop, endpoint), if ever justified — can be added later **without architectural redesign**. Custom telemetry collection is deferred, not permanently excluded (amendment vs the original "never build" proposal). Personal-account AI visibility beyond Microsoft-native capabilities is **not a V1 requirement**.
- **Rationale:** Purview browser extension + Endpoint DLP, Graph usage APIs, Agent 365 monitoring, and partner tools already collect the signals; shipping our own collectors adds endpoint-software burden, security-vendor competition, and works-council exposure (R-08) for marginal V1 gain.
- **Impact:** Stage 5 reframed from "how do we collect?" to "what can we responsibly consume, through what provider contract?" Telemetry provider abstraction becomes a first-class architecture component (Stage 7/8). ADR-003 privacy levels apply to storage/display regardless of provider. Reduces (does not eliminate) R-08.

## ADR-008 — Commercial positioning: portfolio intelligence first, governance core but not the message
- **Status:** Accepted (2026-07-15, amended from Challenge 01 proposal)
- **Decision:** The commercial product is positioned around **AI portfolio intelligence, cost intelligence, business value, and executive decision support**. Governance remains a core capability but is not the primary commercial positioning. Commercial wedge: portfolio economics (spend attribution, licence utilisation, zombie assets, ROI) — governance is what the money buys. Internal Quadient rollout may still sequence governance-first, given the existing CoE mandate (two-masters divergence tracked as R-11).
- **Open remainder:** analyst category strategy — positioning leads with portfolio/value while the recognised Gartner category is "AI Governance Platforms"; straddle risk tracked as OI-008.
- **Impact:** Stage 1 §2/§7-recommendation/§11 revised; Stage 12 roadmap must sequence commercial wedge vs internal rollout explicitly.

## ADR-009 — Eight-bounded-context domain model
- **Status:** Accepted (2026-07-15 — Stage 2 direction approved; both structural invariants explicitly approved by Arun)
- **Decision:** Eight bounded contexts: core — C1 AI Asset Ledger, C2 Governance Orchestration, C3 Cost & Value Intelligence; supporting — C4 Provider Integration, C5 Enterprise Context, C7 Experience & Insight; generic — C8 Trust & Access, C9 Audit & Evidence. Structural invariants: C4 is the only door for external signals (in and out); C7 is the only door for human-facing views.
- **Alternatives considered:** nine contexts with standalone usage-intelligence (rejected — recreates Activity Intelligence gravity); persona-shaped domains (rejected — views are projections); C9 folded into C2 (rejected — audit spans configuration and telemetry changes too).
- **Impact:** All later architecture/data/UX stages organise around these contexts and invariants.

## ADR-010 — "AI Activity Intelligence" dissolved as a domain
- **Status:** Accepted (2026-07-15 — explicitly approved by Arun: AI activity is a capability delivered through Provider Integration and Cost & Value Intelligence, consistent with ADR-007)
- **Decision:** The original brief's Activity Intelligence capability area is not a bounded context. It dissolves into C4 (privacy-enforced telemetry consumption, ADR-003/007) and C3.7 (usage & adoption analytics). Context slot C6 is recorded as intentionally vacant to make the disposition explicit.
- **Rationale:** A standalone activity-intelligence domain would regrow the collector-product ambitions removed by ADR-007.
- **Impact:** Stage 5 (telemetry deep-dive) scopes to provider selection + feasibility per consumed signal, not collection design.

## PD-003 — Planning sequence change: Stage 3 = Microsoft Platform Validation
- **Status:** Accepted (2026-07-15)
- **Decision:** Stage 3 becomes Microsoft Platform Validation (feasibility proof before UX or implementation design); personas/journeys/UX moves to Stage 4. For every capability, Stage 3 validates: whether Microsoft provides it, owning service, API/data source, official support, documentation, authentication model, permissions, rate limits, data freshness, known limitations, missing data, and a confidence level (Confirmed / Likely / Unknown / Requires proof of concept).
- **Rationale:** Stage 1 Recommendation 2 and R-06/R-12: API coverage and identifier stability are go/no-go for the ledger concept; designing UX before feasibility proof risks designing fiction.

## ADR-011 — Minimal V1: "Ledger + Economics"
- **Status:** Accepted (2026-07-15). Arun additionally approved: risk profile + lifecycle status as managed V1 fields on C1.1; full governance workflow orchestration in V1.5; OI-013/R-14 accepted — **Stage 12 must guarantee V1.5 within two quarters of V1 (binding roadmap constraint)**.
- **Decision:** V1 = 25 capabilities (see [challenge-02-v1-minimization.md](challenge-02-v1-minimization.md) §1): federated ledger + provider integration + cost/portfolio intelligence + executive dashboard + operator workspace + trust/audit minimums. Governance orchestration (C2) moves entirely to V1.5 with a **hard covenant: V1.5 ships within two quarters of V1**, else Stage 1's frozen 12-month success criteria fail (OI-013/R-14). Risk profile and lifecycle status are manual V1 fields on C1.1. Removed permanently: full business-capability-map construction (consume-or-tags only); bespoke persona workspaces beyond the V1 trio (parameterised views instead).
- **Alternatives considered:** thin intake form in V1 (rejected — surveys are shelfware); larger V1 with C2 (rejected — roughly doubles build weight without adding to the first sale, contra ADR-008).
- **Impact on prior decisions:** none conflict; refines Stage 2 phasing (Stage 2 → v1.1 on approval). Corrects Stage 2 §4 miscount (34, not 26).

## ADR-012 — Ledger correlation: identity-spine + alias graph + confidence tiers
- **Status:** Accepted (2026-07-15, amended). Approved principles: Entra identity is the primary correlation spine where available; alias graph for cross-system resolution; **visible Match Confidence taxonomy amended by Arun to High / Medium / Low / Manual** (mapping: High = deterministic documented join; Medium = strong heuristic; Low = weak heuristic — never auto-linked, enters merge queue; Manual = operator-decided); insufficient confidence → manual merge queue; **the platform never presents uncertain correlations as facts**.
- **Decision:** C1.3 is an entity-resolution model, not a key join. Each asset owns a set of provider-scoped identifier aliases; Entra Agent ID + appId is the preferred spine where present, never a requirement; every asset carries a visible match-confidence tier (Deterministic / Strong heuristic / Manual / Unmatched); manual merge/split queue; registration-time ID binding from V1.5 converts heuristic→deterministic over time; the coverage map (C1.6) reports correlation quality per surface.
- **Evidence basis:** deterministic cross-surface correlation is documented only for a subset (PPAC `entraAgentId` fields [Confirmed]; Foundry ARM `agentIdentityId` [Confirmed]); the only universal documented join (`agentInstance.sourceAgentId`) was deprecated May 2026 without a shipped replacement [Confirmed]. See [stage-03-microsoft-validation.md](stage-03-microsoft-validation.md) §4.
- **Alternatives considered:** wait for Microsoft's replacement registry API (rejected — timing unknown; alias graph absorbs it when it ships); require Entra Agent ID for all managed assets (rejected — excludes Agent Builder, legacy, third-party assets).
- **Impact:** R-12 → AMBER (managed). Stage 8 data model must implement the alias graph; correlation confidence becomes a product feature (honest-data principle applied to the ledger itself).

## ADR-013 — Copilot Credits: manual CSV import in V1, labelled "Self-reported / Manual Import"
- **Status:** Accepted (2026-07-15)
- **Decision:** Pending a Microsoft API (none exists — Stage 3 [Confirmed]), V1 ingests Copilot Credits consumption via manual CSV import through the standard C4.5 provider contract, with every derived figure labelled "Self-reported / Manual Import". A future API replaces the CSV provider **without architectural change**.
- **Impact:** C4 gains a "manual/file provider" type — also reusable for other API-less feeds. Roadmap-watch quarterly (OI-014).

## ADR-014 — Privacy re-masking invariant
- **Status:** Accepted (2026-07-15)
- **Decision:** If a tenant disables Microsoft report pseudonymization (all-or-nothing tenant toggle), the platform **immediately re-applies its own configurable privacy policies before any user-facing experience**. Invariant: *the platform never exposes more personal information than the customer's chosen telemetry policy (ADR-003 levels, jurisdiction-scoped) permits* — regardless of what upstream sources reveal.
- **Impact:** C4.4 enforcement at ingestion + a policy-enforcement point before every C7 read model. Strengthens the privacy differentiator; adds build weight (accepted). Supersedes nothing; operationalises ADR-003.

## PD-004 — PoC gating
- **Status:** Accepted (2026-07-15)
- **Decision:** Gate-1 PoCs (correlation, Stage 3 §7) run in parallel with Stage 4; Stage 4 must not depend on them. **Stage 5 (Conceptual Data Model) cannot be finalised until Gate-1 PoCs complete** — entity resolution is a foundational architectural dependency.

## ADR-015 — Domain model doctrines ratified
- **Status:** Accepted (2026-07-15, Stage 4 approval)
- **Decision:** (1) ProviderObservations immutable and append-only; (2) entity resolution via ResolutionLinks only — source observations never modified; (3) AIAsset is a single aggregate with polymorphic asset types; (4) RegistrationStatus and OperationalLifecycle are separate state machines; (5) ownership is temporal with first-class Ownerless and Lapsed states; (6) analytics are projections, never system of record; (7) people are references, not aggregates; (8) audit is event-driven with immutable domain events; (9) **privileged-read auditing enabled by default**.
- **Impact:** These are architecture invariants alongside ADR-009's two doors. See [stage-04-domain-model.md](stage-04-domain-model.md).

## ADR-016 — Financial-close support: frozen reporting periods + historical restatement
- **Status:** Accepted (2026-07-15)
- **Decision:** The model must support financial close: frozen reporting periods (a persisted, signed-off statement of a period's allocated cost/value with its input basis) and historical restatement (a new snapshot version superseding — never overwriting — a frozen one). Promotes `ReportSnapshot` from parking lot (PL-011) into the model; refines Stage 4 §11.4.
- **Impact:** Stage 5 must define the ReportingPeriod/ReportSnapshot entities; allocation projections gain a freeze semantic.

## PD-005 — Gate-1 PoC specifications authorised as planning artifacts
- **Status:** Accepted (2026-07-15)
- **Decision:** Gate-1 PoC specifications are written now (see [poc-gate1-specifications.md](poc-gate1-specifications.md)) as planning artifacts only — no implementation (PD-001 stands). Stage 5 finalisation remains gated on their execution results (PD-004).

## ADR-017 — Policy-driven retention with recommended defaults
- **Status:** Accepted (2026-07-15)
- **Decision:** Retention is policy-driven, never hardcoded. Recommended defaults: financial evidence 7y; cost evidence 7y; audit events 7y; provider observations configurable (default 2y); usage telemetry configurable (default 2y); temporary processing data shortest practical. Tenant-configurable within jurisdiction floors/ceilings.
- **Note:** amends Stage 5 §9 draft defaults (audit events 10y → 7y default, configurable upward; observations 24m → 2y default).

## ADR-018 — Flag, Never Block
- **Status:** Accepted (2026-07-15)
- **Decision:** The ledger represents reality, not policy compliance. Poor quality, missing ownership, missing governance, or missing metadata create **governance debt** — never prevent an asset from existing in the ledger. Ratifies Stage 5 §10 as a named platform principle.

## ADR-019 — Experience architecture principles ratified
- **Status:** Accepted (2026-07-15, Stage 6 approval)
- **Decision:** (1) **Trust is a first-class navigation area** — users must always be able to see why a number exists, where it came from, how confident the platform is, what evidence supports it, and when it was refreshed. (2) **Single polymorphic Asset Record** — one primary experience for every asset type; type-specific sections, consistent navigation and mental model. (3) **Single reporting model** — the in-product Executive Dashboard and exported Board Pack are generated from the same data, confidence model, evidence model, and calculations; no separate reporting logic. (4) **The screen test is binding** — every screen exists to serve a decision or workflow; no feature pages, data browsers, or administrative pages without operational purpose. The Stage 6 §11 kill list is precedent.

## ADR-020 — Conceptual architecture ratified (topology, monolith, API, invariants)
- **Status:** Accepted (2026-07-15, Stage 7 approval)
- **Decision:** (1) **OI-002 closed**: pure SaaS deployment for V1; the three-plane separation (ingestion/data, domain/control, experience) must permit a future in-tenant data plane for residency-sensitive customers without product redesign; the hybrid model is NOT built in V1. (2) **Modular Monolith First is binding**: strict bounded-context boundaries; no microservices without a proven operational, scaling, security, or deployment justification. (3) **Public API is Later**: internal contracts and extension boundaries designed cleanly; no supported external API in V1. (4) **Invariants ratified**: C4 is the only path for external signals and control actions; C7 is the only path for human-facing experiences; no component bypasses provider adapters; all user-facing views and exports use privacy-enforced read models; privacy enforcement at both ingestion and read time; partial provider failure degrades visibility honestly without stopping the platform.

## ADR-021 — Security & trust architecture ratified
- **Status:** Accepted (2026-07-15, Stage 8 approval)
- **Decision:** (1) **No-standing-access staff posture**: all staff access to customer tenant data is JIT, time-limited, approval-gated, purpose-bound, fully audited, and visible in the customer's privileged-access log; support-speed trade-off accepted (R-27). (2) **L1 aggregate-only telemetry is the default for every tenant**; L2 requires explicit customer activation + documented purpose + internal approvals + jurisdiction-aware policy; L3/L4 require additional approval controls and enhanced auditing. (3) **Legal hold is a V1 capability**: tenant-scoped, reason-bound, time-stamped, authorised, audited, releasable only through an approved process; retention/deletion never destroy held evidence. (4) **Eleven security principles ratified**: federation-only human access; credentials isolated more strongly than data; no cross-tenant enumeration; transparent capability-based consent; coverage map shows what each grant enables; privileged operations in a distinct security/audit boundary; storage-refusal privacy enforcement; no retention above permitted telemetry level; verifiable evidence integrity; export/deletion/retention/legal-hold as first-class lifecycle capabilities; mandatory multi-tenant failure isolation.

## ADR-022 — Build platform: custom Azure application
- **Status:** Accepted (2026-07-15)
- **Decision:** The platform is a custom Azure application. Power Platform is intentionally not the application platform; it may be *integrated* where appropriate but is never the foundation of the product. Grounds: SaaS commercial model, multi-tenancy fit, architectural fit (append-only/integrity/plug-in contracts), independence positioning. See Stage 9 §1.

## ADR-023 — Target technology stack (amended)
- **Status:** Accepted with amendment (2026-07-15)
- **Decision:** Containerised .NET modular monolith; Azure Container Apps; React + TypeScript; Entra ID; Azure Key Vault; Azure Service Bus; regional deployment stamps; GitHub Actions; Bicep as default IaC.
- **Amendments by Arun:** (1) **PostgreSQL vs Azure SQL is an implementation decision, reversible until build begins; no architectural decision may depend on PostgreSQL-specific capabilities without measurable benefit.** (2) CI/CD and IaC tooling (GitHub/AzDO, Bicep/Terraform) are a **deployment concern, not an architectural concern** — swappable without affecting platform architecture. (3) **Azure lock-in accepted as a conscious commercial decision**; do not optimise for hypothetical multi-cloud.
- **Cloud-portability conditions (documented per Arun's instruction):** the platform could later become cloud-portable because: runtime is containerised (no Azure-proprietary compute model); instrumentation is OpenTelemetry; data layer targets standard SQL (per amendment 1); Azure-proprietary seams are isolated to three adapters — secrets (Key Vault), queueing (Service Bus), blob/WORM storage — each replaceable behind existing module interfaces. **Triggers that would justify portability work:** a committed customer segment contractually requiring non-Azure hosting; acquisition/strategic scenarios requiring cloud neutrality; material adverse change in Azure commercial terms. Absent a trigger, no portability investment.

## ADR-024 — Economics methodology principles
- **Status:** Accepted (2026-07-15, Stage 10 approval). See Stage 10 for full principle list (weakest-material-link composition, categorical confidence, time-saved capture assumptions, forward-only methodology changes, Validated-to-Declared KPI).

## ADR-025 — Economics decisions ratified
- **Status:** Accepted (2026-07-15)
- **Decision:** (1) **Defensibility over impressiveness** — under uncertainty, the platform reports the smaller, more defensible number. (2) **Six evidence classes are the permanent platform standard** (Financially Validated, Measured, Estimated, Self-reported, Inferred, Unknown); no economic metric displays without evidence source, evidence class, as-of date, **and methodology reference**. (3) **Chargeback maturity model**: V1 showback only; chargeback requires two consecutive financially validated periods + Finance approval + stable allocation methodology + executive approval; the platform generates recommendations and ERP-ready exports, never becomes a billing engine. (4) **Default windows** (all configurable): zombie detection 90 days; portfolio ROI trailing 12 months; methodology review quarterly. (5) **Core product principle: unattributed cost is never spread to make reports look complete** — Unattributed remains visible until evidence exists.

## ADR-026 — Commercial model
- **Status:** Accepted (2026-07-15, Stage 11 approval)
- **Decision:** Employee-band pricing with editions (Foundation / Professional / Enterprise). **Rejected: per-agent, per-asset, and per-telemetry pricing — pricing must never discourage governance, registration, or telemetry coverage** (product principle). Professional services accelerate adoption, never become the business model: target <20% of revenue long-term. Chargeback per ADR-025 maturity model; marketplace + partner + certified-implementation model per Stage 11 §C3.

## PD-006 — Blueprint freeze & Stage 11 decisions
- **Status:** Accepted (2026-07-15)
- **Decision:** Stage 11 approved, including: proportional governance operating model (**governance must always be faster than spreadsheets; low-risk assets registrable in minutes**); "First Truth in 10 Business Days" onboarding objective with the Coverage & Baseline Review as the first measurable customer success milestone; the four-stage maturity model (Visible, Governed, Value Managed, Optimized); Part D as the blueprint's formal conclusion. **The entire blueprint is FROZEN**: all stage documents, ADRs, RTM, parking lot, architecture principles, product principles. No further architectural changes except through the formal revision process (header above). Stage 5 remains frozen *as draft v0.9* — its PoC-gated finalisation is a pre-authorised revision upon Gate-1 results, not an open edit.

## PD-007 — Independent Review 01 accepted; Revision Package v1.0 applied
- **Status:** Accepted (2026-07-15)
- **Decision:** Independent Review 01 accepted. Revision Package v1.0 applied under PD-006 — architecture NOT reopened. Changes: Stage 1 → v1.2 (success criteria restated, clock defined); Stage 2 → v1.2 (C4.7 HRIS, C4.8 ERP providers); Stage 5 → v0.10 (E21 ContractCommitment, FX methodology); Stage 10 → v1.1 (realisable-savings classification, price-book confidence); Stage 11 → v1.1 (certification roadmap §C3a); L1→L2 Licence Reclamation Campaign workflow designed (revision package §2); two overclaims glossed (Microsoft licence-insight precision; hybrid = no *architectural* redesign but substantial engineering). New artifacts: Gate-0 framework, Customer Discovery Plan, Competitive Battlecard, Revision Package v1.0.
- **Final readiness determination:** **Yes, with identified implementation risks** (revision package §4: Gate-0 undecided; Gate-1 PoCs unexecuted; discovery pending; OI-003/OI-010 organisational commitments; evidence decay; covenant staffing). **This is the final planning revision — the blueprint is complete.**

---

## Deferred decisions

| ID | Decision | Deferred to | Notes |
|---|---|---|---|
| DD-001 | Product name | Pre-commercialisation | "AI Control Tower" collides with ServiceNow's flagship branding — see risk R-02 |
| DD-002 | Build technology (in-tenant components vs pure SaaS; Azure services) | Stage 7/9 | Constrained by ADR-001 + ADR-004 |
| DD-003 | Which telemetry providers are "supported" in V1 (Graph, Purview, Agent 365, third-party) | Stage 5 | Partially resolved by ADR-007 (pluggable providers, native-first, no custom collectors in V1); remaining scope is provider selection + feasibility per signal |
| DD-004 | ROI measurement methodologies | Stage 10 | Must use Measured/Estimated/Self-reported/Inferred/Unknown taxonomy |
| DD-005 | Which non-Microsoft AI vendors to support first | Stage 12 | Microsoft-first confirmed for initial releases |
