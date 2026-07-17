---
id: EVIDENCE-P5-T01
type: evidence-bundle
schema_version: 1
task: P5-T01
status: submitted
produced_at: 2026-07-17
---

## Task

Experience Layer (C7) — read-model-only API contracts + React/TS SPA for the five areas.

## What changed (files)

- `src/Host/ControlTower.Host.Web/ExperienceApi.cs` — the `/api` contract (tenant-gated group filter; read-model-only): portfolio + Asset Record, economics (executive/portfolio/departments/agents), governance (cases/debt), trust (coverage), admin summary. `Program.cs` maps it.
- `src/Modules/ControlTower.Modules.Ledger/` — `CoverageView` + `ICoverageReadModel` (C1.6) + `InMemoryCoverageReadModel` (honest: 0 providers, no sweep).
- `web/` — Vite React/TS SPA: API client + types (read models only), `EvidenceBadge`/`ConfidenceMix`, `ExecutiveDashboard`, `PortfolioArea` + polymorphic `AssetRecord`, `EconomicsArea`, `GovernanceArea`, `TrustArea`, `AdministrationArea`, `App` (five-area nav), tests.
- CI: `.github/workflows/web.yml` (npm ci + build + test); `dependency-scan.yml` audits web **production** deps; `.prettierignore`/`.gitignore` updated.
- `tests/ControlTower.Host.Web.Tests/ExperienceApiTests.cs` (Development WebApplicationFactory).

## Acceptance criteria → result

| Criterion                                       | Evidence                                                                                                          | Pass/Fail |
| ----------------------------------------------- | ----------------------------------------------------------------------------------------------------------------- | --------- |
| Read-model-only API surface, tenant-gated       | ExperienceApiTests: 400 without tenant; 200 with; each endpoint returns a projection                              | PASS      |
| Asset Record (single polymorphic)               | `/api/portfolio/assets/{id}` + `AssetRecord` component; 404 for unknown                                           | PASS      |
| Honest coverage (C1.6)                          | `/api/trust/coverage` + TrustArea: providers 0, "never" sweep, note                                               | PASS      |
| Evidence everywhere                             | ExperienceApiTests: executive carries evidenceClass/methodology/asOf/validationState; `EvidenceBadge` renders all | PASS      |
| Governance debt + recommendation outcomes shown | GovernanceArea test: debt row, reuse outcome, SLA breach                                                          | PASS      |
| ROI honesty in UI                               | EconomicsArea test: suppressed ROI shown as a range, not a single number                                          | PASS      |
| UI consumes read models only; no calculations   | components take read-model DTOs as props; client is fetch-only                                                    | PASS      |
| Tenant isolation                                | API tenant-gated + tenant-scoped read models                                                                      | PASS      |
| Production deps clean; web CI wired             | `npm audit --omit=dev` 0 vulns; web.yml build+test                                                                | PASS      |
| No provider/Microsoft UI; no blueprint change   | generic read-model rendering only                                                                                 | PASS      |

## Commands run + raw output (local, 2026-07-17)

```
dotnet build ControlTower.sln -c Release → Build succeeded. 0 Warning(s) 0 Error(s)
dotnet test  ControlTower.sln -c Release → 80 passed
  (Host.Web now 19: platform-foundation 3 + Experience API 16)
web: npm run build → tsc -b && vite build → built (38 modules)
web: npm test    → vitest 5 passed (EvidenceBadge, TrustArea, GovernanceArea, EconomicsArea, PortfolioArea)
web: npm audit --omit=dev --audit-level=high → found 0 vulnerabilities
```

## Reviewer notes / technical debt

- The API is enabled in Development (backed by dev in-memory adapters); production wiring uses the same
  modules with Azure adapters (later train). SPA area routing is a lightweight state switcher (no router
  dependency). Dev/build-tooling advisories (vite/vitest) are excluded from the audit gate as they are
  not in the shipped bundle; production deps (react/react-dom) are clean. Screen-test discipline and the
  full Stage 6 IA polish continue in later UX iterations.
