# Build-control rules (`docs/build`)

Scope: the living record of the build. The frozen blueprint (`/docs/blueprint`) is the spec; this
tree is how we execute it. See the root `CLAUDE.md` for invariants and gates.

## Directory contract

- `plans/` — phase plans (`PHASE-N-PLAN.md`).
- `epics/` — epic definitions (`EPIC-*.md`).
- `tasks/` — task contracts (`P<phase>-T<nn>-slug.md`), one file per task; Markdown + YAML frontmatter.
- `state/` — `build-state.yaml` (authoritative), risk register, dev-substitute registry, logs.
- `evidence/` — per-task evidence bundles (command output, gate results). Append-only.
- `approvals/` — **PROTECTED**: signed gate records, committed by Arun only. The agent may read,
  never write, this folder.
- `deviations/` — approved/​proposed deviations from contracts or blueprint.
- `decisions/` — implementation decision records (e.g. reversible choices confirmed within ADRs).
- `risks/` — build risk register entries.
- `reviews/` — review records (arch/security/db reviewer subagents; phase reviews).
- `templates/` — the canonical templates all artifacts must follow.

## IDs & lifecycle

- Phases `PHASE-0`..`PHASE-6`; epics `EPIC-<phase>-<n>`; tasks `P<phase>-T<nn>`.
- Task lifecycle: `draft → approved → in-progress → (blocked|failed) → complete`.
- `build-state.yaml` is updated only as a task's Document step, riding the same PR as the work.

## Update protocol

- Every task updates `state/build-state.yaml` + root `STATUS.md`.
- Evidence is append-only; never rewrite a prior evidence bundle.
- The dev-substitute registry (`state/dev-substitute-registry.md`) must list every dev-only
  substitute in use and its production (Azure) replacement (DEV-001).
