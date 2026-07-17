# Enterprise AI Control Tower — Build Status

_Single source of build truth. Updated by the build agent as part of every task's Document step._

| Field             | Value                                                                           |
| ----------------- | ------------------------------------------------------------------------------- |
| **Current phase** | Phase 0 — Foundations & Gate-1 PoCs                                             |
| **Current epic**  | E2 platform skeleton (this PR); E3 event backbone is next unblocked             |
| **Current task**  | P0-T09 complete → P0-T12 (event backbone) next                                  |
| **Overall state** | **Two PRs open awaiting merge; PoC execution blocked (parallel, not blocking)** |
| **Last updated**  | 2026-07-16                                                                      |
| **Updated by**    | Claude Code (build agent)                                                       |

## Completed work

- **Bootstrap rails + CI (E0/E1)** — PR #1, CI 7/7 green.
- **DEV-001** approved-with-conditions; **DEC-001** DB engine = Azure PostgreSQL Flexible Server.
- **.NET toolchain** installed user-locally (8.0.423); CI uses `setup-dotnet` 8.0.x.
- **E2 platform skeleton** — PR #2 (stacked): modular monolith (Platform + 8 modules C1–C9 + Host.Web + Host.Worker), **unforgeable tenancy context**, adapter ports (secrets/queue/blob/data), event abstractions, and **NetArchTest module-boundary tests** (R-23 keystone). Build 0/0; **7/7 tests pass**; **no vulnerable packages** (test tooling upgraded). Wired into CI (`build-test`, `architecture-gate`, `dependency-scan`). No live tenant used.

## Open pull requests

- **PR #1** — Phase 0 bootstrap (rails + CI + DEV-001 + DEC-001). Awaiting merge.
- **PR #2** — Phase 0 E2 platform skeleton (base = PR #1 branch, stacked). Awaiting merge.

## Failed / blocked work

- **P0-T16 (Gate-1 PoC execution) — BLOCKED**, and **provider integrations (Phase 2 C4 adapters)** — both need a provisioned M365 tenant + consent. Per direction, this is **one parallel workstream, not the critical path**; all tenant-independent work continues.

## Required Arun actions

1. **Review & merge PR #1**, then **PR #2** (agent will not merge). After PR #1 merges, PR #2 retargets to `main`.
2. **Branch protection + CODEOWNERS enforcement + `production` environment** (manual GitHub config).
3. **Provision the Gate-1 PoC tenant** when convenient — unblocks the PoC + provider-integration workstream (not blocking other Phase 0/1 work).

## Test status

- **Local:** build 0/0; unit 4/4 (tenancy); architecture 3/3 (boundaries); vulnerable packages 0. Negative tests confirm CI gates fail on violations.
- **CI:** PR #1 7/7 green; PR #2 runs build-test + architecture-gate + dependency-scan (+ the 6 rails gates) on push.

## Deployment status

- Nothing deployed. No production access. No secrets created or exposed. Nothing merged.

## Development-substitute policy (in force, DEV-001 §7)

Azure is production. Dev-only substitutes (e.g. local Docker Postgres) allowed only via ports/adapters, standard SQL only, replaceable before production, marked dev-only, and never in a production path. Enforced by the `production-readiness` gate + NetArchTest boundaries + the dev-substitute registry.

## Next autonomous action

- **E3 — Event backbone, outbox & integrity-chain skeleton** (append-only store with update/delete denied, hash-chain + WORM-anchor pattern behind the `IEvidenceStore` port, chain verifier, privileged-read audit hook) — tenant-independent; proceed as PR #3.
- Then the **React/TS UI shell** under `/web` (Vite + vitest; buildable/testable with the installed node) and **API contracts** (OpenAPI) — both tenant-independent.
- **Provider integrations + Gate-1 PoC execution** wait on the tenant (parallel workstream).
