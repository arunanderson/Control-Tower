# Stage 8 — Security, Identity, Privacy, Tenant Isolation & Trust Architecture

| | |
|---|---|
| **Version** | 1.0 |
| **Date** | 2026-07-15 |
| **Status** | Draft — awaiting Arun's approval |
| **Related** | [stage-07-conceptual-architecture.md](stage-07-conceptual-architecture.md) (ADR-020), [stage-05-conceptual-data-model.md](stage-05-conceptual-data-model.md) (E17/E19/E20), [stage-03-microsoft-validation.md](stage-03-microsoft-validation.md) (permission evidence), [decision-log.md](decision-log.md) |

**Scope discipline:** conceptual security architecture — no technology selection (that is Stage 9, which this document constrains).

---

## 1. The honest threat headline

This platform aggregates read credentials to customers' Microsoft estates plus their governance, cost, and (policy-permitting) individual usage data. **It is, by design, one of the juiciest key-rings in any customer's environment.** Every decision below flows from that admission. The attacker's prize is not our data — it is *pivot* into customer tenants via our credentials, *exfiltration* of aggregated telemetry, *tampering* with evidence, or *cross-tenant leakage*. A governance product that suffers any of these is not damaged; it is dead. Security here is not a compliance layer — it is the product's licence to exist.

## 2. Threat boundaries

| Boundary | Threat held there | Primary controls (conceptual) |
|---|---|---|
| Customer tenant ↔ platform | Credential theft → tenant pivot (the existential one) | Per-tenant credential isolation, certificate-based auth, least-privilege consent, customer-side revocability + Conditional Access, no standing broad permissions |
| Tenant ↔ tenant | Cross-tenant leakage | Isolation model §3; no shared computation (except opt-in benchmarking, Later, own boundary) |
| Platform staff ↔ tenant data | Insider access / ops overreach | No standing access, JIT + approval, privileged-read audit applies to staff (§8) |
| Internal: domain ↔ privileged zone | Privacy breach via internal path | Privileged zone (§9) with separate identities and audited access even in-process |
| Platform ↔ outbound (V2 control) | Abuse of write powers into customer tenants | Separate consent, separate credentials, human-approved actions at introduction (Stage 7 §7) |
| Evidence store | Tampering with the audit record | Append-only + integrity verification (§13) |

## 3. Tenant isolation model

- **V1: logical isolation, uniformly enforced.** Every store partitioned by tenant; every query tenant-scoped by construction (not by developer discipline — the tenancy context is injected at the boundary and unforgeable within a request); every job, queue, budget, and event stream tenant-scoped (Stage 7 §7).
- **Credential isolation is stronger than data isolation:** per-tenant secrets in per-tenant vault partitions; an adapter process resolves only its own tenant's credentials at execution time; no component can enumerate credentials across tenants.
- **Failure isolation:** per-tenant quotas and queues (noisy-neighbour containment); a tenant can be suspended, throttled, exported, or purged with zero cross-tenant effect.
- **Commercial tiers preserved, not built:** dedicated-instance and customer-managed-key options are boundary-compatible (nothing below assumes shared keys or shared instances) — offered Later, priced accordingly.

## 4. Identity model

**Humans:** federation with the customer's Entra ID only — SSO, no local accounts, no passwords held, ever. The customer's own Conditional Access, MFA, and identity governance therefore apply to our product automatically. Platform staff authenticate against the vendor's own identity plane, entirely separate from customer identity.

**Service identities (platform → customer Microsoft estate):**
- **Default model:** a published **multi-tenant Entra application** with a documented, versioned permission manifest; customers grant admin consent at onboarding. Certificate credentials, rotated; never client secrets in production. [Standard, Confirmed pattern; exact scopes evidenced in Stage 3.]
- **High-assurance alternative (documented, supported):** customer-created app registration in *their* tenant with the same manifest, credentials held per-tenant — gives security-sensitive customers unilateral revocation and full sign-in visibility. More onboarding friction; their choice.
- **Azure-side access:** customer-granted RBAC (Reader) on relevant scopes to a per-customer service principal; Power Platform via PPAC RBAC role assignment (Stage 3 [C] — delegated-only API permissions make this the documented path).
- **Platform-internal identities:** each plane (and the privileged zone) runs under distinct workload identities so that "which component did this" is an identity fact, not a log inference.

