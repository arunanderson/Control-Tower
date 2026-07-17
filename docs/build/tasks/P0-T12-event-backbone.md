---
id: P0-T12
title: Event backbone, outbox, and hash-chain integrity (E3 skeleton)
type: task-contract
schema_version: 1
epic: EPIC-0-3
phase: PHASE-0
status: complete
objective: Add the append-only event store contract, SHA-256 hash chain + verifier, transactional outbox contract, and privileged-read audit hook — production-safe abstractions with tested behaviour, no live tenant.
blueprint_refs:
  - docs/blueprint/stage-04-domain-model.md#9
  - docs/blueprint/stage-07-conceptual-architecture.md#10
  - docs/blueprint/stage-09-technology-deployment.md#3
adr_refs: [ADR-015, ADR-021]
rtm_refs: [BR-15]
allowed_files:
  - src/ControlTower.Platform/**
  - tests/ControlTower.Platform.Tests/**
  - docs/build/**
  - STATUS.md
  - CLAUDE.md
forbidden_files:
  - docs/blueprint/**
  - docs/build/approvals/**
migration_impact: none
acceptance_criteria:
  - Append-only IEventStore (no update/delete in the contract); StoredEvent immutable record
  - Sha256HashChain (pure) + HashChainVerifier detecting tamper/reorder with first-broken position
  - IOutbox contract (enqueue/dequeue/ack) for transactional dispatch
  - IPrivilegedReadAuditor + PrivilegedReadRecord (ADR-015.9, on by default)
  - Build 0/0; tests green (hash determinism, chaining, tamper detection, outbox ordering, audit)
  - No live Microsoft tenant used
evidence_required:
  - docs/build/evidence/EVIDENCE-P0-T12.md
rollback: Revert the E3 PR; no dependents.
requires_human_approval: false # tenant-independent, in the approved Phase-0 plan; merge-train policy
approved_by: Arun (Phase-0 plan + merge-train standing approval, 2026-07-17)
approved_hash: null
---

## Objective

Establish the event/audit integrity foundation (ADR-015/021) as production-safe contracts + pure logic,
validated by tests, with storage implementations provided as test doubles (Azure-backed impls later).

## Definition of done

Build 0/0; Platform.Tests green incl. tamper detection; CI green; Merge Readiness Report posted.

## Rollback

Revert the E3 PR.
