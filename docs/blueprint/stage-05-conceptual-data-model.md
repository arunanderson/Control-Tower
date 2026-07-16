# Stage 5 — Conceptual Data Model

| | |
|---|---|
| **Version** | 0.10 (E21 + FX methodology added per Revision Package v1.0; still PoC-gated for finalisation) |
| **Date** | 2026-07-15 |
| **Status** | **APPROVED as Draft v0.9** (2026-07-15) — finalisation gated on Gate-1 PoCs (PD-004); ⛔PoC uncertainty markers stay until validated. ADR-017 amends §9 defaults (audit 7y default; observations/usage 2y default; all policy-driven). ADR-018 ratifies §10 Flag-Never-Block. |
| **Related** | [stage-04-domain-model.md](stage-04-domain-model.md) v1.1 (ADR-015/016), [poc-gate1-specifications.md](poc-gate1-specifications.md), [decision-log.md](decision-log.md) |

**Scope discipline:** conceptual only — no physical database, no storage technology (Dataverse/SQL/Fabric explicitly out of scope, DD-002 stands). Attribute types are conceptual (text, identifier, timestamp, money, enum, ref).

**Global rules (apply to every entity; not repeated per table):**
- **G1 — Tenant scoping:** every entity carries `tenantId` (ADR-001); no cross-tenant references, ever.
- **G2 — Surrogate identity:** every entity has a platform-issued, opaque, immutable, globally unique surrogate key (`…Id`). Native/external identifiers are *never* primary keys (ADR-012).
- **G3 — Provenance facet:** every attribute value exposed to users carries a provenance class — **Observed / Ledger-owned / Declared / Derived / Override** — plus source ref and timestamp. Defined once here; the SoT column below states each attribute's *default* class.
- **G4 — Audit:** every mutation of Ledger-owned data is a domain event (ADR-015.8); entities below list only their *state*, the event stream is the change history.
- **G5 — Soft delete default:** domain entities are never hard-deleted; they are ended (validTo), tombstoned (with redirect), or retired. Hard delete exists in exactly three cases: GDPR erasure via key severance (§8), retention-expired observations (§9), contractual tenant purge.
- **G6 — Owner:** every entity names a business owner (accountable for its rules/content — from Stage 2 ownership).

---

## 1. Entity catalogue (20 entities)

| # | Entity | Context | Owner | Nature |
|---|---|---|---|---|
| E1 | ProviderConnection | C4 | Platform Admin | Configured, evented |
| E2 | ProviderObservation | C4 | Platform Admin (operation) | **Immutable, append-only** |
| E3 | IngestionRun | C4 | Platform Admin | Immutable log |
| E4 | AIAsset | C1 | AI CoE | Ledger core, evented |
| E5 | IdentityAlias | C1 | AI CoE (system-maintained) | Evented |
| E6 | ResolutionLink | C1 | AI CoE (system + operator) | Evented |
| E7 | OwnershipAssignment | C1 | AI CoE | **Bitemporal** |
| E8 | MergeCase | C1 | AI Governance Team | Evented workflow |
| E9 | TaxonomyScheme / TaxonomyValue | C1 | AI CoE | Versioned |
| E10 | RiskProfileRevision | C1 | Risk | **Bitemporal** revisions |
| E11 | AllocationRule | C3 | Finance | **Bitemporal**, versioned |
| E12 | ValueDeclaration | C3 | Finance (labelling) / declarer (claim) | Revision-chained |
| E13 | ReportingPeriod | C3 | Finance | Evented (Open→Frozen→Restated) |
| E14 | ReportSnapshot | C3 | Finance | **Immutable once frozen**, versioned (ADR-016) |
| E15 | OrgUnit | C5 | EA / Finance | **Bitemporal** tree |
| E16 | JurisdictionProfile | C5 | Privacy / Risk | Versioned |
| E17 | TelemetryPolicy | C8 | Privacy | **Bitemporal**, privileged |
| E18 | RoleAssignment | C8 | Security | Evented |
| E19 | PersonKeyMap | C8 | Privacy | The GDPR severance point (§8) |
| E20 | DomainEventRecord / PrivilegedReadRecord | C9 | AI Governance (operated) / Internal Audit (consumer) | **Immutable** |

Deferred entity stubs (named, not modelled): DependencyRef (V2), GovernanceCase (V1.5 — socket per Stage 4 §10).

