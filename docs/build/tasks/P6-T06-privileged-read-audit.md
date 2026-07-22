---
id: P6-T06
title: Enforce and surface privileged-read auditing
type: task-contract
schema_version: 1
epic: EPIC-6-1
phase: PHASE-6
status: complete
objective: Complete the existing C9/C7 privileged-read audit seam with tenant-scoped immutable records, endpoint metadata enforcement, and a customer-visible Trust-area log, without misclassifying current L1 views as privileged.
blueprint_refs:
  - docs/blueprint/stage-04-domain-model.md#9
  - docs/blueprint/stage-06-experience-architecture.md#9
  - docs/blueprint/stage-08-security-trust-architecture.md#12
adr_refs: [ADR-015, ADR-019, ADR-020, ADR-021]
rtm_refs: [BR-04, BR-11]
allowed_files:
  - src/Modules/ControlTower.Modules.Audit/**
  - src/Host/ControlTower.Host.Web/**
  - tests/ControlTower.Host.Web.Tests/**
  - web/**
  - docs/build/**
  - STATUS.md
forbidden_files: [docs/blueprint/**, docs/build/approvals/**]
preconditions:
  - Event backbone and tenant context are complete
  - C7 Trust area exists
required_tests:
  - privileged endpoint metadata records actor purpose resource and time
  - records are append-only and tenant-isolated
  - unmarked L1 reads do not create privileged-read records
  - Trust API and SPA expose the customer-visible log
security_checks:
  - tenant context is mandatory
  - record enters the hash-chained event store
  - no cross-tenant enumeration
migration_impact: none
acceptance_criteria:
  - A reusable C7 endpoint filter audits every endpoint explicitly marked as privileged-read
  - Audit records contain tenant actor purpose resource occurredAt and correlation identifier
  - The privileged-access log is customer-visible through a tenant-scoped read model
  - Existing L1 endpoints remain unaudited until their clearance changes
  - Backend and SPA tests builds formatting architecture and dependency gates pass
  - No new bounded context aggregate blueprint change or invented L2 data surface
evidence_required: [docs/build/evidence/EVIDENCE-P6-T06.md]
rollback: Revert the PR; C9 returns to its skeleton and no existing product behavior changes.
requires_human_approval: false
approved_by: Product Owner autonomous implementation mandate, 2026-07-22
approved_hash: null
---

## Definition of done

C9 stores tenant-partitioned append-only privileged-read records and their immutable domain events;
C7 applies a reusable explicit-clearance filter and exposes the customer-visible Trust log. Current
aggregate-only L1 views do not generate false privileged-read claims.
