# Stage 7 — Conceptual Architecture & Integration Architecture

| | |
|---|---|
| **Version** | 1.0 |
| **Date** | 2026-07-15 |
| **Status** | Draft — awaiting Arun's approval |
| **Related** | [stage-02-capability-model.md](stage-02-capability-model.md) (ADR-009 invariants), [stage-03-microsoft-validation.md](stage-03-microsoft-validation.md) (integration evidence), [stage-04-domain-model.md](stage-04-domain-model.md), [stage-05-conceptual-data-model.md](stage-05-conceptual-data-model.md), [decision-log.md](decision-log.md) |

**Scope discipline:** no technologies, no Azure resources, no deployment diagrams. Everything here must survive any reasonable technology selection in Stage 8.

---

## 1. System boundaries and the topology question (OI-002)

The platform has three conceptual planes:

- **Ingestion & data plane** — provider adapters, observation store, resolution, projections. Touches customer credentials and customer telemetry.
- **Domain & control plane** — ledger commands, governance, finance, policy, tenancy, the event backbone.
- **Experience plane** — policy-enforced read models, dashboards, exports (the C7 door).

**OI-002 disposition (evaluated as promised):** the conceptual architecture is **deployment-topology-agnostic by construction**. The tenant data boundary (§7) is drawn so the ingestion & data plane *could* be deployed inside a customer's own environment (in-tenant data plane + SaaS control plane) without redesign — the planes communicate only via events and read contracts, never shared storage. **Recommendation: build V1 as pure multi-tenant SaaS (ADR-001), preserve the split as a documented deployment option for residency-sensitive customers, decide activation commercially, not architecturally.** The split becomes a packaging decision, which is where it belongs. → Question 1.

## 2. Conceptual layer diagram

```
 EXTERNAL SURFACES (Microsoft + future vendors)          CUSTOMER CHANNELS (Teams/email/BI)
        │  consume                                              ▲ orchestrate / publish
┌───────▼──────────────────────────────────────────────────────┴───────────────┐
│ C4 PROVIDER INTEGRATION LAYER — the only door in/out for external systems     │
│  adapters (plug-in) → acquire → contract-validate → PRIVACY FILTER (policy    │
│  as-of) → delta-suppress → append OBSERVATION STORE     [control adapters V2] │
└───────┬───────────────────────────────────────────────────────────────────────┘
        │ ObservationIngested
┌───────▼───────────────────────────────┐   ┌───────────────────────────────────┐
│ RESOLUTION PIPELINE (C1 domain svc)    │   │ DOMAIN CORE (C1/C2/C3/C5/C8)      │
│ deterministic pass → heuristic pass →  │◄──┤ commands → aggregates → EVENTS    │
│ auto-link (High/Med) / MergeCase (Low) │   │ (event backbone, append-only=C9)  │
└───────┬───────────────────────────────┘   └───────────┬───────────────────────┘
        │ resolution events                              │ all events
┌───────▼─────────────────────────────────────────────── ▼──────────────────────┐
│ PROJECTION PIPELINE — disposable, rebuildable read models                      │
│  portfolio · economics · governance-debt · coverage/trust · exec read models   │
│  SNAPSHOT FREEZE (ADR-016): persist projection output + input basis            │
└───────┬────────────────────────────────────────────────────────────────────────┘
        │ read contracts
┌───────▼────────────────────────────────────────────────────────────────────────┐
│ POLICY ENFORCEMENT POINT (ADR-014) → C7 EXPERIENCE PLANE — the only door out    │
│  five areas + Asset Record · exports/board pack (same read models, ADR-019.3)   │
└─────────────────────────────────────────────────────────────────────────────────┘
```

Two inherited invariants (ADR-009) + two new ones:
- **I3:** no component reads provider surfaces except C4 adapters; no projection reads observations except via the pipeline (no shortcuts, ever).
- **I4:** no experience reads anything but policy-enforced read models; exports are experiences.

## 3. Integration catalogue (Consume / Publish / Orchestrate)

