# Stage 2 — Capability Model & Domain Map

| | |
|---|---|
| **Version** | 1.2 |
| **Date** | 2026-07-15 |
| **Status** | **APPROVED** 2026-07-15 (ADR-009/010/011 accepted; both structural invariants approved). Phasing per ADR-011 in §4. v1.2: C4.7 (HRIS) and C4.8 (ERP/procurement) providers added per Revision Package v1.0 (PD-006 revision). |
| **Related** | [stage-01-product-vision.md](stage-01-product-vision.md) (frozen v1.1), [decision-log.md](decision-log.md) (ADR-006/007/008 govern this model; ADR-009/010 proposed here), [requirements-traceability-matrix.md](requirements-traceability-matrix.md), [open-issues-parking-lot.md](open-issues-parking-lot.md) |

**Revision history**

| Version | Date | Change |
|---|---|---|
| 1.0 | 2026-07-15 | Initial capability model and domain map around the three-capability core |

Conceptual only — no implementation, databases, APIs, or technology choices. Where a Microsoft *surface* is named as a consumption source, its existence is [Confirmed] per the July 2026 research; the depth of what it exposes **requires validation** in the Microsoft integration stage.

Classification legend: **Consume** = use a native capability/data as-is; **Orchestrate** = coordinate native capabilities or organisational processes without replicating them; **Build** = differentiating capability we create. Many Build capabilities operate *on* consumed data ("build-on-consume") — classification reflects where the differentiating work is.

Phase legend: **V1** = first release; **V2** = fast-follow within year one; **Later** = post-year-one / commercial phase.

---

## 1. Domain overview

The domain decomposes into **eight bounded contexts**: three core (the product), three supporting, two generic.

```
                    ┌────────────────────────────────────────────┐
                    │            C7 EXPERIENCE & INSIGHT          │  views
                    └──────▲──────────────▲──────────────▲───────┘
                           │              │              │
   CORE          ┌─────────┴───┐  ┌───────┴──────┐  ┌────┴────────┐
                 │ C1 AI ASSET │◄─┤ C2 GOVERNANCE│  │ C3 COST &   │
                 │   LEDGER    │  │ ORCHESTRATION│  │ VALUE INTEL │
                 └──────▲──────┘  └───▲──────┬───┘  └────▲────────┘
                        │             │      │ invoke    │
   SUPPORTING   ┌───────┴─────────────┴──────▼───────────┴───────┐
                │ C4 PROVIDER INTEGRATION (pluggable, ADR-007)    │  feeds
                └─────────────────────────────────────────────────┘
                ┌─────────────────────────────────────────────────┐
                │ C5 ENTERPRISE CONTEXT (org, capabilities, juris) │  dimensions
                └─────────────────────────────────────────────────┘
   GENERIC      ┌────────────────────────┐ ┌────────────────────┐
                │ C8 TRUST & ACCESS      │ │ C9 AUDIT & EVIDENCE │  cross-cutting
                └────────────────────────┘ └────────────────────┘
```

A deliberate dissolution (proposed **ADR-010**): "AI Activity Intelligence" from the original brief is **not a bounded context**. It dissolves into C4 (privacy-enforced telemetry consumption) and C3 (usage/adoption analytics). A standalone activity-intelligence domain would recreate the collector-product gravity that ADR-007 removed.

## 2. Bounded contexts and capabilities

### C1 — AI Asset Ledger (CORE)
**Purpose:** the authoritative, federated record of every AI asset. **Business owner:** Enterprise AI CoE.

