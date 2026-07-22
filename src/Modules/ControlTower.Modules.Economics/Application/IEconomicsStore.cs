using ControlTower.Modules.Economics.Domain;

namespace ControlTower.Modules.Economics.Application;

/// <summary>
/// Tenant-scoped store for the one economics semantic model — cost/usage observations and value
/// declarations. Implementations resolve the tenant from the ambient context (ADR-021). Production:
/// Azure Database for PostgreSQL (DEC-001); dev: in-memory. Append-only facts; declarations keep their
/// full revision chain.
/// </summary>
public interface IEconomicsStore
{
    Task AddCostAsync(CostObservation observation, CancellationToken ct = default);
    Task AddUsageAsync(UsageObservation observation, CancellationToken ct = default);
    Task SaveDeclarationAsync(ValueDeclaration declaration, CancellationToken ct = default);
    Task SavePeriodAsync(ReportingPeriod period, CancellationToken ct = default);
    Task AppendSnapshotAsync(ReportSnapshot snapshot, CancellationToken ct = default);

    Task<IReadOnlyList<CostObservation>> CostsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<UsageObservation>> UsageAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ValueDeclaration>> DeclarationsAsync(CancellationToken ct = default);
    Task<ValueDeclaration?> GetDeclarationAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<ReportingPeriod>> PeriodsAsync(CancellationToken ct = default);
    Task<ReportingPeriod?> GetPeriodAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<ReportSnapshot>> SnapshotsAsync(Guid periodId, CancellationToken ct = default);
}
