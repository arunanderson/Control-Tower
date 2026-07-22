# PoC-3 findings — sandbox readiness run (2026-07-23)

## Authentication recipe

- OAuth 2.0 client-credentials token acquisition for Microsoft Graph succeeded.
- The test registration had the application permission `CopilotPackages.Read.All` with tenant admin
  consent.

## Endpoint observations

| Endpoint | Result |
|---|---|
| `GET /v1.0/copilot/admin/catalog/packages` | `403 Forbidden` — tenant requires a Microsoft Agent 365 licence |
| `GET /beta/copilot/admin/catalog/packages` | `403 Forbidden` — same licence requirement |

The Microsoft 365 admin centre showed only the Microsoft 365 E5 Developer subscription. No Agent 365
licence was present or assignable in the tenant.

## Outcome

**Licence prerequisite confirmed; remaining questions blocked.** The run confirms that valid
application permission and admin consent are insufficient without tenant Agent 365 licensing. It
cannot determine package coverage, v1.0/beta field differences, or inventory-scale throttling because
both endpoints reject the tenant before returning catalogue data.

## Required rerun condition

Rerun after Microsoft Agent 365 is licensed for the tenant and the four required agent archetypes plus
a registry-synchronised third-party agent are present.
