---
id: P4-T01
title: Governance Orchestration (C2) — GovernanceCase lifecycle, tiered approvals, reviews, waivers, retirement, reuse decisions
type: task-contract
schema_version: 1
epic: EPIC-4-1
phase: PHASE-4
status: complete
objective: Implement C2 within the existing Governance module and the blueprint's GovernanceCase socket — risk-based intake, tiered approval routing with low-risk auto-approval, reviewer decisions with evidence, recertification, waivers with time-bound expiry, retirement, ownership governance debt, reuse/extend/compose/build-new decision recording, native-control orchestration as contracts only, notifications as domain intents, SLA tracking, and audit events — without duplicating Ledger lifecycle state, building a workflow engine, or enforcing security.
blueprint_refs:
  - docs/blueprint/stage-04-domain-model.md#10
  - docs/blueprint/stage-11-operating-model-roadmap-commercialization.md
  - docs/blueprint/stage-02-capability-model.md#66
adr_refs: [ADR-002, ADR-015, ADR-018, ADR-020, ADR-021]
rtm_refs: [BR-11, BR-12, BR-15]
allowed_files:
  - src/Modules/ControlTower.Modules.Governance/**
  - src/Host/ControlTower.Host.Web/**
  - tests/ControlTower.Modules.Governance.Tests/**
  - docs/build/**
  - STATUS.md
forbidden_files: [docs/blueprint/**, docs/build/approvals/**]
migration_impact: none
acceptance_criteria:
  - GovernanceCase aggregate (typed cases; records decisions that trigger — never duplicate — Ledger transitions)
  - Risk-based intake + tiered approval routing; low-risk auto-approval (registration in minutes; Flag-Never-Block)
  - Business/Technical/Security/Privacy/Finance/Governance reviewers; evidence-backed decisions (actor/reason/evidence/timestamp/outcome preserved)
  - Reviews + recertification (time-bound), exceptions/waivers (time-bound expiry), retirement/decommissioning
  - Ownership governance debt (Ownerless / LapsedOwner) as first-class, surfaced not blocking
  - Reuse/Extend/Compose/Build-New decision recording with justification + outcome
  - Native-control orchestration contracts only (no enforcement in C2); notifications as domain intents only
  - SLA tracking; audit events for every action; tenant-isolated read models
  - No workflow engine, no security enforcement, no model gateway, no new Decision-Intelligence context, no Ledger lifecycle duplication, no blueprint change
evidence_required: [docs/build/evidence/EVIDENCE-P4-T01.md]
rollback: Revert the PR; the Governance module falls back to its marker.
requires_human_approval: false # tenant-independent; emergent within C2; merge-train policy
approved_by: Arun (Priority 4 approval + merge-train standing approval, 2026-07-17)
---

## Definition of done

C2 governance implemented within the existing module; 64 tests green; CI green; Merge Readiness Report posted.
