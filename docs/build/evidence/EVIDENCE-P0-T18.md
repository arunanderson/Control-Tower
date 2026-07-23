---
id: EVIDENCE-P0-T18
type: evidence-bundle
schema_version: 1
task: P0-T18
status: submitted
produced_at: 2026-07-23T10:14:14Z
---

## Task

Record the Product Owner-approved enterprise-wide observable-AI V1 outcome, preserve the frozen
architecture, reconcile current repository truth and establish the dependency-ordered route to
production.

## What changed (files)

- `docs/build/deviations/DEV-002-enterprise-observability-v1.md` — records the approved ADR-007 V1
  scope amendment, acquisition coverage, privacy conditions and retained human gates.
- `docs/build/plans/ENTERPRISE-OBSERVABILITY-DELIVERY-PLAN.md` — maps endpoints, browsers, identity,
  network, SaaS, cloud, agents, APIs, finance and personas onto existing contexts and orders delivery
  through production readiness.
- `docs/build/tasks/P0-T18-enterprise-observability-rebaseline.md` — approved bounded task contract.
- `docs/build/state/build-state.yaml`, `STATUS.md` and `BLOCKED.md` — reconcile PR 20 evidence,
  eliminate stale Cursor wording, separate development slices from production maturity and record the
  next autonomous critical path.
- `docs/build/risks/risk-register.md` — records privacy/works-council, visibility-honesty,
  collector-security and cross-source double-counting risks.

## Acceptance criteria → result

| Criterion                                                 | Evidence                               | Pass/Fail |
| --------------------------------------------------------- | -------------------------------------- | --------- |
| Approved V1 scope amendment recorded                      | DEV-002 frontmatter and §§1–2          | PASS      |
| Every visibility lane maps to existing contexts           | delivery plan §§2–4; DEV-002 §3        | PASS      |
| No new bounded context or C6 capability                   | DEV-002 §3; delivery plan §§1, 8       | PASS      |
| Coverage exposes blind spots and avoids omniscience claim | DEV-002 §§1, 4; delivery plan §5       | PASS      |
| Employee privacy and collector boundaries explicit        | DEV-002 §§5–7                          | PASS      |
| Delivery is dependency ordered                            | delivery plan §§4, 6                   | PASS      |
| PR 20 and Gate-1 findings reconciled                      | `STATUS.md`, `BLOCKED.md`, build state | PASS      |
| Frozen blueprint and product implementation untouched     | changed-path inspection                | PASS      |

## Commands run + raw output

```text
python3 scripts/ci/validate_task_contracts.py
  checked 21 task contract(s); 0 error(s), 0 warning(s)

git diff --check
  [no output; exit 0]

env npm_config_cache=/private/tmp/controltower-npm-cache npx --yes prettier@3 --check
  BLOCKED.md STATUS.md docs/build/deviations/DEV-002-enterprise-observability-v1.md
  docs/build/plans/ENTERPRISE-OBSERVABILITY-DELIVERY-PLAN.md
  docs/build/risks/risk-register.md docs/build/state/build-state.yaml
  docs/build/tasks/P0-T18-enterprise-observability-rebaseline.md
  Checking formatting...
  All matched files use Prettier code style!

staged protected-path inspection
  OK: staged changes do not touch protected paths.

bash scripts/ci/check_production_readiness.sh
  OK: no dev-substitute references in production paths (DEV-001).

env npm_config_cache=/private/tmp/controltower-npm-cache npx --yes prettier@3 --check "**/*.{md,yml,yaml,json}"
  Checking formatting...
  All matched files use Prettier code style!
```

## Architecture and security review

Three read-only audits independently confirmed:

- endpoint, browser, network and vendor collectors are ordinary C4 providers anticipated by ADR-007;
  moving them into V1 is a scope/phasing deviation, not an architecture redesign;
- persona-specific experiences remain C7 projections; no new bounded context is required;
- the current ingestion implementation hard-codes L1 while retaining arbitrary attributes, so live
  endpoint/browser telemetry must not be onboarded before policy-as-of storage refusal is real;
- the next technical slice must replace caller-controlled tenant/actor headers with a validated
  authenticated boundary before broadening acquisition.

## CI run link

https://github.com/arunanderson/Control-Tower/pull/21/checks

## Reviewer notes

- The frozen blueprint and protected approvals folder were not modified.
- This task authorises planning and implementation, not production employee telemetry activation.
- Microsoft Agent 365 is a provider-specific evidence source, not a Control Tower dependency.
