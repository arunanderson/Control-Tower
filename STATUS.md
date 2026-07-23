# Enterprise AI Control Tower — Build Status

_Single source of build truth. Updated by the build agent as part of every task's Document step._

| Field               | Value                                                                                                        |
| ------------------- | ------------------------------------------------------------------------------------------------------------ |
| **Current phase**   | Phase 1 — production security, privacy and durable-data foundation                                           |
| **Current task**    | P1-T05 — canonical event envelope and integrity verifier                                                     |
| **Overall state**   | **Event integrity v1 is green; complete E20 and durable production foundations remain.**                     |
| **Product outcome** | One role-appropriate Control Tower for all technically observable AI use across the corporate-managed estate |
| **Merge policy**    | Merge trains — agent may merge tenant-independent green PRs with a Merge Readiness Report                    |
| **Last updated**    | 2026-07-23                                                                                                   |
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
hardens the canonical event envelope and integrity verifier.

These are **implemented development slices**, not evidence that the production SaaS is finished.

## Current production-foundation train

P1-T02 through P1-T04 established the cryptographically validated Entra human and tenant boundary,
tenant-scoped C8 role/capability authorization and the delegated SPA bearer session. P1-T05 now
hardens the single existing E20 event backbone before durable storage:

- integrity format 1 covers version, tenant, position, globally unique event ID, stable event type,
  occurred/recorded UTC microseconds, privilege classification and opaque payload bytes;
- SHA-256 chains the prior binary digest and a culture/architecture-independent canonical envelope;
- caller buffers cannot mutate retained payloads, and tenant, position and recorded time remain
  store-controlled;
- verification checks structure and cryptography against an independently supplied tenant;
- unanchored integrity is reported honestly, while a separately trusted checkpoint detects suffix
  loss and an internally valid substituted tail;
- all 41 concrete production events have one architecture-locked canonical name and classification,
  including the exact four currently privileged events.

The tenant-independent implementation is locally proven with 189 backend and 114 SPA tests. It is a
provisional integrity frame, not a complete E20 or production evidence claim. P1-T06 must add
aggregate reference, opaque actor, reason and correlation metadata before P1-T07 authors the first
permanent event migration.

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
- Durable E18 role assignments and E19 person-key mapping with RLS, transactional audit,
  active-grant uniqueness, optimistic revocation, field protection, O(1) severance and privileged
  map-read/write auditing.
- C8 telemetry-policy history, C5 jurisdiction/population resolution and universal privacy Gate 2.
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

## Next autonomous train

Production identity/privacy foundation:

1. complete E20 and C8 identity/authorization contracts, including opaque actor and audited
   PersonKey lifecycle semantics (P1-T06);
2. author the durable PostgreSQL event kernel and ephemeral migration 0001 validation (P1-T07);
3. author durable E18/E19 persistence and ephemeral migration 0002 validation (P1-T08);
4. continue telemetry-policy, jurisdiction, privacy Gate 2 and policy-as-of Gate 1 enforcement;
5. continue remaining durable PostgreSQL/RLS, transactional outbox and Azure adapter work.

Endpoint or browser events will not be onboarded before the privacy boundary is real.

## Human gates — no immediate action required

- Representative Microsoft tenant/licensing and consent when a real-provider validation task reaches
  that point.
- Legal/Privacy/works-council approval and employee transparency before applicable endpoint or L2+
  production activation.
- Endpoint signing/MDM/security policy before fleet deployment.
- Finance-owned contract/rate-card validation before production economic claims.
- PD-006 ratification of DEV-002 before production release.
- Production Azure credentials, shared-environment migrations and deployment.

## Capability maturity

| Capability                  | Current maturity                                         | Production closure                                                                      |
| --------------------------- | -------------------------------------------------------- | --------------------------------------------------------------------------------------- |
| Platform foundation         | Entra auth + C8 authorization + event integrity v1 green | Complete E20, durable E18/E19, RLS, IaC, observability and DR                           |
| Asset Ledger (C1)           | Domain and in-memory workflow green                      | Durable repository, production authorisation and real-source validation                 |
| Economics (C3)              | Domain and snapshot workflow green                       | Currency-safe aggregation, production rate cards/providers and durable projections      |
| Governance (C2)             | Domain workflow green                                    | Durable workflow, authorisation, notifications/control adapters and production policy   |
| Experience (C7)             | Development SPA/API green                                | Gate 2 everywhere, persona completion, accessibility/e2e and production hosting         |
| Provider framework (C4)     | Contracts, CSV and sweep pipeline green                  | Durable state, production secrets/queue, source adapters and fleet ingestion            |
| Observation pipeline        | Development path green                                   | Policy-as-of storage refusal, typed/minimised payloads and persistent append-only store |
| Entity resolution           | Deterministic development workflow green                 | Full identifier sets, temporal validity, deduplication and real-source rule evidence    |
| Microsoft providers/PoCs    | Sandbox readiness partially exercised                    | Representative data/licensing plus completed provider implementations                   |
| Endpoint/browser visibility | Approved and planned under DEV-002                       | Collector gateway, signed collectors, privacy/security review and managed deployment    |
| Production readiness        | In progress                                              | All critical-path gaps above plus staging evidence and human production gate            |
