# Challenge 02 — V1 Minimization

| | |
|---|---|
| **Version** | 1.0 |
| **Date** | 2026-07-15 |
| **Status** | For decision — proposes ADR-011 |
| **Related** | [stage-02-capability-model.md](stage-02-capability-model.md), [decision-log.md](decision-log.md) (ADR-006/008 drive the cut), [stage-01-product-vision.md](stage-01-product-vision.md) §7 (success criteria) |

**Correction:** Stage 2 §4 stated "26 V1 capabilities"; the listed IDs actually number **34**. Miscount acknowledged and corrected here. The minimization works from 34.

Mandate: find the minimum set that still delivers a compelling commercial product. Guiding test (ADR-008): what does the first Fortune 500 customer *pay* for on day one? Answer: *"Connect your Microsoft tenant. Within days: your complete federated AI inventory with owners, your full AI spend attributed by business unit, your zombie and waste report, and an executive dashboard you can take to the board — privacy-filtered for every jurisdiction you operate in."* Everything not required for that sentence moves out of V1.

Capability *count* is a poor proxy for effort — the analysis below also weighs build heaviness. The single heaviest V1 item in Stage 2 was the **governance workflow engine (C2)**; removing C2 from V1 shrinks the build far more than the count suggests.

---

## 1. Essential V1 — 25 capabilities, of which only ~14 are Build

| Context | Kept in V1 | Why essential |
|---|---|---|
| C4 Provider Integration | C4.1 inventory, C4.2 telemetry, C4.3 cost, C4.4 privacy enforcement, C4.5 provider contract | No feeds, no product. C4.2 stays: zombie/utilisation detection (the wedge demo) requires usage signals. C4.4/C4.5 are architecturally unretrofittable — day-one or never. |
| C1 Asset Ledger | C1.1 records, C1.2 taxonomy, C1.3 reconciliation, C1.4 ownership, C1.6 coverage map | The ledger is what the economics attach to. C1.3 scope-limited in V1: automated matching where identifiers permit + a manual merge queue (pending R-12 validation). C1.6 is cheap (connector status reporting) and trust-critical. **Risk profile and lifecycle status remain V1 *fields* on C1.1** — captured manually; the workflows that manage them come later. |
| C3 Cost & Value | C3.1 cost acquisition, C3.2 allocation, C3.3 utilisation, C3.4 portfolio economics, C3.5 confidence-labelling framework only, C3.7 usage analytics | The commercial wedge itself. C3.5 splits: the labelling *framework* is cheap and brand-defining (V1); the agreed value *methodology* needs the Finance conversation (V1.5). |
| C5 Enterprise Context | C5.1 org reference model, C5.4 jurisdiction registry (minimal) | C5.1 is the attribution dimension for the CFO demo. C5.4 minimal is required because C4.4 jurisdiction scoping depends on it — a multinational cannot deploy without it. |
| C7 Experience | C7.1 operator workspace (scoped to ledger ops + config + merge queue), C7.2 executive dashboard | The dashboard is the demo; the workspace is whoever runs the product. Nothing else. |
| C8 Trust & Access | C8.1 identity (consume), C8.2 authorisation (simple roles: viewer/operator/admin — delegated administration deferred), C8.3 tenancy & config, C8.4 privacy config & transparency | SaaS + EU deployment minimums. C8.2 simplification is deliberate: rich delegation semantics wait for the personas stage. |
| C9 Audit | C9.1 audit trail | Enterprise sale requirement, and append-only history is near-impossible to retrofit honestly. |

## 2. Move to V1.5 (fast-follow, target within two quarters of V1)

| Capability | Why it can wait — and why not longer |
|---|---|
| C2.1 intake, C2.2 assessment orchestration, C2.3 approvals, C2.5 lifecycle gates | The entire workflow engine. Customers buy the numbers first (ADR-008); governance is what the numbers justify. **Cannot slip past V1.5**: Stage 1's 12-month success criteria (in-platform governance, zero spreadsheets) and the internal Quadient mandate depend on it — see risk below. |
| C1.7 unmanaged-asset triage | The bridge from discovery into governance intake; meaningless until C2.1 exists. The V1 zombie report already *surfaces* unmanaged assets. |
| C3.5 full value methodology | Gated on the Finance ownership conversation (OI-010), which has its own timeline. |
| C5.2 business-capability tags | Enriches portfolio views; BU attribution (C5.1) carries the V1 demo without it. |
| C7.3 agent owner view | The maker give-back matters for governance adoption (Challenge 01 §8.4) — i.e., it matters when C2 lands, not before. |
| C7.4 notifications | Cost-anomaly alerts are compelling but not gating; disciplined cut. |
| C8.2 delegated administration (rich model) | Needs the personas/UX stage to be designed properly; simple roles suffice in V1. |

