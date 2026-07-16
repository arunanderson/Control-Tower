---
id: P0-T00
title: <imperative title>
type: task-contract
schema_version: 1
epic: EPIC-0-0
phase: PHASE-0
status: draft                 # draft | approved | in-progress | blocked | failed | complete
objective: <one sentence>
blueprint_refs: []            # doc#section list
adr_refs: []
rtm_refs: []
allowed_files: []             # globs this task may create/modify
forbidden_files:              # blueprint + approvals are always implicit
  - docs/blueprint/**
  - docs/build/approvals/**
preconditions: []
required_tests: []
security_checks: []
migration_impact: none        # none | authored-not-executed
acceptance_criteria: []       # verifiable statements
evidence_required: []         # artefact paths under docs/build/evidence/
rollback: <how to undo>
requires_human_approval: true
approved_by: null
approved_hash: null
---

## Objective
## Steps (bounded, ordered)
## Definition of done
## Rollback