### E21 ContractCommitment *(added v0.10, Revision Package v1.0)*
| Attribute | Type | SoT | Notes |
|---|---|---|---|
| commitmentId | surrogate | Platform | G2 |
| vendor; scope (SKUs, consumption commitments); documentsRefs | text/refs | **Self-reported** (procurement documents) → **Financially validated** on GL/invoice reconciliation | Ingested via manual-import or ERP provider (C4.8) |
| termStart / termEnd / **renewalWindow** / **trueUpDates** | dates | Self-reported → Fin. validated | Drives realisable-savings classification (Stage 10 v1.1) |
| committedAmount + currency | Money | Self-reported → Fin. validated | Native currency, never converted at rest |
| priceBookRefs | refs | — | Each price-book entry carries its own evidence class |

Owner: Finance/Procurement. Relationship: E21 → assets/licence classes (N:M, as-of semantics). **FX rule (global, added v0.10):** all monetary observations stored in native currency; conversion only at reporting time with dated, sourced rates (treasury rates [Fin. validated] preferred; published rates [Estimated]); consolidation currency per tenant; frozen snapshots (E14) pin their FX rates in the input basis.

## 2. Core entity definitions

### E2 ProviderObservation
| Attribute | Type | SoT / provenance | Notes |
|---|---|---|---|
| observationId | surrogate | Platform | G2 |
| connectionRef | ref E1 | Platform | required |
| ingestionRunRef | ref E3 | Platform | required |
| observationKind | enum {Inventory, Usage, Cost, Identity, Signal} | Platform | drives payload schema family |
| nativeIdentifiers | IdentityAlias set (embedded copy) | **Observed** | as returned by the surface; ⛔PoC: per-archetype alias types from PoC-1 |
| payload | typed attribute set | **Observed** | schema-versioned per provider contract (C4.5) |
| observedAt | timestamp | Observed | surface's own timestamp where given, else sweep time (flagged which) |
| recordedAt | timestamp | Platform | ingestion time |
| privacyMarking | enum L1–L4 | Derived (at ingestion, per E17) | ADR-003/014; set once, immutable |
| deltaStatus | enum {New, Changed, Unchanged-suppressed} | Derived | R-18 mitigation: Unchanged sweeps record a heartbeat on E1, not a new E2 row |

**Business/natural key:** (connectionRef, primary native identifier, observedAt) — uniqueness of *observation events*, not of assets. **Validation:** payload must conform to the provider contract schema version; unknown fields are preserved but marked unvalidated. **Retention class:** per §9.

### E4 AIAsset
| Attribute | Type | SoT / provenance | Mutability |
|---|---|---|---|
| ledgerAssetId | surrogate | Platform | immutable |
| displayName | text | **Ledger-owned** (default seeded from observation = Derived until first edit) | evented |
| assetTypeRef | ref E9 value | Ledger-owned | evented; re-typing is explicit event |
| businessPurpose | text | Ledger-owned | evented; **required at Registered** |
| registrationStatus | enum (Stage 4 §4) | Ledger-owned | guarded transitions only |
| operationalLifecycle | enum (Stage 4 §4) | Ledger-owned | guarded transitions only |
| matchConfidence | enum {High, Medium, Low, Manual} | **Derived** (roll-up rule §5) + Override | override requires reason |
| orgAttributionRef | ref E15 (as-of semantics) | Ledger-owned | evented |
| jurisdictionRefs | ref set E16 | Ledger-owned / Derived from environment geo | evented |
| tombstone / redirectTo | ref E4 | Platform (merge outcome) | set only by MergeCase decision |
| *technical attributes (model, channels, publisher, config…)* | — | **Observed — projected from linked E2, never stored on E4** | n/a |

**No natural key — by design** (ADR-012): asset identity is a resolution outcome. **DQ rules:** Registered+Production ⇒ businessPurpose present, current E7 owner present, current E10 risk profile present (this *is* the registration gate); violations don't block data — they surface as governance-debt flags (Ownerless list, etc.).

### E5 IdentityAlias
| Attribute | Type | SoT | Notes |
|---|---|---|---|
| aliasId | surrogate | Platform | |
| assetRef | ref E4 | Ledger-owned (via resolution) | |
| system + identifierType + value | enum + enum + text | **Observed** | e.g. (PowerPlatform, botId, GUID); (Entra, agentIdentityObjectId, GUID); (M365Catalog, packageId, P_…) |
| firstSeen / lastSeen | timestamps | Derived | staleness input |
| status | enum {Active, Stale, Retired-by-source} | Derived | |