| Integration | Class | Direction | Phase | Evidence |
|---|---|---|---|---|
| PPAC Inventory API | **Consume** | in | V1 | Stage 3 [C] |
| Graph Package Management API | **Consume** | in | V1 (licence-gated) | Stage 3 [C], PoC-3 |
| Entra Agent ID (Graph v1.0) | **Consume** | in | V1 | Stage 3 [C] |
| Graph Copilot usage reports | **Consume** | in | V1 | Stage 3 [C] |
| Graph licence APIs (subscribedSkus, licenseDetails) | **Consume** | in | V1 | Stage 3 [C] |
| ARM (Foundry, Azure OpenAI) + Azure Monitor metrics | **Consume** | in | V1 | Stage 3 [C] |
| Azure Cost Management (Cost Details/Exports/Query) | **Consume** | in | V1 | Stage 3 [C] |
| Purview / O365 Management Activity API (AI interaction records) | **Consume** | in | V1 (PoC-5 gated for per-agent counts) | Stage 3 [C/L] |
| Copilot Credits CSV (manual-import provider) | **Consume** | in | V1 | ADR-013 |
| Entra ID (platform's own authN) | **Consume** | in | V1 | standard |
| Notifications via Teams/email | **Orchestrate** | out | V1.5 | C7.4 |
| Native control invocation (CCS block, Entra CA for agents) | **Orchestrate** | out | V2 | ADR-002; write APIs preview [Stage 3] |
| Board pack / narrative export | **Publish** | out | V1 | ADR-019.3 |
| Curated BI export (read models) | **Publish** | out | V2 | C7.5 |
| Evidence packs | **Publish** | out | V2 | C9.2 |
| Customer webhooks / event feed | **Publish** | out | Later | extension point §8 |

## 4. Synchronization patterns

- **Sweep-based (poll) by default.** Stage 3 evidence: the surfaces we consume are report/inventory APIs without eventing for these datasets. Cadence per surface derives from its measured freshness (PPAC ≤15 min; usage reports 24–72 h; cost 4 h–daily; ARM near-real-time) — sweeping faster than the source refreshes is waste. Graph change notifications for directory objects *may* allow event-driven ownership-lapse detection [Likely — requires validation; optimization, not dependency].
- **Watermarks + idempotent re-runs:** every sweep records its watermark; overlapping re-runs produce no duplicate observations (natural key, Stage 5 E2).
- **Backfill as a first-class mode:** new connection or recovered feed replays history within source limits; projections absorb late data because everything is as-of (Stage 5).
- **Rate-limit citizenship:** per-tenant, per-surface request budgets with backoff (Stage 3: QPU limits, Graph RU model, undocumented limits → conservative defaults until Gate-3 PoCs).

## 5. Pipelines

- **Entity resolution:** stream registration → deterministic pass (rule table, ⛔PoC-1/2) → heuristic scoring → auto-link (High/Medium) or MergeCase (Low/collision) → confidence roll-up → events. Idempotent and re-runnable; **resolution rules are versioned** — a rule change triggers re-evaluation under the new version, emitting confidence-change events; history never rewritten.
- **Projection:** event-driven incremental updates + full rebuild path (disposability is the resilience strategy); every read model declares its policy clearance (ADR-014) and time basis.
- **Reporting:** dashboards and board pack render the *same* read models (ADR-019.3); **freeze** = persist read-model output + input basis into ReportSnapshot; restatement = new version. The board-pack generator is a renderer, never a calculator.
- **Manual override:** command → validate against the overridable list (Stage 5 §7) → event → projections layer Override > Ledger-owned > Observed with provenance. Overrides never enter the observation store.
- **Import/export:** imports only through the manual-import provider (template → validation report → staged commit → observations with Self-reported provenance); exports only from read models (I4) — except evidence packs, which draw from the event record by design.
- **Retention enforcement:** policy-driven background job (ADR-017); erasure = PersonKeyMap severance (Stage 5 §8), an O(1) command, not a pipeline.

## 6. Background processing model

Everything except user commands is asynchronous. Job classes: sweeps, resolution runs, projection updates/rebuilds, snapshot generation, retention enforcement, health probes. Rules: every job idempotent, watermarked, tenant-scoped, budget-aware; failures quarantine (poison-input isolation) and **surface in the Trust area** (J4) — operational failure is a user-visible, first-class product state, not an ops secret.

## 7. Trust and security boundaries

| Boundary | Rule |
|---|---|
| **Tenant isolation** | Every flow, store partition, job, and event is tenant-scoped (G1); no shared computation across tenants except the anonymised benchmarking pipeline (Later, PL-003 — separate, opt-in, own boundary) |
| **Credential boundary** | Provider credentials live only in connection scope, accessed only by that adapter; the domain core never sees credentials |
| **Privacy double-gate** | Gate 1 at ingestion (privacy filter, policy-as-of, sets PrivacyMarking); Gate 2 at read (policy enforcement point). Both must pass; neither trusts the other (defence in depth for ADR-003/014) |
| **Privileged zone** | Telemetry policy administration, de-concealed identifier handling, PersonKeyMap — smallest possible surface, all access privileged-audited (ADR-015.9) |
| **Outbound write boundary** | Control adapters (V2) sit behind separate consent, separate credentials, least privilege, and human-approved commands only at introduction — the platform earns automation rights, it doesn't assume them |
| **Tenant data boundary** | The plane seam from §1: ingestion/data plane exchanges only events + read contracts with the control plane — the property that makes the in-tenant deployment option real |

## 8. Extension points & provider plug-in model (C4.5, ADR-007)

A provider is a packaged adapter declaring a **manifest**: surface identity; capabilities offered (inventory / usage / cost / identity / control); native identifier types contributed (feeds the alias model); payload schema + version; auth requirements; freshness expectation; health semantics. The platform supplies the invariant services — scheduling, watermarking, contract validation, privacy filtering, delta suppression, observation append — so a provider is *only* acquisition logic + declarations.

Consequences by design: the CSV importer is an ordinary provider (ADR-013); a future custom collector is an ordinary provider (ADR-007 — no redesign); a future non-Microsoft vendor (OpenAI/Anthropic/Google admin APIs) is an ordinary provider (cross-vendor promise); providers fail independently (one dead feed degrades coverage, nothing else). Other extension points: export renderers (board pack formats), notification channels, resolution heuristic rules (versioned rule packs), read-model contracts for BI export (V2), customer event feed (Later).

## 9. Failure handling philosophy & resiliency principles

1. **Partial failure is the normal state.** A platform aggregating nine external surfaces will always have something degraded; the design centre is "running well while partially blind — and saying so" (coverage map, banners).
2. **Degrade visibly, never silently.** Stale data is shown stale; missing data is shown missing; nothing is interpolated or fabricated (reconciliation honesty, Stage 5 §10).
3. **Append-only stores are the recovery spine.** Observations + events are the truth; aliases, links, projections, dashboards are all rebuildable from them. Worst case beyond that: re-sweep the providers — the sources still hold their own data.
4. **Blast-radius rules:** tenant failures don't cross tenants; provider failures don't cross providers; projection failures don't block ingestion; experience failures don't block the domain.
5. **Bounded retries, then humans.** Exponential backoff to a limit, then quarantine + Trust-area incident; no infinite retry storms against rate-limited sources.
6. **Idempotency everywhere** — re-delivery and re-runs are safe by construction, which makes recovery boring. Boring recovery is the goal.

## 10. Simplifications chosen (challenge applied)

1. **Modular monolith first.** Bounded contexts are logical modules with enforced interfaces, not network services. Nothing in the domain requires distribution at 6,000-employee scale; microservices here would be complexity cosplay. The context seams make later extraction possible *if* scale demands. (Stage 8 constraint, recorded now.)
2. **One event backbone**, not per-context buses; events are the audit record (ADR-015.8) — one stream, one truth.
3. **No streaming infrastructure requirement.** Sweep cadences are minutes-to-days and volumes are thousands of assets — batch/queue semantics suffice. Kafka-class machinery is explicitly not required by this design.
4. **CQRS where it pays, not as dogma:** commands+events for the domain; plain reads for administrative configuration.
5. **Topology flexibility without dual implementation** (§1): one codebase, one seam, deployment option deferred.
6. **Resisted:** a rules-engine product for resolution heuristics (versioned rule packs suffice); a workflow-engine dependency for V1 (no C2 yet — decide at V1.5 with evidence); an API-gateway/product layer for V1 (no external API consumers yet — the public API is a Later extension point, shaped by read contracts already defined).

---

## Stage-end review

### Summary
Three planes with a deliberate seam (making the in-tenant data plane a packaging option, not a redesign); C4/C7 doors hardened with two new invariants (I3/I4); sixteen integrations classified consume/publish/orchestrate; sweep-based synchronization matched to evidenced source freshness; six pipelines defined; a privacy double-gate; a provider plug-in model where CSV import, custom collectors, and future vendors are all "just providers"; and a failure philosophy whose design centre is *running well while partially blind — visibly*.

### Assumptions
- Modular-monolith scale ceiling holds for target tenants (revisit at multi-hundred-tenant commercial scale — recorded for Stage 8).
- Graph change notifications for directory objects are usable for lapse detection [Likely — validation item; optimization only].
- Sweep cadences within documented rate limits at Quadient scale (Gate-3 PoCs confirm).

### Confirmed facts
No new Microsoft claims; integration classifications trace to Stage 3 evidence.

### Unknowns
Gate-1/2/3 PoC items (unchanged); benchmarking pipeline boundary design (Later, deliberately unshaped); public API demand (Later).

### Risks
- **R-23 (new):** the modular monolith's interface discipline erodes under delivery pressure (module boundaries become suggestions) — mitigation: context interfaces are review-gated; the two-door + I3/I4 invariants are CI-enforceable in build.
- **R-24 (new):** sweep-based freshness may disappoint users trained on real-time dashboards — mitigation: every view states its as-of basis (already ADR-019.1 territory); set expectation in product, not in apology.

### Alternative approaches considered
Event-streaming ingestion platform (rejected — no volume case); microservices-per-context (rejected — §10.1); dual SaaS/in-tenant implementations (rejected — one seam, one codebase); direct provider→dashboard fast path for "live" views (rejected — violates I3, corrupts provenance).

### Questions for Arun
1. **OI-002 disposition:** accept "pure SaaS V1, in-tenant data plane preserved as deployment option at the §7 seam, activation is a commercial decision" — closing OI-002 architecturally?
2. **Modular monolith first** (§10.1) — accept as a binding Stage 8 constraint?
3. **Public API:** confirm "Later, shaped by read contracts" — or does the commercial strategy need a partner-facing API earlier?
4. **Stage 8 proposal:** Security, Identity & Multi-tenancy architecture (consent model, credential architecture, tenant isolation depth, privileged zone design) *before* technology selection (Stage 9) — the security architecture should constrain the technology choice, not inherit from it. Confirm order?

### Recommendations
1. Approve with I3/I4 added to the standing invariant set (two doors, two invariants, now four).
2. Record §10's simplifications as decisions — each will be challenged by future contributors with a favourite technology.
3. Commission Gate-2/3 PoCs after Gate-1 (sequenced, same tenant, marginal cost is low).
