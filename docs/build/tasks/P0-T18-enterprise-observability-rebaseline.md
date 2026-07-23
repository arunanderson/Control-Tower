---
id: P0-T18
title: Rebaseline V1 for enterprise-wide observable AI coverage
type: task-contract
schema_version: 1
epic: EPIC-0-1
phase: PHASE-0
status: complete
objective: Record the Product Owner's approved V1 scope amendment for one Control Tower spanning all technically observable AI use across the corporate-managed estate, without changing the frozen architecture or creating a bounded context.
blueprint_refs:
  - docs/blueprint/decision-log.md#adr-003--privacy-by-design-with-configurable-telemetry-levels
  - docs/blueprint/decision-log.md#adr-007--telemetry-strategy-pluggable-providers-native-first-v1-no-custom-collectors-in-v1
  - docs/blueprint/decision-log.md#adr-009--eight-bounded-context-domain-model
  - docs/blueprint/decision-log.md#adr-010--ai-activity-intelligence-dissolved-as-a-domain
  - docs/blueprint/stage-07-conceptual-architecture.md#8-extension-points--provider-plug-in-model-c45-adr-007
  - docs/blueprint/stage-08-security-trust-architecture.md#12-privacy-enforcement-architecture-adr-0030140159-operationalised
adr_refs:
  [ADR-003, ADR-007, ADR-009, ADR-010, ADR-014, ADR-020, ADR-021, ADR-023]
rtm_refs: [BR-02, BR-04, BR-09, BR-10, BR-13, BR-14, BR-15]
allowed_files:
  - BLOCKED.md
  - STATUS.md
  - docs/build/**
forbidden_files:
  - docs/blueprint/**
  - docs/build/approvals/**
  - src/**
  - tests/**
  - web/**
  - infra/**
preconditions:
  - Product Owner explicitly confirmed that V1 must cover all technically observable AI use across corporate-managed laptops, desktops, browsers, identities, networks, SaaS, cloud, agents and APIs
  - Product Owner explicitly authorised continuation on 2026-07-23
required_tests:
  - task-contract validation passes
  - build-control Markdown and YAML formatting passes
  - protected blueprint and approval paths remain untouched
security_checks:
  - L1 aggregate-only remains the default and prompt or response content is not collected by default
  - L2 or higher employee-linked telemetry remains policy-gated, purpose-bound, jurisdiction-aware and privileged-read audited
  - first-party collectors are acquisition adapters only and do not become endpoint-security, SIEM or employee-scoring products
migration_impact: none
acceptance_criteria:
  - DEV-002 records the approved ADR-007 V1 scope amendment and its conditions
  - one delivery plan covers endpoint browser identity network SaaS cloud agent API finance and governance signals through existing contexts
  - persona views are projections through C7 rather than new bounded contexts
  - coverage language promises all technically observable corporate-managed activity and exposes blind spots rather than claiming unfalsifiable total visibility
  - repository status reflects PR 20 sandbox findings and the production-readiness critical path
  - frozen blueprint product code tests web and infrastructure are untouched
evidence_required: [docs/build/evidence/EVIDENCE-P0-T18.md]
rollback: Revert the documentation-only PR; the frozen blueprint and merged implementation remain unchanged.
requires_human_approval: true
approved_by: Product Owner direct enterprise-visibility clarification and instruction to continue, 2026-07-23
approved_hash: null
---

## Objective

Turn the Product Owner's clarified outcome into a bounded, traceable implementation sequence: one
Control Tower for role-appropriate visibility across the corporate-managed estate, using the existing
C4 ingestion door, C1/C2/C3/C5/C8/C9 capabilities and C7 experience door.

## Steps (bounded, ordered)

1. Record the approved scope amendment and the architectural conditions that keep it inside ADR-009
   and ADR-020.
2. Define the evidence-source coverage model and privacy posture for each acquisition surface.
3. Sequence production foundations before tenant and endpoint collectors.
4. Reconcile `build-state.yaml`, `STATUS.md` and `BLOCKED.md` with merged PR 20 and current gates.
5. Run documentation, contract and protected-path validation and capture evidence.

## Definition of done

The repository—not this conversation—states the corrected product outcome, the approved deviation,
the complete source coverage model, the ordered delivery path and the remaining human gates.

## Rollback

Revert the documentation-only PR.
