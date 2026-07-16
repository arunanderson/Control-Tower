## Task contract

Closes: <P#-T##> — link to `docs/build/tasks/...`

## Type

- [ ] Bootstrap / build-control rails
- [ ] Feature (requires approved task contract + phase-plan)

## Conformance checklist

- [ ] Linked task contract exists and is `approved`
- [ ] Only the contract's `allowed_files` were touched
- [ ] No changes under `docs/blueprint/**` or `docs/build/approvals/**`
- [ ] Evidence bundle added under `docs/build/evidence/`
- [ ] `build-state.yaml` + `STATUS.md` updated
- [ ] All CI gates green
- [ ] No secrets; no dev-substitute reference in any production path (DEV-001)
- [ ] Frozen architecture unchanged (or links an approved deviation)

## Evidence

<links to evidence bundle + CI run>

## Deviations / judgement calls

<none / link to docs/build/deviations/>

🤖 Generated with [Claude Code](https://claude.com/claude-code)
