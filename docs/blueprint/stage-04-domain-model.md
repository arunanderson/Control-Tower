# Stage 4 — Enterprise Domain Model

| | |
|---|---|
| **Version** | 1.1 |
| **Date** | 2026-07-15 |
| **Status** | **APPROVED** 2026-07-15 (ADR-015 ratifies §2–§9 doctrines incl. privileged-read auditing ON by default; ADR-016 amends §11.4 — ReportSnapshot pulled forward for financial close / frozen periods / restatement) |
| **Related** | [stage-02-capability-model.md](stage-02-capability-model.md) v1.1 (contexts C1–C9), [stage-03-microsoft-validation.md](stage-03-microsoft-validation.md) (evidence), [decision-log.md](decision-log.md) (ADR-012/013/014 govern this model) |

**Scope discipline:** business entities, aggregates, value objects, relationships, ownership, lifecycles, domain events, context interactions, source-of-truth rules, entity resolution, temporal history, audit model. **No tables, no databases, no APIs.** Stage 5 (Conceptual Data Model) translates this; it is hard-gated on Gate-1 PoCs (PD-004).

**Design stance:** the smallest domain model that honours the approved ADRs. Ten aggregates. Everything analytical is a *projection* (read model), not a domain object — this single decision keeps the domain from tripling in size.

---

## 1. Domain overview — ten aggregates across the contexts

| Context | Aggregate roots | Purpose |
|---|---|---|
| C4 Provider Integration | **ProviderConnection**, **ProviderObservation** | The door in: configured feeds and their immutable observations |
| C1 AI Asset Ledger | **AIAsset**, **MergeCase**, **TaxonomyScheme** | The ledger: resolved business assets, resolution disputes, the vocabulary |
| C3 Cost & Value | **AllocationRule**, **ValueDeclaration** | The judgment layer: how money maps to the org; what value is claimed |
| C5 Enterprise Context | **OrgModel**, **JurisdictionProfile** | The dimensions: who/where things attribute to |
| C8 Trust & Access | **TenantConfiguration** (incl. TelemetryPolicy) | The rules of the deployment |
| C9 Audit | *(no aggregate — see §9: the audit model is the event record itself)* | |
| C2 Governance (V1.5) | *(seam only — see §10)* | |

## 2. Core aggregates