**Natural key:** (tenantId, system, identifierType, value) — **collision on this key is not a constraint violation; it opens a MergeCase** (two assets claiming one native ID is a resolution dispute, not corrupt data). ⛔PoC: whether native IDs are ever reused by Microsoft surfaces (would require validity windows on the key) — add to PoC-1 observations.

### E6 ResolutionLink
| Attribute | Type | SoT | Notes |
|---|---|---|---|
| linkId | surrogate | Platform | |
| assetRef / observationStreamRef | refs | Ledger-owned | stream = (connection, native primary ID) |
| method | enum {DocumentedJoin, Heuristic, Manual} | Ledger-owned | |
| confidence | enum {High, Medium, Low, Manual} | Derived (method+rule) or Manual | ⛔PoC: rule table finalised from PoC-1/2 |
| rationale | structured text | Derived / Ledger-owned | which signals matched — evidence model §6 |
| linkedAt / linkedBy | timestamp + AuditActor | Platform | |
| status | enum {Active, Severed} | Ledger-owned | severing is evented, historical links retained |

**Lifecycle semantics:** links are never deleted — severed links remain as history (a merge reversal must be reconstructible).

### E7 OwnershipAssignment — bitemporal
personRef (via E19), role {Owner, Delegate, Sponsor}, assetRef, validFrom/validTo (business validity), recordedAt/supersededAt (knowledge time), source {Declared, Derived-from-Entra, Override}, lapseReason. **DQ:** overlapping *Owner*-role validity for one asset is invalid; gaps are legal (= Ownerless, flagged not blocked).

