---
id: P0-BOOTSTRAP
title: Build the Phase-0 build-control rails and CI trust boundary
type: task-contract
schema_version: 1
epic: EPIC-0-1
phase: PHASE-0
status: complete
objective: Establish DB-agnostic rails (CLAUDE.md, .claude config, docs/build structure, templates, state) and the CI gate set before any feature code — the sanctioned single human-reviewed bootstrap PR.
blueprint_refs:
  - docs/automation/automation-operating-model.md#1
  - docs/automation/automation-operating-model.md#7
  - docs/automation/automation-operating-model.md#12
adr_refs: [ADR-020, ADR-021, ADR-023, PD-006]
rtm_refs: [BR-10, BR-15]
allowed_files:
  - CLAUDE.md
  - .gitignore
  - .claude/**
  - docs/build/**
  - .github/**
  - scripts/ci/**
  - STATUS.md
forbidden_files:
  - docs/blueprint/**
  - docs/build/approvals/**
preconditions:
  - Gate-0 approved
  - Phase-0 plan approved
  - DEV-001 approved-with-conditions
required_tests:
  - "python3 scripts/ci/validate_task_contracts.py --check"
  - "bash -n scripts/ci/*.sh"
  - "scripts/ci/validate_protected_paths.sh (dry-run on this branch)"
security_checks:
  - "no secrets committed (gitignore covers .env, keys, secrets)"
  - "deny-writes to docs/blueprint + docs/build/approvals in .claude/settings.json"
migration_impact: none
acceptance_criteria:
  - Root and docs/build CLAUDE.md exist with invariants + DEV-001 policy
  - .claude/settings.json denies writes to blueprint + approvals + workflows
  - docs/build structure + templates + build-state.yaml present
  - Six CI workflows present (format, secret-scan, dependency-scan, protected-paths, task-contract-validation, architecture-gate) plus production-readiness gate
  - CI validators pass locally on this repo
  - Zero product/application code; zero runtime dependencies added
evidence_required:
  - docs/build/evidence/EVIDENCE-P0-BOOTSTRAP-rails.md
rollback: Revert the bootstrap PR; nothing depends on it yet.
requires_human_approval: true
approved_by: Arun (Phase-0 plan approval, 2026-07-16)
approved_hash: null
---

## Objective

Create the rails and CI gates so every later task runs inside an enforced, evidenced, gate-controlled
process — "build the rails before the train."

## Steps

1. Root `CLAUDE.md` + `docs/build/CLAUDE.md` (invariants, DEV-001 policy, gates).
2. `.claude/settings.json` deny-rules; minimal commands + reviewer agents.
3. `docs/build/{plans,epics,tasks,state,evidence,approvals,deviations,decisions,risks,reviews,templates}` + `build-state.yaml`.
4. CI workflows + `scripts/ci` validators, incl. DEV-001 `production-readiness` gate.
5. Validate locally; capture evidence; update state + STATUS.

## Definition of done

All acceptance criteria met; CI validators green locally; PR opened (not merged).

## Rollback

Revert PR; no dependents.
