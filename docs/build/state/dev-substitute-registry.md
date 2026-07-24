# Development-substitute registry (DEV-001)

Every development-only substitute in use, with its production (Azure) replacement. A substitute may
appear here **only** if it meets all four DEV-001 conditions. The `production-readiness` CI gate fails
if any of these appear in production config or `/infra`; the `AdapterBoundaryTests` architecture rules
fail if the kernel or any module depends on an adapter.

| Substitute (dev-only)                | Port                          | Production replacement (Azure)                                                           | Registered where                                            | Status      |
| ------------------------------------ | ----------------------------- | ---------------------------------------------------------------------------------------- | ----------------------------------------------------------- | ----------- |
| `InMemoryEventStore`                 | `IEventStore`                 | Azure Database for PostgreSQL — append-only partitions + WORM-anchored digests (DEC-001) | `AddInMemoryAdapters`, Development only                     | implemented |
| `InMemoryOutbox`                     | `IOutbox`                     | Azure Service Bus                                                                        | `AddInMemoryAdapters`, Development only                     | implemented |
| `InMemoryPrivilegedReadAuditor`      | `IPrivilegedReadAuditor`      | Append-only audit store (PostgreSQL)                                                     | `AddInMemoryAdapters`, Development only                     | implemented |
| `InMemoryPrivilegedAccessProjection` | `IPrivilegedAccessProjection` | PostgreSQL customer-visible audit projection                                             | `AddAuditModule`, Development only                          | implemented |
| `InMemoryLegalHoldStore`             | `ILegalHoldStore`             | Azure Database for PostgreSQL + RLS                                                      | `AddAuditModule`, Development only                          | implemented |
| `InMemorySecretProvider`             | `ISecretProvider`             | Azure Key Vault                                                                          | `AddInMemoryAdapters`, Development only                     | implemented |
| `InMemoryAssetRepository`            | `IAssetRepository`            | Azure Database for PostgreSQL + RLS (DEC-001)                                            | `AddLedgerModule`, Development only                         | implemented |
| `InMemoryAssetLedgerReadModel`       | `IAssetLedgerReadModel`       | PostgreSQL projection (Stage 7 §5)                                                       | `AddLedgerModule`, Development only                         | implemented |
| `AllowAllLedgerAuthorizer`           | `ILedgerAuthorizer`           | C8.2 role/capability evaluator                                                           | `AddLedgerModule` Worker only; Web replaces it              | implemented |
| `InMemoryPersonKeyMap`               | `IPersonKeyMap`               | Field-protected PostgreSQL E19 map + RLS + O(1) severance + privileged read/write audit  | `AddDevelopmentControlTowerAuthorization`, Development only | implemented |
| `InMemoryRoleAssignmentStore`        | `IRoleAssignmentStore`        | PostgreSQL E18 + RLS + atomic event + active-grant uniqueness + optimistic revocation    | `AddDevelopmentControlTowerAuthorization`, Development only | implemented |
| `InMemoryEconomicsStore`             | `IEconomicsStore`             | Azure Database for PostgreSQL (DEC-001)                                                  | `AddEconomicsModule`, Development only                      | implemented |
| `InMemoryGovernanceStore`            | `IGovernanceStore`            | Azure Database for PostgreSQL (DEC-001)                                                  | `AddGovernanceModule`, Development only                     | implemented |
| `InMemoryWatermarkStore`             | `IWatermarkStore`             | Azure Database for PostgreSQL (sync watermarks) (DEC-001)                                | `AddProviderFramework`, Development only                    | implemented |
| `InMemoryObservationStore`           | `IObservationStore`           | Azure Database for PostgreSQL — append-only observation partitions (DEC-001)             | `AddProviderFramework`, Development only                    | implemented |
| `InMemoryProviderConnectionStore`    | `IProviderConnectionStore`    | Azure Database for PostgreSQL + RLS (credential references only)                         | `AddProviderFramework`, Development only                    | implemented |
| `InMemoryProviderJobReceiptStore`    | `IProviderJobReceiptStore`    | Azure Service Bus delivery state + PostgreSQL idempotency receipt                        | `AddProviderFramework`, Development only                    | implemented |
| `InMemoryMergeCaseStore`             | `IMergeCaseStore`             | Azure Database for PostgreSQL (DEC-001)                                                  | `AddLedgerModule`, Development only                         | implemented |
| Local Docker PostgreSQL              | `IEventStore` integration     | Azure Database for PostgreSQL Flexible Server (DEC-001)                                  | P1-T07 tests/CI, disposable loopback database only           | implemented |

Rules: standard SQL only (DEC-001); no provider SDK in the domain; swappable by configuration; never
referenced by production IaC/config. In-memory adapters are registered **only** under
`IsDevelopment()` (see `Host.Web`/`Host.Worker` `Program.cs`). Supabase is **not** used, even as a
dev substitute — local Docker Postgres is the sanctioned relational substitute.
