# Customer Discovery Plan

| | |
|---|---|
| **Version** | 1.0 |
| **Date** | 2026-07-15 |
| **Status** | Ready to execute; runs in parallel with Gate-1 PoCs; feeds Gate-0 criteria 2 |
| **Design** | 15–20 structured interviews, 6 personas, 4 weeks; hypotheses falsifiable, kill criteria pre-committed |

**Discipline:** discovery tests *willingness to pay and act*, not politeness. No demos, no pitching in the first half of any interview. Every hypothesis below is written to be killable.

## 1. Hypotheses under test

| # | Hypothesis | Falsified if… |
|---|---|---|
| H1 | Enterprises with >1,000 Copilot licences cannot answer "what AI runs here, what does it cost, what is it worth" and feel board pressure about it | Most interviewees answer confidently from existing tools |
| H2 | The **independence** argument changes buying behaviour (vendor dashboards distrusted for spend decisions) | Buyers say Microsoft/ServiceNow dashboards are sufficient grounds for licence decisions |
| H3 | A CFO will sponsor or co-sponsor this purchase (economics-led wedge, ADR-008) | CFOs defer entirely to CIO/CISO; economics message doesn't move them |
| H4 | ServiceNow-bundled customers will still pay for an independent alternative | Bundled customers see no incremental value worth a second contract |
| H5 | Employee-band pricing at [test bands: £40–120k/yr mid-market enterprise] clears value perception | Consistent anchoring below £30k or per-seat expectations |
| H6 | Buyers accept a first-product vendor holding tenant read credentials, given the Stage 8 posture (consent tiers, JIT, transparency) — pre-SOC 2 with contractual caveats for design partners | Security/procurement categorically refuse pre-attestation |
| H7 | "Realisable at renewal" savings framing is credible and preferable to gross-waste claims | Buyers shrug at renewal-window framing; want gross numbers (would contradict our defensibility thesis — important either way) |
| H8 | Governance teams want the workflow engine within 6 months of visibility (V1.5 attach thesis) | Visibility alone satisfies; governance workflows seen as "our GRC tool's job" |

## 2. Sample frame (15–20 interviews)

5× CIO / CDO (incl. ≥2 Microsoft-first, ≥2 ServiceNow AI-tier customers) · 3× CFO or FP&A owner of IT spend · 3× CISO / security architecture · 3× Enterprise Architecture / AI CoE leads · 2× Procurement / vendor management · 2–3× existing AI-governance tool users (Credo/Holistic/Zenity/Larridin shops if reachable). Mix: 6k–50k employees, EU + NA, at least 3 with works councils.

## 3. Interview guides (abbreviated; full scripts to be drafted per persona at execution)

**CIO/CDO (30 min):** Walk me through the last time the board asked about AI cost or risk — what did you show them? · What can't you see today? · Who owns AI governance operationally? · (Second half) React to the one-sentence positioning; "what would this have to prove in 90 days?"; budget line it would come from.
**CFO (30 min):** How is AI spend visible to you today — and do you believe those numbers? · Have you ever acted on a vendor's own utilisation report to cut that vendor's spend? (H2 direct test) · React to Validated-to-Declared and realisable-at-renewal framing (H7) · What evidence class would your auditors accept?
**CISO (30 min):** What would a vendor need to show to get tenant-wide read consent? (H6) · How do you evaluate aggregation risk? · Reaction to consent tiers + customer-visible staff access · Deal-breakers list.
**EA/CoE (45 min):** Current inventory/governance mechanics (tool archaeology — where the spreadsheets live) · Merge-queue tolerance: how much curation labour is acceptable? · V1.5 attach interest (H8).
**Procurement (30 min):** First-product vendor requirements; liability/insurance expectations vs data access; DPA/DPIA cycle time for employee-telemetry products (even L1); exit/escrow expectations.
**ServiceNow customers (within CIO/EA slots):** What does AI Control Tower actually do for you today? What's missing? Would independence + Microsoft depth justify a separate purchase? (H4)

## 4. Success criteria (proceed signals, pre-committed)

≥60% of buyer-side interviews confirm H1 with a concrete recent incident · ≥50% of CFOs respond to H2/H7 with "show me more" or stronger · ≥3 organisations agree to design-partner conversations · ≥2 ServiceNow-bundled customers articulate unprompted gaps our wedge fills · No categorical procurement refusal pattern on H6 among design-partner-willing organisations · Median price reaction within 50% of test bands.

## 5. Kill criteria (equally pre-committed)

H1 falsified across the majority (the pain is imagined) · H2 falsified by CFOs specifically (independence moat is rhetorical) · H4 + H6 both falsified (no beachhead: bundled customers won't switch and unbundled won't consent) · Zero design-partner interest after 15 interviews. **Any kill criterion met → Gate-0 defaults toward Option A (internal product), and the commercial thesis returns to the parking lot with the evidence attached.** Killing the commercial thesis does not kill the product — Quadient still needs it.

## 6. Outputs

Evidence pack per hypothesis (verbatims + counts) → Gate-0 scoring · pricing-band evidence → ADR-026 confirmation or revision · objection inventory → battlecard v1.1 · design-partner shortlist → V1 pilot pipeline.
