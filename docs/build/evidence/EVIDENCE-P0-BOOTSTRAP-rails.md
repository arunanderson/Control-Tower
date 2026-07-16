---
id: EVIDENCE-P0-BOOTSTRAP-rails
type: evidence-bundle
schema_version: 1
task: P0-BOOTSTRAP
status: submitted
produced_at: 2026-07-16
---

## Task
Build the Phase-0 build-control rails and CI trust boundary (P0-BOOTSTRAP), plus PoC harness (P0-T15).

## What changed (files)
- `CLAUDE.md`, `docs/build/CLAUDE.md`, `.claude/settings.json`, `.claude/commands/*`, `.claude/agents/*`, `.gitignore`
- `docs/build/`: `plans/`, `tasks/`, `state/build-state.yaml`, `state/dev-substitute-registry.md`, `templates/*`, `decisions/DEC-001-database-engine.md`, `deviations/DEV-001*`, `risks/risk-register.md`, `evidence/` (this file), `epics|approvals|reviews/.gitkeep`
- `.github/`: `pull_request_template.md`, `CODEOWNERS`, `workflows/{format,secret-scan,dependency-scan,protected-paths,task-contract-validation,architecture-gate,production-readiness}.yml`
- `scripts/ci/{validate_task_contracts.py,validate_protected_paths.sh,check_production_readiness.sh,architecture_gate.sh}`
- `poc/`: PoC-1/2/3 harness + README
- `STATUS.md` (updated)

## Acceptance criteria → result
| Criterion | Evidence | Pass/Fail |
|---|---|---|
| CLAUDE.md set with invariants + DEV-001 policy | files present | PASS |
| Deny-writes to blueprint + approvals + workflows | `.claude/settings.json` | PASS |
| docs/build structure + templates + state | tree present | PASS |
| 6 CI gates + production-readiness gate | `.github/workflows/*` (7 files) | PASS |
| Task-contract validator passes on all contracts | see command output below | PASS |
| Shell CI scripts parse | `bash -n` output below | PASS |
| Zero product code / runtime deps | no `src/`, `web/`, package manifests | PASS |

## Commands run + raw output (local, 2026-07-16)

**Positive (all gates green):**
```
bash -n scripts/ci/*.sh poc/*.sh            → ok (all 6 scripts parse)
python3 scripts/ci/validate_task_contracts.py → checked 3 task contract(s); 0 error(s), 0 warning(s); exit 0
bash scripts/ci/check_production_readiness.sh → OK: no dev-substitute references in production paths (DEV-001); exit 0
bash scripts/ci/architecture_gate.sh          → placeholder; exit 0
bash scripts/ci/validate_protected_paths.sh main → OK: no protected-path modifications; exit 0
```

**Negative (gates proven to FAIL on deliberate violations, temp files removed after):**
```
malformed contract (status: not-a-real-status) → FAIL: invalid status ...; exit 1  ✔ gate works
infra/_tmp.bicep containing "localhost:5432"   → PRODUCTION-READINESS VIOLATION; exit 1  ✔ DEV-001 gate works
post-cleanup re-run                            → validator clean; prod-readiness clean; no temp files in tree
```

CI (GitHub Actions) is the authoritative trust boundary; these local runs are advisory and are
reproduced by the PR checks (the PR's Actions run link is added on push).

## CI run link
Populated by the bootstrap PR's Actions run (added on push).

## Reviewer notes
Arch/security review of a docs+CI-only bootstrap: no domain code to review yet; the architecture-test
harness (NetArchTest) and tenant-isolation/privacy suites are Phase-0 E2 / Phase-1 items, not this PR.
