---
id: EVIDENCE-P1-T05
type: evidence-bundle
schema_version: 1
task: P1-T05
status: submitted
produced_at: 2026-07-23T14:01:25Z
---

## Task

Harden the existing event backbone with one versioned canonical storage-integrity frame before
P1-T06 completes the remaining E20 audit metadata and before any permanent PostgreSQL event record
is authored.

## Approval and bounded scope

The Product Owner explicitly approved P1-T05 through P1-T08 on 2026-07-23, including the
event-backbone and C8 interface changes. The exact pre-approval P1-T05 contract artifact is recorded
as:

```text
sha256:5915e8e3c10e8d018fd36fa23e5da9472b9053b1560c206ca3bc92631de2d7f1
```

P1-T05 added no package, database, migration, infrastructure, cloud resource, credential, secret or
tenant configuration. No shared, staging or production migration was authored or executed.

## Blueprint and decision trace

- Stage 4 §5 provides the canonical domain-event vocabulary. Persisted names use that vocabulary
  even where an existing CLR class has a different implementation name.
- Stage 5 G1/G2 and E20 require tenant-scoped records with platform-issued globally unique event
  identifiers and immutable event/audit records.
- Stage 7 §§7, 9 and 10 require one evidence path, failure isolation and honest limits.
- Stage 8 §§13–14 and ADR-021 require tamper-evident evidence with externally anchored integrity;
  this task exposes checkpoint binding but does not claim that the later WORM anchor exists.
- ADR-015 keeps one append-only domain-event audit trail. ADR-020 keeps it in the platform shared
  kernel without introducing another bounded context. ADR-023 is unaffected because no datastore or
  infrastructure was introduced.

## Integrity format 1

The production canonicalizer emits one unambiguous binary envelope:

| Order | Field                    | Encoding                                      |
| ----- | ------------------------ | --------------------------------------------- |
| 1     | integrity format version | signed int32, network byte order              |
| 2     | tenant stream position   | signed int64, network byte order              |
| 3     | tenant ID                | 16-byte RFC 4122 network order                |
| 4     | event ID                 | 16-byte RFC 4122 network order                |
| 5     | event type               | int32 byte length + bounded UTF-8 bytes       |
| 6     | occurred at              | signed int64 Unix microseconds, UTC           |
| 7     | recorded at              | signed int64 Unix microseconds, UTC           |
| 8     | privilege classification | one reviewed enum byte                        |
| 9     | payload                  | int32 byte length + caller-supplied raw bytes |

The prior hash is decoded from canonical uppercase hexadecimal and prepended as its 32 binary digest
bytes; genesis contributes no prior bytes. SHA-256 covers that input plus the complete envelope.
Neither locale, process architecture nor JSON serialization participates.

The executable golden vector fixes the exact bytes and hashes:

```text
Envelope:
00000001000000000000000100112233445566778899AABBCCDDEEFF
FFEEDDCCBBAA9988776655443322110000000009546573744576656E74
000000000012D687FFFFFFFFFFF0BDC1000000000300FF41

Genesis hash:
02CDD29DAEDCD9FF308F08908EC45762FF1FC0105725F8E935A7883AF890FA5F

Same envelope chained after that binary digest:
A142C4BF8F975191F476817B20B9186081B54F489EE907D0AA4FCF2D64EE5E80
```

## What changed

- `StoredEvent` now owns a defensive payload copy and persists format version, stable event type,
  occurred/recorded UTC microseconds, tenant, globally unique event ID, position, privilege,
  previous hash and hash.
- The in-memory development adapter snapshots event identity and occurrence exactly once, captures
  its injected clock exactly once, owns payload bytes, assigns tenant and position, rejects global
  duplicate event IDs before mutation and uses the production canonicalizer.
- `Sha256HashChain` hashes the prior binary digest plus canonical envelope and accepts only canonical
  uppercase SHA-256 text outside genesis.
- `HashChainVerifier` receives the expected tenant independently, checks supported format,
  contiguous one-based positions, tenant equality, unique non-empty event IDs, links and hashes, and
  reports only the first bounded failure.
- An optional separately trusted checkpoint binds format, expected tenant, final position and final
  hash. Without one, success is explicitly `InternallyIntactUnanchored`; with one it is
  `TrustedCheckpointBound`.
- All 41 current concrete production events have one exhaustive architecture-locked persistence
  contract: 37 Standard and the four reviewed Privileged events `RoleAssignmentChanged`,
  `PrivilegedReadRecorded`, `LegalHoldPlaced` and `LegalHoldReleased`.
- Frozen Stage 4 names are retained for `ResolutionLinkRemoved`, `MergeCaseDecided`, `AssetsMerged`,
  `ValueRevised` and `CostFactIngested` without renaming or changing the existing domain classes.

## Acceptance criteria to result

