using ControlTower.Modules.Ledger.Application;
using ControlTower.Modules.Ledger.Domain;
using ControlTower.Platform.Tenancy;

namespace ControlTower.Modules.Ledger.Infrastructure;

/// <summary>
/// DEV-ONLY (DEV-001) tenant-partitioned in-memory repository. Reading the tenant from the ambient
/// context makes cross-tenant access impossible by construction. Production: Azure Database for
/// PostgreSQL with row-level security (DEC-001). Swappable by configuration; never a production path.
/// </summary>
public sealed class InMemoryAssetRepository(ITenantContextAccessor tenants) : IAssetRepository
{
    private readonly Dictionary<TenantId, Dictionary<LedgerAssetId, AIAsset>> _byTenant = [];
    private readonly object _gate = new();

    public Task<AIAsset?> GetAsync(LedgerAssetId id, CancellationToken ct = default)
    {
        var tenant = tenants.Current;
        lock (_gate)
        {
            Bucket(tenant).TryGetValue(id, out var asset);
            return Task.FromResult(asset);
        }
    }

    public Task SaveAsync(AIAsset asset, CancellationToken ct = default)
    {
        var tenant = tenants.Current;
        if (asset.Tenant != tenant)
            throw new InvalidOperationException("Cannot save an asset that belongs to a different tenant.");
        lock (_gate)
        {
            Bucket(tenant)[asset.Id] = asset;
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<AIAsset>> ListAsync(CancellationToken ct = default)
    {
        var tenant = tenants.Current;
        lock (_gate)
        {
            IReadOnlyList<AIAsset> result = Bucket(tenant).Values.ToList();
            return Task.FromResult(result);
        }
    }

    private Dictionary<LedgerAssetId, AIAsset> Bucket(TenantId tenant) =>
        _byTenant.TryGetValue(tenant, out var bucket) ? bucket : _byTenant[tenant] = [];
}
