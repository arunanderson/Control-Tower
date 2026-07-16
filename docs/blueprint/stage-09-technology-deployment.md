# Stage 9 — Technology & Deployment Architecture

| | |
|---|---|
| **Version** | 1.0 |
| **Date** | 2026-07-15 |
| **Status** | Draft — awaiting Arun's approval. Proposes ADR-022 (build platform) and ADR-023 (target stack). |
| **Related** | Constrained by [stage-07-conceptual-architecture.md](stage-07-conceptual-architecture.md) (ADR-020) and [stage-08-security-trust-architecture.md](stage-08-security-trust-architecture.md) (ADR-021); serves the data model (Stage 5) and experience architecture (Stage 6) |

**Method:** requirements-first; every recommendation names the ADR/requirement it satisfies. All named Azure/Microsoft services are long-established GA offerings as of this date [Confirmed-existing]; **exact SKUs, limits, and pricing must be re-validated at build kickoff** (quarterly ritual applies to this document more than any other). Guardrails honoured: no microservices, Kubernetes clusters, streaming infrastructure, API gateways, or distributed-systems machinery — none earned a justification.

---

## 1. Decision Zero — build platform: custom Azure application vs Power Platform

*The most consequential technology decision; full treatment as instructed.*

- **Requirement:** commercial multi-tenant SaaS (ADR-001), modular monolith with enforced boundaries (ADR-020), storage-refusal privacy gating and RLS-grade tenant isolation (ADR-021), append-only stores with integrity chaining (Stage 5/8), plug-in provider contracts (ADR-007), single polymorphic experience (ADR-019).
- **Options evaluated:** (a) Power Platform/Dataverse application; (b) custom application on Azure PaaS; (c) hybrid (Dataverse data + custom services).
- **Recommendation: (b) custom Azure application.**
- **Why Power Platform is rejected — on requirements, not taste:** (1) *Commercial model:* Dataverse-based products require customers' Power Platform licensing/environments or expensive ISV embedding — incompatible with selling vendor-neutral SaaS to arbitrary enterprises (including non-Microsoft-first ones, our cross-vendor promise). (2) *Multi-tenancy:* Dataverse tenancy is environment-per-customer, not row-level multi-tenant SaaS — fleet economics and blast-radius model (ADR-021.11) don't fit. (3) *Architecture fit:* append-only event/observation stores with hash chaining, storage-refusal ingestion gates, plug-in adapter contracts, and CI-enforced module boundaries are all fighting the platform rather than using it. (4) *Independence positioning:* a product whose moat is vendor-independent measurement, built inside the measured vendor's low-code platform, invites both technical and narrative dependence. **Power Platform remains excellent for what it is; it is the wrong chassis for this product.** (c) rejected: inherits (1)+(2) while adding integration seams.
- **Security/multi-tenancy/ops/cost/lock-in:** custom Azure gives full control of the ADR-021 model; costs are consumption-based and fleet-efficient; lock-in is to Azure PaaS (accepted consciously — we are Microsoft-first by strategy; the *code* stays portable via containers and open runtimes).
- **Reconsideration trigger:** none foreseeable — this decision is structural. → **ADR-022.**

## 2. Application platform decisions

