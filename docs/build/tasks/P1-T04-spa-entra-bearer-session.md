---
id: P1-T04
title: Connect the SPA to Entra bearer authentication and server-resolved access
type: task-contract
schema_version: 1
epic: EPIC-1-2
phase: PHASE-1
status: complete
objective: Authenticate the existing C7 React SPA through Microsoft Entra authorization-code with PKCE, attach only delegated API bearer tokens, and drive its tenant, areas, reads and actions exclusively from the server-resolved whoami capability projection.
blueprint_refs:
  - docs/blueprint/stage-02-capability-model.md#c7--experience--insight-supporting
  - docs/blueprint/stage-02-capability-model.md#c8--trust--access-supporting
  - docs/blueprint/stage-06-experience-architecture.md#9-role-based-experience-rules
  - docs/blueprint/stage-08-security-trust-architecture.md#4-identity-model
  - docs/blueprint/stage-08-security-trust-architecture.md#5-authentication-boundaries
  - docs/blueprint/stage-08-security-trust-architecture.md#6-authorization-model
  - docs/blueprint/stage-09-technology-deployment.md#4-identity-isolation-secrets
adr_refs: [ADR-019, ADR-020, ADR-021, ADR-023]
rtm_refs: [BR-08, BR-10, BR-15]
allowed_files:
  - web/package.json
  - web/package-lock.json
  - web/src/**
  - web/vite.config.ts
  - web/tsconfig.json
  - docs/build/**
  - STATUS.md
forbidden_files:
  - docs/blueprint/**
  - docs/build/approvals/**
  - src/**
  - tests/**
  - db/**
  - infra/**
  - poc/**
preconditions:
  - P1-T02 and P1-T03 are merged; Host.Web validates the human tenant and exposes server-resolved effective access through whoami
  - Existing Experience APIs remain Development-only while durable adapters are absent
  - The Product Owner explicitly approved adding @azure/msal-browser 5.17.1 on 2026-07-23
  - The SPA and API remain same-origin; local development may use a narrowly scoped Vite proxy instead of enabling wildcard CORS
  - Microsoft tenant app registration redirect URI scope consent and live configuration remain a later human gate
required_tests:
  - missing malformed or unsafe public authentication configuration fails before MSAL or the protected SPA starts
  - MSAL initializes and handles a redirect exactly once before React renders including under StrictMode
  - no cached account renders an explicit signed-out state and performs no API call
  - one cached or redirect-returned account is selected deterministically while multiple unselected accounts fail closed to explicit selection
  - silent token acquisition requests exactly the configured Control Tower delegated API scope
  - interaction-required token failure never starts an effect-driven login loop and exposes one explicit reauthentication action
  - every whoami and API request carries a bearer access token and no caller-controlled tenant actor operator role group or capability header
  - token acquisition failure performs no network request; 401 403 404 and server/network failures remain distinct and disclose no token or response body
  - whoami completes before API reads and is the only source for displayed tenant roles capabilities and organisation scope
  - malformed unknown or empty effective access fails closed without issuing protected data requests
  - visible areas actions and requests match the server capability projection including non-hierarchical Viewer Operator Administrator and Executive-scope behavior
  - logout is account-specific and clears protected UI state
security_checks:
  - use only the official @azure/msal-browser 5.17.1 package; do not hand-roll OAuth or add @azure/msal-react
  - use authorization-code with PKCE and a public SPA client with no client secret
  - use the organizations authority exact delegated API scope sessionStorage and same-origin API requests
  - initialize MSAL and complete redirect handling before rendering protected React state
  - never persist log decode or use token claims for authorization; tokens remain MSAL-managed
  - never send an ID token to the API
  - tenant role capability and organisation scope come only from the validated whoami response
  - 401 requires explicit reauthentication while 403 remains a signed-in authorization denial
  - retain only bounded business-context headers where an endpoint requires them
  - no CORS relaxation secret tenant action production credential infrastructure or frozen-blueprint change is introduced
migration_impact: none
acceptance_criteria:
  - the SPA has no random local tenant state and sends no X-Tenant-Id X-Actor or equivalent identity header
  - a single testable authentication composition adapter owns MSAL initialization account selection token acquisition reauthentication and logout
  - the API client acquires a fresh cached access token for every call and attaches only Authorization Bearer plus legitimate content or business-context headers
  - whoami bootstraps the session before any area data is requested
  - the SPA renders and fetches only areas and actions supported by the server capability projection while the server remains the authorization authority
  - unauthenticated no-access forbidden and transient-failure states are explicit generic and non-looping
  - a development same-origin proxy is bounded to whoami and api paths
  - SPA typecheck tests production build formatting lockfile and dependency security gates pass
evidence_required: [docs/build/evidence/EVIDENCE-P1-T04.md]
rollback: Revert the PR; no tenant configuration app registration consent datastore migration infrastructure or production deployment is changed.
requires_human_approval: true
approved_by: Product Owner explicit approval on 2026-07-23
approved_hash: null
---

## Objective

Replace the C7 SPA's obsolete caller-controlled development identity with the frozen C8.1 Entra
federation path and the P1-T02/P1-T03 server session boundary. Client capability filtering is an
experience affordance only; Host.Web remains authoritative for every request.

## Steps (bounded, ordered)

1. Add the single approved MSAL Browser dependency and validate public SPA configuration.
2. Bootstrap one MSAL public client, complete redirect handling, and model explicit account,
   reauthentication and logout states.
3. Replace tenant/actor headers with per-request delegated bearer token acquisition.
4. Validate and load whoami before any API read, then capability-filter navigation, data acquisition
   and actions without reading token claims.
5. Add the narrowly scoped development proxy and adversarial unit/component tests.
6. Run all gates, capture evidence, reconcile build state and open the tenant-independent PR.

## Definition of done

The existing SPA reaches the Experience API only as an Entra-authenticated human, displays only
server-projected access, and contains no caller-controlled tenant or authorization path.

## Rollback

Revert the PR.
