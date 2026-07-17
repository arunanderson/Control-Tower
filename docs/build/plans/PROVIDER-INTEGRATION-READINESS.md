---
id: PROVIDER-INTEGRATION-READINESS
title: Provider Integration Readiness Report + Gate-1 Execution Plan
type: readiness-report
schema_version: 1
status: awaiting-tenant
authority:
  - docs/blueprint/poc-gate1-specifications.md
  - docs/blueprint/stage-03-microsoft-validation.md
  - docs/blueprint/implementation-handoff-package.md §7
requires_human_gate: true # Microsoft tenant provisioning + admin consent
---

## 1. Provider Integration Readiness Report

**Phase A (framework) is complete and merged-ready.** The C4.5 provider framework is built and tested,
tenant-independent, with **no provider-specific logic**:

- Provider **contracts**: `IProvider` (manifest + health + acquire), `ProviderManifest`, capability
  model (`ProviderCapability`), auth/authz abstraction (`ProviderAuthRequirement`/`ProviderAuthKind`),
  health/freshness/coverage/error models, versioning (SemVer), native-identifier mapping.
- Framework **services**: `IProviderRegistry` (register/resolve/discover), `ProviderContractValidator`,
  `ProviderDiagnostics`, sync-scheduling abstraction (`SyncSchedule` + `IWatermarkStore`),
  `ProviderTestHarness` (contract-conformance suite), the **manual CSV provider** (ADR-013), and the
  provider integration-test framework.
- **Equality of providers proven:** the harness runs any provider (a fake, a broken one, and the CSV
  provider) through the identical contract checks; a non-conforming provider fails. Microsoft, OpenAI,
  Anthropic, Google, ServiceNow or any future provider register behind the same contracts.
- Tests: 13 provider tests (part of 93 backend green). `/api/admin/providers` exposes manifests.

**Phase B (Microsoft providers + Gate-1 PoCs) is NOT started — it is blocked on a human gate.** No
Graph, Copilot, Entra, or PPAC code exists; no permissions requested. The items below are what Arun
must provision before Phase B proceeds.

## 2. Gate-1 Execution Plan (frozen spec: poc-gate1-specifications.md)

Run behind the existing provider contracts, quarantined in `/poc` (never referenced by `/src`).

- **PoC-1 — appId cross-walk (load-bearing join):** enumerate the same agent from PPAC, Entra Agent ID,
  and Graph Package Management; record every identifier; attempt the joins per archetype
  (modern/legacy/Agent Builder/unpublished). **Escalation: if PoC-1 fails for modern agents, stop and
  escalate to Arun before Stage 5 finalisation.**
- **PoC-2 — manifest ID hole:** sweep Dataverse `bot`/`botcomponent` for the published test agent;
  determine whether the M365 manifest ID is recoverable.
- **PoC-3 — Package API access reality:** confirm `GET /v1.0/copilot/admin/catalog/packages` works
  app-only with `CopilotPackages.Read.All` + an Agent 365 licence outside preview; record coverage,
  v1.0-vs-beta field diffs, and throttling.
- **Result protocol:** findings notes appended to the PoC doc; Stage 3 matrix updated; ADR-012
  confidence rules adjusted; then Stage 5 finalisation (pre-authorised PD-006 revision, human-led).

## 3. Required Microsoft permissions (Entra app registration scopes)

- `CopilotPackages.Read.All` (Graph Package Management — PoC-1/3)
- `AgentIdentity.Read.All`-family (Entra Agent ID — PoC-1)
- **PPAC RBAC Reader** — note PPAC is **delegated-only**; service principals use PPAC RBAC roles
- **AI Administrator** role (Package API delegated path)
- `Reports.Read.All` (Graph Copilot usage reports — app-only)
- **ARM Reader** (Foundry / Azure OpenAI + Azure Monitor)
- Dataverse Web API access for the test environment (PoC-2)

## 4. Required Entra registrations

- One app registration with the scopes in §3 (certificate credential preferred over client secret).
- Separate interactive (delegated) vs data-plane (app-only) usage where the surface demands it.
- Customer-owned app-registration option is acceptable (consent-tier posture, Stage 1).

## 5. Required licences

- **Microsoft Agent 365** licence (gates the Package Management API).
- **Copilot** licences (Graph usage-report data scope).
- **E5 / E5 Compliance** and **Purview PAYG** (only if Purview AI records are exercised — Gate-2, not
  required for Gate-1).
- Viva ≥50 Copilot/Viva licences (only if prompt-count export is tested — not Gate-1).

## 6. Required admin consent

- Tenant admin consent for the app-registration scopes (app-only + delegated as applicable).
- PPAC RBAC role assignment for the operating principal.
- Consent is a **human action** — the agent will not request it automatically.

## 7. Required sample data (agent archetypes — spec preconditions)

- ≥1 Copilot Studio agent created **pre-March-2026** (legacy SP).
- ≥1 **modern** agent (native Entra Agent ID).
- ≥1 **Agent Builder** agent.
- ≥1 agent **published to M365/Teams**.
- 1 **Foundry** project with one published agent.

## 8. Required test users / roles

- Accounts holding **Power Platform admin + AI Administrator + Global Reader**.
- At least one Copilot-licensed user generating usage signal.

## 9. Required validation criteria (Gate-1 exit)

- PoC-1: deterministic chain holds for modern published agents (→ High confidence, ADR-012); each
  archetype's join behaviour documented.
- PoC-2: a documented manifest-ID retrieval path **or** a confirmed dead end.
- PoC-3: working auth recipe + licence-prerequisite confirmation + coverage list + observed throttling.
- Outputs feed Stage 5 finalisation (alias types per archetype, confidence rule table, native-ID reuse).

## 10. Estimated execution time

- **Two weeks** (timebox per handoff §7), assuming the tenant, licences, archetypes, and consent are
  in place at the start.

## 11. Rollback plan

- PoCs are **pre-build validation**, quarantined in `/poc`; nothing they produce enters `/src`
  (architecture-enforced). Rollback = discard `/poc` findings + revoke the app registration/consent.
- No production data, no shared-environment migrations, no deployment are involved. Microsoft provider
  adapters, when built, are ordinary providers behind the existing contracts and are revertible by PR.

## Status

**STOP — awaiting Arun to provision the Microsoft tenant** (§3–§8). On provisioning, execute the Gate-1
PoCs and implement the Microsoft providers behind the existing provider contracts. No blueprint change.
