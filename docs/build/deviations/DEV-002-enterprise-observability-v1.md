---
id: DEV-002
title: Enterprise-wide observable AI coverage in V1
type: deviation-proposal
schema_version: 1
status: approved-with-conditions
raised_by: Product Owner
raised_at: 2026-07-23
decision: approve-with-conditions
decided_by: Arun Anderson — direct Product Owner instruction
decided_at: 2026-07-23
affects_adrs: [ADR-007]
affects_principles:
  - native-first V1 telemetry acquisition
  - custom browser desktop and endpoint collectors deferred from V1
  - personal-account AI visibility beyond Microsoft-native telemetry not required in V1
requires_human_approval: true
---

## 1. Product Owner decision

The V1 outcome is one Enterprise AI Control Tower that gives authorised organisational leaders
role-appropriate visibility into **all technically observable AI use across the corporate-managed
estate**:

- laptops and desktops;
- managed browsers and installed desktop applications;
- corporate identities and network/security control points;
- SaaS applications, AI assistants and developer tools;
- cloud AI services, models, agents and APIs;
- licences, consumption, cost, ownership, value, risk and governance evidence.

DeepSeek, ChatGPT or any other named product is an example of an observable AI service, not a product
boundary. The Control Tower must be vendor-independent.

The promise is deliberately falsifiable. It is not "we see everything everywhere." It is "we
collect every signal technically and lawfully observable within the managed corporate footprint,
show exactly what each signal proves, and expose every known blind spot."

## 2. Deviation from the frozen blueprint

ADR-007 defers custom browser, desktop and endpoint collectors from V1 and does not require
personal-account visibility beyond Microsoft-native telemetry. The Product Owner has made those
outcomes V1 requirements where activity occurs on or through the corporate-managed estate and can be
observed lawfully.

This is a **scope and sequencing amendment to ADR-007 only**:

- native and partner telemetry remains the preferred first source when it provides sufficient,
  supportable evidence;
- first-party managed browser and endpoint collectors are now permitted in V1 where measured
  coverage gaps remain;
- enterprise-versus-personal account classification is collected only where it can be determined
  reliably and within policy;
- native and first-party acquisition are complementary feeds into one evidence model, not separate
  products.

The frozen `/docs/blueprint` remains untouched. This living deviation is the approved build authority.
A human-executed PD-006 ratification remains required before production release.

## 3. Architecture impact

**No architecture redesign is required or authorised.**

- C4 remains the only external door. A browser extension, endpoint service, network feed, identity
  feed or vendor API is an ordinary C4 provider.
- C1 remains the asset and identity-resolution authority.
- C3 remains the usage, cost and value authority.
- C2 remains the governance-orchestration authority.
- C5 remains the organisation, population and jurisdiction authority.
- C8 remains the access and telemetry-policy authority.
- C9 remains the audit, evidence, retention and legal-hold authority.
- C7 remains the only human-facing door. CIO, CISO, People/Transformation and AI/CoE experiences are
  policy-filtered projections, not new bounded contexts.
- C6 remains intentionally vacant.

The platform does not become an endpoint-security product, SIEM, DLP engine, employee-monitoring
suite or process-mining platform. It consumes security/control signals and may orchestrate native
controls, but it does not duplicate them.

## 4. Acquisition coverage

