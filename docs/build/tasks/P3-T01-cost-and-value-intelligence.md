---
id: P3-T01
title: Cost & Value Intelligence (C3) — one economics model, many ROI read models
type: task-contract
schema_version: 1
epic: EPIC-3-1
phase: PHASE-3
status: complete
objective: Build the C3 economics domain within the existing Economics module — cost/usage observations, value declarations with the six evidence classes and the Finance validation ladder, allocation + Unattributed (never spread), reporting periods/snapshots, ROI calculations, and read models (asset, agent, department, business unit, portfolio, executive) as projections over ONE semantic model. Agent/Department/Portfolio ROI are projections, not modules.
blueprint_refs:
  - docs/blueprint/stage-10-economics-methodology.md
  - docs/blueprint/stage-04-domain-model.md#2.6
  - docs/blueprint/stage-02-capability-model.md#81
adr_refs: [ADR-024, ADR-025, ADR-016, ADR-015, ADR-020, ADR-021]
rtm_refs: [BR-04, BR-05, BR-06, BR-13]
allowed_files:
  - src/Modules/ControlTower.Modules.Economics/**
  - src/Host/ControlTower.Host.Web/**
  - tests/ControlTower.Modules.Economics.Tests/**
  - docs/build/**
  - STATUS.md
forbidden_files: [docs/blueprint/**, docs/build/approvals/**]
migration_impact: none
acceptance_criteria:
  - Cost + usage observations (immutable) and value declarations (revision chain, never overwritten)
  - Six evidence classes; every economic figure structurally requires source, class, methodology, as-of, validation state
  - Finance validation ladder (Estimated → SystemObserved → BusinessValidated → FinanceVerified), forward-only
  - Cost allocation + Unattributed cost that is never spread
  - ROI calculations: net benefit, ROI (range + confidence mix, single-point suppressed >25% soft), validated-only ROI, payback, trailing-12-month
  - Read models: Asset Economics, Agent ROI, Department ROI, Business Unit ROI, Portfolio ROI, Executive dashboard — all from one model; Agent ROI is a filter, no module
  - Reporting periods + snapshots; projections reproducible for any historical as-of
  - Tenant-scoped; no live tenant; no new bounded context/aggregate beyond blueprint; no blueprint change
  - Tests prove no economic figure is exposed without evidence class, source, methodology, and as-of
evidence_required: [docs/build/evidence/EVIDENCE-P3-T01.md]
rollback: Revert the PR; the Economics module falls back to its marker.
requires_human_approval: false # tenant-independent; emergent within C3; merge-train policy
approved_by: Arun (Priority 3 approval + merge-train standing approval, 2026-07-17)
---

## Definition of done

One economics semantic model with six-class evidence enforced structurally, the Finance validation ladder, Unattributed-never-spread, ROI honesty rules, and all six read models as projections; 47 tests green; CI green; Merge Readiness Report posted.
