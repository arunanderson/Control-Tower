# Enterprise AI Control Tower — Build Constitution (root)

This file is loaded into every build session. It is the top authority for **how** the
build runs. The **what** is the frozen blueprint in `/docs/blueprint`.

## 1. Source of truth

- `/docs/blueprint/**` is the **frozen** product blueprint. It is **read-only**. Never create,
  edit, move, or delete anything under it. Changes happen only via the PD-006 revision process,
  performed by a human.
- `/docs/automation/automation-operating-model.md` defines the build process.
- `/docs/build/**` is the living build-control layer (plans, contracts, state, evidence, etc.).
- `STATUS.md` (repo root) is the single build-status file; keep it current.

## 2. Frozen architecture invariants (non-negotiable)

- **Two doors:** C4 is the only path in/out for external signals; C7 the only path for
  human-facing experiences (ADR-009/020).
- **I3/I4:** nothing reads provider surfaces except C4 adapters; no pipeline shortcuts;
  experiences/exports read only policy-enforced read models (ADR-020).
- **Observations immutable, append-only, pre-resolution** (ADR-015); resolution via links only.
- **Events are the audit trail** — one hash-chained stream, WORM-anchored (ADR-015/021).
- **Privacy double-gate + storage refusal**; L1 default; re-mask at read (ADR-003/014/021).
- **Flag, never block** (ADR-018). **Honest data**: six evidence classes, categorical never
  numeric, smaller defensible number wins, Unattributed never spread (ADR-024/025).
- **Modular monolith, strictly bounded** (ADR-020); **deliberately boring Azure stack** (ADR-023) —
  new infrastructure must name the requirement that earned it.
- **Tenant-scoped everything; credentials isolated more strongly than data; no cross-tenant
  enumeration** (ADR-021).

## 3. Production target & development substitutes (DEV-001, approved 2026-07-16)

- **Production is Azure.** Supabase / any BaaS is **not** the production backend.
- **Development-only substitutes** (local Docker Postgres, local queue/blob emulators, etc.) are
  permitted **only** when: (1) isolated to dev/test, (2) no architectural dependency — accessed
  through ports/adapters, **standard SQL only** (no engine-specific features; ADR-023 amend. 1),
  (3) replaceable before production without architecture change, (4) clearly marked dev-only and
  listed in `docs/build/state/dev-substitute-registry.md`.
- **No development shortcut may become a production dependency.** CI enforces this
  (`production-readiness` gate + architecture-boundary tests).

## 4. Task-contract protocol

- No work without an **approved task contract** in `docs/build/tasks/`. No contract → no code.
- One writer at a time. One task per session. Work strictly within the contract's `allowed_files`.
- After every task: run local gates, capture raw output to `docs/build/evidence/`, update
  `docs/build/state/build-state.yaml` and `STATUS.md` in the same change.
- Contracts are immutable once approved (content hash in state). If a contract is wrong, stop and
  raise a `docs/build/deviations/` entry — never silently change scope.

## 5. Evidence & honesty

- **Never claim completion without passing validation evidence** captured to `docs/build/evidence/`.
- CI is the trust boundary; local checks are advisory. Never fabricate or assume results.

## 6. Stop conditions

- Stop and write a `BLOCKED.md` (repo root) + set `build-state.blocked` when: a validation fails
  **twice** on the same task; a contract is ambiguous or conflicts with the blueprint; forbidden
  files must be touched; a previously-green gate regresses; or a human gate is reached.

## 7. Human gates (never automated)

Merging PRs · granting Microsoft tenant permissions/consent · executing migrations against shared
or production environments · creating/exposing production secrets · production deployment · changing
frozen architecture · approving deviations · signing phase gates. **Never** commit secrets, force-push
`main`, or merge your own PR.

## 8. Pointers

State: `docs/build/state/build-state.yaml` · Current plan: `docs/build/plans/PHASE-0-PLAN.md` ·
Decisions: `docs/build/decisions/` · Deviations: `docs/build/deviations/` · Templates:
`docs/build/templates/`.
