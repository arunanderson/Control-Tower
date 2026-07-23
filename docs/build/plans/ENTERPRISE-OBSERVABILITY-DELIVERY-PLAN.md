---
id: ENTERPRISE-OBSERVABILITY-DELIVERY-PLAN
title: Enterprise Observability and Production Readiness Delivery Plan
type: delivery-plan
schema_version: 1
status: approved
authority:
  - docs/build/deviations/DEV-002-enterprise-observability-v1.md
  - docs/blueprint/implementation-handoff-package.md
  - docs/blueprint/stage-07-conceptual-architecture.md
  - docs/blueprint/stage-08-security-trust-architecture.md
  - docs/blueprint/stage-09-technology-deployment.md
requires_human_approval: true
approved_by: Product Owner direct enterprise-visibility clarification and instruction to continue, 2026-07-23
---

## 1. Outcome

Deliver one production-ready Enterprise AI Control Tower that:

1. discovers and resolves AI assets across vendors;
2. observes AI activity across the corporate-managed endpoint, browser, identity, network, SaaS,
   cloud, agent and API footprint;
3. joins usage to ownership, organisation, spend, value, risk and governance;
4. gives each authorised persona the right view of the same evidence;
5. proves coverage and exposes blind spots, freshness and confidence;
6. preserves privacy, employee trust, tenant isolation and evidence integrity.

No collector, integration or persona creates a new bounded context. All implementation is an
extension of the frozen architecture under DEV-002.

## 2. One product, many evidence sources

```text
managed endpoints ─┐
managed browsers ──┤
identity/network ──┤
SaaS/vendor APIs ──┼─> C4 acquire/validate/privacy/append ─> events ─> C1/C2/C3/C5/C8/C9
cloud/agent/API ───┤                                             │
finance/licences ──┘                                             └─> C7 role-filtered views
```

One Control Tower does not mean one collection mechanism. It means one normalised evidence model,
one coverage model, one policy boundary and one place to make decisions.

## 3. Role outcomes

| Persona                                 | Default decision view                                                                                             | Data boundary                                                                      |
| --------------------------------------- | ----------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------- |
| CIO                                     | portfolio adoption, duplication, strategic coverage, spend, value and rationalisation                             | aggregated, evidence-labelled, organisation-level                                  |
| CISO / InfoSec                          | shadow AI, risky services, control gaps, device/identity correlation, policy incidents and investigation evidence | least-privilege; L2+ only through purpose-bound audited workflow                   |
| Chief People and Transformation Officer | aggregate adoption, change progress, capability opportunities and organisational patterns                         | no employee scoring; individual metadata concealed unless independently authorised |
| AI / CoE operator                       | inventory, owners, lifecycle, provider health, resolution, governance and ROI operations                          | operational scope constrained by role, organisation and telemetry policy           |
| Auditor / Risk                          | decisions, evidence lineage, policy history, privileged reads, holds, exports and integrity proof                 | read-only, evidence-scoped and fully audited                                       |

All experiences are C7 projections over the same policy-enforced read models.

## 4. Ordered delivery trains

### Train A — Product and build truth

- Record DEV-002 and this plan.
- Reconcile PR 20 sandbox evidence and remove stale Cursor/tenant wording.
- Replace ambiguous "complete" claims with development-slice versus production-maturity truth.

**Exit:** repository states the product outcome, architecture mapping, coverage promise and gates.

### Train B — Security, privacy and durable truth

- Entra federation authentication and workload identity seams.
- Role, organisation and purpose-bound authorisation.
- Unforgeable request/job tenant context and adversarial isolation suite.
- C8 telemetry policy history and C5 population/jurisdiction resolution.
- Privacy Gate 2 for every C7 read; storage-refusal Gate 1 completed against policy-as-of.
- PostgreSQL/RLS repositories for observations, events, connections, receipts, domain state and
  projections.
- Transactional domain-event/outbox persistence and tenant-partitioned watermarks.
- Key Vault credential adapter, Service Bus scheduling/retry/DLQ adapter and Blob WORM anchoring.
- Export, erasure, offboarding deletion, retention and integrity verification.

**Exit:** two-tenant attack fixtures cannot cross boundaries; L1–L4 privacy fixtures cannot
overexpose; restart/retry cannot lose or duplicate authoritative facts; no in-memory production path.

### Train C — Native enterprise telemetry

- Microsoft Entra Agent ID, PPAC/Dataverse, Graph Package Management and licence/usage providers.
- Purview/Defender and Microsoft 365 audit providers for AI site/application activity where licensed.
- Azure Resource Manager, Azure Monitor and Cost Management providers.
- Provider permission packs, health, freshness, throttling, watermarks, quarantine and coverage.
- Equivalent supported network/security, SaaS and cloud providers through the same C4 contract.

