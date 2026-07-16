# Stage 6 — Personas, Jobs to be Done & Experience Architecture

| | |
|---|---|
| **Version** | 1.0 |
| **Date** | 2026-07-15 |
| **Status** | Draft — awaiting Arun's approval |
| **Related** | [stage-02-capability-model.md](stage-02-capability-model.md) (C7 invariant: only door out), [stage-04-domain-model.md](stage-04-domain-model.md), [stage-05-conceptual-data-model.md](stage-05-conceptual-data-model.md), [decision-log.md](decision-log.md) (ADR-011 phasing, ADR-014/015/018 constrain experiences) |

**Scope discipline:** experience architecture, not UI design. No visuals, no layouts, no components.

**The governing test (Arun's rule, adopted as the C7 admission test):** *a screen exists only if it directly helps a user make a decision or complete a workflow.* Data existing is not a reason for a page to exist. Every screen below names its decision or workflow; §11 lists what this test killed.

**Consistency note honoured throughout:** Challenge 02 §4 permanently removed bespoke persona workspaces beyond the V1 trio. Therefore the "Finance experience" below is a set of workflow surfaces *within the shared product*, not a Finance workspace. Same for Risk/Privacy/Audit (parameterised views).

---

## 1. Personas

**Primary (the product is built for their working day):**

| Persona | Archetype | Cares about | Phase |
|---|---|---|---|
| P1 Governance Operator | AI CoE analyst / governance team member | Ledger trustworthiness, governance debt burn-down, queues emptied | V1 |
| P2 Platform Administrator | Tenant admin operating the platform | Feeds healthy, policies correct, access right | V1 |
| P3 Executive Sponsor | CIO first; CFO/CISO/CDO variants | "Is AI under control, what does it cost, what is it worth?" | V1 |

**Secondary (served by purposeful surfaces, not workspaces):**

| Persona | Served by | Phase |
|---|---|---|
| P4 Agent Owner (citizen or pro developer) | "My assets" view + obligations | V1.5 |
| P5 Finance Analyst / Controller | Allocation rules, period close, credits import workflows | V1 (workflow surfaces) |
| P6 Risk / Privacy / CISO / Internal Audit | Parameterised views + evidence export | V1 read-only views; V2 evidence packs |
| P7 BU Leader | BU-scoped executive view (same page, scoped) | V1.5 |

Challenge applied: Stage 1's twelve-persona list collapses into 7, of which only 3 get built-for experiences. P6 is one *view family*, not four experiences — Risk, Privacy, CISO and Audit differ by filter and column set, not by workflow, until V2 evidence packaging.

## 2. Jobs to be Done (the load-bearing set)

| # | Persona | Job ("When… I want… so that…") |
|---|---|---|
| J1 | P1 | When new assets are discovered, I want to triage and register them quickly so the ledger stays complete without becoming my full-time job |
| J2 | P1 | When the resolver is unsure (Low/collision), I want to decide merges with the evidence in front of me so confidence stays honest |
| J3 | P1 | When governance debt accumulates (ownerless, purposeless, stale), I want a prioritised burn-down so the estate trends governed |
| J4 | P2 | When a feed degrades, I want to know before the coverage map embarrasses us so trust in the ledger holds |
| J5 | P2 | When we enter a new jurisdiction or policy changes, I want to configure telemetry levels safely so we never over-expose (ADR-014) |
| J6 | P3 | When the quarterly review comes, I want the four answers (adoption, cost, value, control) with their confidence so I can defend them to the board |
| J7 | P3 | When spend looks wrong, I want to see which BU/asset drives it so I can act, not just worry |
| J8 | P5 | When the period closes, I want to freeze the numbers with their basis so restatements are diffs, not disputes (ADR-016) |
| J9 | P5 | When credits data arrives (CSV, ADR-013), I want to import, validate and label it so the wedge metric exists honestly |
| J10 | P4 | When I own agents, I want to see my obligations and my assets' standing so staying legitimate is cheaper than going shadow |
| J11 | P6 | When audit/regulator asks, I want evidence of decisions and access so the answer is an export, not an archaeology project |

## 3. Decision journeys (the five that matter)

1. **Discovered → Governed** (J1/J3): discovery event → triage queue (is it real? whose is it?) → register (type, purpose, owner, risk fields) → governance debt clears. *Decision points:* register vs reject; owner assignment. V1.5 upgrades the same journey with GovernanceCase gates — the journey shape does not change (Stage 4 §10 socket).
2. **Zombie → Rationalised** (J7): dormancy signal (multi-source: last activity + audit counts + sign-ins) → portfolio economics view ranks candidates with € impact and MatchConfidence → owner consulted (notification, V1.5) → decision recorded (retire / exempt-with-reason override / reassign). *The moat journey — no vendor ships it.*
3. **Merge dispute → Resolved** (J2): collision/Low match → MergeCase with side-by-side evidence (observations, rationale signals) → merge/keep/split → confidence updated, audit event. Queue must show *why the system thinks so* — operators decide, they don't re-derive.
4. **Quarter → Closed** (J8): period closing → allocation preview against rules (as-of org model) → anomalies reviewed → freeze snapshot (basis pinned) → executive narrative export. Restatement journey is the same path with supersession.
5. **Policy → Deployed safely** (J5): proposed telemetry change → impact preview (*what becomes visible/hidden, where, for whom*) → justification (required, ADR-014) → effective-dated activation → privileged audit trail. The preview is the safety feature: nobody should discover policy effects in production.

## 4. Information architecture — five areas, one convergent record

```
PORTFOLIO   ECONOMICS   GOVERNANCE   TRUST   ADMINISTRATION
    └────────────┴───── Asset Record (the 360) ─────┴──────┘
```

- **Portfolio** — the ledger browsed by what it is: estate overview, segments (type/BU/lifecycle), the Asset Record.
- **Economics** — money and value: spend attribution, utilisation, zombie/rationalisation ranking, value declarations, period close (P5 workflows live here).
- **Governance** — the debt and the queues: triage, merge cases, ownerless/stale/unregistered, risk profile work; (V1.5: cases and approvals).
- **Trust** — the platform's own honesty: coverage map (per-surface connection + correlation quality), data freshness, discovery blind spots, privileged-access transparency. *Making Trust a top-level area is deliberate: the coverage map is a differentiator, not a settings page.*
- **Administration** — connections, telemetry policy, roles/delegation, taxonomy, retention (P2 workflows; jurisdiction-aware policy wizard).

**The Asset Record** is the single convergent page: identity & aliases (with MatchConfidence and *why*), business context, ownership timeline, lifecycle, economics, usage, evidence trail. Every area deep-links into it; it is the answer to "tell me everything about this thing." One record page — not one per asset type (polymorphic sections per taxonomy, ADR-015.3).

**Navigation model:** area nav (5) → work surface → Asset Record; global search by any identifier (alias lookup is a first-class search); queues carry counts; no dashboard-of-dashboards.

## 5. Workspace model (per ADR-011 phasing)

| Experience | Composition | Phase |
|---|---|---|
| Governance Operator workspace | Governance + Portfolio + Trust areas, queue-first landing ("what needs me today") | V1 |
| Platform Admin surface | Administration + Trust, health-first landing | V1 |
| Executive experience | One page (§7) + drill-through to Economics/Portfolio read-only | V1 |
| Finance workflow surfaces | Economics: rules editor, close workflow, credits import | V1 |
| Agent Owner view | "My assets": standing, obligations, costs; approve/attest actions | V1.5 |
| Scoped views (BU leader, Risk/Privacy/Audit) | Parameterised Portfolio/Economics/Governance views + exports | V1.5–V2 |

## 6. Dashboard philosophy

1. **A dashboard answers a standing question and offers the next action.** No panel without a decision it serves.
2. **Confidence is part of the number.** Every metric shows its ConfidenceLabel; every asset count shows correlation confidence mix; mixed-confidence aggregates say so (ADR-012: uncertainty is never presented as fact).
3. **"Why do you say that?" in ≤2 steps.** Claim → evidence (observations, events, basis) — the evidence model (Stage 5 §6) exists to be *reachable*, or it's decoration.
4. **As-of basis displayed.** Every view states its time basis; frozen vs live is visually explicit (ADR-016).
5. **Coverage honesty banner.** Any view whose underlying feeds are degraded or partial says so at the top — the product never silently narrows its lens (Stage 1 philosophy: overstated coverage is worse than none).
6. **No vanity metrics.** "Total prompts this month" without a decision attached does not ship.

## 7. The Executive experience (P3, J6/J7)

One page, four questions, each with a headline, trend, confidence, and one drill path:
- **Adoption** — who is using what (policy-permitting granularity).
- **Cost** — spend by BU, trajectory, credits (labelled *Self-reported / Manual Import* until the API exists — the label is visible at board level by design, ADR-013).
- **Value** — declared value with confidence mix; explicitly *not* a single ROI number when the underlying labels don't support one.
- **Control** — governed vs ungoverned estate, governance debt trend, coverage completeness.
Plus the coverage banner and a period selector honouring frozen snapshots. Exportable as a narrative pack (board circulation) — the *same numbers, same labels* as the live page; no separate "board version" of the truth.

## 8. Notification philosophy (designed now, shipped V1.5 per ADR-011)

Notify only when: a decision awaits the recipient (queue assignment, approval), a covenant/threshold they own is breached (budget, freshness, debt SLA), or trust changed (feed died, policy changed, confidence downgraded on assets they own). Digest-first; storms suppressed (one "feed X degraded" per incident, not per asset); every notification deep-links to the acting surface; delivered via native channels (Teams/email — C7.4 orchestrate). No in-product notification center in V1.5 — the queues *are* the inbox; revisit only if evidence demands.

## 9. Role-based experience rules

- Roles (C8.2 V1: viewer/operator/admin + executive scope) filter *areas and actions*; jurisdictional telemetry policy filters *data granularity* — two independent axes, never conflated.
- L2+ individual-level views: gated by role *and* policy; carry a visible notice that access is recorded (ADR-015.9) — transparency to the viewer, not silent surveillance of the viewer.
- Delegated administration (V1.5): BU-scoped operator rights; the IA already partitions by org scope so delegation is a filter, not a fork.

## 10. Governance, Finance, Administration experiences (workflow inventories)

- **Governance (P1):** landing = prioritised debt queue; triage surface (J1); MergeCase surface with side-by-side evidence (J2); registration flow (guided, minimal mandatory set per ADR-018 — completeness is coached, not forced); risk-profile capture (V1 fields).
- **Finance (P5):** allocation rules (versioned, effective-dated, preview-before-activate); period close (J8: preview → anomalies → freeze → export); credits CSV import (J9: template, validation report, labelled ingestion, provenance stamp); restatement (supersede with reason).
- **Administration (P2):** connection management (health, credentials rotation prompts, sweep status); telemetry policy wizard (jurisdiction map, impact preview, justification, effective dating); roles; taxonomy management (values + migration events); retention policy per ADR-017.

## 11. What the screen test killed

| Killed | Why |
|---|---|
| Observation browser ("browse all raw data") | No decision; evidence is reached by drill-down from claims (§6.3) |
| Reports center / report builder | ADR-005; exports live on the views that own the numbers |
| Notification center | Queues are the inbox (§8) |
| Separate Risk / Privacy / CISO / Audit workspaces | Parameterised views (Challenge 02 §4); workflow demand must be proven first |
| Asset-type-specific record pages | One polymorphic Asset Record (ADR-015.3) |
| Standalone "analytics" area | Every analytic lives where its decision lives (Economics/Governance) |
| Global settings sprawl | Administration holds only workflow-bearing configuration; anything else is provisioning-time, not a page |
| Onboarding wizard as a permanent area | Setup is a one-time guided flow inside Administration, not navigation |
| Executive "explore" mode | Executives get answers + one drill path; exploration is the operator's job (scope discipline, not condescension — P3's job is J6/J7, not data spelunking) |