| ID | Capability | Class | Why | Owner | Phase |
|---|---|---|---|---|---|
| C1.1 | Asset registration & record-keeping (owner, purpose, lifecycle status, risk profile per asset) | Build | The system-of-record itself; no vendor offers a cross-surface, business-contextualised ledger | AI CoE | V1 |
| C1.2 | Asset taxonomy & classification scheme (asset types, tiers, tags) | Build | Cross-vendor taxonomy is our vocabulary; native taxonomies are per-surface | AI CoE | V1 |
| C1.3 | Inventory federation & reconciliation (dedupe/match the same asset across Agent 365, Entra Agent ID, PPAC, Foundry, Purview surfaces) | Build (on consumed feeds) | Feeds are consumed (C4); the *identity resolution across surfaces* is differentiating work nobody ships | AI CoE | V1 |
| C1.4 | Ownership & accountability records | Build (consuming Entra Agent ID owner/sponsor data as input [Confirmed surface]) | Native ownership is per-surface and technical; we hold *business* accountability incl. delegates, successors, review dates | AI CoE | V1 |
| C1.5 | Dependency & relationship mapping (asset → knowledge sources, connectors, models, data assets *as dependencies*) | Build (consume native metadata/lineage) | We never construct lineage (PL-007); we link consumed lineage to registered assets | AI CoE | V2 |
| C1.6 | Discovery coverage map (what the ledger can and cannot see, per surface) | Build | Unique honesty feature; sustains trust (Stage 1 §7.2, Challenge §8.5) | AI CoE | V1 |
| C1.7 | Unmanaged-asset triage (discovered-but-unregistered queue feeding governance intake) | Build | The bridge from discovery to governance; drives registration coverage | AI Governance Team | V1 |

### C2 — Governance Orchestration (CORE)
**Purpose:** operational governance as workflows with audit-grade evidence; enforcement delegated (ADR-002). **Business owner:** AI Governance Team.

| ID | Capability | Class | Why | Owner | Phase |
|---|---|---|---|---|---|
| C2.1 | Intake & registration workflow | Build | Organisational process layer; vendors ship controls, not our operating model | AI Governance Team | V1 |
| C2.2 | Risk profiling & assessment orchestration | Build (orchestrating external assessment content where it exists) | Risk *methodology and workflow* are ours; we do not perform bias audits or model evaluations (Stage 1 §5) — we orchestrate and record them | Risk (methodology) / AI Governance (operation) | V1 |
| C2.3 | Approval & gating workflows (configurable per asset tier/jurisdiction) | Build | Same rationale as C2.1; configurability is an ADR-001 requirement | AI Governance Team | V1 |
| C2.4 | Governance policy & standards catalogue (org policies mapped to the native controls that implement them) | Build (content) / Consume (enforcement) | The mapping "our policy X is enforced by native control Y" is the orchestration glue; enforcement itself is native | AI Governance Team | V1 |
| C2.5 | Lifecycle gates & transitions (pilot→production→review→retirement) | Build | Lifecycle *status* lives in C1; transition *rules and gates* live here | AI Governance Team | V1 |
| C2.6 | Attestations & periodic reviews (owner recertification, risk re-assessment) | Build | Recurring governance ritual; produces audit evidence | AI Governance Team | V2 |
| C2.7 | Exceptions & waivers (time-boxed, approved, auditable) | Build | Governance without an exception path gets bypassed (Challenge §8) | AI Governance Team | V2 |
| C2.8 | Native control invocation (request block/unblock, access restriction via native surfaces [Confirmed surfaces: CCS agent blocking, Entra controls; API depth requires validation]) | Orchestrate | ADR-002: we trigger, never replicate, enforcement | Security (control owners) / AI Governance (requesters) | V2 |
| C2.9 | Regulatory compliance mapping (EU AI Act readiness views per asset) | Consume/Partner (content) + Build (linkage) | Framework content is a commodity (Credo/Holistic lane); linking obligations to *our* ledger records is the value | Risk / Privacy | Later |

### C3 — Cost & Value Intelligence (CORE)
**Purpose:** what AI costs, what it returns, and what to do about it. **Business owner:** Finance (methodology) + AI CoE (operation) — joint ownership requires confirmation (Question 2).

