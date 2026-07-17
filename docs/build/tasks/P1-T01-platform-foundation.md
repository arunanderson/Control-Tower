---
id: P1-T01
title: Platform foundation — host composition, tenancy middleware, background dispatcher, dev adapters
type: task-contract
schema_version: 1
epic: EPIC-1-1
phase: PHASE-1
status: complete
objective: Make the hosts runnable and tenant-scoped — DI composition, per-request tenancy middleware, an outbox-draining worker, and dev-only in-memory port adapters behind the DEV-001 seams — with integration tests.
blueprint_refs:
  - docs/blueprint/stage-08-security-trust-architecture.md#3
  - docs/blueprint/stage-07-conceptual-architecture.md#6
  - docs/blueprint/stage-09-technology-deployment.md#2
adr_refs: [ADR-020, ADR-021, ADR-023]
rtm_refs: [BR-10]
allowed_files:
  - src/ControlTower.Platform/**
  - src/Adapters/**
  - src/Host/**
  - tests/**
  - docs/build/**
  - STATUS.md
forbidden_files: [docs/blueprint/**, docs/build/approvals/**]
migration_impact: none
acceptance_criteria:
  - AddControlTowerPlatform DI extension registers the tenant context accessor
  - Per-request TenantResolutionMiddleware opens/reverts an ambient tenant scope
  - Dev-only in-memory adapters (event store, outbox, privileged-read auditor, secret provider) behind the ports; registered only in Development
  - OutboxDispatcher background service drains + acknowledges the outbox
  - Architecture tests: kernel + modules must not depend on adapters
  - Integration tests: /health 200 without tenant; /whoami 400 without tenant header, 200 within a tenant scope
  - Build 0/0; all tests green; 0 vulnerable packages; no live tenant
evidence_required: [docs/build/evidence/EVIDENCE-P1-T01.md]
rollback: Revert the PR; hosts fall back to skeleton.
requires_human_approval: false # tenant-independent; merge-train policy
approved_by: Arun (merge-train standing approval, 2026-07-17)
---

## Definition of done

Runnable, tenant-scoped hosts with a draining worker and dev adapters behind the ports; 18 tests green; CI green; Merge Readiness Report posted.
