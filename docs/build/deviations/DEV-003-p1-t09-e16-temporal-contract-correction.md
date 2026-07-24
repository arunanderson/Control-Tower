---
id: DEV-003
title: P1-T09 E16 temporal contract correction
type: task-contract-deviation
schema_version: 1
status: approved
raised_by: Codex build agent
raised_at: 2026-07-24
decision: approve
decided_by: Product Owner explicit and standing frozen-blueprint conflict-resolution instructions
decided_at: 2026-07-25
affects_task: P1-T09
affects_adrs: []
affects_principles:
  - scoped bitemporality
  - frozen blueprint precedence
requires_human_approval: true
---

## 1. Context

The first approved P1-T09 contract incorrectly required E16 `JurisdictionProfile` revisions to
carry valid-time and record-time semantics and to support two-axis as-of queries.

Independent implementation review found that this contradicted the frozen temporal model:

- Stage 4 §8 names the entities that are bitemporal and assigns simple evented history to everything
  else; E16 is not in the bitemporal list.
- The Stage 5 entity catalogue classifies E16 as **Versioned** and E17 as **Bitemporal**.

The task contract cannot override the frozen blueprint.

## 2. Decision

The Product Owner approved the exact correction on 2026-07-25 and subsequently gave standing
authority to resolve any task-contract/blueprint ambiguity in favor of the frozen blueprint, amend
the task contract accordingly and continue.

P1-T09 therefore implements:

- E16 as immutable simple versioned event history with exact-version, current-state and complete
  history reads;
- current-only, most-restrictive E16 ceiling resolution across every applicable jurisdiction; and
- E17 as the original bitemporal, privileged policy aggregate.

E16 retains a normalized change/event occurrence time for audit evidence. That timestamp is not a
valid-time or record-time query axis. E20 continues to own event-store recorded time.

## 3. Scope and architecture impact

This correction narrows the original E16 contract to the frozen model. It does not:

- modify the frozen blueprint or an ADR;
- introduce a bounded context, dependency, persistence surface, migration, API, UI or privacy gate;
- change the E17 bitemporal contract;
- add a production jurisdiction taxonomy, population mapping or policy value; or
- access a tenant, cloud resource, credential, shared database or production environment.

All implementation remains inside the original Platform shared-kernel, C5, C8, test and
build-control boundaries. The amended approved contract hash records this deviation path explicitly.

## 4. Verification required

Completion evidence must prove:

- E16 exposes no valid-time, record-time or as-of API;
- E16 exact/current/history and current ceiling resolution are tenant-isolated and deterministic;
- E17 still resolves both valid-time and record-time;
- canonical events remain exact and atomic with development state; and
- the complete P1-T09 contract, architecture and CI-equivalent gates remain green.

## 5. Outcome

The deviation is approved and closed by implementation under P1-T09. It records a task-contract
correction only; no blueprint deviation or later PD-006 ratification is required.
