# Revision Package v1.0 — Response to Independent Review 01

| | |
|---|---|
| **Version** | 1.0 |
| **Date** | 2026-07-15 |
| **Status** | Applied under PD-006 (formal revision process). Independent Review 01 accepted by Arun. Architecture NOT reopened — bounded revisions only. This is the final planning revision; the blueprint is complete after it. |

## Change register

| # | Review item | Disposition | Where |
|---|---|---|---|
| 1 | Gate-0 venture decision | New framework document | [gate-0-venture-decision.md](gate-0-venture-decision.md) |
| 2 | Customer discovery | New plan (hypotheses, guides, success/kill criteria) | [customer-discovery-plan.md](customer-discovery-plan.md) |
| 3 | Stage 1 success-criteria repair | Stage 1 → v1.2; criteria restated against ADR-011; clock defined | stage-01 §7 |
| 4 | Economics extensions (contracts, renewals, EA, FX, HRIS/ERP, price-book confidence) | Stage 2 → v1.2 (providers); Stage 5 → v0.10 (E21, FX); Stage 10 → v1.1 (realisable savings, price-book confidence) | respective docs + summary below |
| 5 | L1→L2 licence-reclamation workflow | Designed in §2 below; pointer added to Stage 8 context via this package | §2 |
| 6 | Certification roadmap | §3 below (repairs the dropped Stage 8 → Stage 11 thread) | §3 |
| 7 | Competitive battlecard | New document | [competitive-battlecard.md](competitive-battlecard.md) |
| 8 | Readiness re-evaluation | §4 below | §4 |
| — | Overclaim softening (review rec. 8) | "Never recommend cuts" precision handled in battlecard (Microsoft section); hybrid "packaging-not-redesign" corrected to "no architectural redesign; substantial engineering/operational work" — binding gloss on Stage 7 §1/§6.7 recorded here | battlecard + this register |

## 1. Economics extensions (summary of edits applied)

- **E21 ContractCommitment** (new entity, Stage 5): vendor; scope (SKUs, consumption commitments); term dates; committed amounts + currency; **renewal windows and true-up dates**; linked price-book entries. SoT: procurement/contract documents (Self-reported at capture → Financially validated on reconciliation). Owner: Finance/Procurement. Ingested via the existing manual-import/ERP provider path — extension, not redesign.
- **Realisable-savings classification** (Stage 10): every rationalisation recommendation classifies savings as *Immediately realisable* (consumption stops) / *Realisable at true-up (date)* / *Realisable at renewal (date)* / *Contractually locked*. The "waste" KPI splits accordingly; executive default shows realisable-first (ADR-025 defensibility).
- **Multi-currency & FX** (Stage 5/10): observations stored in native currency, never converted at rest; conversion at reporting time using dated rates (customer treasury rates preferred [Financially validated]; published reference rates otherwise [Estimated] — the rate source is part of the methodology reference); one consolidation currency per tenant; **frozen snapshots pin their FX rates** in the input basis (ADR-016 extension).
- **HRIS + ERP/procurement providers** (Stage 2/C4): HRIS provider (org units, cost centres, person↔org mapping) — V1, with the manual-import provider as the sanctioned fallback where API integration is unavailable at onboarding; ERP/procurement provider (contracts, PO/GL reconciliation feeds) — V1.5. Both are ordinary C4.5 providers.
- **Price-book confidence** (Stage 10): every price-book entry carries an evidence class (contract-sourced = Financially validated; list price = Estimated; absent = Unknown) that flows into weakest-link composition — a licence cost computed from a list price says so.

## 2. L1→L2 operational workflow: named licence reclamation

**Default (all tenants, L1):** aggregate reporting only — "N licences unused in scope X," trends, € ranges. Plus the **handoff pattern** for non-activating customers: the platform emits the *criteria and counts*; the customer's own IT reproduces the named list in native admin tooling (which they already can) — the platform never holds names it isn't permitted to hold.

