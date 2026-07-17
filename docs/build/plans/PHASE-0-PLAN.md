---
id: PHASE-0-PLAN
title: Phase 0 — Foundations & Gate-1 PoCs
type: phase-plan
schema_version: 1
status: proposed # proposed | approved | in_progress | complete
authority:
  - docs/blueprint/implementation-handoff-package.md §6
  - docs/automation/automation-operating-model.md §4, §12
requires_human_approval: true # automation model §2: Arun approves the plan before task contracts
---

## Preconditions (human gates — build does not start until these close)

Per the repository's own authority, **no build (including the bootstrap rails PR) starts before these**:

1. **Gate-0 venture decision closed.** automation §12: "none of this starts before Gate-0 closes"; revision §4: "Gate-0 is undecided — build must not start before it closes." Currently **awaiting executive decision**. — _Arun / executive_
2. **DEV-001 (Supabase) decided.** See `docs/build/deviations/DEV-001-supabase-backend.md`. Recommended: reject; keep Azure + PostgreSQL/Azure SQL. — _Arun_
3. **PoC-tenant provisioning** for Gate-1: representative M365 tenant + Agent 365 licence + four agent archetypes + Entra app registrations. Not autonomous (tenant consent). — _Arun / Platform Admin_

## Objective

Establish the build-control rails, the CI trust boundary, and the architectural skeleton — with the two-doors + I3/I4 invariants machine-enforced **before any feature code** — while executing the Gate-1 correlation PoCs, re-validating Stage 3, closing the database-engine decision (Azure PostgreSQL vs Azure SQL), and finalising Stage 5 from PoC results. Exit leaves Phase 1 unblocked with **zero product features built**.

## Two lanes (different owners, one convergence at GATE-P0)

**Track A — Software foundations (agent-autonomous once Gate-0 + DEV-001 close):**

- **E0 — Build-control rails** (the single human-reviewed bootstrap PR): root + `docs/build` CLAUDE.md; `.claude/settings.json` deny-writes to `/docs/blueprint` and `/docs/build/approvals`; `docs/build/{phases,epics,tasks,evidence,approvals,deviations,risks,reviews,templates}` + `build-state.yaml`; templates; PR template; CODEOWNERS.
- **E1 — CI trust boundary**: GitHub Actions — format, secret scan (gitleaks + push protection), dependency scan, protected-paths gate, task-contract-validation gate, architecture-gate placeholder. Each proven to fail on a deliberate violation.
- **E2 — Platform skeleton & tenancy context** (stack-dependent on DB decision): .NET modular monolith (Host.Web + Host.Worker, Modules C1–C9, Platform/); unforgeable tenancy context injected at boundary; authenticated plane seam; privileged-zone skeleton.
- **E3 — Event backbone, outbox & integrity chain (skeleton)**: one-stream event bus + outbox; append-only store with update/delete denied; hash-chain + WORM-anchor pattern; chain verifier; privileged-read audit hook (on by default).
- **E6 — DB-engine decision & RLS performance spike**: close PostgreSQL-vs-Azure-SQL on Azure (ADR-023 amend. 1); RLS/tenant-isolation spike at representative volume (Stage 9 assumption).

**Track B/C — Validation & finalisation (human/commissioned, parallel):**

- **E5 — Stage 3 re-validation** (kickoff quarterly ritual): roadmap-watch register, `agentRegistry` deprecation, preview→GA, SKU/licence/auth assumptions.
- **E4 — Gate-1 PoC execution** (PoC-1/2/3, `/poc` quarantined, never referenced by `/src`; needs real tenant + credentials — human-run). PoC-1 failure → escalate before Stage 5.
- **E7 — Stage 5 finalisation** (pre-authorised PD-006 revision): resolve the four ⛔PoC markers (alias types per archetype, confidence rule table, native-ID reuse/validity windows); v0.9 → final. Human-led (writes into frozen blueprint via the revision process — the build agent must not edit `/docs/blueprint`).

## Human gates in Phase 0

Gate-0 (precondition) · DEV-001 decision · PHASE-0-PLAN approval (this file) · every task-contract approval · every PR merge · DB-engine decision sign-off · Stage 3 re-validation sign-off · PoC-1 escalation if it fails · Stage 5 finalisation approval · **GATE-P0** phase-exit sign-off.

## Validation gates operational in Phase 0

Compile · architecture tests (two doors, I3/I4, module seams, `/poc` quarantine, tenancy-context-required) · secret scan · dependency scan · protected-path gate · task-contract-validation · immutability tests · hash-chain verifier · pipeline self-test. (Full tenant-isolation and privacy suites are **Phase 1**, not Phase 0.)

## Exit criteria (GATE-P0)

Rails operational; all CI gates green and each proven to fail on a deliberate violation; skeleton compiles with architecture tests green and tenancy context test-enforced; event/outbox/hash-chain skeleton with immutability + verifier tests green; Gate-1 PoCs executed and findings recorded (PoC-1 resolved or escalated); Stage 3 re-validated; DB decision closed; RLS spike meets threshold or has an approved mitigation; Stage 5 finalised via PD-006; **zero product features built**; GATE-P0 approval committed by Arun.

## Next autonomous action (once Gate-0 + DEV-001 close and this plan is approved)

Generate the E0 bootstrap-rails task contracts and open the single human-reviewed bootstrap PR (create files + CI; **no merge**).
