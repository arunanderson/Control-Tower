# Enterprise AI Control Tower — Build Status

_Single source of build truth. Updated by the build agent as part of every task's Document step._

| Field             | Value                                                                           |
| ----------------- | ------------------------------------------------------------------------------- |
| **Current phase** | Phase 0 — Foundations & Gate-1 PoCs                                             |
| **Current epic**  | E0/E1 rails complete; E6 DB decision recorded; E4 PoCs **blocked**              |
| **Current task**  | P0-T16 (PoC execution) — BLOCKED on tenant                                      |
| **Overall state** | **Bootstrap PR open (awaiting merge); PoC execution blocked (awaiting tenant)** |
| **Last updated**  | 2026-07-16                                                                      |
| **Updated by**    | Claude Code (build agent)                                                       |

## Completed work

- Gate-0, DEV-001, Phase-0 plan **approved** by Arun (2026-07-16).
- **DEV-001 recorded** (approve-with-conditions): Azure production; dev-only substitutes under 4 conditions + no-prod-dependency rule.
- **DEC-001 recorded**: database engine = Azure Database for PostgreSQL (Flexible Server), Azure SQL fallback; standard-SQL-only discipline keeps it reversible. Ratified on bootstrap PR merge.
- **Bootstrap rails built (E0+E1)**: root + `docs/build` CLAUDE.md; `.claude/settings.json` deny-rules; full `docs/build` structure + templates + `build-state.yaml` + risk register + dev-substitute registry; PR template; CODEOWNERS; `.gitignore`.
- **CI gate set (E1)**: format, secret-scan, dependency-scan, protected-paths, task-contract-validation, architecture-gate (placeholder), production-readiness (DEV-001). Validators **pass locally** and **proven to fail on deliberate violations** (see `docs/build/evidence/EVIDENCE-P0-BOOTSTRAP-rails.md`).
- **Gate-1 PoC harness (P0-T15)** authored + quarantined in `/poc` (never referenced by `/src`).

## Open pull requests

- **PR #1** — Phase 0 bootstrap (rails + CI + DEV-001 decision + DEC-001 + PoC harness). **Awaiting Arun's review/merge (do not auto-merge).**

## Failed / blocked work

- **P0-T16 (Gate-1 PoC execution) — BLOCKED.** Needs a provisioned M365 tenant (Agent 365 licence + 4 archetypes) + Entra app registrations + consent. See `BLOCKED.md`. Granting tenant permissions is a human gate.

## Required Arun actions (in order)

1. **Review & merge PR #1** (merge is a human gate — the agent will not merge).
2. **Configure branch protection on `main`** + enable "Require review from Code Owners" + create the `production` GitHub environment with required reviewer (manual; not doable by the agent).
3. **Provision the Gate-1 PoC tenant** (Agent 365 + 4 archetypes + app registrations + consent) to unblock P0-T16, then have an operator run the `/poc` scripts and append findings.

## Test status

- Local validators: **all green**; negative tests confirm gates fail on violations. CI runs on the PR (authoritative). No product/unit/integration/e2e tests yet (no application code — correct for Phase 0 rails).

## Deployment status

- Nothing deployed. No production access. No secrets created or exposed.

## Development-substitute policy (in force, DEV-001 §7)

Azure is production. Dev-only substitutes allowed only when isolated to dev/test, no architectural dependency (ports/adapters; standard SQL only), replaceable before production without architecture change, and clearly marked. Enforced by the `production-readiness` CI gate + (future) architecture-boundary tests + the dev-substitute registry.

## Next autonomous action

- **Blocked on human gates** (PR #1 merge; branch protection/environments; Gate-1 tenant). After PR #1 merges: generate E2 task contracts (platform skeleton + tenancy context, Azure-targeted, dev-substitute per policy) and E3 (event backbone/outbox/hash-chain skeleton) as their own contract-approved PRs; wire the real NetArchTest architecture gate. Track B/C (Gate-1 PoC execution, Stage 3 re-validation, Stage 5 finalisation) proceeds once the tenant is provisioned.
