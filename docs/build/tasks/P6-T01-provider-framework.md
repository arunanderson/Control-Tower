---
id: P6-T01
title: Provider Integration (C4.5) — provider framework, contracts, registry, harness (Phase A, tenant-independent)
type: task-contract
schema_version: 1
epic: EPIC-6-1
phase: PHASE-6
status: complete
objective: Build the complete, tenant-independent provider framework (ADR-007 / C4.5) so any future provider — Microsoft, CSV, OpenAI, Anthropic, Google, ServiceNow — implements the same contracts. Provide the registry, manifest/capability/health/versioning/contract-validation/discovery/lifecycle/diagnostics/metadata models, identifier mapping, capability negotiation, auth/authz abstraction, sync-scheduling and freshness/coverage/error models, the Manual CSV provider (ADR-013), the provider test harness, and the provider integration-test framework. No provider-specific business logic outside a provider implementation. Phase B (Microsoft providers + Gate-1 PoCs) STOPS at the tenant gate.
blueprint_refs:
  - docs/blueprint/stage-07-conceptual-architecture.md
  - docs/blueprint/poc-gate1-specifications.md
  - docs/blueprint/stage-03-microsoft-validation.md
adr_refs: [ADR-007, ADR-013, ADR-012, ADR-020]
rtm_refs: [BR-05]
allowed_files:
  - src/Modules/ControlTower.Modules.Providers/**
  - src/Host/ControlTower.Host.Web/**
  - tests/ControlTower.Modules.Providers.Tests/**
  - ControlTower.sln
  - docs/build/**
  - STATUS.md
forbidden_files: [docs/blueprint/**, docs/build/approvals/**]
migration_impact: none
acceptance_criteria:
  - Provider contracts (IProvider, manifest, capability, health, versioning) with contract validation, discovery, lifecycle, diagnostics, metadata
  - Identifier mapping + capability negotiation + auth/authz abstraction + sync scheduling + freshness + coverage + error models
  - Provider registry validates on register, rejects duplicates and non-conforming manifests
  - Manual CSV provider (ADR-013) labels output "Self-reported / Manual Import"
  - Provider test harness runs any provider through identical contract checks (conforming passes; broken fails); integration-test framework in place
  - Framework supports future providers without changing the domain model; no provider-specific logic outside a provider
  - Read-only /api/admin/providers discovery endpoint (manifests only), tenant-gated
  - Backend build 0/0; full test suite green; 0 vulnerable production packages
  - No Microsoft/Graph/Copilot/Entra code; no Gate-1 PoC execution; no permissions requested; no blueprint change
  - Phase B deliverable produced (Provider Integration Readiness Report + Gate-1 Execution Plan + required provisioning)
evidence_required: [docs/build/evidence/EVIDENCE-P6-T01.md]
rollback: Revert the PR; the Providers module + its host wiring + the discovery endpoint are removed; no other module is affected.
requires_human_approval: false # Phase A is tenant-independent; emergent within C4; merge-train policy. Phase B is a hard human gate.
approved_by: Arun (Priority 6 approval + merge-train standing approval, 2026-07-17)
---

## Definition of done

Complete C4.5 provider framework + Manual CSV provider + test harness, wired into the host with a
read-only discovery endpoint; 93 backend tests green; 0 vulnerable production deps; the Phase B
Provider Integration Readiness Report produced; Merge Readiness Report posted. **Phase B STOPS at the
Microsoft-tenant gate** — no Microsoft providers, no Gate-1 execution, no permission requests.
