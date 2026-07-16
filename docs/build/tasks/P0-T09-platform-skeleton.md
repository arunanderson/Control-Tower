---
id: P0-T09
title: Platform skeleton, tenancy context, and architecture-boundary tests (E2)
type: task-contract
schema_version: 1
epic: EPIC-0-2
phase: PHASE-0
status: complete
objective: Stand up the .NET modular-monolith skeleton (Platform + C1–C9 modules + two hosts), an unforgeable tenancy context, and machine-enforced module-boundary tests — all building and passing, wired into CI.
blueprint_refs:
  - docs/blueprint/stage-07-conceptual-architecture.md#10
  - docs/blueprint/stage-09-technology-deployment.md#2
  - docs/blueprint/stage-08-security-trust-architecture.md#3
adr_refs: [ADR-020, ADR-021, ADR-022, ADR-023]
rtm_refs: [BR-10]
allowed_files:
  - ControlTower.sln
  - global.json
  - src/**
  - tests/**
  - .github/workflows/build-test.yml
  - .github/workflows/architecture-gate.yml
  - .github/workflows/dependency-scan.yml
  - scripts/ci/architecture_gate.sh
  - docs/build/**
  - STATUS.md
forbidden_files:
  - docs/blueprint/**
  - docs/build/approvals/**
preconditions:
  - Bootstrap rails present (PR #1 lineage)
  - DEC-001 database engine recorded (Azure PostgreSQL)
  - .NET SDK available (installed user-locally; CI uses setup-dotnet 8.0.x)
required_tests:
  - "dotnet build ControlTower.sln -c Release (0 warnings, 0 errors)"
  - "dotnet test ControlTower.sln (Platform.Tests + ArchitectureTests)"
  - "dotnet list package --vulnerable (none)"
security_checks:
  - "unforgeable tenant context — reading Current outside a scope throws (ADR-021)"
  - "no secrets; adapter seams (secrets/queue/blob/data) defined as ports (DEV-001)"
migration_impact: none
acceptance_criteria:
  - Solution with Platform + 8 modules (C1–C9; C6 vacant) + Host.Web + Host.Worker + 2 test projects
  - Tenancy context enforced by construction (throws without a scope); unit-tested
  - NetArchTest rules: Platform depends on no module; no module depends on another; modules depend on no host
  - Build 0/0; all tests green; no vulnerable packages
  - CI wired: build-test, architecture-gate (real), dependency-scan (real)
  - No live Microsoft tenant required (none used)
evidence_required:
  - docs/build/evidence/EVIDENCE-P0-T09.md
rollback: Revert the E2 PR; the skeleton has no dependents yet.
requires_human_approval: true
approved_by: Arun (Phase-0 plan approval, 2026-07-16)
approved_hash: null
---

## Objective

Create the modular-monolith skeleton and the unforgeable tenancy context, with the R-23 architecture
keystone (module-boundary tests) enforced in CI — no live tenant required.

## Steps

1. Solution + Platform (IModule, TenantId, ITenantContextAccessor, TenantContextAccessor, ports, event abstractions).
2. Eight bounded-context module libraries (Ledger C1 … Audit C9), each referencing only Platform.
3. Host.Web + Host.Worker composition roots.
4. Platform.Tests (tenancy) + ArchitectureTests (NetArchTest boundary rules).
5. Wire CI (build-test, architecture-gate, dependency-scan); validate; capture evidence.

## Definition of done

Build 0/0; 7/7 tests pass; no vulnerable packages; CI wired. PR opened (not merged).

## Rollback

Revert the E2 PR.
