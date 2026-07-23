---
id: EVIDENCE-P1-T04
type: evidence-bundle
schema_version: 1
task: P1-T04
status: submitted
produced_at: 2026-07-23T12:55:48Z
---

## Task

Authenticate the existing C7 React SPA through Microsoft Entra authorization-code with PKCE, send
only delegated API bearer tokens, and derive the visible tenant, roles, capabilities, areas, reads
and actions exclusively from the server-resolved `/whoami` projection.

## Blueprint and decision trace

- Stage 2 C7/C8 and Stage 8 §§4–6: Microsoft Entra authenticates the human; C8 remains the
  authorization authority and C7 renders only policy-filtered experiences.
- Stage 6 §9: Viewer, Operator, Administrator and Executive-scope remain independent curated roles.
  The SPA does not reproduce their role bundles; it consumes the effective capabilities returned by
  C8.
- Stage 9 §4 and ADR-019/020/021/023: the browser uses an official public-client library, no client
  secret, same-origin C7 APIs, server-established tenancy and strict tenant isolation.
- No bounded context, API route, server authorization rule, infrastructure, migration, production
  credential, tenant configuration or frozen-blueprint file changed.

## Dependency approval

The Product Owner explicitly approved `@azure/msal-browser` version `5.17.1` on 2026-07-23.

```text
@azure/msal-browser 5.17.1
└── @azure/msal-common 16.11.2
```

Both versions are locked by `web/package-lock.json`. No React authentication wrapper, OAuth
implementation, client secret or additional runtime package was introduced.

## What changed

- Added one MSAL adapter that validates public configuration before constructing MSAL, fixes the
  authority to `https://login.microsoftonline.com/organizations`, uses the exact
  `api://<application-id>/controltower.access` delegated scope and keeps MSAL state in
  `sessionStorage`.
- Initialization and redirect completion are one single-flight bootstrap and finish before the API
  client or protected React tree is created. No effect starts an interactive flow.
- Zero cached accounts renders signed out. One cached or redirect account is deterministic.
  Multiple unselected accounts require an explicit sanitized selection; indistinguishable cached
  labels delegate selection to Microsoft's `select_account` experience.
- Silent acquisition returns only a non-empty Bearer access token. An MSAL interaction-required
  result is preserved as a distinct, button-driven reauthentication state, including an in-memory
  claims challenge. Transient acquisition failures expose Retry and Sign out without redirecting.
- Logout is account-specific and clears the API session plus all protected React state before the
  redirect promise completes.
- Replaced the caller-controlled random tenant and identity headers with a thin same-origin client.
  Every `/whoami` and `/api` request acquires a fresh MSAL-managed token and sends
  `Authorization: Bearer`; no tenant, actor, operator, role, group or capability header is sent.
- Every protected fetch forces `cache: no-store`, `credentials: omit`, `redirect: error` and
  `referrerPolicy: no-referrer`. This prevents an account switch from reading an HTTP-cached prior
  session and prevents ambient cookies, redirects or referrers from widening the bearer boundary.
- `/whoami` is single-flight, completes before every data read and is runtime-validated against the
  exact public role/capability vocabulary, UUID tenants, canonical Entra actor and `TenantWide`
  organisation scope. Unknown, duplicate, empty or malformed access fails before data requests.
- Area navigation, requests, optional sections and the resolution command are capability-filtered.
  Missing authorization is omitted rather than rendered as an authoritative empty result.
- 401 clears the cached session and offers explicit reauthentication. 403 preserves the signed-in
  authorization denial. Not found, server, network, invalid-response, no-access and token failures
  remain distinct, generic and free of response bodies or token contents.
- The development Vite proxy accepts only a credential-free loopback origin, preserves Host and
  exposes only anchored `/whoami` and `/api` paths. It does not relax CORS.

## Effective client request matrix

`/whoami` always completes first. The server still enforces every endpoint independently.

| Server projection | Capability-authorized SPA reads/actions                                                                 | Explicitly absent from this role's SPA session                                      |
| ----------------- | ------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------- |
| Viewer            | Portfolio assets; executive, portfolio and department economics; governance; coverage; resolution queue | Privileged log, administration and resolution commands                              |
| Operator          | Viewer reads plus the capability-gated resolution command                                               | Privileged log and administration                                                   |
| Administrator     | Coverage, privileged-access log and administration summary                                              | Portfolio, economics, governance, resolution queue and Operator commands            |
| Executive-scope   | Portfolio assets, executive/portfolio economics and coverage                                            | Department detail, governance, resolution, privileged log, administration, commands |

