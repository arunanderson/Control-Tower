# Requirements Traceability Matrix

Version 1.0 — 2026-07-15. **FROZEN with the blueprint (PD-006).** (API column from Stage 3; JTBD mapping added at freeze.)

**JTBD mapping (user-story seeds, from Stage 6 §2):** BR-01/BR-03 ← J1, J3 · BR-02 ← J1, J4 · BR-04 ← J6, J7 · BR-05/BR-13 ← J7, J8, J9 · BR-06 ← J6, J8, J11 · BR-07 ← J1, J2, J3, J10 · BR-08 ← J1–J11 (persona trio + views) · BR-09 ← J5 · BR-12 ← J3 (V2 actions) · BR-15 ← J11. Capability IDs reference [stage-02-capability-model.md](stage-02-capability-model.md); API details and confidence labels in [stage-03-microsoft-validation.md](stage-03-microsoft-validation.md).

Key API mappings from Stage 3 (summary; full matrix in the Stage 3 doc): BR-01/BR-02 → PPAC Inventory API [GA], Graph Package Mgmt API [Agent 365-licensed, PoC], Entra Agent ID Graph v1.0 [GA], ARM Foundry/AOAI [GA]; BR-05 → Graph subscribedSkus/licenseDetails [GA] + Azure Cost Mgmt APIs [GA] + Copilot Credits [**no API — manual feed pending**]; BR-13 → Graph Copilot usage reports [GA, user-level only] + Purview Mgmt Activity API [PoC] + Azure Monitor token metrics [GA]; BR-09 → concealment/re-masking design requirement (OI-015). Populated progressively; capabilities, user stories, architecture components, APIs, data entities, security and reporting requirements are added as later stages define them.

Chain: **Business Requirement → Product Capability → User Story → Architecture Component → API → Data Entity → Security Req → Reporting Req**

## Business requirements (from Stage 1 success criteria and drivers)

| ID | Business requirement | Source | Capability | Story | Arch | API | Data | Security | Reporting |
|---|---|---|---|---|---|---|---|---|---|
| BR-01 | All registered AI assets held in a single authoritative registry with owner, business purpose, lifecycle status | Stage 1 §7 | C1.1, C1.2, C1.4 | — | — | — | — | — | — |
| BR-02 | Automated discovery of unmanaged AI assets wherever technically possible, with explicit discovery-coverage reporting | Stage 1 §7 | C4.1, C1.3, C1.6, C1.7 | — | — | — | — | — | — |
| BR-03 | Every production AI asset carries an assigned risk profile | Stage 1 §7 | C2.2, C1.1 | — | — | — | — | — | — |
| BR-04 | Executive dashboards: adoption, cost, governance posture, business value | Stage 1 §7 | C7.2 (over C1/C2/C3 read models) | — | — | — | — | — | — |
| BR-05 | AI spend attributable by business unit | Stage 1 §7 | C3.1, C3.2, C5.1 | — | — | — | — | — | — |
| BR-06 | AI business value measurable via agreed methodologies, with confidence labelling (Measured/Estimated/Self-reported/Inferred/Unknown) | Stage 1 §7 | C3.5, C3.6 | — | — | — | — | — | — |
| BR-07 | Governance processes (registration, approval, lifecycle gates) operational in-platform, not spreadsheet-driven | Stage 1 §7 | C2.1, C2.3, C2.5, C1.7 | — | — | — | — | — | — |
| BR-08 | Persona-appropriate experiences for primary and secondary personas | Stage 1 §6 | C7.1–C7.3 (V1 trio), C7.6 (Later) | — | — | — | — | — | — |
| BR-09 | Configurable telemetry privacy levels (L1–L4), jurisdiction-scopable, audited — applied to all consumed telemetry regardless of provider | ADR-003 | C4.4, C8.4, C5.4, C9.1 | — | — | — | — | — | — |
| BR-10 | Multi-tenant, modular, configurable-governance SaaS architecture | ADR-001 | C8.3 (+ architecture stage) | — | — | — | — | — | — |
| BR-11 | Consume native Microsoft governance surfaces (Agent 365, Entra Agent ID, CCS, Purview, PPAC, Foundry, Copilot analytics) rather than rebuild | ADR-004 | C4.1–C4.3 | — | — | — | — | — | — |
| BR-12 | Orchestrate (not replicate) enforcement via native Microsoft controls | ADR-002 | C2.8, C4.6 | — | — | — | — | — | — |
| BR-13 | Portfolio economics: surface under-used licences, orphaned/zombie assets, consumption anomalies, and rationalisation opportunities across vendors | ADR-006/008 | C3.3, C3.4, C3.7 | — | — | — | — | — | — |
| BR-14 | Pluggable telemetry provider architecture: V1 = native Microsoft + supported third-party; new providers (incl. custom collectors if later justified) added without architectural redesign | ADR-007 | C4.5 | — | — | — | — | — | — |
| BR-15 | Evidence-grade, immutable audit trail of governance decisions and configuration changes, exportable for audit | Stage 1 §8.12 | C9.1, C9.2 | — | — | — | — | — | — |
