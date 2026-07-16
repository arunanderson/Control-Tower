# Implementation Handoff Package

| | |
|---|---|
| **Version** | 1.0 |
| **Date** | 2026-07-15 |
| **Status** | For Arun's review. Implementation guidance only — no code, no Cursor prompts (those follow only on Arun's explicit approval). |
| **Blueprint state** | FROZEN (PD-006): 11 approved stages, 26 ADRs + 6 process decisions, RTM v1.0, all conflicts resolved |

**Audience:** the implementation team and its AI coding agent. **Contract:** the frozen documents are the specification; this package is the map, the guardrails, and the order of work. Where this package and a stage document appear to disagree, the stage document wins and the discrepancy is a defect in this package.

---

## 1. Recommended reading order

1. **README.md** — the map and the freeze.
2. **stage-01-product-vision.md** (v1.1) — what this is and is not; then **challenge-01** and **challenge-02** — *why* it is that (the two documents that killed the wrong product; read them to avoid rebuilding it).
3. **decision-log.md** (v2.0) — all 32 decisions; the fastest way to absorb the project's judgment.
4. **stage-02-capability-model.md** (v1.1) — contexts C1–C9, phasing, the two doors.
5. **stage-04-domain-model.md** (v1.1) — aggregates, events, doctrines. Read before the data model.
6. **stage-05-conceptual-data-model.md** (v0.9, PoC-gated) — 20 entities; note the ⛔PoC markers: they are your first work items, not blemishes.
7. **stage-03-microsoft-validation.md** + **poc-gate1-specifications.md** — the evidence base and its expiry warnings.
8. **stage-07-conceptual-architecture.md** → **stage-08-security-trust-architecture.md** → **stage-09-technology-deployment.md** — the how, in dependency order.
9. **stage-06-experience-architecture.md** — what gets built for whom; the screen kill list is binding (ADR-019.4).
10. **stage-10-economics-methodology.md** — the defining capability's rules; the rendering constraint (§ opening) is a build requirement.
11. **stage-11** Parts A–D — workflows (V1.5), operating model, roadmap, conclusion.
12. **open-issues-parking-lot.md** + **requirements-traceability-matrix.md** — what's deliberately not here, and the BR↔JTBD↔capability chains.

## 2. Executive summary (for the implementation team)

You are building **the independent ledger of enterprise AI**: a multi-tenant SaaS that federates AI asset inventories from Microsoft surfaces (later, other vendors), resolves them into one honest ledger with visible match confidence, attributes cost and value with a six-class evidence taxonomy, and orchestrates governance without ever blocking reality from being recorded. V1 is "the ledger with money attached" (25 capabilities); V1.5 adds the governance engine under a **hard two-quarter covenant**; V2 adds control actions and evidence packs. The product's entire commercial thesis is trustworthiness: every number carries evidence source, evidence class, as-of date, and methodology reference — structurally, not editorially. When implementation pressure suggests softening any honesty mechanism (labels, Unattributed bucket, coverage banners, confidence tiers), that is R-30: escalate, don't soften.

## 3. Frozen architecture principles (the non-negotiables)

1. **Two doors:** C4 is the only path in/out for external signals and control actions; C7 the only path for human-facing experiences (ADR-009).
2. **I3/I4:** no provider access outside adapters, no pipeline shortcuts; experiences and exports read only policy-enforced read models (ADR-020).
3. **Observations are immutable, append-only, pre-resolution**; resolution happens through links only (ADR-015).
4. **Events are the audit trail** — one stream, hash-chained, WORM-anchored; no unaudited actors including staff and the platform itself (ADR-015/021).
5. **Privacy double-gate with storage refusal**: never store above the tenant's telemetry level; re-mask at read regardless of upstream settings; L1 is every tenant's default (ADR-003/014/021).
6. **Flag, never block** — the ledger records ungoverned reality as governance debt (ADR-018).
7. **Honest data everywhere**: six evidence classes, weakest-material-link composition, visible confidence mixes, categorical never numeric, Unattributed never spread, smaller defensible number wins (ADR-024/025).
8. **Modular monolith, strictly bounded** (ADR-020); **deliberately boring stack** (ADR-023) — new infrastructure must name the requirement that earned it.
9. **Everything tenant-scoped, credentials isolated more strongly than data, no cross-tenant enumeration** (ADR-021).
10. **Pricing/product must never create incentives against governance** (ADR-026) — relevant to telemetry metering and any usage-based mechanism you may be asked to add.