| ID | Capability | Class | Why | Owner | Phase |
|---|---|---|---|---|---|
| C3.1 | Cost data acquisition (licence, consumption billing, cloud AI spend [Confirmed surfaces: M365 Cost Management/Copilot Credits, Azure billing; depth requires validation]) | Consume | Meters exist natively per vendor; rebuilding metering is impossible and pointless | Finance | V1 |
| C3.2 | Cross-vendor cost allocation & attribution (BU/cost-centre attribution, blending licence + consumption + cloud) | Build | The blend across vendors and contract types is the differentiator; no vendor blends beyond its own meter | Finance | V1 |
| C3.3 | Licence & consumption utilisation analytics (assignment vs active use) | Build (on consumed telemetry) | Feeds rationalisation; vendors report usage, not *under*-use economics | Finance / AI CoE | V1 |
| C3.4 | Portfolio economics & rationalisation (zombie/orphaned assets, under-used licences, consumption anomalies, reallocation recommendations) | Build | The independence moat: recommendations no vendor will make about itself (ADR-006/008) | AI CoE / Finance | V1 |
| C3.5 | Value measurement methodology & confidence labelling (Measured/Estimated/Self-reported/Inferred/Unknown) | Build | Methodology + honesty is product IP (Stage 1 §8.7) | Finance (methodology owner) | V1 (methodology + labelling), V2 (full ROI models) |
| C3.6 | ROI & business-case tracking (declared expected value at registration vs realised) | Build | Closes the loop registration→outcome; unique because it joins ledger + value | Finance / BU Leaders | V2 |
| C3.7 | Usage & adoption analytics (privacy-level-constrained views by org/BU/asset) | Build (on consumed telemetry) | Absorbed from dissolved "Activity Intelligence" (ADR-010); serves value + dormancy detection | AI CoE | V1 |
| C3.8 | Cross-customer benchmarking | Build | PL-003; only meaningful multi-tenant | Product (commercial) | Later |

### C4 — Provider Integration (SUPPORTING)
**Purpose:** the pluggable provider layer (ADR-007) — every external signal enters here, privacy-filtered at the door. **Business owner:** Platform Administrators (operation); Privacy (filter rules).

| ID | Capability | Class | Why | Owner | Phase |
|---|---|---|---|---|---|
| C4.1 | Inventory providers (Agent 365 registry, Entra Agent ID, PPAC Inventory, Foundry, Purview surfaces [Confirmed surfaces; API depth requires validation — next Microsoft stage]) | Consume | ADR-004/007; native registries are the source of truth for technical existence | Platform Admin | V1 |
| C4.2 | Telemetry providers (Copilot usage reports via Graph [Confirmed GA], Purview DSPM for AI signals, Agent 365 monitoring [Confirmed surfaces; depth requires validation]) | Consume | ADR-007: native-first, no custom collectors in V1 | Platform Admin | V1 |
| C4.3 | Cost providers (Copilot Credits/Cost Management, Azure billing exports [Confirmed surfaces]) | Consume | Feeds C3.1 | Platform Admin | V1 |
| C4.4 | Privacy-level enforcement at ingestion (L1–L4 filters applied before storage; jurisdiction-scoped) | Build | ADR-003 made technical: levels enforced at the door, not by dashboard discipline | Privacy (rules) / Platform Admin (operation) | V1 |
| C4.5 | Provider contract & extensibility framework (add providers — incl. third-party or future custom collectors — without redesign) | Build | The ADR-007 architectural promise; conceptual contract defined in architecture stage | Product/Platform | V1 (contract), Later (third-party providers) |
| C4.6 | Control adapters (outbound: invoke native enforcement on behalf of C2.8) | Orchestrate | Same pluggable pattern, write direction | Security / Platform Admin | V2 |
| C4.7 | HRIS provider (org units, cost centres, person↔org mapping — feeds C5.1) *(added v1.2, Revision Package v1.0)* | Consume | BU attribution (BR-05) depends on org/cost-centre data; manual-import provider is the sanctioned fallback at onboarding | EA / Finance | V1 |
| C4.8 | ERP/procurement provider (contracts, commitments, PO/GL reconciliation — feeds E21 and price book) *(added v1.2)* | Consume | Realisable-savings classification and Financially-validated promotion require contract and GL data | Finance / Procurement | V1.5 |

### C5 — Enterprise Context (SUPPORTING)
**Purpose:** the organisational reference model that makes intelligence attributable. **Business owner:** Enterprise Architecture / AI CoE.

| ID | Capability | Class | Why | Owner | Phase |
|---|---|---|---|---|---|
| C5.1 | Organisational reference model (BUs, cost centres, geography/jurisdiction) | Consume where available (directory/HR sources) + Build (minimal reference model) | Attribution dimension for everything; we hold the minimum, not an HR system | EA / Finance | V1 |
| C5.2 | Business capability map (assets tagged to business capabilities) | Build (content owned by EA) — V1 as simple tagging; full map Later | Differentiator #4, but building an EA tool is creep; if the org already has a capability map (LeanIX etc.) we consume it — Question 3 | EA | V1 (tags) / Later (full map) |
| C5.3 | Business process references (asset links to a *named* process — nothing more, per PL-006) | Build (minimal) | Deliberate anti-creep boundary from Stage 1 §5 | BU Leaders | V2 |
| C5.4 | Jurisdiction & regulatory context registry (which regimes apply where; drives C4.4 scoping and C2.9 later) | Build | Multinational requirement (Stage 1); no native equivalent | Privacy / Risk | V1 (minimal) |

