---
type: blocked
schema_version: 1
task: P0-T16
blocked_at: 2026-07-23
severity: medium
awaiting: Human actions only when the corresponding validation or production gate is reached
---

## Build status

The build is **not globally blocked**. Tenant-independent product, security, privacy, persistence,
provider, collector, test, packaging and infrastructure-authoring work may continue autonomously.

## Microsoft Gate-1 validation

The authorised Microsoft 365 developer sandbox was exercised on 2026-07-23:

- app-only token acquisition succeeded;
- Agent ID application reads succeeded and returned an empty inventory;
- the tenant contains none of the representative modern, legacy, Agent Builder, published Copilot
  Studio or Foundry agent archetypes required by the frozen PoC;
- both Graph Package Management endpoints returned `403` because the tenant has no Microsoft Agent
  365 licence;
- PoC-2 has no published bot/manifest test data.

P0-T16 therefore remains **inconclusive/blocked for representative validation**, not unstarted.
Agent 365 is not a Control Tower product dependency; it gates only validation and data available from
that provider surface.

### Human action when this gate is reached

Provide or designate a tenant with the required licences and representative agent archetypes, then
approve the documented permissions/consent. Confidence rules remain unchanged until real evidence
exists.

## DEV-002 production gates

Enterprise-wide endpoint/browser visibility is approved for tenant-independent implementation under
`DEV-002`. Production activation remains gated on:

- human PD-006 ratification of the frozen ADR-007 scope text;
- Legal, Privacy and works-council approval where applicable;
- employee transparency and an approved telemetry purpose/retention policy;
- endpoint signing certificates, MDM deployment policy and enterprise-security allow-listing.

No prompt/response content, keystrokes, screen contents or documents may be collected by default.

## Retention jurisdiction authority

P5-T04 must use an authoritative, versioned, Legal-owned jurisdiction-policy source. Tenant
configuration may choose only within those externally governed bounds. The engine can be built to
fail closed; production legal values cannot be invented by the implementation agent.

## Production environment gates

The agent may author and validate Azure infrastructure, migrations and deployment artifacts.
Production credentials, execution against shared/production data, and production deployment remain
human gates.
