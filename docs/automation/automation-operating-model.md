# Build Automation Operating Model — Claude Code + Cursor

| | |
|---|---|
| **Version** | 1.0 |
| **Date** | 2026-07-15 |
| **Status** | Analysis for Arun's review. Not part of the frozen blueprint. No implementation performed. |
| **Related** | [implementation-handoff-package.md](implementation-handoff-package.md) (phase authority), ADR-020/021/022/023 (constraints), [revision-package-v1.md](revision-package-v1.md) §4 (readiness) |

**Terminology note (important):** Claude Code (Anthropic's terminal agent) and Cursor (the IDE) are separate products. Claude Code runs inside Cursor's integrated terminal; Cursor's own AI agent is a different system. This analysis designs for **Claude Code as the automation engine**, Cursor as the editing/review shell. Mixing both agents as *writers* in one repo is not recommended (two writers, two behaviour models, one merge conflict generator) — use Cursor's agent for human-driven spot edits and review assistance only.

**Confidence labels used:** [Confirmed] = documented, stable Claude Code capability · [Assumption] = reasonable but unverified in your environment · [Verify] = check in the installed version before relying on it.

---

## 1. Repository preparation

### 1.1 Monorepo structure (recommended)

```
/docs/blueprint/          ← the frozen KB, verbatim, read-only (this folder's 25 documents)
/docs/build/
  plans/                  ← phase implementation plans (PHASE-2-PLAN.md …)
  tasks/                  ← task contracts (P2-T04.md …), one file per task
  state/                  ← build-state.yaml, risk register, migration log, deployment log
  evidence/               ← per-task evidence bundles (test output, screenshots, gate results)
  approvals/              ← signed gate records (GATE-P1.md — committed by Arun only)
  deviations/             ← approved deviations from contracts/blueprint (rare, explicit)
/src/                     ← .NET modular monolith; one module folder per bounded context
  Modules/{Ledger,Governance,Economics,Providers,EnterpriseContext,Experience,Trust,Audit}/
  Platform/               ← tenancy context, event backbone, outbox, shared kernel
  Host.Web/  Host.Worker/ ← the two deployables (ADR-023)
/web/                     ← React + TypeScript SPA
/db/migrations/           ← versioned, immutable-once-merged migration scripts
/infra/                   ← Bicep modules + stamp definitions
/tests/                   ← architecture tests, tenant-isolation suite, privacy suite, e2e
/poc/                     ← Gate-1 PoC scripts + findings; NEVER referenced by /src (enforced)
/.claude/                 ← Claude Code configuration: CLAUDE.md targets, commands, agents, hooks, settings
/.github/workflows/       ← CI pipelines
```

Rationale: blueprint physically separated from build artifacts (frozen vs living); evidence and approvals in-repo so the audit trail of the *build* uses the same doctrine as the product (events + evidence); PoCs quarantined per the handoff package rule.

### 1.2 CLAUDE.md strategy — yes, root + module level

[Confirmed] Claude Code automatically reads `CLAUDE.md` at the repo root and in nested directories relevant to the files being worked on; this is the primary mechanism for persistent instructions.

| File | Permanent contents |
|---|---|
| `/CLAUDE.md` (root) | The constitution: (1) blueprint is read-only — never edit `/docs/blueprint`; (2) the four invariants (two doors, I3/I4) + ADR-015 doctrines in one screen; (3) task-contract protocol: "work only within an approved task contract; no contract, no code"; (4) evidence rule: "no claim of success without command output captured to `/docs/build/evidence`"; (5) stop conditions (see §11); (6) approval boundaries (see §3); (7) pointers: state file, current phase plan, decision log |
| `/src/CLAUDE.md` | Module boundary rules (allowed inter-module references = events + read contracts only), naming conventions, tenancy-context requirement on every query path, "no PostgreSQL-specific SQL without a `deviations/` entry (ADR-023 amendment)" |
| `/web/CLAUDE.md` | Read-model-only data access (I4), evidence/confidence display obligations (ADR-025: no economic figure without class + as-of + methodology ref), accessibility baseline |
| `/db/CLAUDE.md` | Migrations immutable once merged; every migration paired with rollback + validation note; **never run against any shared environment** (local/ephemeral only) |
| `/infra/CLAUDE.md` | Bicep conventions, stamp model, "no new Azure resource types without an approved deviation" |
| `/docs/build/CLAUDE.md` | State-file update protocol, evidence bundle format, approval-file semantics (Claude may *read*, never *write*, `/approvals`) |

[Verify] Enforcement hardening: Claude Code's permission system (`settings.json` allow/deny rules) and hooks can technically block writes to protected paths (blueprint, approvals) rather than merely instructing against them — configure both; instruction + enforcement, not either alone.

## 2. Build orchestration

Contract-driven, phase-gated:

1. **Phase planning session** (one per phase): Claude reads the handoff package + the phase's blueprint references → produces `PHASE-N-PLAN.md`: scope, RTM/ADR mapping, task list with dependencies, risk notes. **Arun approves the plan** (gate) before any task contract is generated.
2. **Task contract generation**: from the approved plan, Claude drafts task contracts (§5) — each mapped to blueprint sections, ADRs, RTM rows. Contracts are reviewed/approved in a PR (cheap to review, expensive to skip).
3. **Task execution loop** (§3): one task per session, in contract order, respecting dependencies.
4. **Validation after every task**: local gates (§7) + evidence capture; failure = stop, record, surface.
5. **Phase exit**: all tasks complete + phase-level checks green + evidence bundle complete → Claude assembles `GATE-PN-REQUEST.md`; **Arun signs by committing the approval file**; next phase unlocks.
6. **Resume**: any new session bootstraps from `/CLAUDE.md` → `build-state.yaml` → current contract. No dependence on chat history, ever (§6).

Custom slash commands ([Confirmed] `.claude/commands/`) make this ergonomic: `/phase-plan N`, `/next-task`, `/run-gates`, `/close-task`, `/gate-request` — each command is a stored prompt encoding the protocol so it's executed identically every time.

## 3. The autonomous loop

**Plan → inspect → implement → test → review → document → commit candidate → approval gate**, per task:

| Step | What happens | Autonomous? |
|---|---|---|
| Plan | Re-read contract + referenced blueprint sections only; write micro-plan into the task file | Yes |
| Inspect | Read current code in allowed files; verify preconditions; check state file | Yes |
| Implement | Code strictly within allowed-files list | Yes |
| Test | Write/execute required tests + run local gates; capture raw output to evidence | Yes |
| Review | Invoke read-only reviewer subagents (architecture, security — §10); address findings | Yes |
| Document | Update state file, evidence bundle, traceability note; ADR-impact note if any | Yes |
| Commit candidate | Branch commit(s) + open PR with contract link + evidence links | Yes (PR creation, never merge) |
| Approval gate | Human review/merge; phase gates; boundary approvals | **Never automated** |

**Requires Arun's approval (hard list):** architecture-boundary changes (anything touching module interfaces, the event backbone, tenancy context); any new dependency (NuGet/npm/Azure resource type); database migrations (authoring is autonomous; *merging and executing* is gated); Entra app registration/permission scope changes; anything in `/infra`; task-contract modifications after approval; deviations from blueprint; production releases; secrets of any kind.

**Never automated (beyond approval — structurally excluded):** production deployment; Entra admin consent grants; execution of migrations against shared environments; writes to `/docs/blueprint` or `/approvals`; tenant data operations; signing gates; editing the frozen ADRs.

**Context refresh between tasks:** one task = one fresh session. The contract carries the *exact* blueprint references (document + section), so the session loads a few thousand tokens of authority, not the whole KB. Long outputs (test logs) go to evidence files, not conversation. [Confirmed] `--continue`/`--resume` exist, but the design deliberately doesn't depend on them — the repo *is* the memory.

**Anti-drift mechanisms (layered):** (1) architecture tests in CI failing on module-boundary violations — machine enforcement of I1–I4 (the R-23 mitigation, now doing double duty against agent drift); (2) allowed/forbidden file lists per contract, enforced by hooks [Verify hook capability in installed version] and by PR diff review; (3) read-only architecture-compliance subagent reviews every diff against the invariants; (4) CI is the trust boundary Claude cannot fake.

**Anti-silent-requirement-change:** contracts are immutable once approved (record a content hash in the state file); if implementation reveals the contract is wrong, the *only* path is a `deviations/` entry + halt + Arun approval. The PR template requires a "contract conformance" section listing any judgement calls.

**Anti-unproven-success:** the evidence rule (root CLAUDE.md) + a post-task hook that blocks `close-task` unless the evidence bundle contains fresh gate output [Verify hook wiring] + CI as backstop: merge requires green checks Claude cannot manufacture. Claims without evidence are defined as task failure, not as optimism.

## 4. Phase structure (authority: implementation handoff package; numbering below is yours — mapping note at end)

| Phase | Inputs | Key deliverables | Depends on | Automated checks | Human gates | Exit criteria | Rollback | Evidence |
|---|---|---|---|---|---|---|---|---|
| **0 — Foundations + Gate-1 PoCs** | Handoff pkg; PoC specs; empty repo | Repo skeleton + control artifacts (§12); CI with architecture/secret/dependency gates **before feature code**; tenancy + event backbone + outbox skeleton; PoC findings in `/poc` | Gate-0 decision (organisational) | Compile; architecture tests green on skeleton; secret scan; pipeline self-test | Repo structure PR; PoC findings review (**PoC-1 failure = escalation per spec**); DB engine decision closes here | Rails operational; PoCs answered; Stage 5 finalised (pre-authorised revision) | Trivial (nothing depends on it) | Pipeline runs; PoC findings notes; architecture-test baseline |
| **1 — Platform foundation** | Stage 7/8/9; Stage 5 final | RLS-enforced persistence + unforgeable tenancy context; event store + hash chain; Key Vault integration pattern; Entra auth (web) skeleton; **tenant-isolation + privacy test suites built now as permanent gates** | 0 | Full §7 gate set incl. new isolation/privacy suites | Migration merges; any infra PR | Two-tenant fixture proves isolation under attack tests; privacy Gate 2 enforcement demonstrable | Revert PRs; DB rebuilt from migrations (no shared envs yet) | Isolation/privacy suite reports; chain verifier output |
| **2 — Provider integration + observations** | Stage 3 (re-validated at kickoff); C4 contracts | Provider contract (manifest) implementation; PPAC + Entra Agent ID + licence providers; observation store (append-only, delta suppression); privacy Gate 1; coverage facts | 1 | Gates + provider contract tests + immutability tests (update/delete denied) | New Graph/PP scopes (consent = human); provider addition PRs | Real-tenant sweep lands observations with correct privacy markings; coverage facts accurate | Disable provider (kill-switch); observations append-only so no corruption path | Sweep evidence vs tenant reality sample; immutability test output |
| **3 — Ledger + entity resolution** | Stage 4/5; ADR-012; PoC-1/2 confidence rules | AIAsset + aliases + ResolutionLinks; deterministic + heuristic passes; merge queue; ownership; taxonomy; debt projections | 2 | Gates + resolution regression fixtures (golden files from PoC archetypes); confidence roll-up property tests | Resolution rule-pack changes | Archetype fixtures resolve at expected confidence; collision → MergeCase demonstrated | Re-resolution is idempotent/re-runnable by design — safe rollback is rule-version revert | Fixture results; merge/split audit events |
| **4 — Cost & value intelligence** | Stage 10 v1.1; E21; ADR-013 | Cost providers (Azure Cost Mgmt, licence, credits CSV); allocation rules/runs; utilisation + zombie detection; realisable-savings classification; Unattributed bucket | 2 (3 for asset attribution) | Gates + allocation arithmetic golden tests; weakest-link label propagation tests; FX conversion tests | Price-book/contract data handling; allocation-rule semantics review with Finance | Known-input fixtures produce hand-verified allocations incl. Unattributed; every figure carries class+as-of+methodology | Allocation runs are disposable projections — rerun after fix | Golden-test results; a full labelled cost statement for the fixture tenant |
| **5 — Experience, trust, reporting, exports** | Stage 6; ADR-019/025 | Operator workspace; executive page; Trust area (coverage map, privileged-access log); Asset Record; board-pack export (same read models); policy enforcement point in front of everything | 3, 4 | Gates + e2e journeys (Playwright); accessibility; "no unlabelled economic figure" automated UI assertion; export/dash parity test | UX review of the three experiences; screen-test conformance (kill list) | The five decision journeys demonstrable end-to-end on fixture tenant | UI-layer reverts are low-risk | e2e videos/screens; parity diff (dashboard vs pack); a11y report |
| **5b — Enterprise readiness & V1 ship** *(handoff Phase 5 items your list folds away — kept explicit)* | Stage 8; ADR-016/017/021 | JIT access flows; legal hold; export/deletion; ReportingPeriod/Snapshot freeze; retention engine; regional stamp deploy (staging→prod) | 1–5 | Gates + DR restore drill; retention/hold interaction tests; snapshot immutability tests | **Production deployment (human-only)**; pen-test findings review; Quadient onboarding go | V1 live internally; First-Truth-in-10-days executed; **covenant clock starts** | Stamp blue/green or redeploy-from-IaC; data stores restore-tested | DR drill log; onboarding artefacts; go-live checklist signed |
| **6 — Governance orchestration (V1.5)** | Stage 11 Part A; Stage 4 socket | GovernanceCase workflows into existing transitions; owner view; notifications; delegated admin; full value validation workflow; forecasting | 5b + covenant staffing | Gates + workflow state-machine tests; SLA timer tests | Workflow semantics review with Governance team | A2 workflows live; intake ≤10 min for low tier demonstrated | Feature-flagged rollout per workflow — flags are the rollback | Workflow evidence; timing measurements |

**Mapping note:** your phase list omitted the handoff's enterprise-readiness/V1-ship phase; it is preserved above as 5b rather than silently merged — those items (legal hold, JIT, snapshots, deployment) gate the covenant clock and must not evaporate in renumbering.

## 5. Task specification format

**Recommendation: Markdown files with YAML frontmatter, in `/docs/build/tasks/`, one file per task.** Reasons: human-reviewable in PRs (contracts get approved via review), machine-parseable frontmatter, diff-able history, no external system dependency, native to how Claude Code reads repos. GitHub Issues optionally *mirror* contracts for visibility, but the repo file is the single source of truth (Issues aren't versioned with the code). Pure JSON/YAML rejected: contracts need prose (objective, steps, rollback) that humans must actually read.

Schema (frontmatter keys): `id` (e.g., P2-T04) · `phase` · `status` (draft/approved/in-progress/blocked/failed/complete) · `objective` (one sentence) · `blueprint_refs` (doc§section list) · `adr_refs` · `rtm_refs` · `allowed_files` (globs) · `forbidden_files` (globs; blueprint + approvals always implicit) · `preconditions` · `steps` (bounded, ordered) · `required_tests` · `security_checks` · `migration_impact` (none/authored-not-executed) · `acceptance_criteria` (verifiable statements) · `evidence_required` (artefact list) · `rollback` · `approved_by`/`approved_hash`.

## 6. Persistent state (no chat-history dependence)

**`/docs/build/state/build-state.yaml`** — the single resume point: current phase; current task + status; completed tasks (id, PR, evidence path, date); failed/blocked tasks with reason; open build risks; last gate results summary; migration ledger (authored → merged → executed-in-env, per environment); deployment status per stamp/environment; approvals index (gate file + commit); blueprint traceability pointer (RTM deltas made during build); **resume instructions** (literally: "read root CLAUDE.md, this file, then `/docs/build/tasks/<current>`").

Rules: Claude updates state as part of every task's Document step; state changes ride in the same PR as the work (state can't drift from reality); evidence is append-only; a human can reconstruct the entire build from git history alone — the build inherits the product's own audit doctrine.

## 7. Quality automation (gate set)

| Gate | Tooling class | Runs |
|---|---|---|
| Compilation | dotnet build, tsc | Local + CI |
| Unit tests | xUnit, Vitest | Local + CI |
| Integration tests | Testcontainers-style ephemeral DB/Service Bus emulator | Local + CI |
| End-to-end | Playwright journeys (the five Stage 6 journeys as the canonical suite) | CI (nightly + pre-gate) |
| Static analysis | Roslyn analyzers + ESLint/typescript-eslint, warnings-as-errors | Local + CI |
| **Architecture tests** | NetArchTest-class rules: module boundaries, two doors, I3/I4, "no provider SDK outside /Providers" | Local + CI — **the anti-drift keystone** |
| Dependency scanning | dotnet vulnerable-package audit, npm audit, Dependabot | CI |
| Secret scanning | gitleaks pre-commit hook + GitHub push protection | Local + CI |
| **Tenant isolation** | Custom suite: cross-tenant read/write attempts under every module's query paths, RLS bypass attempts | CI, required — built in Phase 1 |
| **Authorization** | Role/scope matrix tests per endpoint/read model | CI, required |
| **Privacy enforcement** | Fixture data at L1–L4 markings vs policy configurations; assert Gate 2 refusals; "no unlabelled economic figure" UI assertion | CI, required |
| Migration validation | Apply → verify → rollback → re-apply on ephemeral DB; schema drift check | CI, gate for any migration PR |
| Accessibility | axe automated pass on key surfaces | CI (Phase 5+) |
| Performance | k6-class smoke budgets (ingestion sweep, dashboard read) | CI nightly (Phase 3+) |
| Evidence integrity | Hash-chain verifier as a test | CI |
| Infrastructure validation | bicep lint + what-if against staging | CI, human-approved apply |
| Container scanning | Trivy/registry scanning | CI |

Product-specific suites (isolation, authz, privacy, evidence) are *product requirements expressed as gates* — they exist from Phase 1 and everything later must pass them. [Assumption] Specific tool choices above are indicative; final selection at build kickoff within these classes.

## 8. Git workflow

- **Branches:** protected `main`; short-lived task branches `task/P2-T04-observation-store`; no long-running phase branches (merge tasks individually — small integrable increments).
- **Commits:** small, one logical change; conventional format with task ID (`feat(P2-T04): append-only observation store with delta suppression`); soft guideline ≤400 changed lines per task diff (matches §11 task sizing).
- **PRs:** one per task; template requires contract link, conformance notes, evidence links, migration impact, ADR-touch declaration. Claude opens PRs and responds to review comments [Confirmed via gh CLI]; **Claude never merges** — enforced structurally: branch protection (required reviews + required checks), and the automation identity granted no merge/admin rights. Never rely on instructions alone for this.
- **Reverting:** failed pre-merge work = abandon branch; failed post-merge = revert PR (never force-push, never history rewrite — same doctrine as the product's ledger).
- **Production:** no human or agent pushes to production directly; production changes exist only as promoted, approved artifacts (§9).

## 9. CI/CD relationship

Clean separation of trust:

1. **Claude local checks** — fast feedback loop (build, tests, lint, gitleaks). Advisory: they guide the agent but *prove nothing* (Claude reports could theoretically be wrong; evidence helps but isn't the boundary).
2. **CI (GitHub Actions, PR pipeline)** — the **trust boundary**. Everything in §7 runs here; results are unfakeable by the agent; merge is impossible without green. Claude reads Actions results (gh CLI) and iterates on failures autonomously [Confirmed pattern].
3. **Staging deployment** — automated on merge to main (staging stamp), followed by automated smoke suite. Claude may *read* staging telemetry/logs to diagnose; it does not hold write credentials to Azure. [Assumption: staging credentials live in GitHub environments, not in Claude's reach.]
4. **Production deployment** — GitHub Environments with required reviewer (Arun) = a manual approval that no automation can satisfy; deploys promoted artifacts only.
5. **Human approvals** — PR review (task level), gate files (phase level), environment approvals (deploy level): three independent human locks.

[Verify] `claude-code-action` (Claude Code in GitHub Actions for PR review assistance / labelled-issue implementation) — exists as a supported integration; verify availability and permissions model in your GitHub org before relying on it. It is an *addition* (review assist), not a replacement for the local operating model.

## 10. Agent and sub-agent strategy

**Recommended: one writer, several read-only specialists.**

- **Main implementation session** (one at a time): the only writer, always inside a task contract.
- **Read-only reviewer subagents** [Confirmed: Claude Code supports custom subagents with scoped tools]: `arch-reviewer` (diff vs invariants/ADRs), `security-reviewer` (diff vs ADR-021 checklist: tenancy on every query, no secrets, privacy gates), `db-reviewer` (migration quality, rollback completeness). Invoked in the Review step; findings recorded in evidence.
- **Docs scribe**: can be a subagent or simply part of the Document step — separate agent not required.
- **Final phase review**: a *fresh* session (not a subagent of the implementer) with an adversarial prompt over the phase diff + gate evidence — the independent-review pattern this project already uses, applied to code.
- **Parallelism:** default **no** — one writer prevents conflicting edits by construction. If schedule demands it, parallel sessions only via **git worktrees on file-disjoint task contracts** (backend module vs `/web`) [Confirmed worktree pattern works; Verify ergonomics in your setup]; the allowed-files lists make disjointness checkable before launching. Frontend/backend as separate *specialist writer sessions* is acceptable under this rule; separate "backend agent vs frontend agent" personas add little beyond the worktree isolation itself.
- **Context loss prevention:** subagents receive the contract + diff, not the conversation; everything durable goes to files; the state file is the shared memory.

## 11. Long-running automation — can the whole build run unattended?

**No — and it shouldn't, by design.** The gates you mandated (architecture, security, migrations, deployment, tenant permissions, production) occur frequently enough that "unattended end-to-end" is structurally impossible; the right target is **maximum autonomy *between* gates**.

- **Max safe autonomous task size:** one contract ≈ ≤400 diff lines, one module, one session, roughly a half-day human-equivalent. Larger objectives are split at contract-generation time, not during execution.
- **Autonomous run length:** a *task batch* — consecutive dependency-free tasks within a phase that touch no gate — may run back-to-back unattended (e.g., overnight: implement + test + PR for 3–5 small tasks). Each still produces its own PR; nothing merges until morning review.
- **Mandatory stops:** any hard-gate trigger (§3); validation failing **twice** on the same task (the repair-loop cap — after two attempts, stop, write a `BLOCKED` status with diagnosis, move on only if an independent task exists); contract ambiguity or blueprint conflict; any need to touch forbidden files; any unexpectedly failing *previously-green* gate (possible regression — human eyes).
- **Failure surfacing:** state file status + PR comment + a top-level `BLOCKED.md` listing anything awaiting a human, so Arun's morning starts with one file.
- **Rate/context limits:** fresh session per task; contracts reference precise blueprint sections; logs to files; batch scheduling respects subscription limits [Verify plan limits in your environment].
- **Session resume:** stateless by design — any new session executes the resume instructions in the state file. `--resume` is a convenience, never a dependency [Confirmed the flags exist].
- **Endless-repair prevention:** the two-attempt cap + "diagnosis over persistence" instruction (a blocked task with a good diagnosis is a success state for the agent) + no self-modification of contracts or gates, ever.

## 12. Recommended operating model (conclusion)

**Architecture in one line:** *contract-driven, evidence-based, gate-controlled, one-task-per-session autonomy — the repo is the memory, CI is the trust boundary, humans are the only merge and deploy path.*

**Control artifacts that must exist (before feature code):** root + area `CLAUDE.md`s · `.claude/settings.json` (permissions: deny blueprint/approvals writes) · `.claude/commands/` (phase-plan, next-task, run-gates, close-task, gate-request) · `.claude/agents/` (arch-reviewer, security-reviewer, db-reviewer) · `/docs/build/{plans,tasks,state,evidence,approvals,deviations}` with `build-state.yaml` seeded · PR + gate-request templates · branch protection + GitHub environments · the CI pipeline with architecture/secret/dependency gates live.

**Safest autonomy level:** *supervised task-batch autonomy* — Claude plans phases (approved), generates contracts (approved), then implements/tests/documents/PRs autonomously in batches, halting at every defined boundary. Expect **~70–85% of code, tests, migrations-as-authored, docs, and evidence to be produced autonomously**; expect ~100% of *judgment* to remain human.

**Required human gates (consolidated):** phase-plan approval · contract approval · every PR merge · migration merge + any execution against shared environments · all `/infra` changes · Entra consent/scopes · staging incident triage where data is involved · production deploys · gate sign-offs · deviations · anything touching the frozen blueprint.

**Arun's day-to-day (steady state):** Morning (30–60 min): read `BLOCKED.md`, review overnight PRs against contracts + evidence, merge/comment, sign any gate requests. Midday (optional): approve next task batch / contract set. The weekly rhythm adds: phase-plan reviews, a look at the risk register, and the quarterly re-validation ritual once integrations are live. You review *decisions and evidence*, not keystrokes.

**Realistically automatable end-to-end:** module code, tests (incl. the product-specific suites), migration authoring, Bicep authoring, documentation, evidence bundles, PR mechanics, failure diagnosis, state maintenance. **Cannot be safely automated:** merge and deploy decisions, consent grants, secrets, migrations against shared data, production operations, PoC tenant setup (needs real credentials + licences), and every judgment the blueprint reserved for humans (gate sign-offs, deviations, the covenant call).

**First action before implementation begins:** none of this starts before **Gate-0 closes** (blueprint order stands). The first *technical* action after Gate-0: a single human-reviewed bootstrap PR creating the control artifacts and CI gates above — **build the rails before the train**. The very first thing Claude Code should ever do in this repo is read a CLAUDE.md that already constrains it.

### Capability ledger
- **[Confirmed] Claude Code capabilities relied on:** CLAUDE.md (root + nested); custom slash commands; hooks; permission rules/settings; subagents with scoped tools; headless/non-interactive execution; session resume flags; git + gh CLI operation (branch/commit/PR/read Actions results); MCP extensibility.
- **[Assumptions]:** GitHub as the host (Azure DevOps variant works but changes §9 mechanics); Azure credentials never provisioned to the agent; one automation identity with no merge rights; team available for morning review cadence.
- **[Verify in installed environment]:** hook event coverage + settings schema in the installed version; `claude-code-action` availability/permissions in the org; worktree-parallelism ergonomics; subscription rate/usage limits for batch runs; Cursor-terminal integration quirks.
- **[Recommendations]** are everything else in this document.
