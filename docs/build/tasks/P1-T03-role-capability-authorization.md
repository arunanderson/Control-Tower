---
id: P1-T03
title: Enforce tenant-scoped role and capability authorization
type: task-contract
schema_version: 1
epic: EPIC-1-2
phase: PHASE-1
status: complete
objective: Resolve server-controlled C8 role assignments for the validated human and tenant, derive only curated V1 capabilities, and deny every existing Experience API route unless its explicit capability and tenant-wide V1 organisation scope are satisfied.
blueprint_refs:
  - docs/blueprint/stage-02-capability-model.md#c8--trust--access-supporting
  - docs/blueprint/stage-04-domain-model.md#5-domain-events-the-canonical-set
  - docs/blueprint/stage-05-conceptual-data-model.md#1-entity-register
  - docs/blueprint/stage-06-experience-architecture.md#9-role-based-experience-rules
  - docs/blueprint/stage-08-security-trust-architecture.md#6-authorization-model
adr_refs: [ADR-001, ADR-015, ADR-020, ADR-021, ADR-023]
rtm_refs: [BR-08, BR-10, BR-15]
allowed_files:
  - src/Modules/ControlTower.Modules.Trust/**
  - src/Host/ControlTower.Host.Web/**
  - tests/ControlTower.Host.Web.Tests/**
  - docs/build/**
  - STATUS.md
forbidden_files:
  - docs/blueprint/**
  - docs/build/approvals/**
  - src/ControlTower.Platform/**
  - src/Modules/ControlTower.Modules.Ledger/**
  - src/Modules/ControlTower.Modules.Governance/**
  - src/Modules/ControlTower.Modules.Economics/**
  - src/Modules/ControlTower.Modules.Providers/**
  - src/Modules/ControlTower.Modules.EnterpriseContext/**
  - src/Modules/ControlTower.Modules.Experience/**
  - src/Modules/ControlTower.Modules.Audit/**
  - src/Adapters/**
  - src/Host/ControlTower.Host.Worker/**
  - web/**
  - db/**
  - infra/**
preconditions:
  - P1-T02 is merged and the validated tenant human and canonical actor boundary remains authoritative
  - V1 organisation scope is tenant-wide only; business-unit delegation remains V1.5
  - Existing Experience APIs remain Development-only while only development adapters exist
  - The Product Owner's standing instruction is to continue the production-foundation critical path autonomously
  - ASP.NET Core built-in authorization is sufficient and no new package is introduced
required_tests:
  - anonymous or invalid identities receive 401 while authenticated humans without an assignment receive a generic 403
  - exactly Viewer Operator Administrator and Executive-scope roles are accepted
  - every API route carries exactly one explicit capability requirement
  - Viewer and Executive-scope cannot invoke commands
  - Operator can invoke resolution and reporting-period commands but not administration privileged-access or legal-hold operations
  - Administrator can invoke administration privileged-access and legal-hold operations but has no implicit Operator inheritance
  - Executive-scope receives only the executive aggregate prescribed economics and portfolio drill paths reporting periods and coverage
  - multiple assignments union only capabilities from curated bundles
  - token role group capability and request-header claims cannot grant access
  - role assignments are isolated by internal tenant including when two tenants use the same oid
  - denied privileged reads do not execute require purpose or create audit records
  - role assignment changes append the canonical C8 RoleAssignmentChanged event
  - the Web host bridges ILedgerAuthorizer to C8 capabilities and no longer resolves the permissive authorizer
  - Production still maps no Experience API routes
security_checks:
  - role assignments are resolved from a server-controlled C8 port using the ambient internal tenant and validated oid
  - no direct capability grants custom roles group claims app-role claims or authorization headers are accepted
  - capability bundles are immutable explicit and non-hierarchical
  - tenant-wide is the only representable V1 organisation scope
  - role and organisation authorization complete before purpose or privileged-read audit filters execute
  - authorization failure responses disclose no tenant resource assignment or required-role details
  - platform staff JIT break-glass privacy policy and data-granularity authority are not modelled as customer roles
  - no secret production credential tenant action infrastructure or frozen-blueprint change is introduced
migration_impact: none
acceptance_criteria:
  - C8 owns the RoleAssignment entity assignment reader and immutable role-to-capability catalogue
  - RoleAssignment mutations are tenant-scoped and evented while the development store remains replaceable through a port
  - every existing API endpoint names one fine-grained capability and authorization fails closed
  - the existing whoami response exposes server-resolved effective roles capabilities and TenantWide organisation scope for the later SPA task
  - purpose remains additional bounded business context and never authorizes a request
  - the existing Ledger authorization seam is composed through the same C8 evaluator without a module-to-module dependency
  - SPA bearer acquisition Entra activation durable persistence Gate 2 and delegated administration remain separate tasks
  - backend build tests architecture formatting dependency and security gates pass
evidence_required: [docs/build/evidence/EVIDENCE-P1-T03.md]
rollback: Revert the PR; no datastore migration tenant configuration or external assignment is changed.
requires_human_approval: true
approved_by: Product Owner standing instruction to continue the approved production-foundation critical path autonomously, reaffirmed 2026-07-23
approved_hash: null
---

## Objective

Close the authenticated-but-unrestricted Experience API gap with the frozen C8.2 V1 role model.
Assignments are server-controlled and tenant-scoped; roles are curated bundles; capabilities are
never granted directly; and organisation scope is explicitly tenant-wide.

## Steps (bounded, ordered)

1. Add the C8 RoleAssignment lifecycle, server-controlled port, development store and
   RoleAssignmentChanged event.
2. Define the four frozen V1 roles and the minimum fine-grained capability catalogue required by
   the existing C7 routes.
3. Resolve effective access from active assignments and expose it through the existing whoami
   session projection.
4. Add built-in ASP.NET Core capability policies and assign exactly one to each existing API route.
5. Bridge the existing Ledger authorization port in the Web composition root to C8 without changing
   either module boundary.
6. Prove the role matrix, default-deny, tenant isolation, non-authoritative claims/headers and
   privileged-read ordering with local signed-token tests.
7. Run all gates, capture evidence and reconcile build state.

## Definition of done

An authenticated tenant user can reach only the areas and actions granted by a server-controlled C8
role assignment; missing assignments, unrecognised claims and caller-controlled headers fail closed.

## Rollback

Revert the PR.
