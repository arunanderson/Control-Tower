using ControlTower.Modules.Economics.Application;
using ControlTower.Modules.Economics.Domain;
using ControlTower.Platform.Tenancy;

namespace ControlTower.Modules.Economics.Infrastructure;

/// <summary>DEV-ONLY (DEV-001) tenant-partitioned in-memory economics store. Production: Azure PostgreSQL (DEC-001).</summary>
public sealed class InMemoryEconomicsStore(ITenantContextAccessor tenants) : IEconomicsStore
{
    private sealed class Bucket
    {
        public List<CostObservation> Costs { get; } = [];
        public List<UsageObservation> Usage { get; } = [];
        public Dictionary<Guid, ValueDeclaration> Declarations { get; } = [];
        public Dictionary<Guid, ReportingPeriod> Periods { get; } = [];
        public List<ReportSnapshot> Snapshots { get; } = [];
    }

    private readonly Dictionary<TenantId, Bucket> _byTenant = [];
    private readonly object _gate = new();

    public Task AddCostAsync(CostObservation observation, CancellationToken ct = default)
    {
        var tenant = tenants.Current;
        if (observation.Tenant != tenant) throw new InvalidOperationException("Cross-tenant cost write rejected.");
        lock (_gate) BucketFor(tenant).Costs.Add(observation);
        return Task.CompletedTask;
    }

    public Task AddUsageAsync(UsageObservation observation, CancellationToken ct = default)
    {
        var tenant = tenants.Current;
        if (observation.Tenant != tenant) throw new InvalidOperationException("Cross-tenant usage write rejected.");
        lock (_gate) BucketFor(tenant).Usage.Add(observation);
        return Task.CompletedTask;
    }

    public Task SaveDeclarationAsync(ValueDeclaration declaration, CancellationToken ct = default)
    {
        var tenant = tenants.Current;
        if (declaration.Tenant != tenant) throw new InvalidOperationException("Cross-tenant declaration write rejected.");
        lock (_gate) BucketFor(tenant).Declarations[declaration.Id] = declaration;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<CostObservation>> CostsAsync(CancellationToken ct = default)
    {
        var tenant = tenants.Current;
        lock (_gate) return Task.FromResult<IReadOnlyList<CostObservation>>(BucketFor(tenant).Costs.ToList());
    }

    public Task<IReadOnlyList<UsageObservation>> UsageAsync(CancellationToken ct = default)
    {
        var tenant = tenants.Current;
        lock (_gate) return Task.FromResult<IReadOnlyList<UsageObservation>>(BucketFor(tenant).Usage.ToList());
    }

    public Task<IReadOnlyList<ValueDeclaration>> DeclarationsAsync(CancellationToken ct = default)
    {
        var tenant = tenants.Current;
        lock (_gate) return Task.FromResult<IReadOnlyList<ValueDeclaration>>(BucketFor(tenant).Declarations.Values.ToList());
    }

    public Task<ValueDeclaration?> GetDeclarationAsync(Guid id, CancellationToken ct = default)
    {
        var tenant = tenants.Current;
        lock (_gate)
        {
            BucketFor(tenant).Declarations.TryGetValue(id, out var declaration);
            return Task.FromResult(declaration);
        }
    }

    public Task SavePeriodAsync(ReportingPeriod period, CancellationToken ct = default)
    {
        var tenant = tenants.Current;
        if (period.Tenant != tenant) throw new InvalidOperationException("Cross-tenant reporting period write rejected.");
        lock (_gate) BucketFor(tenant).Periods[period.Id] = period;
        return Task.CompletedTask;
    }

    public Task AppendSnapshotAsync(ReportSnapshot snapshot, CancellationToken ct = default)
    {
        var tenant = tenants.Current;
        if (snapshot.Tenant != tenant) throw new InvalidOperationException("Cross-tenant report snapshot write rejected.");
        lock (_gate)
        {
            var snapshots = BucketFor(tenant).Snapshots;
            if (snapshots.Any(x => x.Id == snapshot.Id || (x.PeriodId == snapshot.PeriodId && x.Version == snapshot.Version)))
                throw new EconomicsException("Report snapshot identity and version must be unique.");
            snapshots.Add(snapshot);
        }
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ReportingPeriod>> PeriodsAsync(CancellationToken ct = default)
    {
        var tenant = tenants.Current;
        lock (_gate) return Task.FromResult<IReadOnlyList<ReportingPeriod>>(BucketFor(tenant).Periods.Values.OrderBy(x => x.Start).ToList());
    }

    public Task<ReportingPeriod?> GetPeriodAsync(Guid id, CancellationToken ct = default)
    {
        var tenant = tenants.Current;
        lock (_gate)
        {
            BucketFor(tenant).Periods.TryGetValue(id, out var period);
            return Task.FromResult(period);
        }
    }

    public Task<IReadOnlyList<ReportSnapshot>> SnapshotsAsync(Guid periodId, CancellationToken ct = default)
    {
        var tenant = tenants.Current;
        lock (_gate)
            return Task.FromResult<IReadOnlyList<ReportSnapshot>>(BucketFor(tenant).Snapshots.Where(x => x.PeriodId == periodId).OrderBy(x => x.Version).ToList());
    }

    private Bucket BucketFor(TenantId tenant) =>
        _byTenant.TryGetValue(tenant, out var bucket) ? bucket : _byTenant[tenant] = new Bucket();
}