The SPA does not inspect ID-token or access-token claims and does not infer one role from another.

## Acceptance criteria → result

| Criterion                                                                      | Evidence                                                             | Result |
| ------------------------------------------------------------------------------ | -------------------------------------------------------------------- | ------ |
| Invalid public configuration fails before MSAL/protected render                | Configuration matrix and startup tests                               | PASS   |
| Redirect handling completes exactly once before StrictMode render              | Adapter single-flight and startup-order tests                        | PASS   |
| Signed-out and unresolved multi-account states make no API request             | App account-state tests                                              | PASS   |
| Silent token acquisition requests exactly the delegated API scope              | MSAL adapter request assertions                                      | PASS   |
| Interaction-required is explicit and transient failure does not redirect       | Adapter, composition and App failure-state tests                     | PASS   |
| Every request uses a fresh bearer with no caller identity/authority headers    | Executable all-client-method request matrix                          | PASS   |
| Protected responses cannot survive through the browser HTTP cache              | `/whoami` and all-endpoint `no-store` assertions                     | PASS   |
| `/whoami` validates and completes before protected data requests               | Single-flight, deferred-response and hostile-projection tests        | PASS   |
| Areas, optional sections, reads and actions match effective capabilities       | Viewer, Operator, Administrator and Executive-scope component tests  | PASS   |
| 401, 403, 404, server, network, invalid and token failures are safe/distinct   | Client failure matrix and App state tests                            | PASS   |
| Logout clears protected UI/cache before account-specific redirect completion   | Deferred-logout App test and adapter request assertion               | PASS   |
| Development proxy is loopback-only and anchored to `/whoami` plus `/api`       | Target rejection matrix and exact proxy-map test                     | PASS   |
| No server, datastore, infrastructure, tenant or frozen-blueprint change occurs | Changed-file scope check and production-readiness/architecture gates | PASS   |

## Verification

```text
dotnet restore ControlTower.sln
  All projects are up-to-date for restore.

dotnet build ControlTower.sln -c Release --no-restore
  Build succeeded. 0 Warning(s). 0 Error(s).

dotnet test ControlTower.sln -c Release --no-build --verbosity normal
  Platform 10/10; Ledger 27/27; Governance 17/17; Economics 20/20;
  Providers 24/24; Architecture 5/5; Host.Web 67/67.
  Total backend: 170 passed, 0 failed.

cd web && npm run typecheck
  TypeScript completed with no errors.

cd web && npm test -- --run
  13 test files passed; 114 tests passed; 0 failed.

cd web && npm run build
  187 modules transformed; production bundle built successfully.

PATH=/Users/arunanderson/.dotnet:$PATH bash scripts/ci/architecture_gate.sh
  5/5 passed.

python3 scripts/ci/validate_task_contracts.py
  checked 24 task contract(s); 0 error(s), 0 warning(s).

bash scripts/ci/check_production_readiness.sh
  OK: no dev-substitute references in production paths (DEV-001).

dotnet list ControlTower.sln package --vulnerable --include-transitive
  No vulnerable NuGet packages.

npm --prefix web audit --omit=dev --audit-level=high
  found 0 vulnerabilities.

npx --yes prettier@3 --check "**/*.{md,yml,yaml,json}"
  All matched files use Prettier code style.

git diff --check
  [no output; exit 0]

forbidden-path and targeted credential/private-key scans across changed files
  [no matches; gitleaks is not installed locally and remains a required PR check]
```

## Independent review

- Architecture reviewer: no remaining P0, P1 or P2 findings after capability-filtered optional
  sections, same-origin proxy and protected-fetch cache hardening.
- Security reviewer: no remaining P0, P1 or P2 findings after ambiguous-account delegation and
  distinct interaction-required/transient token handling.
- Test reviewer: no remaining P0, P1 or P2 findings after executable startup ordering,
  deferred-logout, all-request and proxy matrices.

## Deliberately deferred Microsoft tenant activation

The tenant-independent browser implementation is complete. Live activation remains a Microsoft
tenant human gate and requires:

- one public SPA app registration with the deployed and approved local redirect URIs;
- one API app registration exposing delegated scope `controltower.access`;
- delegated API permission and tenant consent;
- production Host audience and allowed external-to-internal tenant mapping values;
- durable E18 role assignment and E19 person-key resolution for the signed-in test user.

No client secret is required or permitted for the public SPA. No tenant action was taken in this
task.

## CI

Pull request checks: https://github.com/arunanderson/Control-Tower/pull/24/checks

## Rollback

Revert the P1-T04 PR. No migration, tenant permission, app registration, infrastructure or
production configuration changed.
