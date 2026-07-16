# Stage 11 — Governance Workflows, Operating Model, Roadmap & Commercialization

| | |
|---|---|
| **Version** | 1.1 |
| **Date** | 2026-07-15 |
| **Status** | **APPROVED — FROZEN** (ADR-026, PD-006). v1.1: certification roadmap added (§C3a) per Revision Package v1.0. |
| **Related** | Every prior stage; binding constraints: OI-013 covenant, ADR-008 (positioning), ADR-011 (phasing), ADR-021 (security posture), ADR-025 (economics) |

---

# Part A — Governance workflows & operating model

## A1. Governance operating model (three tiers)

| Tier | Body | Responsibility | Platform surface |
|---|---|---|---|
| Direct | **AI Governance Board** (CIO chair; CISO, CDO, CRO, CFO, Privacy, Audit) | Policy, risk appetite, exceptions above threshold, quarterly portfolio review | Executive page + board pack |
| Operate | **AI CoE** (platform owner) | Ledger operations, workflow execution, methodology operations with Finance, coverage stewardship | Operator workspace |
| Delegate | **Domain owners** (Security, Privacy, Risk, Finance, BU leaders, Agent Owners) | Domain approvals, attestations, BU-scoped administration (V1.5) | Scoped views + queues |

RACI principle: the platform records who was Responsible/Accountable for every decision *as an event* — the operating model is enforced by the audit trail, not by a slide.

## A2. Governance workflows (the C2/V1.5 design — filling the Stage 4 socket)

Workflow principles first (challenged against Challenge 01 §8 shelfware findings): **proportional** (effort scales with risk tier — a personal productivity agent is not a customer-facing production agent), **SLA-bound** (every queue has a response-time promise; governance slower than the spreadsheet gets bypassed), **evidence-emitting** (every step is an event), **faster than the alternative** (registration ≤10 minutes for low tiers; the maker gets legitimacy + visibility in return).

1. **Intake & registration:** dual entry — discovery-triggered triage (C1.7) and maker self-service. Tiered forms: low-risk = type/purpose/owner only; higher tiers add data-access, audience, jurisdiction questions that drive the risk profile.
2. **Risk assessment:** rule-assisted tiering (asset class + data sensitivity + audience + jurisdiction → proposed tier); human confirmation at higher tiers; Risk owns the methodology (C2.2); reassessment triggers: material change events, periodic review, incident.
3. **Approval gates:** tier-dependent (auto-approve low; single approver mid; board-level for exceptional); gates attach to *lifecycle transitions* (Stage 4 state machines — the socket snaps on as designed).
4. **Attestation:** periodic owner recertification ("still exists, still needed, still mine, purpose unchanged") — default annual, quarterly for top tier; non-response → Lapsed → governance debt.
5. **Exception/waiver:** time-boxed, reason-bound, approver-recorded, auto-expiring — the pressure valve that keeps gates honest.
6. **Decommissioning:** retirement checklist (dependents notified via DependencyRefs V2, licence/consumption stops verified, evidence archived) — closes the zombie loop with Financially-validated savings (Stage 10 §7).

## A3. Platform operating model (running the product)

Quarterly rituals (now consolidated): **Microsoft re-validation** (Stage 3 shelf-life), **methodology review** (ADR-025), **coverage review** (blind-spot acceptance is an explicit sign-off, not a default), **policy review** (telemetry levels vs jurisdictions). Standing operations: connection health, merge queue SLAs, release management, incident response with pre-committed customer disclosure (Stage 8 rec. 3).

# Part B — Adoption

## B1. Customer onboarding (time-to-first-truth ≤ 10 business days)

1. Consent ceremony (packs per Stage 8 §7; coverage map shows exactly what each grant enables) → 2. first sweeps + resolution → 3. **Coverage & Baseline Review**: "here is your estate, its owners, its costs, its blind spots — labelled" (the product's first honest artefact) → 4. telemetry policy workshop (L1 default; L2 is the customer's documented choice, ADR-021) → 5. registration campaign (triage backlog burn-down with SLA) → 6. Finance methodology onboarding (price book, allocation rules, close calendar — the §A1 Finance commitment made concrete).