## 5. Authentication boundaries

User → experience plane (customer Entra); adapter → provider surface (per-tenant vaulted credential, per-surface); plane → plane (workload identity across the §1/Stage 7 seam — this seam is authenticated *now*, in the monolith, precisely so the future in-tenant split changes topology, not trust model); staff → production (vendor identity + JIT elevation only).

## 6. Authorization model

Three orthogonal axes, never conflated (extends Stage 6 §9):
1. **Role** (capability grants): Viewer, Operator, Administrator, Executive-scope in V1 (C8.2); capabilities are fine-grained, roles are curated bundles; direct capability grants are not offered (bundle sprawl is how RBAC dies).
2. **Org scope:** which slice of the estate (tenant-wide in V1; BU-scoped with delegated administration in V1.5). Delegation rule: **a delegated administrator can never widen data granularity, raise telemetry levels, or alter privacy policy** — delegation distributes work, never privacy authority.
3. **Data granularity** (telemetry policy, E17): jurisdiction/population-resolved; conflicts resolve to the **most restrictive** applicable policy; the read-time enforcement point evaluates axis 3 *after* axes 1–2 pass — a Global Admin with L1 policy sees aggregates, full stop.

## 7. Customer consent architecture

Consent is a product surface, not a legal formality:
- **Tiered permission packs** mapping to feeds: Baseline (inventory: PPAC, Entra Agent ID, licence reads), Usage (Graph reports), Economics (cost/billing scopes), Compliance signals (Purview/Management Activity), each independently consentable — and the **coverage map displays exactly what each granted/withheld pack enables/hides** (consent transparency as a Trust-area feature).
- **Write consent (V2 control actions) is a separate, later, explicit event** — never bundled into read onboarding.
- Every consented scope is enumerated, justified, and versioned in the manifest; scope additions require re-consent, and release notes say why. Scope creep is treated as a breaking change.

## 8. Least privilege, privileged access, break-glass

- **Least privilege:** the permission manifest holds only scopes evidenced as necessary in Stage 3; each new scope requires an ADR-level justification.
- **Staff access:** **no standing access to tenant data.** Production access is JIT, time-boxed, approval-gated, reason-required, session-recorded conceptually, and **customer-visible** (staff access to a tenant's data appears in that tenant's privileged-access log — ADR-015.9 applies to us, not just to them). This is expensive. It is also the only posture consistent with selling trust.
- **Break-glass:** dual-control (two named roles), time-boxed, scoped to one tenant, automatically expiring, fully audited, **customer-notified after use, always**. Break-glass that customers never hear about is a backdoor with paperwork.

## 9. Privileged zone

The smallest possible enclave holding: TelemetryPolicy administration, PersonKeyMap (E19), de-concealed identifier handling, credential vault access paths. Properties: separate workload identity; every read/write privileged-audited; no bulk export path; field-level protection (§11); even in the modular monolith it is a *logical enclave with its own identity and audit perimeter* — the one place where in-process trust is deliberately broken.

## 10. Data classification

| Class | Contents | Handling highlights |
|---|---|---|
| D1 Product metadata | taxonomies, rule templates | Normal |
| D2 Tenant business metadata | assets, purposes, ownership, governance state | Tenant-isolated, standard encryption |
| D3 Financial | cost observations, allocations, snapshots | D2 + financial retention (ADR-017), frozen-immutability |
| D4 Personal telemetry | L2+ usage data, person-linked records | Privileged-adjacent: policy-gated reads, privileged-read audit, PersonKeyMap indirection only |
| D5 Secrets | provider credentials, keys | Vault-only, per-tenant partition, never in logs/events/config |
| D6 Evidence | domain events, privileged-read records, snapshots | Append-only, integrity-verified, legal-hold capable |

## 11. Encryption & secrets boundaries (conceptual)

Encrypted in transit and at rest universally — stated once, assumed everywhere. The *boundaries* that matter: key separation per tenant (logical in V1; customer-managed keys as a Later tier the design must not preclude); **field-level protection for D4/D5** (PersonKeyMap values, de-concealed identifiers, credentials) so datastore-level compromise still yields pseudonyms; key rotation as routine operation, not incident response; secrets never leave the vault boundary — adapters receive short-lived credentials at execution, nothing persists them.

