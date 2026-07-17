using ControlTower.Modules.Governance.Application;
using ControlTower.Modules.Governance.Domain;
using ControlTower.Platform.Tenancy;

namespace ControlTower.Modules.Governance.Infrastructure;

/// <summary>DEV-ONLY (DEV-001) tenant-partitioned in-memory governance store. Production: Azure PostgreSQL (DEC-001).</summary>
public sealed class InMemoryGovernanceStore(ITenantContextAccessor tenants) : IGovernanceStore
{
    private sealed class Bucket
    {
        public Dictionary<GovernanceCaseId, GovernanceCase> Cases { get; } = [];
        public List<GovernanceDebtItem> Debt { get; } = [];
    }

    private readonly Dictionary<TenantId, Bucket> _byTenant = [];
    private readonly object _gate = new();

    public Task SaveCaseAsync(GovernanceCase governanceCase, CancellationToken ct = default)
    {
        var tenant = tenants.Current;
        if (governanceCase.Tenant != tenant) throw new InvalidOperationException("Cross-tenant case write rejected.");
        lock (_gate) BucketFor(tenant).Cases[governanceCase.Id] = governanceCase;
        return Task.CompletedTask;
    }

    public Task<GovernanceCase?> GetCaseAsync(GovernanceCaseId id, CancellationToken ct = default)
    {
        var tenant = tenants.Current;
        lock (_gate)
        {
            BucketFor(tenant).Cases.TryGetValue(id, out var governanceCase);
            return Task.FromResult(governanceCase);
        }
    }

    public Task<IReadOnlyList<GovernanceCase>> CasesAsync(CancellationToken ct = default)
    {
        var tenant = tenants.Current;
        lock (_gate) return Task.FromResult<IReadOnlyList<GovernanceCase>>(BucketFor(tenant).Cases.Values.ToList());
    }

    public Task AddDebtAsync(GovernanceDebtItem debt, CancellationToken ct = default)
    {
        var tenant = tenants.Current;
        if (debt.Tenant != tenant) throw new InvalidOperationException("Cross-tenant debt write rejected.");
        lock (_gate) BucketFor(tenant).Debt.Add(debt);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<GovernanceDebtItem>> DebtAsync(CancellationToken ct = default)
    {
        var tenant = tenants.Current;
        lock (_gate) return Task.FromResult<IReadOnlyList<GovernanceDebtItem>>(BucketFor(tenant).Debt.ToList());
    }

    private Bucket BucketFor(TenantId tenant) =>
        _byTenant.TryGetValue(tenant, out var bucket) ? bucket : _byTenant[tenant] = new Bucket();
}
