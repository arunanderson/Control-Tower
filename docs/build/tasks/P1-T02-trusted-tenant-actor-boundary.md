---
id: P1-T02
title: Establish the trusted tenant and actor request boundary
type: task-contract
schema_version: 1
epic: EPIC-1-2
phase: PHASE-1
status: complete
objective: Authenticate every non-health web request, map an allowed Entra directory tenant to the internal tenant, and derive canonical human actor identity exclusively from validated token claims so caller-controlled headers cannot forge the HTTP tenancy or audit boundary.
blueprint_refs:
  - docs/blueprint/stage-08-security-trust-architecture.md#3-tenant-isolation-model
  - docs/blueprint/stage-08-security-trust-architecture.md#4-identity-model
  - docs/blueprint/stage-08-security-trust-architecture.md#5-authentication-boundaries
  - docs/blueprint/stage-09-technology-deployment.md#4-identity-isolation-secrets
adr_refs: [ADR-001, ADR-015, ADR-020, ADR-021, ADR-023]
rtm_refs: [BR-10, BR-15]
allowed_files:
  - src/Host/ControlTower.Host.Web/**
  - tests/ControlTower.Host.Web.Tests/**
  - docs/build/**
  - STATUS.md
forbidden_files:
  - docs/blueprint/**
  - docs/build/approvals/**
  - src/ControlTower.Platform/**
  - src/Modules/**
  - src/Adapters/**
  - src/Host/ControlTower.Host.Worker/**
  - web/**
  - db/**
  - infra/**
preconditions:
  - PR 21 is merged and DEV-002 production sequencing is authoritative
  - Existing ambient tenant context remains the tenant-scoping mechanism
  - Authentication tests use local signing material only and require no Microsoft tenant action
  - The official Microsoft.AspNetCore.Authentication.JwtBearer 8.0 package is approved as the implementation of blueprint-mandated Entra authentication under the Product Owner's production-foundation mandate
  - The official Microsoft.IdentityModel.Validators 8.19.2 package is explicitly approved by the Product Owner on 2026-07-23 for Microsoft Entra multi-tenant signing-key issuer validation
required_tests:
  - health and readiness remain anonymous
  - whoami and every API endpoint return 401 without authentication
  - a correctly signed delegated token with valid issuer audience lifetime tid oid and sub maps to the internal tenant
  - missing malformed empty or duplicate tid oid or sub is rejected with a generic 401
  - issuer and tid mismatch is rejected
  - a signing key scoped by Microsoft metadata to one Entra tenant cannot validate a token from another tenant
  - a correctly signed token from a tenant absent from the server-side tenant mapping is rejected
  - two external directories cannot map to the same internal tenant
  - app-only tokens are rejected from the human Experience boundary
  - a delegated token without the exact configured Control Tower scope is rejected
  - X-Tenant-Id cannot override the validated tid claim
  - X-Operator and X-Actor cannot override the validated actor claim
  - privileged reads keep purpose as explicit business context and audit the token-derived actor
  - cross-tenant resource access remains concealed
  - issuer audience signature and expiry validation failures are rejected
security_checks:
  - production authentication uses the official ASP.NET Core JWT bearer handler
  - the Microsoft-supported signing-key issuer validator binds OIDC JWK issuer metadata to the token issuer and tenant
  - OIDC metadata and issuer templates are HTTPS-only and authority-host compatible
  - inbound claim mapping is disabled so tid oid and sub semantics are explicit
  - tenant scope opens only after successful signature audience lifetime and issuer validation
  - a server-side allowlist maps the external directory tid to the internal TenantId
  - external-to-internal tenant mapping is one-to-one
  - the canonical human actor is entra tenant id plus oid; sub is required signed provenance and never a fallback identity
  - actor purpose and approval-reference values are bounded and control characters are rejected
  - authentication failures disclose no tenant or resource existence
  - no token signing material secret tenant identifier or production credential is committed
migration_impact: none
acceptance_criteria:
  - X-Tenant-Id X-Operator and X-Actor are not trusted anywhere in Host.Web
  - a non-empty validated tid resolves through an allowed-tenant mapping before the internal tenant scope opens
  - actor is canonical entra tenant id plus the validated non-empty GUID oid with a required signed sub claim
  - tokens without delegated scope are rejected from the human Experience boundary
  - health and readiness are the only anonymous mapped endpoints
  - purpose and approval reference remain caller-supplied business data rather than identity
  - local signed-token tests prove valid and adversarial request boundaries
  - command endpoints remain Development-only until role and capability authorisation is implemented
  - backend build tests architecture formatting dependency and security gates pass
  - no module boundary frozen blueprint tenant permission infrastructure or production deployment change
evidence_required: [docs/build/evidence/EVIDENCE-P1-T02.md]
rollback: Revert the PR; no datastore migration or tenant configuration is changed.
requires_human_approval: true
approved_by: Product Owner instruction to continue the approved production-foundation critical path and explicit approval of Microsoft.IdentityModel.Validators 8.19.2, 2026-07-23
approved_hash: null
---

## Objective

Replace the development-only caller-controlled identity boundary with a validated, testable Entra JWT
boundary. Preserve the existing ambient tenant context and all domain/module APIs.

## Steps (bounded, ordered)

1. Add the official ASP.NET Core JWT bearer handler and explicit multi-tenant issuer validation.
2. Resolve exactly one `tid`, `oid` and `sub`, reject app-only tokens, and map the directory tenant
   through a server-side allowed-tenant registry.
3. Open the ambient tenant scope and bind a canonical actor from that validated request context, then
   require authorisation on `/whoami` and `/api`.
4. Replace operator and privileged-read actor headers with the authenticated actor.
5. Convert host integration tests to locally signed tokens and add adversarial boundary coverage.
6. Run all gates, capture evidence and reconcile build state.

## Definition of done

A forged request header cannot choose the tenant or the audit actor; only a cryptographically
validated principal can enter a tenant-scoped web operation.

## Rollback

Revert the PR.
