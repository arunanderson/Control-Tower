# Enterprise AI Control Tower — Build Status

_Single source of build truth. Updated by the build agent as part of every task's Document step._

| Field               | Value                                                                                                        |
| ------------------- | ------------------------------------------------------------------------------------------------------------ |
| **Current phase**   | Phase 1 — production security, privacy and durable-data foundation                                           |
| **Current task**    | P1-T09 — establish E16 jurisdiction and E17 telemetry-policy foundations                                     |
| **Overall state**   | **P1-T09 is locally complete and green; tenant-independent PR checks are pending.**                          |
| **Product outcome** | One role-appropriate Control Tower for all technically observable AI use across the corporate-managed estate |
| **Merge policy**    | Merge trains — agent may merge tenant-independent green PRs with a Merge Readiness Report                    |
| **Last updated**    | 2026-07-25                                                                                                   |
| **Updated by**      | Codex build agent                                                                                            |

## Product scope now locked

The Control Tower covers the corporate-managed endpoint, browser, identity, network, SaaS, cloud,
agent, API, licence and finance footprint. Named tools such as DeepSeek or ChatGPT are examples, not
boundaries. CIO, CISO/InfoSec, People/Transformation, AI/CoE and Audit experiences are
policy-filtered C7 views over the same evidence.

DEV-002 moves custom managed browser/endpoint collection and technically supportable
enterprise-versus-personal classification into V1. It does **not** change the frozen architecture:
all external signals still enter through C4; C1/C2/C3/C5/C8/C9 retain their authorities; all human
experiences leave through C7; C6 stays vacant.

The product promise is all **technically observable** activity within the managed corporate
footprint, with explicit blind spots, freshness, confidence and policy limits. It will not claim
visibility that cannot be evidenced.

See
`docs/build/plans/ENTERPRISE-OBSERVABILITY-DELIVERY-PLAN.md` and
`docs/build/deviations/DEV-002-enterprise-observability-v1.md`.

## Merged on `main`

PRs #1–#18 delivered the build rails, modular host, in-memory development foundations, Asset Ledger
(C1), Economics (C3), Governance (C2), Experience (C7), provider framework and CSV provider (C4),
observation ingestion, entity resolution, resolution workbench, coverage/freshness, privileged-read
audit, provider sweep jobs, reporting snapshots and legal holds. PR #19 reconciled the build record.
PR #20 recorded the Microsoft sandbox readiness findings. PR #21 records the enterprise-observability
scope amendment and production delivery plan. PR #22 establishes the trusted Entra tenant and human
actor request boundary. PR #23 establishes tenant-scoped C8 role and capability authorization. PR
#24 connects the SPA to Entra delegated Bearer authentication and server-resolved access. PR #25
hardens the canonical event envelope and integrity verifier. PR #26 completes the E20 semantic
record and the C8 E18/E19 identity and authorization contracts. PR #27 persists the E20 kernel in
PostgreSQL with forced RLS, immutable rows and tenant-serialized atomic append. PR #28 persists the
C8 E18/E19 foundations with tenant isolation, privileged field protection and atomic E20 evidence.

These are **implemented development slices**, not evidence that the production SaaS is finished.

## Current production-foundation train

P1-T02 through P1-T04 established the cryptographically validated Entra human and tenant boundary,
tenant-scoped C8 role/capability authorization and the delegated SPA bearer session. P1-T05/P1-T06
now freeze the complete E20 and C8 semantics before durable storage:

- integrity format 2 covers version, tenant, position, event identity/type, aggregate reference,
  typed opaque actor, occurred/recorded UTC microseconds, optional reason/correlation, privilege and
  payload bytes with explicit nullable-presence encoding;
- SHA-256 chains the prior binary digest and a culture/architecture-independent canonical envelope;
- Platform owns the only `PersonKey` and `AuditActor`; public production actor/person slots are
  architecture-enforced and raw Entra/email/GUID workload actor shapes fail closed;
- E19 is a tenant-bound, bidirectional and O(1)-severable raw-identity perimeter whose every
  protected read is recorded through the complete C9 event, sink and customer-visible projection
  path before release;
- E18 assignment/revocation is versioned, optimistic, atomic at the adapter seam and returns typed
  authoritative/idempotent/conflict results under hostile concurrency;
- Host resolves the human to `PersonKey` once and persists only the server-resolved opaque actor;
- invalid evidence and provider-attribution mismatch fail before domain/observation mutation.

The local implementation gates are green with 253 backend and 114 SPA tests. P1-T07 adds the first durable
implementation behind the unchanged event-store port: migration 0001, forced RLS, a non-owner
runtime role, transaction-local tenant binding, database-enforced immutability and atomic
tenant-stream serialization. Apply, verify, guarded rollback, re-apply and hostile integration
tests pass only against the approved disposable PostgreSQL image.

P1-T08 adds Trust-owned PostgreSQL outer adapters behind the unchanged E18/E19 ports. Migration
0002 provides forced-RLS role assignments, a privileged field-protected person-key perimeter,
tenant-keyed O(1) lookup, atomic state-plus-E20 evidence, audit-before-release and constant-time
severance. Its apply, verify, guarded rollback, exact baseline restoration, re-apply and hostile
suite are green on disposable `postgres:16.14-alpine3.24`. No shared, staging or production
migration ran; no production key provider, Host composition, cloud resource or WORM anchor was
introduced.

