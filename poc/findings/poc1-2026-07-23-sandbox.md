# PoC-1 findings — sandbox readiness run (2026-07-23)

## Scope

Tenant: authorised Microsoft 365 developer sandbox. The application-only test registration was
configured with `AgentIdentity.Read.All`, `AgentRegistration.Read.All`, and
`CopilotPackages.Read.All`, with tenant admin consent.

## Observations

- Application-only token acquisition succeeded.
- `GET /v1.0/servicePrincipals/microsoft.graph.agentIdentity` returned `200 OK` with zero items.
- The tenant has no Agent ID identities and does not contain the required modern, legacy,
  Agent Builder, published Copilot Studio, and Foundry test-agent archetypes.
- No PPAC inventory record or package record exists for a representative agent, so the
  `copilotPackage.appId` to PPAC `entraAppId` cross-walk could not be attempted.

## Outcome

**Inconclusive — representative-tenant prerequisites are absent.** This run validates the
application-only Agent ID read path and confirms an empty inventory; it does not validate or refute
the deterministic join required by PoC-1. ADR-012 confidence rules must remain unchanged.

## Required rerun condition

Rerun against a tenant containing all archetypes required by the frozen Gate-1 specification,
including an existing pre-March-2026 legacy service-principal agent.