## B2. Customer maturity model (drives success, packaging, and roadmap conversations)

| Level | Name | Achieved when |
|---|---|---|
| M1 | **Visible** | Coverage reviewed; ledger populated; baseline economics live; Unattributed % known |
| M2 | **Governed** | Workflows live; debt trending down; every production asset owned/purposed/risk-profiled |
| M3 | **Value-managed** | Validated closes running; Validated-to-Declared ratio reported to executives |
| M4 | **Optimised** | Chargeback-ready (ADR-025 preconditions met); portfolio steering decisions traceable to platform recommendations |

## B3. Internal Quadient rollout (the reference implementation)

Governance-first sequencing (per ADR-008's internal note): CoE + IT estate first, then two pilot BUs, then global. Works-council/legal engagement starts **now** (OI-003 — longest lead item; L1 default makes the initial conversation easy, L2 activation is a separate, later, documented ask). Quadient is reference, not template: every Quadient-specific configuration must be expressible as tenant configuration (R-10 guard).

# Part C — Release & commercialization

## C1. Roadmap

| Release | Content | Gate |
|---|---|---|
| **V1 — "The Ledger with Money Attached"** | ADR-011's 25 capabilities: federated ledger + alias graph, coverage map, cost attribution + showback, zombie detection, executive page + board pack, L1 privacy default, audit trail, legal hold | Gate-1 PoCs pass; Stage 5 finalised; naming decision (DD-001) before *commercial* launch only |
| **V1.5 — "The Governance Engine"** (**hard covenant: ≤ 2 quarters after V1**, OI-013/R-14) | C2 workflows (§A2), agent-owner view, notifications, delegated administration, capability tags, full value methodology + validation workflow, run-rate forecasting | Covenant is a binding roadmap constraint — staffing must be committed at V1 kickoff, not after |
| **V2** | Control invocation (write consent), attestations at scale, evidence packs, BI export, scenario modelling, chargeback exports (preconditions per ADR-025), SIEM event publishing, dependency mapping | Two validated closes for chargeback; write-API maturity re-validated (Stage 3 watch) |
| **Future** | Non-Microsoft providers (OpenAI/Anthropic/Google admin surfaces), cross-tenant benchmarking (PL-003), compliance content packs (EU AI Act wave, 2027 timing per Stage 1 research), in-tenant data plane option (ADR-020 seam), marketplace of provider/rule packs | Each gated by demand evidence, not roadmap ambition |

## C2. Release, versioning, feature flags

Quarterly minors, monthly patches; product semver; **provider contracts versioned independently** (a Microsoft surface change ships as a provider update, not a product release — the C4.5 payoff). Feature flags: tenant-scoped, audited configuration (they are governance objects like any other config), used for progressive delivery, edition gating, and **provider kill-switches** (a misbehaving feed is disabled per-tenant in seconds — coverage map says so honestly).

## C3. Commercial packaging & pricing principles (proposed ADR-026)

- **Editions:** **Foundation** (M1: ledger, coverage, baseline economics, showback) → **Professional** (M2–M3: governance engine, validation workflow, closes, forecasting) → **Enterprise** (M4 + control invocation, evidence packs, delegated admin, premium security: CMK/dedicated instance/customer-owned app registration).
- **Pricing metric: employee-band + edition.** The decisive argument: **per-asset pricing punishes registration and per-telemetry-volume pricing punishes coverage — both create incentives against the product's own mission.** Employee bands scale with value, are auditable, and never make a customer hesitate to register one more agent. Per-user seats rejected (value scales with estate, not logins). **Pricing must never create an incentive against governance — elevate to product principle.**
- **Licence-tier honesty in sales:** the Stage 3 §5 dependency map is a *pre-sales artefact* — customers see what their Microsoft licensing enables before buying (OI-004 as consultative selling, R-26 mitigation: Foundation demos value on the no-premium baseline).
- **Marketplace:** Azure Marketplace transactable listing; Microsoft co-sell track [pursue; programme details require validation at the time]; the ADR-004 complement-position is the co-sell narrative.
- **Partners:** SIs for implementation (methodology certified, B1 as the playbook); managed-governance MSPs ("CoE-as-a-service" partners) for customers without one — the platform enables the service, the vendor does not become the consultancy; security/AI-governance vendors as **provider partners** (their signals enter through C4 contracts — adjacent competitors become channel).
- **Professional services discipline:** fixed-scope packages (Discover 2w / Baseline 4w / Governed 8w mirroring B2); services held **< 20% of revenue** — this is a product company; services exist to make the product land, not to become the business.
- **Training & docs:** role-based certification (Operator, Finance Analyst, Executive orientation — 90 minutes, not a course); **docs-as-product** including a public trust centre (permission manifest, security architecture summary, privacy levels explained) — Stage 8's transparency is marketing.

## C3a. Certification roadmap *(added v1.1 per Revision Package v1.0 — repairs the Stage 8 Rec. 2 thread)*

SOC 2 Type I ~3–4 months post-V1 operations; **Type II gates broad commercial launch** (6–12 month observation window — the previously undeclared commercial year, now planned); ISO 27001 year 2; pen test pre-launch; trust centre at V1. Design-partner sales proceed pre-attestation with contractual caveats. Full table: [revision-package-v1.md](revision-package-v1.md) §3.

## C4. Business success metrics

Time-to-first-truth (≤10 days); coverage % and registration coverage per tenant; Validated-to-Declared ratio (portfolio-wide — the product's own honesty grade); V1→V1.5 attach rate; net revenue retention; covenant compliance (V1.5 on time); services revenue share (<20%); reference-ability (M3+ customers willing to speak).

---

# Part D — Blueprint conclusion

## D1. Executive summary

Eleven stages ago this was "an agent registry with governance features." The red-team killed that product before Microsoft could (Agent 365 shipped its core while we planned). What survived the challenge is stronger: **the independent ledger of enterprise AI — every asset, every euro, every outcome; governed, attributed, and provable — across every vendor.** Three capabilities (federated ledger, governance orchestration, cost & value intelligence), eight bounded contexts, twenty entities, a deliberately boring Azure stack, a security architecture that assumes it is the target, and an economics methodology that would rather report a smaller number than an indefensible one. Governance is the backbone; independence and honest economics are the product.

## D2. Final positioning

*For enterprises running AI at scale on Microsoft and beyond, [Product] is the independent AI portfolio-intelligence and governance platform that tells the board what AI costs, what it returns, and that it is under control — with every number carrying its evidence, its confidence, and its source. Unlike platform vendors' own dashboards, it has no stake in the answer.*

## D3. Why customers buy

1. They cannot answer "what AI do we run, what does it cost, what is it worth, is it controlled?" today — and their board is asking.
2. **Independence:** no CFO accepts the vendor's own dashboard as grounds to cut that vendor's licences; we are the only party structurally able to say "cut."
3. **Defensible numbers:** evidence-classed economics survive Finance and Audit scrutiny — the first AI ROI figures they can put in front of a board without flinching.
4. **Governance that doesn't block:** Flag-Never-Block + proportional workflows make governance cheaper than shadow behaviour.
5. **Microsoft depth without Microsoft dependence:** evidence-validated integrations today, vendor-neutral by architecture tomorrow.

## D4. Why competitors cannot easily copy it

- **Microsoft/ServiceNow (structural):** the conflict of interest is not a feature gap — a platform vendor recommending cuts to its own platform repudiates itself. This moat widens as their AI revenue grows.
- **Adjacent startups (cultural + accumulated):** the honesty architecture (weakest-link labels, Unattributed buckets, Validated-to-Declared) is easy to describe and brutal to retrofit — a product that shipped inflated dashboards cannot adopt honest ones without repudiating its installed base. Meanwhile the alias graph, evidence chains, and registration-time bindings compound per tenant — a data advantage that transfers to no competitor.
- **Consultancies:** can copy the methodology, cannot ship the system of record.
- Honest caveat: none of this stops a well-funded entrant *starting* honest. Speed to the reference customer (Quadient at M3) is the perishable part of the moat.

## D5. Remaining risks (consolidated top set)

| Risk | State |
|---|---|
| R-01 Microsoft absorption of adjacent value | Standing; quarterly re-validation ritual; moat concentrated in independence + org-process + honesty layers |
| R-12 correlation residual | Gate-1 PoCs pending — **the** implementation gate |
| R-14 V1.5 covenant slip | Binding constraint; staff at V1 kickoff |
| R-25 aggregation-target security | Never closable; ADR-021 posture + pre-committed disclosure |
| R-31 Finance under-resourcing validation | The biggest *organisational* risk to the defining capability (OI-010) |
| R-03 market consolidation | Speed + reference-ability; naming/category work (DD-001/OI-008) before commercial launch |
| R-30 pressure to soften honest numbers | ADR-025 makes them principles; treat softening requests as strategy regressions |

## D6. Assumptions register (summary)

Quadient representative but not hardcoded (R-10 guard); CoE mandate real and staffed; Finance accepts §A1/Stage 10 §8 role; Microsoft APIs stable enough between quarterly re-validations; L1-default satisfies EU works councils for launch; modular monolith ceiling holds to commercial scale; V1.5 within two quarters is deliverable by the eventual team (the one assumption no planning document can prove).

## D7. Open questions (carried into implementation)

OI-003 works-council/legal (in motion, longest lead); OI-010 Finance staffing (concrete ask exists); DD-001 naming + OI-008 category strategy (pre-commercial-launch); Gate-1/2/3 PoC results; price book availability; org tooling standards (GitHub/Bicep confirmations); Quadient BU pilot selection.

## D8. Readiness assessment

**Ready:** strategy (challenged twice, refocused once), capability model, domain model, conceptual data model (95% — PoC-gated sections bounded), experience architecture, conceptual/integration architecture, security architecture, technology direction, economics methodology, operating model, roadmap, commercial frame. Internally consistent: 26 ADRs, 5 process decisions, every stage approved, conflicts resolved in-line (taxonomy v2 propagated; ADR-011 phasing honoured throughout).

**Gates before implementation:** (1) Gate-1 PoCs (correlation) — commissioned, not executed; (2) legal review of erasure/retention posture; (3) Finance co-ownership commitment; (4) build-kickoff re-validation of Stage 3/9 specifics. **Verdict: the blueprint is complete and coherent at the conceptual level and ready for implementation handoff once Gate 1 closes.** Nothing in the remaining gates threatens the architecture; they threaten only the schedule.

---

## Stage-end review

### Summary
The closing stage: proportional SLA-bound governance workflows filling the Stage 4 socket; a three-tier operating model enforced by the audit trail; onboarding to first-truth in ten days; a four-level maturity model that aligns success, packaging, and roadmap; the covenant-bound release plan; and a commercial model whose pricing principle — never create an incentive against governance — is itself a product differentiator.

### Assumptions / Confirmed facts / Unknowns / Risks
Consolidated in D5–D7 (this stage's function). No new Microsoft claims. New proposal requiring decision: ADR-026 (editions, employee-band pricing, marketplace/partner/services model, <20% services discipline).

### Alternative approaches considered
Per-asset and consumption pricing (rejected — perverse incentives, §C3); becoming the managed-service provider ourselves (rejected — partner-enabled instead); launching commercial and internal simultaneously (rejected — Quadient reference first, R-11 sequencing).

### Questions for Arun
1. **Approve ADR-026** (commercial packaging, employee-band pricing, partner/services model)?
2. **Approve Part D** as the blueprint's formal conclusion (positioning, moat, readiness verdict)?
3. Any final-stage corrections before the blueprint is frozen as a whole?
4. On your approval, I will prepare the **implementation handoff package** (per your instruction: not before) — proposed contents: consolidated blueprint index with reading order for an implementation agent, the frozen ADR set, RTM completion (BR↔JTBD↔capability↔stage mapping), Gate-1 PoC commissioning brief, and build-kickoff checklist (re-validation items, staffing preconditions, covenant clock). Confirm or amend that package scope.

### Recommendations
1. Approve and freeze the blueprint; run the two long-lead organisational actions (OI-003 legal, OI-010 Finance) immediately — they gate reality, not documents.
2. Commission Gate-1 PoCs this quarter; the covenant clock starts at V1 kickoff and every week of PoC delay is a week of clock.
3. Decide the product name before any external conversation (DD-001) — "Enterprise AI Control Tower" is a fine internal codename and a lawsuit-adjacent brand.
