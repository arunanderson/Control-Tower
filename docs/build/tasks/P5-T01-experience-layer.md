---
id: P5-T01
title: Experience Layer (C7) — read-model-only API contracts + React/TS SPA for the five areas
type: task-contract
schema_version: 1
epic: EPIC-5-1
phase: PHASE-5
status: complete
objective: Expose the existing Ledger, Economics, and Governance read models through the approved Portfolio, Economics, Governance, Trust, and Administration experiences — read-model-only (I4), tenant-scoped, honest-data (evidence/confidence/as-of, coverage/freshness, governance debt, recommendation outcomes). No business logic in the UI; no calculations; no provider-specific or Microsoft-specific assumptions.
blueprint_refs:
  - docs/blueprint/stage-06-experience-architecture.md
  - docs/blueprint/stage-07-conceptual-architecture.md#2
  - docs/blueprint/stage-02-capability-model.md#122
adr_refs: [ADR-019, ADR-020, ADR-014, ADR-025]
rtm_refs: [BR-04]
allowed_files:
  - src/Host/ControlTower.Host.Web/**
  - src/Modules/ControlTower.Modules.Ledger/**
  - tests/ControlTower.Host.Web.Tests/**
  - web/**
  - .github/workflows/**
  - .prettierignore
  - .gitignore
  - docs/build/**
  - STATUS.md
forbidden_files: [docs/blueprint/**, docs/build/approvals/**]
migration_impact: none
acceptance_criteria:
  - Complete read-model API contract (/api/*) exposing Portfolio + Asset Record, Economics (executive/portfolio/departments/agents), Governance (cases/debt), Trust (coverage), Administration — tenant-gated, read-model-only
  - C1.6 coverage read model (honest — reports what it cannot see)
  - React/TS SPA (Vite) with the five areas and Executive Dashboard, Portfolio, single polymorphic Asset Record, Economics views, Governance workbench, Trust & Coverage, Administration
  - UI consumes read models only; no domain access; no calculations; no API-contract bypass
  - Evidence/confidence/validation/as-of shown on every economic figure; coverage/freshness honest; governance debt + recommendation outcomes shown; tenant-isolated
  - Backend integration tests + SPA component tests green; production deps clean; web CI wired
  - No provider-specific UI; no Microsoft assumptions; no tenant dependency; no blueprint change
evidence_required: [docs/build/evidence/EVIDENCE-P5-T01.md]
rollback: Revert the PR; the API surface and /web are removed; domain unaffected.
requires_human_approval: false # tenant-independent; emergent within C7; merge-train policy
approved_by: Arun (Priority 5 approval + merge-train standing approval, 2026-07-17)
---

## Definition of done

Read-model-only API + SPA for the five areas; 80 backend tests + 5 SPA tests green; web CI wired; Merge Readiness Report posted.