## 3. Move to V2 (unchanged or newly demoted)

C1.5 dependency mapping; C2.4 policy catalogue (demoted from V1 — valuable, but its enforcement-mapping value arrives with C2.8 control invocation, which is V2); C2.6 attestations; C2.7 waivers; C2.8 + C4.6 control invocation; C3.6 ROI/business-case tracking; C5.3 process references; C7.5 BI export; C9.2 evidence packaging.

## 4. Remove completely

| Removed | Rationale |
|---|---|
| C5.2 *full business capability map construction* (the "Later" tier of it) | Building a capability-mapping tool is EA-suite creep in permanent disguise. Final position: **consume** an existing EA map if the customer has one; otherwise tags only, forever. The "build full map Later" option is deleted. |
| C7.6 *bespoke persona workspaces* beyond the trio | Report-recipient *views* (parameterised dashboards) replace dedicated workspaces permanently. A workspace per function is headcount-shaped scope creep; demand must prove itself through view usage first. C7.6 narrows to "additional persona views." |

Nothing else newly removable: Stage 1/2 already stripped the removable mass (collectors, process mining, lineage construction, BI builder, RAI execution, security monitoring).

## 5. The smallest V1 a Fortune 500 would pay for

The §1 set is that minimum, and the reasoning cuts both ways:

- **Any smaller stops being payable.** Drop C4.2/C3.3/C3.4 and it's an inventory list — Agent 365 does that natively; nobody pays. Drop C3.2/C5.1 and there's no CFO story. Drop C4.4/C5.4/C8.4 and it cannot be deployed in the EU. Drop C1.3 and the ledger double-counts assets — credibility gone. Drop C9.1/C8.3 and it fails enterprise procurement.
- **Any larger delays the payable thing.** The workflow engine (C2) roughly doubles V1 build weight while adding nothing to the first cheque (ADR-008).
- Net: **25 capabilities, ~14 Build / 8 Consume-or-Orchestrate / 3 minimal-Build reference models.** V1 product in one line: *the federated AI ledger with money attached.*

**The V1.5 covenant (critical):** this cut only works if V1.5 (the governance engine) ships within two quarters of V1. If V1.5 slips beyond the 12-month window, Stage 1's frozen success criteria (in-platform governance, zero spreadsheet governance) and the internal mandate fail. The cut is a sequencing bet, not a descoping of governance. Logged as **OI-013**.

## Proposed ADR-011 — Minimal V1: "Ledger + Economics"
V1 = the 25 capabilities in §1 (ledger, provider integration, cost/portfolio intelligence, two experiences, trust/audit minimums). Governance orchestration (C2) is V1.5 with a hard two-quarter covenant. Removals per §4. Stage 2 to be revised to v1.1 on approval.

---

### Stage-end review (challenge scope)

**Summary:** 34 → 25 V1 capabilities, but the real reduction is build weight: the workflow engine leaves V1. V1 sells the independent numbers; V1.5 delivers the governance those numbers justify.

**Assumptions:** V1.5 within two quarters is achievable (unvalidated until an engineering estimate exists — planning-stage assumption); risk-profile-as-field satisfies interim internal governance recording.

**Confirmed facts:** none new (no research in this challenge).

**Unknowns:** C1.3 feasibility still gates everything (R-12, Stage 3); Finance ownership timing (OI-010).

**Risks:** **OI-013 / R-14 (new):** V1.5 slip breaks the frozen 12-month success criteria and the internal mandate — the covenant must be tracked as a first-class roadmap constraint. **R-15 (new):** V1 without C2 means early adopters govern via fields + process outside the platform; if they build habits around that, V1.5 adoption friction rises.

**Alternatives considered:** keeping C2.1 (intake only) in V1 as a thin form — rejected: a thin intake without gates/approvals is a survey, and surveys are shelfware (Challenge 01 §8.2); governance enters whole in V1.5 or not at all. Keeping C7.4 notifications in V1 for cost alerts — genuinely marginal call, cut for discipline; cheap to pull forward if V1 timeline allows.

**Questions for Arun:**
1. Approve ADR-011 (minimal V1, V1.5 covenant, §4 removals)?
2. Accept risk-profile-as-manual-field for V1 internal governance recording (BR-03 partially satisfied until V1.5)?
3. Any V1.5 item you'd force back into V1 for the *internal* Quadient deployment, accepting the delay to first sellable release?

**Recommendations:** Approve ADR-011; instruct that the Stage 3 validation matrix prioritises the 25 V1 capabilities' surfaces first (V1.5/V2 surfaces validated second-pass); revisit C7.4 at V1 planning if capacity allows.