## 4. Frozen ADR set (index)

PD-001..006 (process: stage gates, KB format, resequencing, PoC gating, PoC specs, freeze). ADR-001 SaaS-from-day-one · 002 observe+orchestrate · 003 privacy levels L1–L4 · 004 complement-Microsoft · 005 not-a-replacement boundaries · 006 three-capability core · 007 pluggable telemetry providers · 008 portfolio-intelligence-first positioning · 009 eight contexts + two doors · 010 Activity Intelligence dissolved · 011 minimal V1 + V1.5 covenant · 012 alias-graph entity resolution (High/Medium/Low/Manual) · 013 credits CSV "Self-reported / Manual Import" · 014 re-masking invariant · 015 domain doctrines (9) · 016 frozen reporting periods + restatement · 017 policy-driven retention · 018 Flag-Never-Block · 019 experience principles (Trust area, polymorphic record, single reporting model, screen test) · 020 topology/monolith/API/invariants · 021 security posture (staff JIT, L1 default, legal hold, 11 principles) · 022 custom Azure (not Power Platform) · 023 stack (DB engine reversible; tooling = deployment concern; portability conditions) · 024/025 economics principles + decisions · 026 commercial model. Full text: decision-log.md.

## 5. Blueprint component pointers (with implementation notes)

| Component | Document | Implementation notes |
|---|---|---|
| Capability map | stage-02 v1.1 | Module structure of the monolith = C1–C9; phasing per ADR-011 §4 |
| Domain model | stage-04 v1.1 | Ten aggregates + C2 socket; state machines are spec, not suggestion |
| Conceptual data model | stage-05 v0.9 | Translate to physical schema only after Gate-1; keep engine-portable per ADR-023 amendment; the three ⛔PoC sections are the only unfinalised design |
| Integration architecture | stage-07 + stage-03 | Provider contract (C4.5 manifest) is the first thing to design in code; all 16 integrations classified; **re-validate Stage 3 findings at kickoff — they are dated 2026-07-15 and decay** |
| Security architecture | stage-08 | Privileged zone, consent packs, JIT access, legal hold are V1 build items, not hardening backlog |
| Technology architecture | stage-09 | SKU-level validation at kickoff; PostgreSQL-vs-Azure-SQL decision closes at build start |
| Experience architecture | stage-06 | Build order: operator workspace → executive page → Trust area; kill list binding |
| Economics methodology | stage-10 | The (evidence, class, as-of, methodology) rendering constraint is a platform-level type, not per-widget discipline |
| Workflows (V1.5) | stage-11 Part A | Socket already in the domain model; do not pre-build in V1 beyond events + correlation refs |

## 6. Implementation roadmap & Cursor implementation phases

