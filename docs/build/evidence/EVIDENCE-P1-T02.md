---
id: EVIDENCE-P1-T02
type: evidence-bundle
schema_version: 1
task: P1-T02
status: submitted
produced_at: 2026-07-23T11:06:25Z
---

## Task

Replace the development caller-controlled HTTP tenant and actor boundary with a validated Entra
human request identity, an allowed external-to-internal tenant mapping and a canonical audit actor.

## Blueprint and decision trace

- Stage 8 §§3–5: unforgeable request tenancy, Entra-only human federation and authenticated
  user-to-experience-plane boundary.
- Stage 9 §4.1 and ADR-023: Microsoft Entra ID is the platform identity provider; no local identity
  store is introduced.
- ADR-015 and ADR-021: tenant isolation and audit identity are established at the boundary.
- No bounded context, module interface, event contract, datastore, infrastructure or frozen
  blueprint file changed.

## What changed

- `ControlTowerAuthentication` configures the official ASP.NET Core JWT bearer handler with strict
  signature, algorithm, audience, lifetime, Entra v2 issuer and delegated-scope validation.
- The Product Owner explicitly approved `Microsoft.IdentityModel.Validators` 8.19.2. Its supported
  signing-key issuer validator binds tenant-independent Microsoft OIDC JWK issuer metadata to the
  token issuer and tenant.
- `ConfigurationAllowedTenantDirectory` maps a validated external `tid` one-to-one to an internal
  `TenantId` and fails closed on invalid or ambiguous configuration.
- `AuthenticatedTenantContextMiddleware` opens the existing ambient tenant scope only after
  authentication, human-claim validation and allowed-tenant resolution.
- The canonical human audit actor is `entra:{tid}:{oid}`. `X-Tenant-Id`, `X-Operator` and `X-Actor`
  are no longer read by Host.Web.
- `X-Purpose` and `X-Approval-Reference` remain bounded single-value business context; they do not
  participate in authentication or identity.
- `/health` and `/ready` explicitly allow anonymous access. `/whoami` and all mapped `/api`
  endpoints require authentication. Development command APIs remain unmapped in Production.
- Host integration tests now use ephemeral local RSA signing material and Entra-shaped OIDC/JWK
  metadata. No credential, secret or tenant action is required.

## Acceptance criteria → result

| Criterion                                                               | Evidence                                                        | Result |
| ----------------------------------------------------------------------- | --------------------------------------------------------------- | ------ |
| Caller headers cannot select tenant or actor                            | Header-forgery integration tests and zero Host.Web header reads | PASS   |
| Signed delegated human maps to one internal tenant                      | `Signed_identity_maps...` and mapping tests                     | PASS   |
| Missing, malformed, empty or duplicate identity claims fail generically | Authentication boundary claim matrix                            | PASS   |
| App-only and wrong-scope tokens are rejected                            | `App_only_and_wrong_scope_tokens_are_rejected`                  | PASS   |
| Token issuer, `tid` and signing-key issuer form one trust chain         | issuer/tid and tenant-A-key/tenant-B-token adversarial tests    | PASS   |
| Signature, audience and lifetime failures are rejected                  | invalid-token matrix                                            | PASS   |
| Unknown directory tenants fail before a tenant scope opens              | unonboarded-tenant test                                         | PASS   |
| Health/readiness are the only anonymous endpoints                       | executable all-route metadata/request test                      | PASS   |
| Production command API is absent pending authorisation                  | Production endpoint inspection                                  | PASS   |
| Parallel requests do not bleed tenant or actor context                  | 40-request two-tenant concurrency test                          | PASS   |
| Authentication failures do not disclose tenant/resource existence       | generic challenge and cross-tenant comparison tests             | PASS   |

## Verification

```text
dotnet restore ControlTower.sln
  All projects restored successfully.

dotnet build ControlTower.sln -c Release --no-restore
  Build succeeded. 0 Warning(s). 0 Error(s).

dotnet test ControlTower.sln -c Release --no-build --verbosity normal
  Platform 10/10; Ledger 27/27; Governance 17/17; Economics 20/20;
  Providers 24/24; Architecture 5/5; Host.Web 48/48.
  Total backend: 151 passed, 0 failed.

bash scripts/ci/architecture_gate.sh
  5/5 passed.

python3 scripts/ci/validate_task_contracts.py
  checked 22 task contract(s); 0 error(s), 0 warning(s).

bash scripts/ci/check_production_readiness.sh
  OK: no dev-substitute references in production paths (DEV-001).

dotnet list ControlTower.sln package --vulnerable --include-transitive
  No vulnerable NuGet packages.

npm run build && npm test
  SPA build passed; 6 files and 10 tests passed.

npm audit --omit=dev --audit-level=high
  found 0 vulnerabilities.

dotnet format ControlTower.sln --verify-no-changes --no-restore --include <changed C# files>
  [no output; exit 0]

npx --yes prettier@3 --check "**/*.{md,yml,yaml,json}"
  All matched files use Prettier code style.

rg 'X-Tenant-Id|X-Operator|X-Actor' src/Host/ControlTower.Host.Web
  [no matches]
```

## Security review

- Microsoft requires multi-tenant APIs to validate the token issuer, `tid` and the issuer attached to
  the selected signing key. The implementation uses Microsoft's supported
  `EnableAadSigningKeyIssuerValidation` primitive rather than a locally copied security algorithm.
- The test configuration includes the same JWK `issuer` metadata shape published by the Microsoft
  `organizations` endpoint. A token that is otherwise completely valid is rejected when the selected
  key is scoped to another tenant; matching concrete and tenant-template issuers are positive
  controls.
- Audience is required at startup, metadata must use HTTPS, inbound claim mapping is disabled and
  authentication responses suppress diagnostic details.
- This slice authenticates a human request and establishes tenant/actor identity. It does not claim
  production role/capability authorisation, JIT staff access or SPA token acquisition.
- Independent read-only architecture and security reviewers reported no actionable P0, P1 or P2
  findings after the signing-key issuer fix.

## CI

Pull request checks: https://github.com/arunanderson/Control-Tower/pull/22/checks

## Rollback

Revert the P1-T02 PR. There is no migration, tenant permission, infrastructure or production
configuration change.