## 12. Privacy enforcement architecture (ADR-003/014/015.9 operationalised)

- **Gate 1 (ingestion):** policy-as-of filtering; PrivacyMarking set immutably; jurisdiction resolution via E16/E17; anything above the permitted level is **not stored** (not stored-and-hidden — the difference matters in subpoenas and breaches: you cannot leak what you never kept).
- **Gate 2 (read):** every read model declares clearance; the enforcement point evaluates the *current* policy — policy tightening takes effect at the next read, with no re-ingestion needed.
- **Re-masking (ADR-014):** if the customer de-conceals Microsoft reports, Gate 1 marks, Gate 2 masks per policy; the platform's display never follows the upstream toggle.
- **Jurisdiction-aware controls:** one tenant, many jurisdictions, resolved per data subject's population mapping; most-restrictive-wins on ambiguity; policy changes are effective-dated, justified, privileged events (E17).
- **Privileged-read auditing:** ON by default (ADR-015.9), covering customer users *and platform staff*; the viewer sees a notice (Stage 6 §9); the log is visible in the Trust area.
- **Challenged assumption — do we even need L2 in V1?** Recommendation: **ship V1 with L1 (aggregate-only) as the default-on state; L2 is an explicit customer activation** requiring policy configuration and (where applicable) their works-council/legal sign-off (OI-003). This converts our biggest deployment risk (R-08) into the customer's deliberate, documented choice — and V1's wedge (portfolio economics) works at L1 granularity for most metrics. → Question 2. *(Gloss per Revision Package v1.0: the L1-vs-named-reclamation tension found by Independent Review 01 is resolved by the scoped, time-boxed "Licence Reclamation Campaign" workflow — [revision-package-v1.md](revision-package-v1.md) §2.)*

## 13. Evidence integrity

Append-only event record with **verifiable integrity** (conceptually: chained integrity proofs over the event sequence, periodic anchoring, verifiable on export); frozen snapshots immutable with pinned basis (ADR-016); trusted time source for occurredAt/recordedAt discipline; evidence exports carry their integrity proof so a regulator can verify the record wasn't rewritten. **Legal hold** (new requirement surfaced this stage): a hold marker suspends retention-deletion for scoped data until released — without it, ADR-017 retention automation could destroy evidence mid-dispute. → added to model as a TenantConfiguration capability.

## 14. Security event model & administrative audit

Security-relevant occurrences are first-class events: authentication anomalies, consent grants/revocations, policy changes, privileged reads, break-glass, staff access, export/deletion requests, integrity check results. Two consumers: the tenant's own Trust area, and **outbound publish to the customer's SIEM** (V2 publish integration — we emit, they analyse; we are not a SIEM, ADR-005). All platform-administrative and staff actions are themselves evented into the same record — the audit model has no unaudited actors.

## 15. Tenant data export, deletion, retention enforcement

- **Export (anti-lock-in commitment):** complete tenant package — observations, events, ledger state, snapshots, configuration — in a documented format, self-service, integrity-proofed. A governance system of record that holds data hostage forfeits the trust it sells.
- **Deletion:** contractual offboarding purge (all planes, including derived stores and backups within a defined window) with a deletion attestation; person-level erasure remains key severance (Stage 5 §8).
- **Retention enforcement:** policy engine per ADR-017 + legal-hold precedence (§13) + jurisdiction floors; retention deletions are themselves evented (deletion without a record of deletion fails audit).

## 16. Pure SaaS vs future hybrid — implications (comparison only; ADR-020 stands)

| Dimension | Pure SaaS (V1) | Future in-tenant data plane |
|---|---|---|
| Provider credentials | In our vault (per-tenant partitions) | Stay in customer environment — the pivot-risk (§1) largely evaporates |
| D4 personal telemetry | Held by us under double-gate | Never leaves customer environment; control plane sees aggregates/read contracts only |
| Consent story | Admin consent to our multi-tenant app | Customer-hosted app registration natural fit |
| Staff access | JIT into our production | Near-zero: we operate software, not their data |
| Keys | Logical separation; CMK as tier | Customer keys native |
| Evidence integrity | Our anchoring, exportable proofs | Split anchoring (control-plane events + tenant data-plane events) — *hardest part of the future split* |
| Ops burden | Ours (SaaS economics) | Shared — upgrade orchestration into customer environments is the real cost |

