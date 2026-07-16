# Gate-0 — Venture Decision Framework

| | |
|---|---|
| **Version** | 1.0 |
| **Date** | 2026-07-15 |
| **Status** | Framework complete; **the decision itself belongs to Quadient executive leadership** — this document structures it, it cannot make it |
| **Related** | [independent-review-01.md](independent-review-01.md) Q8.1, [revision-package-v1.md](revision-package-v1.md), ADR-001 |

**The question:** who owns, funds, staffs, and sells this product? Every downstream commitment (the V1.5 covenant, the commercial packaging, ~15–20% of V1 build weight) depends on the answer. Build must not start before this gate closes.

## 1. The three options

| | **A — Internal product** | **B — Incubated venture** | **C — Commercial venture (spin-out / new entity)** |
|---|---|---|---|
| Ownership | CIO organisation; AI CoE as product owner | Quadient digital/innovation unit with dedicated product lead | Separate entity; Quadient as anchor investor + first customer |
| Funding | IT budget, business-case-justified by internal savings | Ring-fenced innovation funding, stage-gated | Seed/venture funding; Quadient equity |
| Commercial ambition | None (option preserved by architecture) | Design-partner sales (2–5 friendly logos) while proving internally | Full go-to-market per Stage 11 |
| V1 scope impact | Descope ~15–20% commercial build weight (marketplace plumbing, edition gating, parts of consent tiering); keep multi-tenant architecture (cheap at design time, ADR-001) | Full V1 as designed | Full V1 + earlier SOC 2 investment |
| Team | 4–6 engineers + CoE (indicative — sizing requires the estimate this blueprint deliberately lacks) | 6–10 + product/design | 10–15 + GTM hires |
| Honest failure mode | Shelfware when CoE reorganises; talent drains from an unowned tool | Two-masters paralysis (R-11 institutionalised) | Quadient strategy conflict: a CCM company funding an unrelated SaaS |

## 2. Decision criteria (score before choosing)

1. **Strategic intent:** does Quadient leadership *want* a software venture outside CCM? (If no — Option A, honestly, and stop paying the commercial tax.)
2. **Customer-discovery results** (companion plan): ≥ the success thresholds → B/C viable; kill criteria hit → A.
3. **Capital:** is 18–24 months of funded runway available without IT-budget cannibalisation? (If no — not C.)
4. **Talent:** can a dedicated product leader be named within one quarter? (If no named owner — A by default; an unowned B is worse than an honest A.)
5. **Risk appetite:** is Quadient prepared to hold the aggregation-target liability (R-25) commercially, including insurance and disclosure obligations? 

## 3. Exit criteria (gate closes when…)

☐ One option formally selected by the executive sponsor group (CIO + CFO minimum) ☐ Named accountable product owner ☐ Funding envelope approved for the selected option ☐ V1 scope adjusted accordingly (descope list pre-identified above — a one-week revision, not a redesign) ☐ V1.5 covenant staffing commitment signed against the selected team shape ☐ Decision recorded as an ADR with reconsideration triggers (e.g., A→B upgrade on discovery evidence).

## 4. Recommendation (advisory only)

**Option B**, contingent on discovery results: it preserves the commercial option the architecture already paid for, forces a named owner and ring-fenced funding, and defers the spin-out question until design-partner evidence exists. If criteria 1 or 4 fail, choose A without embarrassment — the blueprint explicitly makes internal-only a legitimate outcome, and an honest A beats a zombie B.