The Product Owner approved adding only
`tests/ControlTower.Host.Web.Tests/RoleAuthorizationTests.cs` to P1-T08's scope solely to normalize
the stale timestamp fixture exposed by Linux CI. The fixture now uses the existing production
canonicalizer; production validation and application code are unchanged. Fresh local verification
is green: the targeted regression is 1/1, the backend is 253/253, PostgreSQL is 26/26, the hostile
class is 12/12, architecture is 15/15 and the SPA is 114/114. All nine PR #28 workflows are green,
including Release build with zero warnings/errors, Linux backend, gitleaks and protected paths.

P1-T09 establishes the frozen privacy-policy authorities without connecting either enforcement
gate. Platform owns the sole L1–L4 marking and opaque privacy references. C5 owns tenant-scoped E16
simple versioned jurisdiction-ceiling history, while C8 owns tenant-scoped E17 bitemporal telemetry
policy, current E16 ceiling validation and conservative jurisdiction/population-aware resolution.
Both development adapters fail closed, reject hostile tenant and event tuples, and preserve atomic
state-plus-canonical-event behavior at the in-memory seam. Existing C4 ingestion remains L1 by
default.

The Product Owner authorized correction of the original P1-T09 contract where it contradicted the
frozen blueprint: E16 is Versioned, not Bitemporal; E17 remains Bitemporal. DEV-003 records this
contract correction without changing the blueprint or ADRs. Fresh local verification is green:
303/303 backend, 14/14 C5, 34/34 C8, 26/26 disposable PostgreSQL, 17/17 architecture and 114/114
SPA; Release build has zero warnings and zero errors. GitHub PR workflows remain pending.

## Microsoft sandbox evidence

- App-only authentication and Agent ID reads succeeded.
- Agent ID inventory is empty.
- The tenant lacks representative agent archetypes and a published Copilot Studio/Foundry test
  estate.
- Graph Package Management v1.0 and beta both reject the unlicensed tenant with `403`; Microsoft Agent
  365 licensing is confirmed as a provider prerequisite.
- PoC-1/2/3 remain incomplete for correlation, manifest recovery, coverage and throttling.

Microsoft Agent 365 is not a dependency for the Control Tower. It is one optional evidence source.

## Production gaps on the critical path

- JIT staff access and production Entra app/onboarding configuration and consent.
- Durable and Host-composed E16/E17 authorities, authoritative applicability data and universal
  privacy Gate 2. The domain and development-adapter foundations are complete.
- Policy-as-of storage refusal at Gate 1; the current development ingestion path must not be used for
  endpoint telemetry until this is complete.
- Azure PostgreSQL/RLS repositories, transactional event/outbox semantics, durable tenant watermarks
  and job receipts.
- Azure Service Bus retry/DLQ, Key Vault credentials, Blob/WORM anchors and verification jobs.
- Export, erasure, tenant offboarding deletion and authoritative retention enforcement.
- Microsoft, security/network, SaaS, cloud, finance and vendor providers.
- Tenant-bound first-party browser and endpoint collection, signing, fleet deployment and health.
- Cross-source deduplication, temporal semantics, complete identifier preservation and multi-currency
  economics.
- Role-complete CIO, CISO, People/Transformation, AI/CoE and Audit experiences.
- Azure IaC, observability, performance, threat-model, DR, staging and production evidence.

## Approval boundary

P1-T09 is complete and green locally. Its corrected contract, implementation, evidence and
build-control state are aligned with the frozen blueprint. The tenant-independent PR may merge only
after all required GitHub checks pass and no reviewer finding remains.

After merge, the next bounded implementation slice is universal C7 read-time Privacy Gate 2 over
the shared C5/C8 authorities. Policy-as-of storage refusal at C4 Gate 1 follows Gate 2.

Endpoint or browser events will not be onboarded before the privacy boundary is real.

## Human gates

- Representative Microsoft tenant/licensing and consent when a real-provider validation task reaches
  that point.
- Legal/Privacy/works-council approval and employee transparency before applicable endpoint or L2+
  production activation.
- Endpoint signing/MDM/security policy before fleet deployment.
- Finance-owned contract/rate-card validation before production economic claims.
- PD-006 ratification of DEV-002 before production release.
- Production Azure credentials, shared-environment migrations and deployment.

## Capability maturity

| Capability                  | Current maturity                                                     | Production closure                                                                      |
| --------------------------- | -------------------------------------------------------------------- | --------------------------------------------------------------------------------------- |
| Platform foundation         | Entra auth, durable E20/E18/E19 and E16/E17 domain foundations green | Complete production composition, persistence, IaC, observability and DR                 |
| Asset Ledger (C1)           | Domain and in-memory workflow green                                  | Durable repository, production authorisation and real-source validation                 |
| Economics (C3)              | Domain and snapshot workflow green                                   | Currency-safe aggregation, production rate cards/providers and durable projections      |
| Governance (C2)             | Domain workflow green                                                | Durable workflow, authorisation, notifications/control adapters and production policy   |
| Experience (C7)             | Development SPA/API green                                            | Gate 2 everywhere, persona completion, accessibility/e2e and production hosting         |
| Provider framework (C4)     | Contracts, CSV and sweep pipeline green                              | Durable state, production secrets/queue, source adapters and fleet ingestion            |
| Observation pipeline        | Development path green                                               | Policy-as-of storage refusal, typed/minimised payloads and persistent append-only store |
| Entity resolution           | Deterministic development workflow green                             | Full identifier sets, temporal validity, deduplication and real-source rule evidence    |
| Microsoft providers/PoCs    | Sandbox readiness partially exercised                                | Representative data/licensing plus completed provider implementations                   |
| Endpoint/browser visibility | Approved and planned under DEV-002                                   | Collector gateway, signed collectors, privacy/security review and managed deployment    |
| Production readiness        | In progress                                                          | All critical-path gaps above plus staging evidence and human production gate            |