---

## Stage-end review

### Summary
Seven personas (three built-for), eleven JTBD, five decision journeys, a five-area IA converging on a single polymorphic Asset Record, workspace model aligned to ADR-011 phasing, and six dashboard rules that operationalise honest-data/ADR-012/016 at the experience layer. Trust is a top-level area. The screen test killed nine screen families before they were born.

### Assumptions
- Operators tolerate queue-first landings (standard for ops tooling; validate in first usability pass).
- Finance accepts working inside the shared product rather than a dedicated workspace (consistent with Challenge 02; organisational confirmation pending OI-010's broader Finance conversation).
- Native channels (Teams/email) suffice for V1.5 notifications — no evidence yet that an in-product center is needed.

### Confirmed facts
None new (no Microsoft claims in this stage).

### Unknowns
- Real queue volumes (triage/merge) at Quadient scale — affects prioritisation UX, not IA; sized after Gate-1 PoCs + first sweeps.
- Whether BU leaders need V1.5 scoped views or V2 (adoption pressure will tell).
- Executive export format preference (in-product page vs generated pack) — Question 3.

### Risks
- **R-21 (new):** the Asset Record is the product's most-visited page and could bloat into a junk drawer — mitigation: its sections obey the same screen test (each section = a question someone asks about an asset), reviewed each release.
- **R-22 (new):** queue-first governance UX fails if ingestion runs are noisy (false-positive discoveries flood triage) — couples UX quality to C4 delta-suppression quality (R-18); track jointly.

### Alternative approaches considered
- Persona-workspaces IA (a home per persona) — rejected: duplicates surfaces, violates Challenge 02, and fragments the single-truth Asset Record.
- Dashboard-first landing for operators — rejected: operators act on queues; dashboards are for standing questions (P3/P5).
- In-product notification center — rejected for V1.5 (§8), revisit on evidence.

### Questions for Arun
1. Approve the five-area IA with **Trust as a top-level area** (the contrarian choice — most products bury coverage in settings; I argue it's the credibility engine)?
2. Approve the **single polymorphic Asset Record** as the convergent page (vs per-type records)?
3. **Executive delivery:** is the quarterly artefact an in-product page executives visit, an exported narrative pack, or both (my recommendation: both, same numbers, same labels)?
4. **Stage 7 proposal:** Conceptual architecture & integration architecture (assembling contexts, providers, enforcement points, multi-tenancy conceptually — still no technology choices), absorbing the original Stages 7–9 scope sequencing. Confirm?

### Recommendations
1. Approve with the screen-test kill list recorded as binding precedent (§11) — it will be relitigated by every future stakeholder who wants "their page."
2. Carry J-numbers into the RTM as the user-story seed (BR ↔ JTBD ↔ capability linkage) at next RTM revision.
3. Validate the two UX-critical couplings early in build: queue quality ↔ delta-suppression (R-22), Asset Record discipline (R-21).
