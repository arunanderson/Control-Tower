# Enterprise AI Control Tower — Build Status

_Single source of build truth. Updated by the build agent as part of every task's Document step._

| Field               | Value                                                                                                        |
| ------------------- | ------------------------------------------------------------------------------------------------------------ |
| **Current phase**   | Phase 1 — production security, privacy and durable-data foundation                                           |
| **Current task**    | P1-T02 — trusted Entra tenant and human actor request boundary                                               |
| **Overall state**   | **Trusted HTTP identity is green; the production product remains materially incomplete.**                    |
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
actor request boundary.

These are **implemented development slices**, not evidence that the production SaaS is finished.

## Current production-foundation train

P1-T02 replaces the caller-controlled HTTP tenant and actor headers with a cryptographically
validated human request boundary:

- the ASP.NET Core bearer handler validates signature, exact API audience, lifetime and a strict
  Entra v2 tenant issuer;
- Microsoft's supported signing-key issuer validator binds tenant-independent OIDC JWK metadata to
  the token issuer and tenant;
- exactly one non-empty `tid`, `oid` and `sub` plus the configured delegated scope are required;
- an external directory tenant maps one-to-one through a server-side allowlist to the internal
  `TenantId`;
- the canonical audit actor is `entra:{tid}:{oid}` and cannot be replaced by request headers;
- `/health` and `/ready` are the only anonymous endpoints; development command APIs remain absent
  from Production until role/capability authorisation exists.

The boundary is tenant-independent and locally proven with ephemeral signing material. Connecting
it to a real tenant still requires production app registration values and tenant onboarding.

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

- Production role/capability/org-scope authorisation, SPA bearer integration, JIT staff access and
  production Entra app/onboarding configuration.
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

1. role/capability/org-scope authorisation and SPA bearer integration;
2. telemetry-policy and jurisdiction ports;
3. read-time Gate 2 plus policy-as-of Gate 1 storage refusal;
4. adversarial isolation tests spanning authentication, authorisation and privacy;
5. durable PostgreSQL/RLS and transactional outbox work after those boundaries are test-enforced.

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

| Capability                  | Current maturity                         | Production closure                                                                      |
| --------------------------- | ---------------------------------------- | --------------------------------------------------------------------------------------- |
| Platform foundation         | Entra JWT human boundary green           | Role/org authorisation, RLS, durable adapters, IaC, observability and DR                |
| Asset Ledger (C1)           | Domain and in-memory workflow green      | Durable repository, production authorisation and real-source validation                 |
| Economics (C3)              | Domain and snapshot workflow green       | Currency-safe aggregation, production rate cards/providers and durable projections      |
| Governance (C2)             | Domain workflow green                    | Durable workflow, authorisation, notifications/control adapters and production policy   |
| Experience (C7)             | Development SPA/API green                | Gate 2 everywhere, persona completion, accessibility/e2e and production hosting         |
| Provider framework (C4)     | Contracts, CSV and sweep pipeline green  | Durable state, production secrets/queue, source adapters and fleet ingestion            |
| Observation pipeline        | Development path green                   | Policy-as-of storage refusal, typed/minimised payloads and persistent append-only store |
| Entity resolution           | Deterministic development workflow green | Full identifier sets, temporal validity, deduplication and real-source rule evidence    |
| Microsoft providers/PoCs    | Sandbox readiness partially exercised    | Representative data/licensing plus completed provider implementations                   |
| Endpoint/browser visibility | Approved and planned under DEV-002       | Collector gateway, signed collectors, privacy/security review and managed deployment    |
| Production readiness        | In progress                              | All critical-path gaps above plus staging evidence and human production gate            |
