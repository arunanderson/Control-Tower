# Development-substitute registry (DEV-001)

Every development-only substitute in use, with its production (Azure) replacement. A substitute may
appear here **only** if it meets all four DEV-001 conditions. CI (`production-readiness` gate) fails
if any of these appear in production config or `/infra`.

| Substitute (dev-only) | Purpose | Production replacement (Azure) | Access boundary (port/adapter) | Status |
|---|---|---|---|---|
| Local Docker PostgreSQL | Local dev DB / RLS spike scratch | Azure Database for PostgreSQL Flexible Server (DEC-001) | `IDataStore` / EF Core provider | planned (E2) |
| Local blob/WORM emulator (e.g. Azurite) | Local evidence/anchor storage | Azure Blob Storage (immutable/WORM) | `IEvidenceStore` adapter | planned (E3) |
| Local queue (in-memory / container) | Local background jobs | Azure Service Bus | `IJobQueue` adapter | planned (E3) |
| Local secrets (env/user-secrets) | Local dev secrets | Azure Key Vault | `ISecretProvider` adapter | planned (E1/E2) |

Rules: standard SQL only (DEC-001); no provider SDK in the domain; swappable by configuration;
never referenced by production IaC/config. Supabase is **not** listed — it is not adopted, even as a
default dev substitute (local Docker Postgres is preferred); if ever used it is a dev-only Postgres
scratch instance under these same rules.