| # | Decision | Requirement | Recommended | Options rejected (why) | Reconsider when |
|---|---|---|---|---|---|
| 2.1 | Backend runtime | Deep Graph/ARM/Entra SDK support; enterprise talent; long-term support; monolith discipline tooling | **.NET (C#)**, single solution, one module per bounded context, architecture tests enforcing I1–I4 | Node/TS (weaker typed module enforcement at this scale); Java (equal capability, weaker first-party Microsoft SDK cadence); Python (ops maturity for long-lived monolith) | Never for V1; team composition could reopen pre-build |
| 2.2 | Frontend | Five-area IA, data-dense operator UX, polymorphic Asset Record (ADR-019) | **React + TypeScript** SPA | Blazor (smaller ecosystem/talent for rich data UX); Angular/Vue (viable; React chosen on ecosystem + hiring) | Team skills at build kickoff |
| 2.3 | Modular monolith structure | ADR-020.2; enforced seams | One deployable **web app** + one **worker** (same codebase, two processes); modules = C1..C9; in-process events with **outbox**; the Stage 7 plane seam is an authenticated internal contract from day one | Separate services per context (forbidden without justification) | Proven scaling/security/deployment need (ADR-020) |

## 3. Data platform decisions

| # | Decision | Requirement | Recommended | Rejected | Reconsider when |
|---|---|---|---|---|---|
| 3.1 | Primary operational store | Relational integrity + polymorphic payloads (Stage 5: 20 entities, JSON attribute bags) + row-level tenant isolation + as-of queries | **Azure Database for PostgreSQL (Flexible Server)** — relational core + JSONB for observation payloads/alias sets + **row-level security** bound to unforgeable per-request tenant context | Azure SQL (viable near-equal; JSONB ergonomics and licence-cost economics favour PostgreSQL); Cosmos DB (relational workload, cross-document consistency needs, cost unpredictability); Dataverse (see §1) | Azure SQL substitution acceptable if org standardises on it — decision is *one relational store with RLS*, engine second |
| 3.2 | Immutable observation storage | Append-only, retention classes (ADR-017), volume control (R-18) | Same PostgreSQL, **append-only partitioned tables** (time+tenant), triggers denying update/delete; **archival tiering to Azure Blob Storage with immutability (WORM) policies** per retention class | Separate specialised store in V1 (unearned complexity); data lake first (no analytics case yet) | Observation volume or analytics demand at commercial scale |
| 3.3 | Event persistence | Events = audit (ADR-015.8); hash-chained integrity (ADR-021) | Append-only event table (same store), **hash-chained**, with periodic **anchor digests written to WORM Blob**; archived segments tiered to WORM | EventStoreDB/Kafka (streaming machinery, forbidden without case); Cosmos change-feed patterns (unearned) | Event volume, or hybrid split (anchoring across planes) |
| 3.4 | Projection storage | Disposable, rebuildable read models | Separate schema, same PostgreSQL; rebuild jobs in worker | Separate read-store (Redis/Elastic) in V1 | Measured read-latency pain |
| 3.5 | Analytics platform | V1 dashboards = read models; no big-data case (thousands of assets) | **None beyond the database in V1.** Curated Parquet/CSV export (V2, C7.5) feeds customers' own BI; Fabric/Synapse explicitly not adopted | Fabric/Synapse/warehouse (volumes don't justify; ADR-005 keeps us out of the BI business) | Cross-tenant benchmarking build (PL-003) |
| 3.6 | Search | Global lookup by name/alias (Stage 6) — exact-match dominant | PostgreSQL indexes + full-text for names | Azure AI Search (unearned; add if faceted/fuzzy demand proven) | Operator feedback post-V1 |

## 4. Identity, isolation, secrets

| # | Decision | Requirement | Recommended | Rejected | Reconsider |
|---|---|---|---|---|---|
| 4.1 | Identity platform | Federation-only (ADR-021); customer Entra consent model (Stage 8 §4) | **Microsoft Entra ID**: multi-tenant app for customer SSO + the consented data-access app (separate app registrations for interactive vs data-plane, cert-credentialed); platform workloads use **managed identities** | Custom IdP/local accounts (forbidden); Entra External ID (B2C-style — wrong audience) | n/a |
| 4.2 | Tenant isolation implementation | ADR-021 §3; no cross-tenant enumeration | RLS (3.1) + tenant context injected at request/job boundary (unforgeable in code) + per-tenant Service Bus session/queue budgets + per-tenant storage prefixes; dedicated-instance tier later via deployment stamps (§6) | Schema-per-tenant (migration burden at fleet scale); DB-per-tenant V1 (cost; keep as premium tier option) | Premium-tier demand |
| 4.3 | Secrets & credential isolation | Credentials isolated more strongly than data (ADR-021) | **Azure Key Vault**: separate vaults per plane; per-tenant secret partitioning + RBAC such that adapters resolve only their tenant's credential; certificates over client secrets; rotation automated. **Per-tenant vault** as premium/regulated tier [cost model requires validation] | Secrets in config/app settings (forbidden); single shared vault for everything (violates ADR-021.2) | Vault-per-tenant economics at fleet scale |

## 5. Processing, integration, output