### 2.1 ProviderObservation (C4) — the immutable fact of what a surface said
- **Identity:** ObservationId (platform-issued).
- **Nature:** append-only, never edited, never deleted (subject to retention policy). One observation = one snapshot of one native record from one provider surface at one time.
- **Composition:** ProviderRef (which connection/surface), ObservedAt, NativeIdentifierSet (value object: the surface's own IDs — botId+environmentId, appId, ARM ID, package ID, agentIdentityId…), AttributePayload (typed attribute set as observed), PrivacyMarking (what L-level content it carries — set at ingestion per ADR-003/014).
- **Key rule:** observations are pre-resolution. They know nothing about AIAssets. Resolution links point *at* them, never the reverse — so re-resolution never rewrites source facts.
- Cost and usage facts are also observations (CostObservation / UsageObservation sub-kinds sharing the same contract): same door, same immutability, same privacy marking. A CSV import under ADR-013 produces observations whose ProviderRef is the *manual-import provider* — identical shape, honest source label for free.

### 2.2 ProviderConnection (C4)
- **Identity:** ConnectionId. Configured instance of a provider for a tenant: surface type, scope (which tenant/subscriptions), credential reference (not the credential), schedule, enabled state, health status.
- Emits the coverage facts that C1.6's coverage map projects (connected / degraded / disconnected, last successful sweep, correlation quality per §6).

### 2.3 AIAsset (C1) — the aggregate the whole product hangs on
- **Identity:** LedgerAssetId (platform-issued; never a native ID — ADR-012).
- **Core attributes (ledger-owned, business context):** DisplayName, AssetType (taxonomy value), BusinessPurpose, RegistrationStatus, OperationalLifecycleState, RiskProfile (V1 managed field), MatchConfidence (High/Medium/Low/Manual — asset-level roll-up), org attribution refs, capability tags (V1.5).
- **Child entities:**
  - **ResolutionLink** — connects the asset to a ProviderObservation stream: native identifier set, match method (documented-join / heuristic / manual), per-link confidence, linkedAt, linkedBy (system or operator). The **alias graph** is the set of ResolutionLinks + their NativeIdentifierSets.
  - **OwnershipAssignment** — PersonRef + role (Owner / Delegate / Sponsor) + validFrom/validTo. Temporal: ownership history is never overwritten. An asset with no *current* assignment is **Ownerless** — a first-class queryable condition (principle §8.11 of Stage 1), not a null.
  - **DependencyRef** (V2) — typed reference to another asset or external artefact (knowledge source, connector) — reference only, per PL-007/008.
- **Invariants:** (1) an asset presented as a single thing to any user carries its MatchConfidence visibly — *uncertain correlations are never presented as facts* (ADR-012); (2) Low-confidence candidate matches never auto-link — they open a MergeCase; (3) business-context attributes are only mutable through commands that emit domain events (→ §9 audit); (4) native/technical attributes are never edited on the asset — they live in observations and are *projected*.

### 2.4 MergeCase (C1) — the manual merge queue as a domain object
- **Identity:** CaseId. Candidate set (assets and/or unresolved observation streams), system rationale (which signals suggested the match), status (Open → Decided → Reopened), decision (Merge / KeepSeparate / Split), decidedBy, decidedAt.
- Merging produces a surviving LedgerAssetId + tombstone with redirect (external references must never dangle); splitting reverses it. Both are events, both auditable, both reversible-by-new-case — never by history rewrite.

### 2.5 TaxonomyScheme (C1)
- **Identity:** SchemeId + Version. The controlled vocabulary: asset types (agent, declarative agent, flow, model deployment, MCP server, external AI service…), tiers, tags. Versioned; assets reference scheme values, and re-versioning never silently re-types existing assets (migration is an explicit, evented operation). This is the "what counts as an AI asset" definition (OI-011) given a home.

### 2.6 AllocationRule & ValueDeclaration (C3)
- **AllocationRule:** RuleId + Version; effective-dated mapping logic (cost scope → org units, by driver: assignment, headcount, usage share, direct tag); owned by Finance; changes are events. Allocation *runs* are projections computed from rules + cost observations + OrgModel-as-of-period — **not** domain aggregates (they hold no decisions, only arithmetic; rerunnable, disposable).
- **ValueDeclaration:** DeclarationId; asset ref, declared benefit (typed: hours saved, revenue, cost avoided…), methodology ref, ConfidenceLabel (**Measured / Estimated / Self-reported / Inferred / Unknown** — mandatory, never defaulted silently), declaredBy, period, revision chain. The honest-data principle is a domain invariant here, not a UI convention.

### 2.7 OrgModel & JurisdictionProfile (C5)
- **OrgModel:** temporal tree of OrgUnits (BUs, cost centres, geography) with validFrom/validTo per node and edge. **Reorgs never rewrite history** — attribution always resolves against the org-model-as-of-the-period being reported (§8).
- **JurisdictionProfile:** jurisdiction ref + applicable regime markers + the telemetry level ceiling permitted there. Referenced by TelemetryPolicy and by C4 ingestion filters.

### 2.8 TenantConfiguration (C8) — including TelemetryPolicy
- **Identity:** TenantId. Governance configuration, role model, and **TelemetryPolicy**: the L1–L4 level per jurisdiction/population, each capability toggle, effective-dated with full history. Policy changes are privileged domain events (ADR-014 hinges on their auditability).
- **ADR-014 as an invariant of the model:** every read model that reaches C7 declares its **required policy clearance**; the enforcement point compares clearance against the *current* TelemetryPolicy at read time. De-concealed upstream data raises what C4 *ingests*, never what C7 *shows*.

## 3. Value objects (shared kernel)

NativeIdentifierSet · ProviderRef · PersonRef (Entra object ID + display snapshot; **people are never aggregates** — §11.2) · MatchConfidence (High/Medium/Low/Manual) · ConfidenceLabel (Measured/Estimated/Self-reported/Inferred/Unknown) · Money · Period · LifecycleState · RegistrationStatus · RiskProfile (tier + assessedAt + assessedBy + method) · PrivacyMarking · JurisdictionRef · TaxonomyValueRef · AuditActor (human / system / provider).

Two deliberately distinct concepts that must never merge: **MatchConfidence** (how sure we are two records are the same asset) and **ConfidenceLabel** (how a metric was derived). Conflating them was an early design temptation; they answer different questions to different audiences.

## 4. Lifecycles — two orthogonal state machines on AIAsset

**RegistrationStatus (the ledger's relationship to the asset):**
`Discovered → Triaged → Registered → Retired` (+ `Rejected` for not-an-asset determinations; `Merged` as tombstone). V1: transitions are direct, guarded, evented operations by authorised operators. V1.5: transitions become outcomes of GovernanceCases (§10) — same events, new trigger.

**OperationalLifecycleState (the asset's own life):**
`Draft → Pilot → Production → UnderReview → Suspended → Retired`. In V1 a managed field (ADR-011) with guarded transitions; the state machine is defined *now* so V1.5 gates snap onto existing transitions rather than redefining them.

**Ownership lifecycle:** Assigned → Confirmed (owner attests, V1.5) → **Lapsed** (departure/role-change detected via Entra observation) → Reassigned. Lapse detection is a resolution of observations against OwnershipAssignments — an event, not a batch overwrite.

## 5. Domain events (the canonical set)

- **C4:** ObservationIngested, ProviderConnected/Degraded/Disconnected, IngestionPolicyApplied (what was filtered at the door, per ADR-003/014 — evidence that filtering happened without recording the filtered content).
- **C1:** AssetDiscovered, AssetTriaged, AssetRegistered, AssetRetired/Rejected, ResolutionLinkAdded/Removed, MatchConfidenceChanged, MergeCaseOpened/Decided, AssetsMerged, AssetSplit, OwnershipAssigned/Lapsed/Reassigned, LifecycleStateChanged, RiskProfileAssigned/Revised, TaxonomyVersionPublished.
- **C3:** AllocationRuleChanged, ValueDeclared/Revised, CostFactIngested (specialisation of ObservationIngested).
- **C5:** OrgModelChanged, JurisdictionProfileChanged.
- **C8:** TelemetryPolicyChanged (privileged), RoleAssignmentChanged, TenantConfigurationChanged.
- Events carry: actor (AuditActor), occurredAt, recordedAt, reason (optional operator note), correlation ref (e.g., MergeCase, and later GovernanceCase).

## 6. Bounded-context interactions (event-driven, honouring the two doors)

```
ProviderConnection ──ObservationIngested──▶ C1 resolution engine ──▶ AIAsset (links/confidence)
                                        └──▶ C3 (cost/usage facts)          │
OrgModelChanged ────────────────────────────▶ C3 (as-of attribution)        │ asset events
TelemetryPolicyChanged ─▶ C4 ingestion filters + pre-C7 enforcement         ▼
All events ────────────────────────────────────────────────────▶ Audit record (C9)
C1/C3/C5 state ──▶ projections (coverage map, dashboards, portfolio views) ──▶ C7 only
```
C4 remains the only door in; C7 the only door out (ADR-009 invariants). The resolution engine is a *domain service* in C1 (stateless policy over observations + links), not an aggregate.

## 7. Source-of-truth rules (the "we own what we add" doctrine)

| Data | Source of truth | Ledger's role |
|---|---|---|
| Technical existence & configuration of assets | Provider surfaces (Microsoft et al.) | Observe, snapshot, project — never edit |
| Business context: purpose, ownership accountability, risk profile, lifecycle, registration, tags | **The ledger** | Authoritative; evented mutations only |
| Identity resolution (that N records are one asset) | **The ledger** | Authoritative, with visible MatchConfidence |
| People & org identities | Entra (+ HR sources later) | Reference + display snapshot (PersonRef) |
| Cost facts | Billing/source systems | Observe immutably |
| Cost *allocation* | **The ledger** (AllocationRules) | Authoritative judgment |
| Declared value | The declarer | Record + label; the ledger owns the *labelling*, not the claim |
| Telemetry policy & its enforcement | **The ledger** | Authoritative (ADR-014) |

## 8. Temporal history — scoped bitemporality

Full bitemporality everywhere is over-engineering. Scoped rule:

- **Bitemporal (valid-time + record-time):** OrgModel, OwnershipAssignments, AllocationRules, TelemetryPolicy, RiskProfile revisions — everywhere a *restatement question* ("what did we believe then?" vs "what was true then?") will be asked by Finance or Audit.
- **Append-only with observedAt:** ProviderObservations (their own temporality is the observation stream).
- **Simple evented history:** everything else (display name changes, purpose edits) — the event log *is* the history.
- Reporting rule: every projection is computed *as-of* a stated time basis, and says which.

## 9. Audit model — the event record is the audit trail

No separate "audit table" concept. **C9 = the immutable, complete record of the domain events themselves**, plus two additions:

1. **Privileged-access records:** reads of individual-level (L2+) data through C7 are themselves recordable events (configurable per tenant) — auditing observation, not just mutation. Required to make ADR-003 L4 "auditable diagnostics" real.
2. **Evidence views (C9.2, V2):** exportable packages = filtered event history + the as-of states they imply.

Properties: append-only; events carry actor/time/reason/correlation; TelemetryPolicyChanged and de-concealment-related events are flagged privileged; retention policy per tenant. This makes "evidence-grade audit" (Stage 1 §8.12) a structural property rather than a feature bolted on.

## 10. The C2 seam (governance, V1.5) — modelled as a boundary, not built

One future aggregate is named now so V1 doesn't paint over its socket: **GovernanceCase** (intake, approval, review, exception — typed cases carrying decisions that *trigger the same asset transitions V1 exposes directly*). V1's design obligation is only: (a) every V1 transition is command+event (done, §4/§5); (b) events carry an optional GovernanceCase correlation ref (done, §5). Nothing else about C2 is designed in this stage — deliberately.

## 11. Challenges applied to this model (simplifications chosen)

1. **One AIAsset aggregate, not a class per asset type.** Agents, flows, deployments, MCP servers differ by taxonomy value + typed attribute payloads from observations — not by domain behaviour. A type hierarchy would multiply every workflow by N types for zero behavioural difference. If a type later *earns* distinct behaviour (invariants of its own), it can be split out then.
2. **People are references, never aggregates.** The platform holds PersonRefs and policy-filtered projections of usage — it is not an HR system and must never accrete one. This is also the strongest structural privacy guarantee available: you cannot leak a person-profile you never built.
3. **Analytics are projections.** Utilisation, zombie lists, dashboards, coverage map, allocation runs — all disposable, recomputable read models. Domain aggregates hold only *decisions and facts*: rules, declarations, links, states.
4. **Allocation runs are not aggregates** (arithmetic, not judgment). Counter-considered: Finance may want to "freeze" a reported quarter — solved by as-of reporting + bitemporal rules, not by promoting runs to domain objects. If regulatory sign-off of a specific run is later required, a thin `ReportSnapshot` aggregate can be added (parked).
5. **The resolution engine is a domain service, not an aggregate** — it owns no state; links and cases own the state.
6. **Scoped bitemporality** (§8) instead of universal — cut ~40% of the temporal complexity where no restatement question exists.
7. **What was resisted:** modelling prompts, knowledge sources, or connectors as aggregates (they are DependencyRefs or observation payloads — Stage 1 cuts hold); a "Report" aggregate; a "Notification" aggregate (V1.5 orchestration, stateless).

---

## Stage-end review

### Summary
Ten aggregates, two structural doctrines: *observations are immutable and pre-resolution* (the alias graph points at facts, never rewrites them), and *the event record is the audit trail*. Entity resolution per ADR-012 with Arun's High/Medium/Low/Manual taxonomy; ADR-013's CSV feed drops in as an ordinary provider; ADR-014 is enforced as a read-time invariant, not a hope. The C2 socket exists; C2 is not designed.

### Assumptions
- Gate-1 PoCs will not invalidate the *shape* of ResolutionLink (they may adjust which joins rate High) — PD-004 gates Stage 5, not this model.
- Typed attribute payloads on observations can absorb per-surface schema drift without domain change (provider-contract concern, C4.5).
- Entra remains the people-identity source; HR-source enrichment (job changes for lapse detection) deferable.

### Confirmed facts
None new — this stage consumes Stage 3's evidence; no new Microsoft claims are made.

### Unknowns
- Whether Finance requires sign-off-frozen allocation runs (→ ReportSnapshot, parked — Question 3).
- Observation retention economics at scale (Stage 5/8 concern, flagged early).
- Privileged-read auditing default posture (on/off per tenant) — privacy vs auditability trade-off (Question 4).

### Risks
- **R-18 (new):** observation volume growth (every sweep snapshots every asset) — mitigation direction: delta-detection at ingestion (only material changes produce observations); to be sized in Stage 5.
- **R-19 (new):** single-aggregate asset model could strain if a type develops rich unique behaviour — mitigation: split-when-earned rule documented in §11.1.
- R-12 residual unchanged (Gate-1 PoCs pending).

### Alternative approaches considered
- Per-type asset class hierarchy — rejected (§11.1).
- Person aggregate with usage history — rejected on privacy structure (§11.2).
- Universal bitemporality — rejected (§8).
- Separate audit store designed apart from events — rejected: two sources of truth for "what happened" is how audit systems lie.

### Questions for Arun
1. **Approve the ten-aggregate model** and the two doctrines (immutable pre-resolution observations; events-as-audit)?
2. **Privileged-read auditing (L2+ views):** default ON for all tenants (stronger governance story, more audit volume) or tenant-configurable? My recommendation: default ON, configurable OFF only with a recorded justification.
3. **Finance restatement:** is as-of reporting sufficient for Quadient Finance, or is formally frozen/signed reporting (ReportSnapshot) a known requirement I should pull forward from the parking lot?
4. **Stage 5 confirmation:** Conceptual Data Model (translating this model to data entities + retention + volumetrics), finalisation gated on Gate-1 PoCs per PD-004 — and do you want the Gate-1 PoC *specifications* written as part of Stage 5 prep, since they're now on the critical path?

### Recommendations
1. Approve with §11's simplifications as recorded decisions (they will be re-litigated by every future contributor otherwise).
2. Commission Gate-1 PoC specifications immediately (they gate Stage 5; writing the specs is planning, not implementation, so PD-001-compliant).
3. Treat the two doctrines as architecture invariants alongside ADR-009's two doors.
