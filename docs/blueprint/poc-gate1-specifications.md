# Gate-1 PoC Specifications — Correlation Feasibility

| | |
|---|---|
| **Version** | 1.0 |
| **Date** | 2026-07-15 |
| **Status** | Planning artifact (PD-005). **No implementation under this project** — execution is commissioned separately. Stage 5 finalisation is gated on these results (PD-004). |
| **Related** | [stage-03-microsoft-validation.md](stage-03-microsoft-validation.md) §4/§7, ADR-012 |

Common preconditions: a representative Microsoft 365 tenant with (a) at least one Agent 365 licence assigned, (b) several Copilot Studio agents — including at least one created pre-March-2026 (legacy SP), one modern (native Entra Agent ID), one Agent Builder agent, one published to M365/Teams, (c) a Foundry project with one published agent, (d) Power Platform admin + AI Administrator + Global Reader roles available, (e) an Entra app registration with `CopilotPackages.Read.All`, PPAC RBAC Reader, `AgentIdentity.Read.All`-family, ARM Reader.

## PoC-1 — The appId cross-walk (the load-bearing join)

**Question:** For a Copilot Studio agent published to Microsoft 365, does `copilotPackage.appId` (Graph Package Management API) equal the PPAC Inventory record's `entraAppId`, and does PPAC's `entraAgentId` resolve to a Graph `agentIdentity` object for the same agent — giving a complete deterministic chain: PPAC bot GUID ↔ Entra identity ↔ M365 package?

**Method (conceptual):** enumerate the same agent from all three surfaces; record every identifier each returns; attempt the joins; repeat for each agent archetype in the preconditions (modern, legacy, Agent Builder, unpublished).

**Success criteria:** chain holds deterministically for modern published Copilot Studio agents (→ these rate **High** confidence in ADR-012 terms); each archetype's join behaviour is documented (expected: legacy = tag-heuristic only → Medium; Agent Builder = no Entra chain → Medium/Low; unpublished = PPAC-only → High single-surface).

**Feeds Stage 5:** which IdentityAlias types exist per archetype; the confidence-assignment rules table.

## PoC-2 — The manifest ID hole

**Question:** Is the M365 manifest ID (generated when a Copilot Studio agent is published to Teams/M365) recoverable from any API-accessible store — specifically Dataverse `botcomponent` rows for the publication channel — closing the bot-GUID↔manifestId hole without relying on appId?

**Method:** for the published test agent, sweep its Dataverse `bot`/`botcomponent` rows via the Web API; search component payloads for the manifest ID observed in `copilotPackage.manifestId`; document where (if anywhere) it appears.

**Success criteria:** either a documented retrieval path (→ second deterministic join, redundancy for PoC-1), or a confirmed dead end (→ the hole is permanent until Microsoft ships the registry-replacement API; alias graph relies on appId + heuristics for this edge).

## PoC-3 — Package API access reality

**Question:** Does `GET /v1.0/copilot/admin/catalog/packages` work app-only with `CopilotPackages.Read.All` plus an Agent 365 licence in a normal tenant (outside preview/Frontier programs)? Resolve the GA/preview documentation contradiction. Do registry-synced third-party agents and all four agent types appear? What are v1.0 vs beta field differences and effective throttling behaviour at inventory-scan volume?

**Success criteria:** documented working auth recipe + licence prerequisite confirmation + coverage list + observed throttling → feeds the C4.1 provider contract and the licence-degradation tiers (OI-004).

## Out-of-scope for Gate 1
Gate-2 (economics: Azure Cost Management PAYG depth, Purview audit-record pipeline, Credits CSV fidelity) and Gate-3 (operational limits) run later; they do not gate Stage 5.

## Result protocol
Each PoC produces a findings note appended to this document (versioned), updates the Stage 3 validation matrix rows it touches, and adjusts ADR-012 confidence-assignment rules if needed. If PoC-1 fails for modern agents (chain broken), **escalate to Arun before Stage 5 finalisation** — that outcome would demote the ledger's High tier to Medium for the largest asset class and warrants a scope conversation, not a silent workaround.