| # | Decision | Requirement | Recommended | Rejected | Reconsider |
|---|---|---|---|---|---|
| 5.1 | Background jobs | Idempotent, watermarked, quarantining, tenant-budgeted (Stage 7 §6) | **Azure Service Bus** (queues + scheduled messages + DLQ-as-quarantine) driving the worker process | Kafka/Event Hubs (streaming, forbidden); Functions-per-job sprawl (fragments the monolith); in-app-only scheduler (no durable retry/DLQ semantics) | n/a — queues are the earned amount of infrastructure |
| 5.2 | Integration execution | Provider plug-in contract (ADR-007/C4.5), testable, contract-enforced | Provider adapters as **in-worker plug-in modules** implementing the C4.5 manifest; no external iPaaS | Logic Apps/Power Automate (per-connector economics, weak contract testing, drift risk); Azure Data Factory (ETL tool, wrong shape) | A provider requiring long-running orchestration beyond worker patterns |
| 5.3 | File import | ADR-013 CSV path with validation + staged commit | Blob upload → worker validation pipeline → observations via manual-import provider | Direct-to-DB import (bypasses the C4 door — forbidden by I3) | n/a |
| 5.4 | Export & board pack | Same read models as dashboards (ADR-019.3) | Server-side generation in worker (HTML template → PDF), artefacts to tenant-scoped Blob with expiring links | Client-side export (inconsistent numbers risk); reporting-server products (ADR-005) | n/a |
| 5.5 | Evidence integrity mechanism | Verifiable integrity (ADR-021.9) | Hash chain (3.3) + **WORM Blob anchors** + verification job + integrity proof included in evidence exports; **Azure Confidential Ledger evaluated as premium upgrade [requires validation — fit/cost]** | Blockchain-adjacent machinery (theatre); trust-us-it's-append-only (fails the requirement) | Regulator/customer demand for third-party anchoring |
| 5.6 | Monitoring & observability | Ops + the Trust area needs health facts | **Azure Monitor + Application Insights** via **OpenTelemetry** instrumentation (portability of instrumentation preserved); coverage-map health derives from platform events, not from the APM (product truth ≠ ops tooling) | Third-party APM in V1 (unearned cost) | Fleet scale / multi-cloud ops |

## 6. Deployment, environments, resilience