Design consequence already honoured: the plane seam is authenticated and contract-based now (§5), and Gate 1 lives in the data plane — so the hybrid future moves *where* things run, not *how* trust works.

## 17. Security assumptions challenged (summary)

1. *"Multi-tenant app consent is fine because everyone does it"* — everyone also gets breached through it; hence the credential isolation model, cert-only auth, the high-assurance alternative (§4), and pivot-risk as headline (§1).
2. *"Staff need standing access to support customers"* — rejected; JIT + customer-visible access is the posture the product's own positioning demands (§8).
3. *"Store everything, filter at display"* — rejected; Gate 1 refuses storage above policy (§12) — you cannot leak what you never kept.
4. *"L2 telemetry is needed for V1 value"* — challenged; L1-default recommended (§12), L2 as customer activation.
5. *"Audit the users"* — insufficient; audit the operators, the staff, and the platform itself (§14: no unaudited actors).

---

## Stage-end review

### Summary
A security architecture whose organising admission is that the platform is a high-value key-ring: per-tenant credential isolation stronger than data isolation, federation-only human identity, tiered transparent consent, a privileged zone with its own perimeter inside the monolith, storage-refusal privacy gating, evidence with verifiable integrity plus legal hold, no-standing-access staff posture with customer-visible access logs, and an L1-default privacy recommendation that converts the works-council risk into a documented customer choice.

### Assumptions
- Customers accept SSO-only (no local accounts) — standard for enterprise SaaS; sales qualifier.
- JIT staff-access tooling is operationally affordable at V1 team size (process-heavy at first; the posture is non-negotiable, the tooling can mature).
- Integrity anchoring achievable without exotic infrastructure (Stage 9 to confirm mechanism; requirement stands regardless).

### Confirmed facts
Permission scopes referenced trace to Stage 3 evidence; no new Microsoft claims.

### Unknowns
- Whether high-assurance customers demand the customer-owned app registration path at a rate that changes onboarding design (sales evidence will tell).
- Works-council positions per jurisdiction (OI-003 — unchanged, still the longest-lead item).
- SIEM-export demand timing (V2 assumption).

### Risks
- **R-25 (new, structural):** aggregation-target risk (§1) — never closable, only managed; standing mitigations are the §2 table; incident response and disclosure posture must be defined pre-launch (flagged for Stage 10/12 operational readiness).
- **R-26 (new):** consent friction vs sales velocity — four packs and a manifest review lengthen onboarding; mitigation: Baseline pack alone must demo value (it does: inventory + licence economics).
- **R-27 (new):** JIT-only staff access slows early support; accepted cost; revisit tooling, never posture.

### Alternative approaches considered
Standing support access with audit (rejected — §17.2); store-then-mask (rejected — §17.3); single monolithic consent (rejected — transparency tiering is both safer and better sales UX); secrets in per-component config (rejected without discussion).

### Questions for Arun
1. **Approve the architecture**, including no-standing-access staff posture (R-27's cost accepted) and customer-visible staff access logs?
2. **L1-by-default, L2-as-explicit-activation** (§12) — approve as the V1 privacy posture? (It also de-risks the internal Quadient rollout against OI-003.)
3. **Legal hold** — added to the model this stage (§13); confirm as a V1 requirement (my view: yes — retention automation without hold capability is an audit liability from day one).
4. **Stage 9 proposal:** Technology & Deployment Architecture (selection constrained by ADR-020/021 and this document), followed by Stage 10 = Cost/ROI methodology. Confirm?

### Recommendations
1. Approve; promote §1's framing into the commercial narrative — "we designed as if we were the target, because we are" is a sales asset in security reviews.
2. Start the security-certification path planning (SOC 2-class attestation) in Stage 12 — commercial buyers will require it before any of this architecture matters to them.
3. Define the incident disclosure posture (time-bound customer notification commitments) as part of Stage 12 operational readiness — pre-committed honesty is the cheapest crisis management there is.
