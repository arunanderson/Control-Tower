---
id: DEV-001
title: Proposed use of Supabase as the Control Tower backend
type: deviation-proposal
schema_version: 1
status: approved-with-conditions # proposed | approved | rejected | withdrawn
raised_by: Claude Code (build agent)
raised_at: 2026-07-16
decision: approve-with-conditions # pending | approve | reject | approve-with-conditions
decided_by: Arun (a.anderson@quadient.com) — direct instruction (to be ratified by merging PR #1)
decided_at: 2026-07-16
affects_adrs: [ADR-021, ADR-022, ADR-023]
affects_principles:
  - "credentials isolated more strongly than data (ADR-021)"
  - "verifiable evidence integrity / WORM anchoring (ADR-021, Stage 9 §5.5)"
  - "custom Azure application (ADR-022)"
  - "Azure stack + Azure lock-in accepted (ADR-023)"
requires_human_approval: true
---

## 1. Context

A Supabase organisation and a project named **"Control Tower"** (`azfvzsdspgvcyadnqgcg`, region **West EU / Ireland**, created 2026‑07‑16) have been provisioned and shared with the build agent. Before any code is written against it, the frozen blueprint requires that Supabase be assessed against the accepted ADRs and that it **not be adopted silently**.

Supabase is a **third‑party, AWS‑hosted Backend‑as‑a‑Service** (managed PostgreSQL + PostgREST auto‑API + GoTrue auth + Storage + Edge Functions + Realtime + Vault).

## 2. What the frozen blueprint says (verbatim)

- **ADR‑022:** "_The platform is a custom **Azure** application._"
- **ADR‑023:** "_Containerised .NET modular monolith; **Azure Container Apps**; React + TypeScript; Entra ID; **Azure Key Vault**; **Azure Service Bus**; regional deployment stamps; GitHub Actions; **Bicep** as default IaC._"
  - Amendment 1: "_**PostgreSQL vs Azure SQL** is an implementation decision, reversible until build begins_" — the reversible choice is the **relational engine**, not the hosting model or platform.
  - Amendment 3: "_**Azure lock‑in accepted as a conscious commercial decision**; do not optimise for hypothetical multi‑cloud._"
  - Portability: "_Azure‑proprietary seams are isolated to three adapters — **secrets (Key Vault), queueing (Service Bus), blob/WORM storage** — each replaceable behind existing module interfaces._"
- **ADR‑021:** "_credentials isolated more strongly than data_"; "_verifiable evidence integrity_"; "_storage‑refusal privacy enforcement_"; "_mandatory multi‑tenant failure isolation._"

**The DB‑engine reversibility clause does not authorise moving the platform to an AWS‑hosted BaaS.** That is a change to ADR‑022/023, i.e. a change to frozen architecture, which the build agent is explicitly not permitted to make autonomously.

## 3. Capability‑by‑capability assessment

| Required capability             | Supabase reality                                                                                                 | Frozen requirement                                                                                                          | Verdict                                                                                    |
| ------------------------------- | ---------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------ |
| Commercial multi‑tenancy        | Postgres + RLS; single project‑per‑region                                                                        | Multi‑tenant SaaS, regional stamps (ADR‑001/023)                                                                            | ⚠️ Feasible logically; stamp/residency model differs                                       |
| Tenant isolation / RLS          | **Native Postgres RLS — a genuine strength**                                                                     | RLS‑grade, unforgeable tenant context (ADR‑021)                                                                             | ✅ Strong (RLS) / ⚠️ "unforgeable by construction" still needs the app tier, not PostgREST |
| Auth with Entra ID              | Supabase Auth supports OIDC/SAML SSO incl. Entra; or bypass and use Entra directly                               | Entra ID (ADR‑023)                                                                                                          | ✅ Feasible either way                                                                     |
| Immutable provider observations | Append‑only tables + triggers denying update/delete                                                              | Append‑only, immutable (ADR‑015)                                                                                            | ✅ Feasible on any Postgres                                                                |
| Append‑only domain events       | Same                                                                                                             | Hash‑chained event stream (ADR‑015/021)                                                                                     | ✅ Feasible (hash chain in app)                                                            |
| Evidence integrity              | Hash chain feasible; **Supabase Storage has no WORM/immutability policy**                                        | Hash‑chain **+ WORM blob anchors** (ADR‑021, Stage 9 §5.5)                                                                  | ❌ **Gap** — no equivalent to Azure Blob immutable (WORM) storage                          |
| Audit history                   | Postgres append‑only                                                                                             | Event record = audit (ADR‑015)                                                                                              | ✅ Feasible                                                                                |
| Legal holds                     | App‑level                                                                                                        | V1 capability (ADR‑021)                                                                                                     | ✅ Feasible                                                                                |
| Configurable retention          | App‑level + pg_cron                                                                                              | Policy‑driven (ADR‑017)                                                                                                     | ✅ Feasible (interacts with WORM gap)                                                      |
| Regional data residency         | Project region = **AWS EU‑Ireland**, single‑region per project                                                   | **Azure** regional stamps, tenant pinned (ADR‑022/023)                                                                      | ❌ **AWS, not Azure**; multi‑region = multiple projects                                    |
| Background jobs                 | pg_cron + pgmq queues + Edge Functions                                                                           | **Azure Service Bus** (DLQ‑as‑quarantine, sessions) (ADR‑023 §5.1)                                                          | ⚠️ Weaker durable‑queue semantics                                                          |
| Secrets isolation               | **Supabase Vault = secrets inside Postgres** (pgsodium)                                                          | **Key Vault, plane‑separated, per‑tenant partitions; credentials isolated MORE strongly than data** (ADR‑021, Stage 9 §4.3) | ❌ **Conflict** — secrets co‑located with data is weaker isolation                         |
| Enterprise scalability          | Enterprise tier scales Postgres                                                                                  | Fleet/stamp model (ADR‑023)                                                                                                 | ⚠️ Directionally OK; model differs                                                         |
| Future migration portability    | "just Postgres" for DB = portable; **Supabase Auth/Storage/Edge/PostgREST/Realtime = Supabase‑specific lock‑in** | Proprietary seams isolated to 3 adapters (ADR‑023)                                                                          | ⚠️ DB portable; platform features increase lock‑in                                         |

## 4. Impact analysis

**Adopting Supabase as the production backend would change frozen architecture:**

- **ADR‑022** (custom **Azure** application) → the platform would run on an **AWS‑hosted third‑party BaaS**. Direct conflict.
- **ADR‑023** (Azure Container Apps / Key Vault / Service Bus / Bicep; Azure lock‑in accepted; portability via three isolated Azure adapters) → replaced/bypassed. The reversibility clause covers only PostgreSQL‑vs‑Azure‑SQL, not this.
- **ADR‑021** — two material weakenings: (a) **secrets‑in‑DB** violates "credentials isolated more strongly than data" and the credential‑boundary doctrine (Stage 7 §7); (b) **no WORM storage** weakens "verifiable evidence integrity."
- **Residency:** moves customer data from **Azure EU** to **AWS EU (Ireland)** — a data‑protection / DPA / sovereignty change requiring legal review, and it collides with the Azure‑stamp residency model.

**Where Supabase is genuinely strong:** native Postgres **RLS** (tenant isolation), managed Postgres, fast developer velocity, Entra SSO support. None of these strengths require adopting Supabase specifically — they are Postgres/Entra capabilities available on Azure Database for PostgreSQL Flexible Server, which the blueprint already selects.

## 5. Recommendation

**REJECT Supabase as the production platform backend.** It changes ADR‑022 and ADR‑023 and materially weakens two ADR‑021 security principles (secrets isolation, evidence‑integrity WORM). The blueprint's frozen DB decision remains **PostgreSQL vs Azure SQL on Azure** (ADR‑023 amendment 1), to be closed in Phase 0 alongside the RLS performance spike and Gate‑1 results.

**Optional narrowly‑bounded carve‑out (for Arun to weigh, not recommended by default):** Supabase _could_ be permitted as an **ephemeral local/dev PostgreSQL sandbox or a Gate‑1 / RLS‑spike scratch environment** — provided it is: managed‑Postgres only (no Supabase Auth / Storage / Edge Functions / Realtime / PostgREST as product surfaces), carries **no customer or tenant data**, and is not on any production path. Even this risks baking in Supabase‑isms; the fact that the project is named "Control Tower" in the production EU region suggests production intent, so this carve‑out should be an explicit, recorded choice if desired.

## 6. Decision required from Arun

- [ ] **Reject** — keep Azure + PostgreSQL/Azure SQL per ADR‑022/023 (recommended).
- [ ] **Approve with conditions** — permit the bounded dev/PoC sandbox carve‑out in §5 only.
- [ ] **Approve** — adopt Supabase as backend; this requires a **formal PD‑006 revision** of ADR‑022/023 (and ADR‑021 mitigations for secrets + WORM), authored and approved before any build against it.

**Status: STOPPED pending Arun's decision. No code will be written against Supabase until this deviation is decided.**

## 7. Decision (recorded 2026-07-16)

**Arun's decision (direct instruction): APPROVE WITH CONDITIONS.**

- **Azure remains the production target.** Supabase — and any BaaS — is **rejected as a production backend**. ADR‑022/023 stand unchanged; the frozen DB decision remains **PostgreSQL vs Azure SQL on Azure**. **No PD‑006 revision is triggered** (frozen architecture is unchanged).
- **Development‑only substitutes are permitted** (e.g. local Docker PostgreSQL, local queue/blob emulators, or a managed‑Postgres/Supabase scratch instance) **only** under **all** of:
  1. **Isolated to development/test** — never referenced by production IaC (Bicep), production config, or any production path.
  2. **No architectural dependency** — accessed solely through the port/adapter interfaces (DB via **standard SQL only**, no engine‑specific features per ADR‑023 amend. 1; secrets/queue/blob behind their adapters); no provider SDK in the domain.
  3. **Replaceable before production without changing application architecture** — substitutes are alternate adapter implementations, swapped by configuration.
  4. **Clearly marked development‑only** — naming + config convention; listed in a dev‑substitute registry.
- **Hard rule:** no development shortcut may become a production dependency.

### Enforcement (to be wired when the rails / CI are built — E0/E1)

- **Architecture‑boundary tests:** no provider SDK or engine‑specific API outside its adapter; the domain depends only on ports (extends the two‑doors / I3‑I4 rule set).
- **Production‑readiness CI gate:** fails if any dev‑substitute reference (dev adapter, emulator host, non‑Azure endpoint) appears in production config or `/infra`.
- **Standard‑SQL‑only check** on migrations (ADR‑023 amend. 1).
- **Dev‑substitute registry** in `docs/build/state/` — every substitute + its production (Azure) replacement, so replacement before production is a checklist, not archaeology.

This decision closes DEV‑001. It does **not** close Gate‑0, approve the Phase 0 plan, or grant tenant access — those remain open (see STATUS.md).