**Exit:** sandbox/fixture sweeps land immutable, privacy-marked observations and honest coverage facts;
real-tenant validation remains evidence-gated per source.

### Train D — First-party managed collection

- Tenant-bound collector enrolment and signed event envelope.
- C4 push-ingestion gateway with authentication, anti-replay, schema versioning, rate budgets and
  quarantine.
- Managed browser extension for supported corporate browsers.
- Managed endpoint collector for supported corporate desktop/laptop operating systems, including
  installed AI applications, process/use metadata and local-model runtimes.
- MDM packaging, deployment health, kill switch, privacy configuration and automatic updates.
- Coverage projection for device enrolment, heartbeat, version, tamper/degradation and unsupported
  surfaces.

**Exit:** approved test devices demonstrate browser, desktop application and local-model activity
without collecting prompt/response, keystroke, document or screen content; disabled or unhealthy
collection appears as a blind spot.

### Train E — Cross-source intelligence

- Preserve complete native identifier sets and temporal validity.
- Resolve endpoint/browser/network/SaaS/cloud evidence to assets, identities, devices and
  organisation with categorical confidence.
- Snapshot/event/time-bucket semantics prevent presence telemetry from being counted as interactions.
- Multi-currency economics, contract/licence cost, token/request cost and Unattributed treatment.
- Usage, adoption, anomaly and rationalisation projections with evidence class, as-of and methodology.
- Governance cases and native-control orchestration consume findings without becoming enforcement.

**Exit:** the same activity is not double-counted across sources; uncertain joins stay uncertain;
every number and finding is explainable.

### Train F — One Control Tower experience

- CIO portfolio and economics view.
- CISO coverage, shadow-AI, risk and investigation view.
- People/Transformation aggregate adoption and change view.
- AI/CoE operations, Asset Record and resolution workbench.
- Trust area covering consent, source coverage, freshness, confidence, blind spots, privileged access
  and evidence integrity.
- Board pack and exports generated from the same read models.

**Exit:** canonical persona journeys pass policy, accessibility, evidence-labelling and export-parity
tests.

### Train G — Production hardening and release preparation

- Bicep Azure regional stamp, Container Apps, PostgreSQL, Service Bus, Key Vault, immutable Blob,
  monitoring, alerting and budgets.
- Container/dependency/secret/SAST/DAST scanning and threat-model closure.
- Load, soak, failure-isolation, provider-throttling and endpoint-fleet tests.
- Backup/restore, regional recovery, retention/legal-hold and integrity drills.
- Operational runbooks, support/JIT process, tenant onboarding and deletion rehearsal.
- Staging deployment and end-to-end sandbox evidence.

**Exit:** production readiness report has no unresolved critical control, evidence or architecture
gap. Production deployment remains a human gate.

## 5. Coverage truth model

For every tenant and acquisition surface, C7 must display:

- connection and consent state;
- enabled capabilities and telemetry-policy limit;
- enrolled/expected population or resource scope where knowable;
- last attempted and successful acquisition;
- freshness target and current state;
- correlation quality;
- known unsupported or withheld areas;
- provider/collector health and version;
- evidence class for every derived claim.

No percentage may be labelled "AI usage coverage" without a defensible denominator and methodology.

## 6. Immediate autonomous critical path

1. Close P0-T18.
2. Implement production authentication/authorisation and privacy-policy foundations.
3. Replace shared in-memory truth with PostgreSQL/RLS and transactional outbox semantics.
4. Add Azure production adapters and IaC as gated task contracts.
5. Implement native providers and first-party collector gateway in parallel once the privacy and
   identity boundaries are green.

Microsoft Agent 365 licensing is not a dependency for the product. It gates only the evidence that
specific Agent 365 APIs can supply.

## 7. Human gates and access needed later

No additional access is required for the immediate tenant-independent work. The build will stop only
when it reaches:

- Entra/Microsoft tenant permission or consent actions;
- representative tenant licences and data needed for real-source validation;
- Legal/Privacy/works-council approval for applicable L2+ or endpoint collection;
- endpoint signing, MDM policy and enterprise security allow-list actions;
- Finance-owned rate cards, contracts or validated cost mappings;
- production Azure credentials, shared-environment migration approval or production deployment;
- PD-006 ratification of DEV-002.

## 8. Non-goals

- No prompt/response surveillance by default.
- No employee productivity scoring or automated employment decisions.
- No replacement SIEM, EDR, DLP, identity platform, agent builder or cloud control plane.
- No separate persona products, telemetry bounded context or C6 revival.
- No unsupported claim of universal visibility.
