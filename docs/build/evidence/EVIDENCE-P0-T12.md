---
id: EVIDENCE-P0-T12
type: evidence-bundle
schema_version: 1
task: P0-T12
status: submitted
produced_at: 2026-07-17
---

## Task

Event backbone, outbox, and hash-chain integrity (E3 skeleton).

## What changed (files)

- `src/ControlTower.Platform/Events/` — `IEventStore` (append-only), `StoredEvent` (immutable), `Sha256HashChain`, `HashChainVerifier` (+ `ChainVerificationResult`), `IOutbox` (+ `OutboxMessage`); `Events.cs` keeps `IDomainEvent` + `IHashChain`.
- `src/ControlTower.Platform/Audit/PrivilegedRead.cs` — `IPrivilegedReadAuditor` + `PrivilegedReadRecord` (ADR-015.9, on by default).
- `tests/ControlTower.Platform.Tests/EventBackboneTests.cs` — 6 tests + in-memory test doubles.

## Acceptance criteria → result

| Criterion                                     | Evidence                                                         | Pass/Fail |
| --------------------------------------------- | ---------------------------------------------------------------- | --------- |
| Append-only store contract (no update/delete) | `IEventStore` exposes only Append + ReadAll                      | PASS      |
| Immutable stored event                        | `StoredEvent` is a `record` with chain links                     | PASS      |
| Deterministic hash chain                      | test: same inputs → same hash; differs on prev/payload           | PASS      |
| Tamper detection                              | test: tampered payload → `IsIntact=false`, FirstBrokenPosition=3 | PASS      |
| Outbox ordering + ack                         | test: drains in order; acked not re-delivered                    | PASS      |
| Privileged-read audit                         | test: read recorded                                              | PASS      |
| Build clean; tenant-independent               | 0/0; no tenant used                                              | PASS      |

## Commands run + raw output (local, 2026-07-17; .NET 8.0.423)

```
dotnet build ControlTower.sln -c Release → Build succeeded. 0 Warning(s) 0 Error(s)
dotnet test  ControlTower.sln -c Release → Platform.Tests: Passed 10/10 ; ArchitectureTests: Passed 3/3
```

## CI run link

Populated by the E3 PR's Actions run.

## Reviewer notes

Production storage implementations (Azure Database for PostgreSQL append-only partitions + WORM-anchored
digests via `IEvidenceStore`) are a later phase; here the storage contracts are exercised with in-memory
test doubles, while the integrity logic (hash chain + verifier) is real, production-safe Platform code.