| # | Decision | Requirement | Recommended | Rejected | Reconsider |
|---|---|---|---|---|---|
| 6.1 | Deployment platform | Two processes, containers, no cluster ops (ADR-020) | **Azure Container Apps** (web + worker as containers; managed scale; no cluster administration). Containerisation is also the **hybrid data-plane enabler**: the data-plane container deployable into a customer environment later (ADR-020.1) | AKS (forbidden without justification); App Service (acceptable fallback — containers chosen for the hybrid option); VMs (no) | If ACA constraints bite, App Service is the sanctioned retreat |
| 6.2 | IaC | Reproducible stamps | **Bicep** (Azure-native, first-party) | Terraform (fine; chosen against only because estate is Azure-only — org standard may override) | Multi-cloud tooling mandate |
| 6.3 | CI/CD | Boundary-enforcing builds (R-23: architecture tests in CI) | **GitHub Actions**: build → architecture tests (I1–I4, module seams) → security scans → deploy per environment | Azure DevOps (equivalent; org preference may override) | Org standard |
| 6.4 | Environment strategy | Safe change + data residency | dev → test → staging → prod; **prod as regional stamps** (EU first — Quadient; US/APAC as commercial demand arrives); **tenant pinned to stamp** = residency answer without per-tenant infra | Per-tenant environments (fleet-uneconomic); single global prod (fails residency) | Regulated-market entries |
| 6.5 | Backup & recovery | Rebuildability doctrine (Stage 7 §9.3) + PITR | DB point-in-time restore + geo-redundant backups; WORM Blob is its own protection; **recovery order: restore append-only stores → rebuild everything else** (projections/aliases are derived) | Backup-everything-equally (wasteful; derived stores don't need it) | n/a |
| 6.6 | Disaster recovery | Honest targets over heroic ones | Paired-region DR; initial targets **RPO ≤ 1 h, RTO ≤ 8 h** (V1-honest; sweeps re-fill gaps since sources retain their own data — the platform's DR is intrinsically forgiving) | Active-active multi-region V1 (unearned cost/complexity) | Commercial SLA pressure |
| 6.7 | Hybrid compatibility | ADR-020.1 (option preserved, not built) | Data-plane container + authenticated plane contract (§2.3) + Gate-1 privacy filtering living in the data plane = the future in-tenant deployment needs packaging + split anchoring work (Stage 8 §16), **not redesign** | Building hybrid now (forbidden by ADR-020.1) | First committed residency-blocked customer |

## 7. Target architecture (one paragraph)

A containerised .NET modular monolith (web + worker) on Azure Container Apps, PostgreSQL with RLS as the single store (operational, append-only observation/event partitions, projection schemas), Service Bus for jobs, Key Vault with plane-separated vaults and per-tenant credential partitioning, Entra ID for all identity, hash-chained events anchored to WORM Blob, React/TypeScript front end, Bicep + GitHub Actions, deployed as regional stamps with tenants pinned to regions — EU stamp first. Nothing distributed that didn't earn it; every seam that the hybrid future needs already authenticated and containerised.

## 8. Challenges applied

1. **The stack is deliberately boring.** One database, one queue, two processes. Every exotic component was interrogated and none survived. The differentiators of this product are the ledger semantics, honesty model, and governance workflows — none of which are bought with infrastructure.
2. **PostgreSQL over Azure SQL** is the closest call in the document (3.1) — decided on JSONB ergonomics + cost; explicitly reversible pre-build without architectural consequence.
3. **Container Apps over App Service** is bought *for the hybrid option*, not for scale vanity — if ACA friction appears, App Service is the sanctioned retreat (6.1).
4. **Fabric/Synapse resisted** despite the Microsoft-first instinct — a thousand-asset estate needs a database, not a lakehouse. The analytics platform decision will be re-taken when benchmarking (PL-003) creates a real multi-tenant analytical workload.
5. **Confidential Ledger not adopted by default** — integrity requirements are met by chain+WORM; ACL stays a validated-later premium option rather than a dependency.

---

## Stage-end review

### Summary
Custom Azure application (Power Platform rejected on requirements — ADR-022); boring-by-design stack (ADR-023): .NET modular monolith on Container Apps, PostgreSQL+RLS single store with append-only partitions and WORM-anchored hash-chained events, Service Bus worker, plane-separated Key Vaults, Entra-only identity, regional stamps for residency, hybrid option preserved through containerised data plane and authenticated plane contracts.

### Assumptions
- Team can staff .NET + React (standard enterprise profile); reopen 2.1/2.2 at build kickoff if not.
- PostgreSQL RLS performance adequate at fleet scale [validate at build with representative volumes].
- Per-tenant vault economics acceptable for premium tier only [validate].
- All service capabilities cited are stable GA offerings; SKU-level validation at build kickoff (the quarterly ritual owns this document).

### Confirmed facts
No new Microsoft API claims; services referenced are long-established Azure offerings [Confirmed-existing]; SKU/limit specifics deliberately not asserted.

### Unknowns
ACL fit/cost (5.5); vault-per-tenant economics (4.3); RLS performance envelope (3.1); org-level tooling standards (Bicep/Terraform, GitHub/AzDO) that may override 6.2/6.3.

### Risks
- **R-28 (new):** single-store concentration (PostgreSQL carries operational+observations+events+projections) — accepted for V1 simplicity; partition/archival design (3.2/3.3) is the pressure valve; split triggers documented per decision.
- **R-29 (new):** Azure PaaS lock-in — consciously accepted (Microsoft-first strategy); containers + OpenTelemetry + standard SQL keep the *code* portable; recorded so commercialisation diligence finds a decision, not an accident.

### Alternative approaches considered
Power Platform chassis (rejected — §1, the stage's defining rejection); serverless-everything (Functions sprawl vs monolith discipline); AKS (forbidden, and rightly); event-store/streaming products (no volume case); Fabric-first analytics (no workload case).

### Questions for Arun
1. **Approve ADR-022** (custom Azure application; Power Platform rejected on the four grounds in §1)?
2. **Approve ADR-023** (target stack as §7), with 3.1 (PostgreSQL vs Azure SQL) flagged as the one deliberately reversible-pre-build choice — any organisational standard that should decide it?
3. Organisational tooling standards: GitHub vs Azure DevOps, Bicep vs Terraform — do Quadient standards dictate, or is the recommendation free to stand?
4. **Stage 10 proposal:** Cost, ROI & Value Measurement Methodology (the C3.5 methodology with Finance as co-owner, confidence-labelling rules, allocation approaches). Confirm?

### Recommendations
1. Approve; record §8.1 ("deliberately boring") as the stack's design principle — every future proposal to add infrastructure must name the requirement that earned it.
2. Re-validate all SKU/limit specifics at build kickoff; this document ages faster than any other in the KB.
3. If commercial diligence ever questions Azure concentration (R-29), the answer is the container seam — rehearse it in the data room narrative.
