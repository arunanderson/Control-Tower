---
id: DEC-001
title: Database engine — resolve the reversible ADR-023 choice
type: decision-record
schema_version: 1
status: recorded # recorded | ratified | superseded
decided_by: Claude Code (build agent) under Arun's delegation ("resolve the remaining database decision")
decided_at: 2026-07-16
ratified_by: null # ratified on bootstrap PR merge
authority: ADR-023 amendment 1; Stage 9 §3.1
---

## Decision

**Target engine: Azure Database for PostgreSQL (Flexible Server).** Azure SQL remains the
**sanctioned fallback**. This resolves the one deliberately-reversible choice in ADR-023 amend. 1.

## Rationale (from the frozen blueprint, Stage 9 §3.1)

- Relational core + **JSONB** for observation payloads / alias sets; **row-level security** bound to
  the per-request tenant context; as-of queries. PostgreSQL fits these directly.
- Cost economics and JSONB ergonomics favour PostgreSQL over Azure SQL (Stage 9 §3.1, §8.2).
- Stage 9 explicitly records this as reversible pre-build with Azure SQL as the near-equal fallback.

## Constraints that keep it reversible (ADR-023 amend. 1 + DEV-001)

- **No architectural decision may depend on PostgreSQL-specific capabilities without measurable
  benefit.** Data access goes through a provider-abstracted layer (e.g. EF Core); the **domain uses
  standard SQL semantics**; engine-specific features (e.g. specific JSONB operators) live behind the
  data-access adapter, not in domain logic.
- Development may use **local Docker PostgreSQL** as a dev-only substitute (DEV-001); it must remain
  swappable for Azure Database for PostgreSQL Flexible Server by configuration alone.

## Validation (outstanding, not blocking this record)

- **RLS performance spike** at representative volumes (Stage 9 assumption; R-28/RLS) runs in E2/E6
  once the persistence skeleton exists. If RLS latency fails threshold, revisit via a new decision
  record (Azure SQL fallback or partitioning mitigation) — the abstraction keeps this cheap.

## Status

Recorded now to unblock E2 (persistence skeleton). **Ratified when the bootstrap PR is merged.**
Does not depend on Gate-1 PoC results (those govern entity-resolution / Stage 5, not the engine).
