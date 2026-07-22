---
id: P6-T07
title: Execute tenant-scoped provider sweep jobs through the worker
type: task-contract
schema_version: 1
epic: EPIC-6-1
phase: PHASE-6
status: complete
objective: Complete the existing C4 background-job seam by emitting tenant-scoped provider sweep requests to the outbox and executing them in the worker through the provider registry and invariant ingestion pipeline, with idempotent retry and no in-process production scheduler.
blueprint_refs:
  - docs/blueprint/stage-04-domain-model.md#2.2
  - docs/blueprint/stage-07-conceptual-architecture.md#4
  - docs/blueprint/stage-07-conceptual-architecture.md#6
  - docs/blueprint/stage-09-technology-deployment.md#5.1
adr_refs: [ADR-007, ADR-015, ADR-020, ADR-023]
rtm_refs: [BR-01]
allowed_files:
  - src/Modules/ControlTower.Modules.Providers/**
  - src/Host/ControlTower.Host.Worker/**
  - tests/ControlTower.Modules.Providers.Tests/**
  - docs/build/**
  - STATUS.md
forbidden_files: [docs/blueprint/**, docs/build/approvals/**]
preconditions:
  - Provider framework and observation ingestion are complete
  - Event store, outbox, worker dispatcher, and tenant context are complete
required_tests:
  - request emits one self-contained tenant-scoped sweep job
  - worker resolves connection and provider then executes invariant ingestion
  - completed-job replay is idempotent
  - failed jobs release their claim for bounded external retry
  - tenant and provider failures do not cross boundaries
security_checks:
  - job payload contains no credentials or connection settings
  - tenant scope is re-established from a validated message
  - provider access occurs only through C4 registry
migration_impact: none
acceptance_criteria:
  - ProviderConnection stores credential references only and declares surface capability schedule and enabled state
  - Sweep request enters the hash-chained event store and outbox with no secret material
  - Worker handler loads the tenant connection resolves the registered provider and invokes ObservationIngestionService
  - Job receipt claim prevents completed replay and releases on failure
  - Scheduling remains an external Azure Service Bus adapter responsibility; no durable in-process scheduler is introduced
  - Backend build tests architecture formatting and dependency gates pass
  - No new bounded context provider-specific rule Microsoft assumption or blueprint change
evidence_required: [docs/build/evidence/EVIDENCE-P6-T07.md]
rollback: Revert the PR; provider framework and direct ingestion service remain unchanged.
requires_human_approval: false
approved_by: Product Owner autonomous implementation mandate, 2026-07-22
approved_hash: null
---

## Definition of done

A self-contained sweep job moves through event store to outbox to host-composed worker handler, then
re-enters the correct tenant and calls the existing C4 invariant ingestion pipeline. Claims make replay
safe; failure releases for bounded Service Bus retry/DLQ. No timer substitutes for Azure scheduling.