**Activated path — "Licence Reclamation Campaign" (scoped L2 subset):**
1. **Activation:** explicit customer decision per ADR-021(2) — documented purpose ("licence reclamation"), internal approvals, jurisdiction scoping. The activation is *capability-scoped*: identity + licence assignment + last-activity dates only; no app-level behaviour, no content, nothing else moves to L2.
2. **Campaign, not surveillance:** reclamation runs as a **time-boxed campaign** (default 30 days) with a defined population scope; standing named-visibility is not offered by this workflow.
3. **Output controls:** the named list is visible only to the authorised reclamation role; every view privileged-read-audited (ADR-015.9); export of the list is itself an audited, watermarked event.
4. **Transparency:** affected-population notice supported (configurable per jurisdiction/works-council agreement); the Trust area records the campaign's existence, scope, and duration.
5. **Closure:** campaign ends → named data access closes → outcomes recorded as events → realised savings enter the Financially-validated pipeline; the aggregate story continues at L1.

This resolves the review's L1-vs-wedge tension: the default posture stays aggregate; the money workflow exists as a deliberate, bounded, auditable exception — which is itself a differentiator ("licence reclamation without standing surveillance").

## 3. Certification roadmap (repairing the dropped thread)

| Milestone | Timing (relative to V1 build) | Commercial dependency |
|---|---|---|
| Security controls designed-in (policies, evidence collection, SDL, logging) | Phase 0–5 (build) — Stage 8 already specifies the architecture; this adds the attestation evidence discipline | Design-partner sales possible pre-attestation **with contractual caveats** (discovery H6 tests appetite) |
| Independent penetration test | Pre-V1 launch | Gate for any external tenant |
| **SOC 2 Type I** | ~3–4 months after V1 operations begin | Opens early commercial conversations beyond design partners |
| **SOC 2 Type II** | After 6–12 months' observation window | **Gate for broad commercial launch** — this is the "hidden year" the review surfaced; it is now planned, not discovered |
| **ISO 27001** | Initiate in parallel with Type II observation; certify year 2 | EU enterprise procurement expectation |
| Trust centre (public permission manifest, security summary, privacy levels) | At V1 launch | Sales asset from day one; costs little, signals much |

Owner: the Gate-0-named product owner; the attestation clock is one more reason Gate-0 precedes build.

## 4. Final readiness re-evaluation

**Question: is the blueprint ready for implementation?**

**Answer: Yes, with identified implementation risks.**

The planning work is complete: strategy (challenged three times, revised each time), capability model, domain model, data model (PoC-gated sections bounded and pre-authorised), experience, integration, security, technology, economics (now with contracts/FX/HRIS realism), operating model, roadmap, commercial frame, discovery plan, Gate-0 framework, certification path, and battlecard. Both documented contradictions found by the review are repaired. No architectural unknowns remain that planning can resolve — everything left is resolved by *doing*.

The identified implementation risks (all carried in the risk register and kickoff checklist, none resolvable on paper):

1. **Gate-0 is undecided** — build must not start before it closes (the checklist's first organisational gate; the framework is ready).
2. **Gate-1 PoCs unexecuted** — correlation quality (R-12) is validated only in contact with a real tenant; escalation path defined.
3. **Discovery results pending** — may revise the commercial thesis (kill criteria pre-committed); cannot break the architecture.
4. **Finance co-ownership and works-council engagement** (OI-010/OI-003) — organisational commitments with long lead times, now with concrete asks.
5. **Stage 3/9 evidence decay** — kickoff re-validation is mandatory; the quarterly ritual owns it.
6. **The covenant is a bet until Gate-0 staffs it** — R-14 remains the roadmap's sharpest constraint.

None of these blocks the *start* of implementation in the defined order (Gate-0 → discovery + Gate-1 PoCs → Phase 0). This is the final planning revision; the blueprint is complete.