### C6 — *(intentionally vacant)*
Reserved historical marker: "AI Activity Intelligence" dissolved by proposed ADR-010 into C3.7 and C4.2/C4.4. Recorded so the original brief's major capability area has an explicit disposition rather than a silent disappearance.

### C7 — Experience & Insight (SUPPORTING)
**Purpose:** persona experiences and reporting — *views over the three cores, never a fourth core*. **Business owner:** AI CoE (product owner).

| ID | Capability | Class | Why | Owner | Phase |
|---|---|---|---|---|---|
| C7.1 | Governance operator workspace (CoE/governance team daily surface) | Build | V1 persona trio (Stage 1 §8.10) | AI CoE | V1 |
| C7.2 | Executive dashboards (adoption, cost, governance posture, value — per Stage 1 success criteria) | Build | The recurring executive ritual that prevents shelfware (Challenge §8.3) | AI CoE / CIO | V1 |
| C7.3 | Agent owner view (my assets, obligations, approvals, costs) | Build | Gives makers something back (Challenge §8.4) | AI CoE | V1 |
| C7.4 | Notifications & subscriptions (governance events, cost alerts) | Orchestrate | Deliver through native channels (Teams/email); we compose, we don't build a comms platform | AI CoE | V1 |
| C7.5 | Data export & BI integration (curated read models to the org's BI tooling) | Build (minimal) | We are not a BI platform (ADR-005); export beats replicating Power BI | Platform Admin | V2 |
| C7.6 | Additional persona experiences (Finance, Risk, Privacy, Audit, developer views) | Build | PL-010: report-recipient views first, workspaces only when proven | per function | Later |

### C8 — Trust & Access (GENERIC)
**Purpose:** identity, authorisation, tenancy, configuration. **Business owner:** Security + Platform Administrators.

| ID | Capability | Class | Why | Owner | Phase |
|---|---|---|---|---|---|
| C8.1 | Identity & authentication | Consume | Entra is the identity platform (ADR-004); building auth would be malpractice | Security | V1 |
| C8.2 | Authorisation & delegated administration (role model reflecting the delegated-ownership operating model from Stage 1 §6) | Build | Our persona/delegation semantics are product-specific; enforcement uses consumed identity | Security / AI CoE | V1 |
| C8.3 | Tenancy & configuration management (multi-tenant config, configurable governance, telemetry levels per jurisdiction) | Build | ADR-001 foundation; configuration *is* the product for multinationals | Platform Admin | V1 |
| C8.4 | Privacy configuration & transparency (level settings, employee-facing transparency about what is collected/shown) | Build | ADR-003 transparency commitment; differentiator #8 | Privacy | V1 |

### C9 — Audit & Evidence (GENERIC)
**Purpose:** the platform's own accountability. **Business owner:** operated by AI Governance Team; designed for Internal Audit as consumer.

| ID | Capability | Class | Why | Owner | Phase |
|---|---|---|---|---|---|
| C9.1 | Immutable audit trail (governance decisions, ledger changes, configuration/telemetry-level changes) | Build | Evidence-grade audit is principle §8.12; spans all contexts, hence generic | AI Governance Team | V1 |
| C9.2 | Evidence packaging (export audit-ready evidence sets for internal audit/regulators) | Build | V1 minimal export; richer packs with C2.9 Later | Internal Audit (consumer) | V2 |

## 3. Relationships between bounded contexts

| Upstream → Downstream | Relationship | Nature |
|---|---|---|
| C4 → C1 | Supplier: inventory feeds | C1 reconciles; C4 conforms to external providers and shields the cores from their shapes (anti-corruption at the boundary) |
| C4 → C3 | Supplier: telemetry + cost feeds (privacy-filtered by C4.4) | C3 never touches raw provider data |
| C1 → C2 | Customer/supplier: assets are the subjects of governance | C2 writes lifecycle status back to C1 (single writer per attribute: C1 owns records, C2 owns transitions) |
| C1 → C3 | Supplier: the asset dimension for all economics | Value/cost claims only attach to ledger assets — the traceability principle |
| C2 → C4 | C2.8 requests enforcement through C4.6 adapters | Write direction; audited via C9 |
| C5 → C1/C2/C3 | Shared reference dimensions (org, capability, jurisdiction) | Conformist consumers of C5's model |
| C1/C2/C3 → C7 | Read models for experiences | C7 is strictly downstream; no business logic in views |
| C8 → all | Access decisions, tenancy scoping, privacy configuration | Cross-cutting policy |
| all → C9 | Event stream of auditable actions | C9 is append-only, downstream of everything |

Two structural rules: **C4 is the only door for external signals** (in and out), and **C7 is the only door for human-facing views**. Everything between those doors is the three cores plus reference data.

## 4. Phasing (v1.1 — per ADR-011, approved 2026-07-15)

*(v1.0's "26 capabilities" was a miscount of a 34-item list; superseded by the minimized phasing below — rationale in [challenge-02-v1-minimization.md](challenge-02-v1-minimization.md).)*

- **V1 — "Ledger + Economics" (25 capabilities, ~14 Build):** C1.1–C1.4, C1.6; C3.1–C3.4, C3.5 (labelling framework), C3.7; C4.1–C4.5; C5.1, C5.4 (minimal); C7.1 (scoped), C7.2; C8.1, C8.2 (simple roles), C8.3, C8.4; C9.1. Risk profile + lifecycle status are managed *fields* on C1.1 in V1.
- **V1.5 — governance engine (hard covenant: within two quarters of V1, OI-013/R-14):** C2.1, C2.2, C2.3, C2.5; C1.7; C3.5 (full value methodology); C5.2 (tags); C7.3, C7.4; C8.2 (delegated administration).
- **V2:** C1.5, C2.4, C2.6, C2.7, C2.8+C4.6, C3.6, C5.3, C7.5, C9.2.
- **Later:** C2.9, C3.8, C4.5 third-party/custom providers, C7.6 (as parameterised views only).
- **Removed permanently (Challenge 02 §4):** full business-capability-map construction (consume-or-tags only); bespoke persona workspaces beyond the V1 trio.

V1 shape check against ADR-008: V1 sells the independent numbers (federated ledger with money attached); V1.5 delivers the governance the numbers justify. Control *invocation* (C2.8) waits for V2: people-processes first, machine-actions later, reducing early blast-radius and consent scope.

## 5. Capabilities removed entirely (beyond Stage 1 cuts)

| Removed | Reason |
|---|---|
| "AI Activity Intelligence" as a standalone domain | Dissolved (proposed ADR-010) into C4 consumption + C3.7 analytics; a standalone domain would regrow collector ambitions |
| In-product BI / report builder | ADR-005 (not a Power BI replacement); C7.5 export instead |
| Security monitoring & incident response for AI | Defender/SIEM/Zenity lane; C4 may later *consume* their findings as a provider |
| Responsible-AI evaluation execution (bias audits, red-teaming, model evals) | Stage 1 §5; C2.2 orchestrates and records, never performs |
| Prompt/knowledge content management, process mining, lineage construction | Already cut in Stage 1 (PL-006..008); confirmed absent from the model |
| AI Fluency / adoption coaching | PL-001; adoption *analytics* (C3.7) yes, coaching product no |

## 6. Challenge to this model

1. **Is C5 a real context or three tables?** Honest answer: in V1 it is close to reference data. It earns context status only because jurisdiction logic (C5.4) drives privacy enforcement and, later, compliance mapping — behaviour, not just data. If Stage 3+ shows no behaviour, demote it to a shared kernel of C1/C3. Kept, flagged.
2. **Joint Finance ownership of C3 is organisationally fragile.** If Finance won't own the value methodology, C3.5 degrades into CoE self-grading — exactly the inflated-ROI pattern we position against. This is an organisational precondition, not a design detail (Question 2).
3. **26 V1 capabilities is still a lot.** The compression lever if needed: push C2.4 (policy catalogue) and C5.2 (capability tags) to V2 — the ledger, intake, cost attribution, and dashboards are the incompressible minimum. Flagged, not recommended yet: both cut capabilities are cheap conceptually and expensive to retrofit.
4. **Reconciliation (C1.3) is the hidden hard problem.** Cross-surface identity resolution looks like plumbing but determines ledger trustworthiness; if the Microsoft stage shows surfaces lack stable correlatable identifiers, C1.3 becomes semi-manual triage and V1 scope must say so honestly. Elevated to risk R-12.
5. **Where is "AI asset" defined?** The model presumes a shared definition of what counts as an AI asset (agent vs flow vs model vs prompt). That definition is the taxonomy (C1.2) and it gates everything — it must be an early Stage 3 deliverable, or scope disputes will relitigate every stage.

---

## Stage-end review

### Summary
Eight bounded contexts (three core, three supporting, two generic), 38 named capabilities, each classified consume/orchestrate/build with rationale, ownership, and phasing; 26 in V1. "AI Activity Intelligence" is dissolved as a domain (proposed ADR-010). C4 is the single door in, C7 the single door out; the three cores sit between.

### Assumptions
- A workable business-capability tagging source exists or can be minimally created (Question 3).
- Finance will accept methodology ownership of C3.5 (Question 2).
- The delegated-ownership operating model (Stage 1 §6) will be organisationally real, giving C8.2 semantics to implement.
- Native surfaces provide sufficient identifiers for C1.3 reconciliation [Unknown — next Microsoft stage].

### Confirmed facts
- Consumption *surfaces* named in C4 exist as products [Confirmed, July 2026 research]: Agent 365 registry, Entra Agent ID, PPAC Inventory + API, Graph Copilot usage reports (GA), Purview DSPM for AI, M365 Cost Management/Copilot Credits, Foundry Control Plane.
- No claims are made in this stage about specific API fields, permissions, or completeness — all flagged "requires validation".

### Unknowns
- API depth/identifier stability per surface (gates C1.3, C3.1, C4.1–C4.3) — next Microsoft stage.
- Whether Quadient maintains a business capability map to consume (Question 3).
- Which third-party (non-Microsoft) providers matter first for C4.5 (deferred, DD-005).

### Risks
- **R-12 (new):** cross-surface asset reconciliation may lack stable identifiers, degrading the ledger's authority — the single largest technical risk to the product concept; validate early.
- **R-13 (new):** joint Finance/CoE ownership of value methodology may not materialise organisationally, undermining differentiator #2.
- Existing R-06 (API coverage) now concentrates on C4; R-09 (scope) mitigated by the 26-capability V1 but see Challenge point 3.

### Alternative approaches considered
- **Nine contexts with standalone Usage & Adoption Intelligence:** rejected — recreates "Activity Intelligence" gravity; analytics belong in C3, filters in C4.
- **Folding C9 into C2:** rejected — audit must also evidence configuration and telemetry-level changes (C8/C4 actions), so it spans contexts.
- **Organising contexts around personas (governance workspace, finance workspace…):** rejected — persona views are projections (C7); persona-shaped domains duplicate logic.

### Questions for Arun (needed before Stage 3)
1. **Approve ADR-009** (eight-context model as above) and **ADR-010** (dissolve Activity Intelligence into C3/C4)?
2. **Finance ownership:** can Finance realistically own the value-measurement methodology (C3.5) at Quadient? If not, who is the credible internal owner of "honest numbers"?
3. **Business capability map:** does Quadient maintain one (e.g., in an EA tool)? Consume it, or start with a minimal tag set we define?
4. **V1 cut line:** accept the 26-capability V1, or apply the compression lever (push C2.4 policy catalogue and C5.2 capability tags to V2)?
5. **Stage order:** I recommend the next stage be **Microsoft integration & API validation** (originally Stage 4) rather than personas/UX — it is the go/no-go for C1.3/C4 and Stage 1 Recommendation 2 said to schedule it early. Confirm reorder (Stage 3 = Microsoft validation, Stage 4 = personas/journeys/UX)?
6. **Control invocation timing:** C2.8/C4.6 in V2 (my recommendation) — or does the internal governance mandate need V1 blocking actions?

### Recommendations
1. Approve the model with ADR-009/010; treat the two structural rules (C4 only door in, C7 only door out) as architectural invariants for all later stages.
2. Commission the **AI asset definition/taxonomy (C1.2)** as the first deliverable of Stage 3 regardless of stage order — everything downstream disputes without it.
3. Validate **C1.3 reconciliation feasibility** (identifier stability across Agent 365/Entra/PPAC/Foundry) as the first question of the Microsoft stage — it is R-12 and gates the ledger's credibility.
4. Secure the **Finance co-ownership conversation** now (organisational, long lead time, gates differentiator #2).