| Corporate surface         | Provider strategy                                                              | Minimum evidence                                                                                                                                                | Explicit blind spots                                                                                  |
| ------------------------- | ------------------------------------------------------------------------------ | --------------------------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------- |
| Managed browser           | Native Purview/Defender/SWG feed plus first-party extension where needed       | app/domain classification, time bucket, interaction count, pseudonymous user/device, enterprise/personal classification only when reliable, collector heartbeat | unsupported browser/profile, disabled extension, encrypted/app traffic outside browser                |
| Managed endpoint          | EDR/MDM inventory plus first-party signed endpoint collector where needed      | AI application/process identity, install/use time bucket, local model/runtime metadata, pseudonymous user/device, collector health                              | unmanaged/offline device, unsupported OS, tampered or unhealthy collector                             |
| Network/security controls | secure web gateway, proxy, DNS, CASB, Defender or equivalent provider          | destination/service classification, allowed/blocked result, device/user correlation confidence, time bucket                                                     | direct/off-network paths, privacy relays, traffic without attributable identity                       |
| Identity and access       | Entra/SSO and equivalent identity providers                                    | sign-in, application/service principal, account type where authoritative, organisation mapping                                                                  | non-federated/personal identity not exposed by the source                                             |
| SaaS and AI vendors       | supported admin/usage APIs and audit exports                                   | asset, user/aggregate activity, model/feature metadata, plan/licence and native identifiers                                                                     | vendor API omissions, sampling, retention limits, personal accounts outside enterprise administration |
| Cloud, agents and APIs    | Azure/AWS/GCP control planes, AI gateways, model providers and agent platforms | resource/agent/model identity, caller/workload, tokens/requests, region, cost and ownership evidence                                                            | direct keys or unmanaged subscriptions outside connected control planes                               |
| Finance and licences      | billing, cost management, procurement, licence and approved manual providers   | money with currency, billing period, confidence class, methodology and attribution status                                                                       | off-contract spend, card purchases or invoices not connected                                          |

Every provider publishes health, freshness, granted capability, expected coverage and correlation
quality. Missing or degraded evidence is a first-class Trust-area fact.

## 5. Privacy and employee trust conditions

The scope amendment does not weaken ADR-003, ADR-014 or ADR-021:

1. **L1 aggregate-only is the default.**
2. Prompt text, response text, keystrokes, screen contents and document contents are not collected by
   default.
3. Gate 1 refuses storage above the effective telemetry policy; Gate 2 re-evaluates current policy
   before every human-facing read.
4. L2+ employee-linked metadata requires an explicit purpose, customer activation, jurisdiction and
   population resolution, role/scope authorisation, retention policy and privileged-read audit.
5. People/Transformation views are aggregate organisational insights by default. The platform does
   not produce individual productivity scores or automated employment decisions.
6. CISO investigations may expose authorised L2+ metadata only through purpose-bound, time-limited,
   auditable workflows.
7. Works-council, Legal, Privacy and employee-notice gates must close before production activation of
   affected endpoint or L2+ capabilities.

## 6. First-party collector security conditions

- Device enrolment is tenant-bound; packages are signed and distributed through approved enterprise
  management.
- Collector-to-C4 messages are authenticated, schema-versioned, timestamped and replay-resistant.
- Collectors send only the minimum metadata enabled by policy and never provider credentials.
- Collection can be disabled per tenant, population, capability and device group.
- Health and tamper signals affect coverage; they never silently manufacture activity.
- A compromised collector is treated as untrusted input. C4 validation, privacy filtering, delta
  suppression, quarantine and evidence rules still apply.

## 7. Human gates retained

- PD-006 ratification of the frozen ADR-007 text before production release.
- Legal/Privacy/works-council approval and employee transparency for applicable jurisdictions.
- Microsoft or other tenant permission/consent actions.
- Endpoint code-signing certificates, MDM/endpoint deployment policies and security allow-listing.
- Production Azure credentials, shared-environment migrations and production deployment.

These gates do not block tenant-independent implementation, tests, packaging or sandbox validation.

## 8. Decision consequences

V1 carries additional endpoint engineering, security review, cross-platform packaging, privacy and
operational-support cost. In exchange, the product outcome now matches the non-negotiable enterprise
visibility requirement and no longer depends on a single vendor's licensing or telemetry coverage.

If first-party collection is withdrawn, disable its provider connections and deployment policies.
Append-only observations remain evidence; coverage visibly degrades rather than being rewritten.
