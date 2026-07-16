# Open Issues & Parking Lot

Version 1.4 — 2026-07-15 (Stage 3: OI-009 resolved, OI-004 evidence-based, OI-014..016 added). Living document. Ideas and issues that must not be lost but are outside the current stage.

## Open issues (require resolution at the stage indicated)

| ID | Issue | Raised | Resolve in | Notes |
|---|---|---|---|---|
| OI-001 | Product naming: "AI Control Tower" is ServiceNow's flagship product name and company tagline. Commercialisation under this name invites confusion and possible trademark risk. | Stage 1 | Pre-commercialisation (DD-001) | Internal working name acceptable for now |
| OI-002 | ~~SaaS vs data residency~~ **Resolved 2026-07-15 → ADR-020**: pure SaaS V1; three-plane seam preserves the in-tenant data plane as a future deployment option; not built in V1. | Stage 1 | Closed | Stage 8 compares implications without reopening |
| OI-003 | Works council / employee representative consultation likely required before L2+ telemetry in FR/DE and possibly other jurisdictions. Legal review needed; product must support jurisdiction-scoped telemetry levels (different levels per country within one tenant). | Stage 1 | Stage 5 | Jurisdiction-scoped config is a probable hard requirement |
| OI-004 | Licence degradation tiers — **now evidence-based** (Stage 3 §5): Package API requires Agent 365 licence [Confirmed]; guaranteed no-premium baseline = PPAC + ARM + Entra core + licence + Azure cost APIs. Formalise tiers in Stage 12 / sales collateral. | Stage 1 | Stage 12 | Converted from risk to consultative selling tool |
| OI-005 | ~~"100% inventory" unfalsifiable~~ **Resolved 2026-07-15**: registration coverage + discovery coverage reframing accepted at Stage 1 freeze. | Stage 1 | Closed | |
| OI-006 | Enterprise-vs-personal account detection for third-party AI tools. **Deferred by ADR-007**: personal-account visibility beyond Microsoft-native is not a V1 requirement; revisit only if a custom/partner telemetry provider is justified. | Stage 1 | Deferred (post-V1) | |
| OI-007 | Prompt sophistication estimation without storing prompt content. **Deferred by ADR-007** — depends on collection mechanisms out of V1 scope. | Stage 1 | Deferred (post-V1) | |
| OI-008 | Category straddle: commercial positioning leads portfolio/cost/value (ADR-008) while the analyst category is Gartner "AI Governance Platforms"; procurement-classification risk. Needs explicit analyst/category strategy. | Challenge 01 | Pre-commercialisation | |
| OI-009 | ~~Cross-surface reconciliation feasibility~~ **Resolved 2026-07-15** (Stage 3 §4): deterministic for modern subset, not universal; ADR-012 alias-graph pattern proposed; residual risk in Gate-1 PoCs. | Stage 2 | Closed (→ ADR-012, PoC backlog) | R-12 = AMBER managed |
| OI-010 | Finance ownership of value methodology (C3.5): organisational commitment required; without it, differentiator #2 degrades to CoE self-grading (R-13). | Stage 2 | Arun / org decision | Long lead time — start now |
| OI-011 | AI asset definition & taxonomy (C1.2) must be an early deliverable of the next stage; gates all scope discussions. | Stage 2 | Stage 3 | |
| OI-012 | Business capability map source: consume existing EA tool map vs define minimal tag set (C5.2). | Stage 2 | Stage 4 (awaiting Arun answer) | Build-full-map option deleted by Challenge 02 §4 |
| OI-013 | **V1.5 covenant** (ACCEPTED 2026-07-15): governance engine (C2) must ship within two quarters of V1 (R-14). **Binding roadmap constraint — Stage 12 must guarantee it.** | Challenge 02 | Stage 12 roadmap | Accepted by Arun |
| OI-014 | ~~Copilot Credits posture~~ **Resolved 2026-07-15 → ADR-013** (manual CSV, "Self-reported / Manual Import"). Remaining: quarterly roadmap-watch for the API. | Stage 3 | Quarterly re-validation | R-16 mitigated |
| OI-015 | ~~De-concealment/re-masking~~ **Resolved 2026-07-15 → ADR-014** (re-masking invariant accepted). Design lands in Stage 5/8 (C4.4 + pre-C7 enforcement point). | Stage 3 | Stage 5/8 design | |
| OI-016 | **Agent-level usage has no API** (admin-center preview report only) [Confirmed]; candidate workaround: per-agent interaction counts from Purview audit records [PoC 5]. Roadmap-watch ID 497999. | Stage 3 | PoC Gate-2 | |

## Parking lot (good ideas, not now)

| ID | Idea | Source | Notes |
|---|---|---|---|
| PL-001 | AI Fluency / skills-development module (Larridin-style adoption coaching) | Project brief | Candidate for later release; not core to governance backbone v1 |
| PL-002 | MCP server registry and MCP transaction governance (ServiceNow added an "AI Gateway" for MCP) | Stage 1 research | Growing relevance; revisit at Stage 2 capability model |
| PL-003 | Marketplace/benchmark network effects: anonymised cross-tenant benchmarking of AI adoption/ROI once multi-tenant | Stage 1 | Strong commercial differentiator; privacy design implications |
| PL-004 | Guardian-agent pattern (agents governing agents) — Gartner 2026 construct | Stage 1 research | Watch; do not build v1 |
| PL-005 | Non-human identity (NHI) governance beyond AI agents (service accounts, OT/IoT) — ServiceNow heading there | Stage 1 research | Likely out of scope; boundary discussion Stage 2 |
| PL-006 | Business process mining / workflow discovery ("Workflow Intelligence" from original brief) | ADR-006 cut | Revisit only if customers demand AI-process linkage deeper than named-process references |
| PL-007 | Data lineage construction (own crawling/parsing) | ADR-006 cut | Consume Purview/Fabric lineage; build only AI-asset→knowledge-source→capability linkage |
| PL-008 | Prompt library and knowledge-source content management | ADR-006 cut | Register as assets only; content stays in builder tools |
| PL-009 | Custom telemetry collectors (browser extension, desktop app, endpoint service) | ADR-007 | Not permanently excluded; requires pluggable-provider contract (Stage 7/8) + explicit justification + works-council/legal clearance before revival |
| PL-010 | Persona experiences beyond V1 trio (governance operator, executive, agent owner) — Finance, Risk, Privacy, Audit, citizen/pro developer experiences | ADR-006 | Sequence in personas stage / Stage 12; report-recipient views first |
| PL-011 | ~~`ReportSnapshot` aggregate~~ **Pulled forward 2026-07-15 → ADR-016** (financial close is a confirmed requirement); modelled in Stage 5. | Stage 4 §11.4 | Resolved |