| Criterion                                                                      | Result |
| ------------------------------------------------------------------------------ | ------ |
| Every stored integrity field is covered and tamper-evident                     | PASS   |
| Format zero and unknown formats fail closed                                    | PASS   |
| Payload ownership is deep across append, result and read                       | PASS   |
| Reorder, duplicate, interior removal and invalid links fail at the first gap   | PASS   |
| Unanchored prefixes never claim suffix completeness                            | PASS   |
| Checkpoint detects truncation and an internally valid substituted tail         | PASS   |
| Empty and populated verification remain bound to an independent tenant input   | PASS   |
| IDs are globally unique while positions and chains remain tenant-specific      | PASS   |
| UTC microsecond normalization round-trips without byte or hash drift           | PASS   |
| Fixed GUID, integer, timestamp, length and binary-prior vectors are executable | PASS   |
| Canonical names and privilege classifications are exhaustive and unique        | PASS   |
| One platform stored-event model and append/read-only store contract remain     | PASS   |
| Existing module, Host and SPA behavior remains green                           | PASS   |

## Verification

```text
dotnet restore ControlTower.sln
  All projects are up-to-date for restore.

dotnet build ControlTower.sln -c Release --no-restore
  Build succeeded. 0 Warning(s). 0 Error(s).

dotnet test ControlTower.sln -c Release --no-build --verbosity minimal
  Platform 25/25; Ledger 27/27; Governance 17/17; Economics 20/20;
  Providers 24/24; Architecture 9/9; Host.Web 67/67.
  Total backend: 189 passed, 0 failed.

cd web && npm run typecheck
  TypeScript completed with no errors.

cd web && npm test -- --run
  13 test files passed; 114 tests passed; 0 failed.

cd web && npm run build
  187 modules transformed; production bundle built successfully.

PATH=/Users/arunanderson/.dotnet:$PATH bash scripts/ci/architecture_gate.sh
  9/9 passed.

python3 scripts/ci/validate_task_contracts.py
  checked 25 task contracts; 0 errors, 0 warnings.

bash scripts/ci/check_production_readiness.sh
  OK: no dev-substitute references in production paths (DEV-001).

bash scripts/ci/validate_protected_paths.sh origin/main
  OK: no protected-path modifications.

dotnet list ControlTower.sln package --vulnerable --include-transitive
  No vulnerable NuGet packages.

npm --prefix web audit --omit=dev --audit-level=high
  found 0 vulnerabilities.

npx --yes prettier@3 --check "**/*.{md,yml,yaml,json}"
  All matched files use Prettier code style.

dotnet format ControlTower.sln --verify-no-changes --no-restore --include <P1-T05 C# files>
  [no output; exit 0]

git diff --check
  [no output; exit 0]

targeted credential/private-key scan
  [no sensitive-value match; gitleaks is not installed locally and remains a required PR check]
```

The repository-wide `dotnet format --verify-no-changes` also reports whitespace in the unchanged
forbidden file `EntityResolutionService.cs`. Its working-tree object and `origin/main` object are
identical (`59805cd1b85f9add92809d1605b1f1139af19f90`); all P1-T05 C# files pass the targeted
formatter check. The required repository CI format job checks Markdown/YAML/JSON with Prettier.

## Independent review

- Blueprint/test reviewer: no remaining P0, P1 or P2 findings after the canonical names were aligned,
  all 41 production event contracts were exhaustively locked, and the explicit adversarial test
  obligations were closed.
- Architecture/security reviewer: no remaining P0, P1 or P2 findings after global event-ID
  uniqueness, snapshot-once event metadata and independent expected-tenant verification were added.

## Explicitly deferred

Integrity format 1 represents the fields present in the current skeleton. It does **not** claim the
complete E20 semantic record or production-grade evidence. P1-T06 must add `aggregateRef`, opaque
`AuditActor`, optional reason and `correlationRef`, then issue the completed integrity format before
P1-T07 authors migration 0001. Trusted-checkpoint persistence, Blob/WORM anchoring and verification
jobs also remain mandatory later production-foundation work.

No outbox transaction, durable event store, PostgreSQL adapter, migration, WORM anchor, production
configuration or tenant action was introduced.

## Merge readiness report

- Blueprint alignment: PASS; Stage 4 canonical names and Stage 5 G1/G2/E20 are enforced.
- ADR compliance: PASS; one append-only platform event model, no context or infrastructure drift.
- Tests passed: PASS; 189 backend and 114 SPA.
- CI status: pending branch publication.
- Security status: PASS locally; PR gitleaks remains authoritative.
- Architecture status: PASS; 9/9 architecture tests and independent review clean.
- Technical debt introduced: none. P1-T06 completion work is an explicit approved dependency, not
  an omitted production claim.
- Known risks: an unanchored stream proves only its supplied prefix; the verifier reports that
  honestly.
- Deviations requested: none.
- Merge recommendation: merge when all required PR checks are green.

## Rollback

Revert the P1-T05 PR. No persistent data, migration, tenant, infrastructure or production
configuration changed.