### E12 ValueDeclaration
declarationId; assetRef; benefitType enum; quantity + unit + Money; period; methodologyRef; **confidenceLabel enum {Measured, Financially validated, Estimated, Self-reported, Inferred, Unknown} — required, no default** (a declaration without a label is invalid, not "Unknown" — forcing the conversation is the point); declaredBy; evidenceRefs (§6); revisionOf (chain, prior revisions immutable). *(Taxonomy expanded to six labels 2026-07-15 per Arun's Stage 10 directive — ADR-024.)*

### E13/E14 ReportingPeriod & ReportSnapshot (ADR-016)
- **ReportingPeriod:** periodId, calendar bounds, state {Open → Closing → **Frozen** → Restated}, frozenAt/frozenBy.
- **ReportSnapshot:** snapshotId, periodRef, version, **immutable persisted outputs** (allocated cost/value statements) + **input basis** (as-of timestamps + refs to rule versions, org-model version, observation watermark), signedBy, supersedes (prior version ref). Restatement = new version + reason; frozen versions are never modified (G5 hardened to immutable). Answering "why does Q2 look different now?" = diff of two snapshot bases — by construction, not by forensics.

### E17 TelemetryPolicy — bitemporal, privileged
levels per jurisdiction/population (L1–L4), per-capability toggles, validFrom/validTo, recordedAt, changedBy, justification (**required** — privileged event per ADR-014). **Validation:** level ≤ jurisdiction ceiling (E16); violations are rejected, not flagged.

### E19 PersonKeyMap — the GDPR severance point
Maps an opaque platform `personKey` ↔ Entra object ID + display snapshot. **Every other entity references people only by personKey.** Erasure request ⇒ sever/anonymise the map entry; usage observations and events retain the orphaned personKey — aggregates survive, the person is unrecoverable. This makes right-to-erasure an O(1) operation instead of a data-purge crawl. Owner: Privacy.

### E20 DomainEventRecord / PrivilegedReadRecord
Event: eventId, type, aggregateRef, actor (AuditActor), occurredAt/recordedAt, reason, correlationRef, payload (attribute deltas), privileged flag. PrivilegedReadRecord: who viewed which L2+ read model, when, under which policy version (ADR-015.9 — ON by default). Both immutable; retention §9.

## 3. Relationships & cardinality (core)

| Relationship | Cardinality | Lifecycle semantics |
|---|---|---|
| E1 → E2 (connection produces observations) | 1 : N | Deleting/disabling a connection never touches its observations; coverage map shows the dead feed |
| E4 ↔ E2 via E6 (asset ↔ observation streams) | N : M through links | Links sever, never delete; observations never re-point |
| E4 → E5 (asset has aliases) | 1 : N | Aliases follow merge (re-parented by MergeCase event); collision ⇒ MergeCase |
| E4 → E7 (ownership) | 1 : N temporal | Gaps legal (Ownerless); overlap of Owner role invalid |
| E4 → E10 (risk revisions) | 1 : N append | Latest-as-of projection |
| E4 → E15 (org attribution) | N : 1, **as-of** | Reorg never rewrites: attribution resolves against E15 validity at the period being reported |
| E8 → E4/E2 (merge case candidates) | 1 : N | Decision emits merge/split events; tombstone+redirect on merged asset |
| E11/E12/E2(cost) → E14 (snapshot basis) | N : M frozen refs | Snapshot pins exact versions; supersession only by new version |
| E17 → E2 (policy governs ingestion marking) | 1 : N | Marking uses policy *as-of ingestion*; later policy changes affect display (ADR-014), never remark history |

## 4. Identity strategy (summary)

Surrogate everywhere (G2); business/natural keys only for uniqueness semantics: E5 alias key (collision→MergeCase), E9 (scheme, code, version), E13 (tenant, calendar period), E2 observation event key. AIAsset deliberately has none. External references (URLs, exports) use ledgerAssetId + tombstone redirects so merged assets never 404 conceptually.

## 5. Confidence model

- **Link confidence** (E6): assigned by rule table — DocumentedJoin→High; corroborated heuristic (≥2 independent signals)→Medium; single weak signal→Low (**never auto-linked**; MergeCase); operator decision→Manual. ⛔PoC: the definitive join list per archetype comes from PoC-1/2.
- **Asset roll-up** (E4): matchConfidence = the *lowest* confidence among Active links whose observations contribute any displayed technical attribute; assets with a single stream = that link's confidence; Manual overrides recorded with reason. Rationale: an asset is only as certain as its least certain displayed claim — anything else *presents uncertain correlation as fact*, violating ADR-012.
- **Metric confidence** (ConfidenceLabel) is orthogonal (Stage 4 §3) and mandatory on E12 and on every C7 metric surface.

## 6. Evidence model

Every Ledger-owned or Derived assertion must be able to answer "why do you say that?": ResolutionLinks carry rationale (matched signals + observation refs); risk/lifecycle/ownership changes carry the triggering event + optional documents refs; ValueDeclarations carry methodology + evidenceRefs; ReportSnapshots carry the full input basis; coverage map assertions carry connection health history. Evidence is refs-to-immutable-things (observations, events, snapshot bases) — never copies.

## 7. Manual override strategy

Overridable (with actor + reason + optional expiry, provenance = Override, original value still projected underneath): displayName, assetType (re-classification), matchConfidence, org attribution, dormancy exclusion ("this asset is seasonal, not zombie"). **Not overridable:** observations (immutable), event history, frozen snapshots, telemetry policy ceilings, confidence labels on *others'* declarations. Overrides are events; expired overrides revert visibly. The rule of the house: **an override changes what we assert, never what we observed.**

## 8. Privacy & erasure mechanics

PersonKeyMap indirection (E19) + privacy marking at ingestion (E2) + display-time policy enforcement (ADR-014) + privileged-read records (E20). Erasure = key severance; retention of pseudonymous aggregates is documented as the lawful-basis position **[requires legal review — OI-003 workstream, not a design assumption]**.

## 9. Retention & deletion (conceptual classes)

| Class | Contents | Default retention (tenant-configurable within jurisdiction floors/ceilings) |
|---|---|---|
| Observations — inventory | E2 Inventory/Identity | 24 months full, then summarised (delta chains preserved for active assets) |
| Observations — usage/cost | E2 Usage/Cost | 7 years (financial basis for snapshots) |
| Domain events | E20 | 10 years (audit-grade), never user-deletable |
| Privileged-read records | E20 | ≥ policy requirement, privileged |
| Frozen snapshots | E14 | Life of tenant + contractual |
| Personal key map | E19 | Until erasure request or leaver+N policy |
| Read models / projections | — | Disposable, rebuildable, no retention semantics |

Soft vs hard per G5. Tenant offboarding = cryptographic/complete purge, contractually defined (commercial requirement, ADR-001).

## 10. Data quality & validation rules (beyond per-entity rules above)

- **Completeness gates:** Registered production assets: purpose + owner + risk profile (surfaced as governance debt, not write-blocks — the platform reports reality, it doesn't refuse to see it).
- **Staleness:** observation streams with lastSeen > threshold (per surface freshness from Stage 3) degrade the coverage map and flag affected assets' technical attributes as stale — displayed, not hidden.
- **Consistency:** enum domains closed (confidence taxonomies, states); transitions only via guards (Stage 4 state machines); Money requires currency; periods must not overlap within E13.
- **Reconciliation honesty rule:** where two surfaces disagree (Microsoft's own counts don't reconcile — Stage 3), the model stores both observations and displays the discrepancy; it never averages, picks silently, or fabricates agreement.

## 11. Challenges applied (simplifications chosen)

1. **20 entities, not 40:** attribute payloads absorb per-surface variety (no per-provider entity types); analytics/projections excluded from the conceptual model (Stage 4 §11.3 upheld); GovernanceCase and DependencyRef remain stubs.
2. **Embedded native IDs on E2 + separate E5:** deliberate duplication — observations must be self-contained forever (immutability), aliases must be queryable for reverse lookup. Copy-on-ingest, reconciled by the resolution service. Rejected alternative: normalising observation IDs through E5 (breaks observation immutability the day an alias re-parents).
3. **RiskProfile as revision entity (E10), not asset attribute:** Risk owns its revision history and methodology independently of CoE-owned asset attributes — organisational ownership drove the split, not data theory.
4. **PersonKeyMap adds one entity to delete none:** the O(1) erasure design is cheaper than any purge pipeline it replaces.
5. **Resisted:** modelling notifications, dashboards, or "reports" as entities; a generic "tag" entity (capability tags are E9 values); per-attribute history tables (events are the history, G4).

---

## Stage-end review

### Summary
Twenty entities; every attribute carries a source-of-truth/provenance class; identity is surrogate-everywhere with alias collisions treated as resolution disputes; scoped bitemporality per Stage 4 §8 plus ADR-016 frozen snapshots with restatement-by-versioning; GDPR erasure as key severance; retention in five classes; DQ rules that flag rather than block (the ledger reports reality). Finalisation blocked only on ⛔PoC items: alias types per archetype, the confidence rule table, and native-ID reuse semantics.

### Assumptions
- Delta-suppression at ingestion (E2.deltaStatus) adequately controls observation volume (R-18) — sizing confirmed in the architecture stage.
- Pseudonymous-aggregate retention post-erasure is legally defensible in target jurisdictions — **flagged for legal review, not assumed** (OI-003).
- Provider contract schema versioning (C4.5) can absorb surface payload drift without conceptual change.

### Confirmed facts
None new (no new Microsoft claims; Stage 3 evidence referenced only).

### Unknowns
⛔PoC items (§2 E5/E6, §5); jurisdiction retention floors/ceilings enumeration (Stage 9/legal); observation volumetrics at 6,000-employee scale.

### Risks
- **R-20 (new):** copy-on-ingest duplication (E2 embedded IDs vs E5) could drift if the resolution service has bugs — mitigation: E5 is always rebuildable from E2 (observations are the truth); drift detection is a standing DQ job.
- R-18 (observation volume) partially mitigated by deltaStatus design; residual to Stage 7/8.

### Alternative approaches considered
Normalised observation identifiers (rejected — §11.2); universal per-attribute history (rejected — events suffice); blocking DQ gates (rejected — a governance ledger that refuses to record ungoverned reality defeats its purpose); "Unknown" as default confidence label (rejected — mandatory labelling forces the honesty conversation).

### Questions for Arun
1. Approve the conceptual model as **draft-pending-PoC** (finalisation on Gate-1 results per PD-004), with ⛔PoC sections clearly bounded?
2. **Retention defaults** (§9): acceptable starting points for Quadient, or does Finance/Legal mandate different floors (esp. 7-year usage/cost basis)?
3. **DQ posture** — flag-not-block (§10): confirmed? (The alternative — refusing unregistered production assets — is available but contradicts the discovery mission.)
4. **Stage 6 proposal:** with the data model drafted, I recommend Stage 6 = **Personas, journeys & UX architecture** (deferred from the original sequence; V1 trio needs design before the architecture stage assembles everything). Alternative: governance & lifecycle detail (C2/V1.5 design). Which?

### Recommendations
1. Approve draft; commission Gate-1 PoCs now (specs ready, PD-005) — they are the only blocker to finalisation.
2. Send §8/§9 (erasure position + retention classes) to legal review in parallel — longest lead time item in this stage.
3. Hold the line on flag-not-block; it will be challenged internally the first time an ungoverned production agent appears on an executive dashboard — that discomfort is the product working.