*(Phases for the coding agent; each ends with working, testable software. Not prompts — prompt generation awaits Arun's approval.)*

- **Phase 0 — Foundations & PoC execution (concurrent):** repo + CI with architecture tests (two doors, I3/I4, module seams — R-23 mitigation from day one); tenancy/context plumbing; event backbone + outbox + hash chain; **execute Gate-1 PoCs 1–3** (poc-gate1-specifications.md); finalise Stage 5 (pre-authorised revision) with PoC results.
- **Phase 1 — The door in:** provider contract (C4.5) + first three providers (PPAC Inventory, Entra Agent ID, licence APIs — the no-premium baseline); observation store with delta suppression; privacy Gate 1; coverage facts.
- **Phase 2 — The ledger:** AIAsset + aliases + resolution pipeline with confidence tiers; merge queue; ownership; taxonomy; registration fields; governance-debt projections.
- **Phase 3 — The money:** cost providers (Azure Cost Mgmt, licence costs, credits CSV provider); allocation rules + runs; utilisation + zombie detection; Unattributed bucket.
- **Phase 4 — The door out:** policy enforcement point (Gate 2); operator workspace; executive page; Trust area (coverage map, freshness, privileged-access log); board pack from the same read models.
- **Phase 5 — Enterprise readiness:** remaining V1 security (JIT access flows, legal hold, export/deletion), ReportingPeriod/Snapshot freeze, retention engine; Quadient onboarding (First Truth in 10 days) — **V1 ships; covenant clock starts.**
- **Phase 6 (V1.5, ≤2 quarters) — The governance engine:** C2 workflows into the socket; owner view; notifications; delegated admin; full value methodology + validation workflow; forecasting.
- Gate-2/3 PoCs run during Phases 3–5 (economics depth, throttling reality).

## 7. Gate-1 PoC commissioning guide

Spec: **poc-gate1-specifications.md** (frozen). Commissioning requirements: a representative M365 tenant with Agent 365 licence + the four agent archetypes; app registrations per spec preconditions; **timebox: two weeks**; deliverable: findings notes appended to the PoC doc + Stage 3 matrix updates + the confidence rule table for Stage 5 finalisation. **Escalation trigger (verbatim from spec): if PoC-1 fails for modern agents, escalate to Arun before Stage 5 finalisation — scope conversation, not silent workaround.** PoCs are pre-build validation, not product code; nothing written for them enters the codebase.

## 8. Build kickoff checklist

**Organisational (gate reality, not documents):** ☐ Finance co-ownership committed (OI-010 — Stage 10 §8 is the ask; R-31 if unstaffed) ☐ works-council/legal engagement started (OI-003; L1-default makes launch clean, L2 needs their runway) ☐ V1.5 team capacity committed at V1 kickoff (OI-013 covenant) ☐ product name decided before any external conversation (DD-001) ☐ Quadient pilot BUs selected.
**Technical:** ☐ Gate-1 PoCs executed, Stage 5 finalised ☐ Stage 3 findings re-validated (quarterly ritual — first run is kickoff) ☐ Stage 9 SKU/limit validation ☐ DB engine decision closed ☐ GitHub/Bicep vs org standards confirmed (deployment concern, ADR-023) ☐ contract price book sourced ☐ EU stamp region + DR pair chosen.
**Governance of the build itself:** ☐ formal revision process socialised (PD-006) ☐ architecture tests wired into CI before feature code ☐ R-30 escalation path named (who hears "can we soften this number?").

## 9. Risks requiring validation during implementation

| Risk | Validate by |
|---|---|
| R-12 correlation quality at real scale | Gate-1 PoCs + Phase 2 with production data |
| R-16 credits CSV operational sustainability | Phase 3 with Finance; quarterly API roadmap-watch (OI-014) |
| R-17 Microsoft API churn | Kickoff re-validation + provider-contract isolation; watch register (Stage 3 §8) |
| R-18/R-22 observation volume & triage noise | Phase 1 delta-suppression metrics at Quadient scale |
| R-23 monolith boundary erosion | Architecture tests in CI from Phase 0 |
| R-26 consent friction | First onboarding: measure time-to-consent per pack |
| R-28 single-store concentration | Load/volume tests Phase 3; split triggers per Stage 9 |
| RLS performance (Stage 9 assumption) | Phase 0 spike with representative volumes |

## 10. Items intentionally deferred (do not build; do not lose)

**V2:** control invocation + write consent, evidence packs, BI export, scenario modelling, chargeback exports, SIEM publishing, dependency mapping, attestations at scale. **Later/parked (with revisit criteria in the parking lot):** non-Microsoft providers, cross-tenant benchmarking (PL-003), custom telemetry collectors (PL-009 — revival requires justification + legal clearance), compliance content packs (C2.9 — 2027 EU AI Act wave), in-tenant data plane (ADR-020 seam; first residency-blocked customer), public API, AI Fluency (PL-001), process mining / lineage construction / prompt content management (permanently cut, Challenge 02). **Never:** anything on the Stage 6 §11 screen kill list, per-asset/per-telemetry pricing, peanut-butter allocation, numeric confidence scores, storage above telemetry level.

---

**Next step, on Arun's approval only:** generation of the Cursor implementation prompts (per-phase prompt packs referencing the frozen documents). Not produced yet, per instruction.
