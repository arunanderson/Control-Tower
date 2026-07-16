# Stage 10 — Cost Intelligence, Business Value, ROI Methodology & Portfolio Economics

| | |
|---|---|
| **Version** | 1.1 |
| **Date** | 2026-07-15 |
| **Status** | **APPROVED — FROZEN** (ADR-024/025). v1.1: realisable-savings classification and price-book confidence added per Revision Package v1.0 (PD-006 revision). |
| **Related** | [stage-03-microsoft-validation.md](stage-03-microsoft-validation.md) (what is measurable), [stage-05-conceptual-data-model.md](stage-05-conceptual-data-model.md) (E11–E14), [stage-06-experience-architecture.md](stage-06-experience-architecture.md) (J6–J9), [decision-log.md](decision-log.md) (ADR-013/016/017) |

**The stakes, stated plainly:** this is the defining capability. The market is saturated with inflated AI ROI claims; the product's entire commercial thesis (ADR-006/008) is being the one source a CFO believes. Accordingly, this methodology optimises for *defensibility over impressiveness*. Where that means smaller numbers, smaller numbers win.

**Governing rule (Arun's mandate):** no ROI claim may be presented without an evidence source and a confidence level. Operationalised below as: the rendering layer cannot display an economic figure whose read model lacks (evidenceRef, confidenceLabel, as-of basis) — structurally impossible, not editorially discouraged.

---

## 1. Evidence hierarchy & the six-label confidence taxonomy (v2)

| Label | Definition | Example | Promotion path |
|---|---|---|---|
| **Measured** | Machine-observed from an authoritative meter, ingested via C4 | Azure cost line items; licence assignment counts; token metrics; last-activity dates | → Financially validated via reconciliation |
| **Financially validated** | Reconciled against the finance system of record (invoice/GL/PO match) by the Finance workflow | Azure invoice matched to Cost API totals; realised licence savings confirmed in GL | Terminal (highest) |
| **Estimated** | Derived from Measured inputs through a documented, versioned model with stated assumptions | Allocated shared costs; annualised run-rate; € conversion of validated hours | → Financially validated when actuals land |
| **Self-reported** | Human-declared, attributable to a named declarer | Value declarations; Copilot Credits CSV (ADR-013); business-case benefits | → Estimated/Validated via evidence attachment + Finance review |
| **Inferred** | Statistically or heuristically derived from behavioural signals | Time-saved estimates from usage patterns; adoption-productivity correlations | → Self-reported (owner confirms) → upward |
| **Unknown** | Explicitly absent | Unattributed cost remainder; unmeasured benefit categories | Any |

**Composition rules:** (1) A composite figure inherits the **weakest label among its material components** (materiality: any component ≥10% of the figure). (2) Aggregates display their **confidence mix** (e.g., "€2.1M — 78% Measured/Validated, 22% Self-reported"), never a blended single label when mixed. (3) Labels never silently upgrade; promotion is a Finance-workflow event with evidence. (4) **Confidence stays categorical** — no numeric confidence scores (0.87-style precision is false authority; six honest words beat two dishonest decimals). → this is the "confidence scoring" answer: we deliberately don't score, we classify.

## 2. Cost attribution model

**Cost objects:** asset → business unit → business capability (tags, V1.5) → tenant total. **Cost layers:**

| Layer | Source | Label at ingestion | Attribution |
|---|---|---|---|
| Licence costs (Copilot, Agent 365, premium SKUs) | Graph licence APIs [Measured] + price book (contract prices, Finance-maintained) | Measured (quantity) × Financially validated (price) | Direct to user's BU via org model |
| Metered consumption (Azure OpenAI/Foundry, Azure infra) | Azure Cost Management [Measured] | Measured | Direct via resource↔asset resolution; tag strategy |
| Copilot Credits | CSV import (ADR-013) | **Self-reported / Manual Import** | Per user/agent as the CSV provides |
| Shared/platform costs (the platform itself, shared services) | Finance input | Financially validated | Allocation rules (§3) |
| Unattributable remainder | — | **Unknown — first-class, always visible** | **Never spread** |

**The Unattributed principle (challenged and kept):** costs that cannot be attributed with at least Estimated confidence go to a visible "Unattributed" bucket. Peanut-butter spreading of remainders is the single fastest way to lose Finance's trust — a BU leader who finds one wrongly-spread euro discounts the whole platform. Shrinking the Unattributed bucket is a *tracked KPI*, not an embarrassment to hide.

**Full-cost vs marginal views:** both supported, never mixed in one figure; every cost KPI names its basis. (A zombie agent's "savings if retired" is *marginal* — licence + consumption — not its full-cost allocation share; conflating these inflates savings claims, the exact sin we exist to prevent.)

## 3. Allocation, chargeback, showback

- **Allocation rules (E11):** versioned, effective-dated, Finance-owned; drivers limited to four defensible types — direct assignment, headcount, usage share (Measured signals only), explicit tag. Exotic drivers (revenue share, "strategic weighting") rejected: gameable and undefendable.
- **Allocation runs:** projections against org-model-as-of-period (Stage 5); every allocated figure carries rule version + driver provenance.
- **Showback first (recommendation):** V1/V1.5 = showback (visibility without money movement). **Chargeback** = V2, and only as an **export to the customer's ERP/GL** — the platform computes and evidences; it never becomes a billing engine (ADR-005 boundary). Preconditions for chargeback: two consecutive Financially-validated closed periods and Finance sign-off on rules. Chargeback before trust is established converts every allocation debate into a budget war.

## 4. Value attribution model & validation workflow

**Benefit types:** time saved, cost avoided, revenue enabled, risk reduced, quality improved. Each with its measurement honesty profile:

- **Time saved — the trap handled explicitly.** Hours may be Inferred (usage patterns) or Self-reported (owner declaration). The € conversion is *always* Estimated at best, with the utilisation assumption stated (saved hours ≠ captured value unless capacity was actually redeployed or reduced — the classic 30-year-old IT-value fallacy). The platform reports "X hours (Inferred) → €Y (Estimated, at Z% capture assumption)" — never "€Y saved" bare. Financially validated only when Finance confirms realised capture (headcount avoidance, overtime reduction, output increase).
- **Cost avoided / revenue enabled:** Self-reported with mandatory evidence refs; validated against actuals where the GL can show them.
- **Risk reduced / quality:** recorded and narrated, *not monetised by default* — fabricated risk-€ conversions are where credibility goes to die. Monetisation possible only with a customer-approved, documented method (Estimated ceiling).

**Value validation workflow (Finance-owned, V1.5 with C3.5-full):**
Declared (Self-reported) → evidence attached → materiality triage (small claims: sampled; large claims: mandatory review) → Finance review → **Financially validated** or returned with reasons → periodic revalidation (declared value decays: unrevalidated claims older than the policy window demote to Unknown-flagged, visibly). Anti-gaming: claims attach only to governed assets with owners; the declarer is named on the figure; **person-hour ceiling checks** across claims (the same person's hours cannot be saved twice by two initiatives — dedup at the org level); Finance sampling audits.

## 5. ROI methodology

- **Asset/initiative ROI:** (validated + estimated value − full cost) / full cost, presented as a **range with confidence mix**, trailing-12-month basis by default.
- **Presentation rules (hard):** no single-point ROI when >25% of the value side is Self-reported/Inferred — show the range and the mix; portfolio ROI aggregates only across like-confidence tiers, with a "validated-only ROI" always shown alongside any broader figure; the platform's own cost is included in portfolio totals (a governance platform that exempts itself from its own economics is a joke customers will make for us).
- **The honesty KPI:** *Validated-to-Declared ratio* — what fraction of claimed value survived Finance validation. This number is the product's character reference; it belongs on the executive page.

## 6. Forecasting & scenario modelling (phased)

- **V1.5 — run-rate forecasting:** consumption and licence-cost trajectories from Measured trends, presented as bands (Estimated), with explicit assumption sets (price book version, growth basis). No point forecasts.
- **V2 — scenario modelling:** licence tier changes, agent scaling, price changes, rationalisation scenarios — each scenario = assumptions + Measured baseline + Estimated deltas; scenarios are saved, versioned, comparable. Never presented as predictions; presented as arithmetic on stated assumptions.
- **Rejected:** ML-based cost prediction in any phase before ample tenant history exists — dressing extrapolation as intelligence violates the honesty position.

## 7. Portfolio optimisation, zombie detection, licence optimisation

- **Zombie/waste tiers (multi-signal, per Stage 3 evidence — no single source suffices):**
  - *Dormant:* no usage signals across all available sources (activity dates, audit interaction counts, agent sign-ins) for the policy window (default 90 days), with per-source coverage stated — dormancy confidence depends on telemetry coverage, and says so.
  - *Orphaned:* Ownerless (E7 gap) — governance debt and economic risk simultaneously.
  - *Underused licence:* assigned, no activity in window (Measured) — the cleanest savings class.
  - *Over-provisioned:* consumption patterns far below plan/capacity (Estimated).
- **Recommendation outputs:** every recommendation carries € impact (marginal basis, §2), confidence label, evidence drill-down, and a *seasonality/owner-consultation guard* (owner notified before any recommendation escalates — V1.5; the "seasonal, not zombie" override exists, Stage 5 §7). **The platform never auto-actions** (ADR-002); it recommends, evidences, and records the decision.
- **Realisable-savings classification** *(added v1.1, Revision Package v1.0)*: every savings figure is classified against E21 ContractCommitment data as **Immediately realisable** (consumption stops) / **Realisable at true-up (date)** / **Realisable at renewal (date)** / **Contractually locked**. The waste KPI reports these separately; the executive default leads with realisable amounts (ADR-025 defensibility). Where no contract data exists, savings display as "realisability Unknown — contract data missing" — never as implicitly bankable. **Price-book confidence** *(added v1.1)*: contract-sourced prices = Financially validated; list prices = Estimated; absent = Unknown — flowing into weakest-link composition, so a saving computed from a list price says so.
- **Recovered-waste tracking:** recommendations → decisions → realised savings (Financially validated when licence counts/GL confirm) — closing the loop is what makes the next recommendation credible.

## 8. Financial governance model

Finance owns: the methodology (this document's rules as configurable policy), the price book, allocation rules, the validation workflow, period close (ADR-016), and methodology change control (versioned; changes apply to future periods only — retroactive methodology changes require a formal restatement with reasons, never a silent recalc). CoE owns operations and data plumbing. A **quarterly methodology review** (Finance + CoE) examines: Unattributed bucket trend, Validated-to-Declared ratio, label-mix drift, rule disputes. *(This makes OI-010 — Finance's co-ownership — an operating commitment, not a hope; it remains the stage's key organisational dependency.)*

## 9. Executive KPIs (challenged individually)

| KPI | Decision it serves | Basis | Survived challenge? |
|---|---|---|---|
| Total AI spend + trend, by BU | Budget steering | Measured/Fin-validated | Yes |
| Cost per active user (by licence class) | Licence negotiation, tier decisions | Measured | Yes |
| Utilisation (assigned vs active) | Reharvesting | Measured | Yes |
| Waste identified / recovered | Rationalisation follow-through | Estimated → Fin-validated | Yes — the wedge KPI |
| **Validated-to-Declared value ratio** | Trust in the value pipeline | Structural | Yes — the honesty KPI |
| Validated value + confidence mix | Investment continuation | Per §4/§5 | Yes |
| Unattributed cost % | Attribution quality | Structural | Yes — kept visible on purpose |
| Governed-estate % + debt trend | Control posture | Ledger | Yes (control quadrant) |
| Coverage % | How much the platform can see | Structural | Yes (trust quadrant) |
| ~~Org-wide productivity uplift %~~ | — | Would be Inferred dressed as Measured | **Killed** — offered only as clearly-labelled Inferred analysis with method disclosure, never as a KPI |
| ~~Headline ROI multiple~~ | — | Single number, mixed confidence | **Killed** as a standalone; exists only with range + mix (§5) |
| ~~AI maturity score~~ | — | Vanity composite, no decision | **Killed** |

## 10. Executive narratives

Generated narrative = the four questions (Stage 6 §7) + *what changed, what drove it, what needs deciding* — with labels inline ("cost rose 12% (Measured), driven by Foundry consumption in BU-X; declared value awaiting validation: €0.4M (Self-reported)"). Narratives never strip labels for readability; the label *is* the readability. Board pack renders the same read models (ADR-019.3). One editorial rule: narratives state at most three drivers and at most three asks — executive attention is the scarcest resource the product touches.

## 11. Challenges applied (summary)

Killed: numeric confidence scores; peanut-butter allocation; single-point ROI; monetised risk-by-default; productivity-uplift and maturity-score KPIs; ML forecasting theatre; chargeback-before-trust; bare "€ saved" from time savings. Kept deliberately uncomfortable: the Unattributed bucket on the executive page; the Validated-to-Declared ratio; the platform's own cost in portfolio totals; dormancy confidence tied to coverage. Each of these will generate stakeholder pressure to soften — that pressure is the product working (same doctrine as ADR-018).

### Proposed ADR-024 — Economics methodology principles
Six-label taxonomy (mandated by Arun; supersedes the five-label set everywhere); weakest-material-link composition with visible confidence mix; categorical-not-numeric confidence; first-class Unattributed bucket, never spread; showback before chargeback, chargeback only as ERP export with two validated closes as precondition; no single-point ROI beyond the 25% soft-label threshold; time-saved monetisation always Estimated with stated capture assumption; risk/quality not monetised by default; methodology changes forward-only with formal restatement otherwise; Validated-to-Declared ratio as a standing executive KPI.

---

## Stage-end review

### Summary
An economics methodology that buys trust with visible discomfort: six evidence-ranked labels with weakest-link composition, an Unattributed bucket no one can hide, time-saved value handled with the capture-assumption honesty the industry skips, showback-before-chargeback, killed vanity KPIs, and a Validated-to-Declared ratio that grades the platform's own value pipeline in front of the board.

### Assumptions
- Finance staffs the validation workflow and quarterly review (OI-010 — the stage's standing organisational dependency; §8 defines the ask concretely).
- The contract price book is obtainable and maintainable (without it, licence costs are list-price Estimated, stated as such).
- Credits CSV cadence (monthly at minimum) is operationally sustainable until the API ships (OI-014 watch).

### Confirmed facts
None new — measurability constraints trace to Stage 3.

### Unknowns
Capture-assumption defaults per benefit type (set with Finance at methodology onboarding); dormancy window defaults per asset class (90-day starting point, tune with data); Gate-2 PoC results (Purview per-agent counts) affect dormancy confidence quality.

### Risks
- **R-30 (new):** stakeholder pressure to soften the uncomfortable numbers (Unattributed, Validated-to-Declared, ranges instead of points) — mitigation: ADR-024 makes them principles, not preferences; the sales narrative sells them as features.
- **R-31 (new):** Finance under-resources validation → value pipeline stalls at Self-reported → executive page shows mostly-unvalidated value → product looks weak *because it's being honest*. Mitigation: materiality triage (§4) keeps the workflow lean; the Validated-to-Declared KPI makes the bottleneck visible as a resourcing fact.
- R-16 residual (credits manual feed) unchanged; labels carry it honestly.

### Alternative approaches considered
Numeric confidence scoring (rejected §1); full activity-based costing (rejected — four defensible drivers beat forensic allocation nobody audits); benefits realisation as a separate module (rejected — validation is a workflow on declarations, not a product); hiding Unattributed inside "shared costs" (rejected with prejudice).

### Questions for Arun
1. **Approve ADR-024** (methodology principles as listed)?
2. **Chargeback preconditions** (two Financially-validated closes + Finance sign-off) — confirm, or does Quadient Finance want chargeback earlier/never?
3. **Default windows:** 90-day dormancy, trailing-12-month ROI basis, quarterly methodology review — acceptable starting defaults?
4. **Stage 11 proposal:** Governance & Lifecycle Workflows (the C2/V1.5 design: intake, approval, risk assessment, attestation, exceptions — the deferred governance engine), followed by Stage 12 = Roadmap, Phasing & Operating Model as the closing stage. Confirm?

### Recommendations
1. Approve; put the Validated-to-Declared ratio in the first executive demo — it is the single clearest expression of why this product exists.
2. Open the Finance co-ownership conversation (OI-010) with §8 as the concrete proposal — it is now the blueprint's most important unstaffed role.
3. Treat every future request to soften §11's uncomfortable numbers as a strategy regression, not a UX improvement (R-30).
