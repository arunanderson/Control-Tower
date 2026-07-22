using ControlTower.Platform.Tenancy;

namespace ControlTower.Modules.Audit;

/// <summary>DEV-001 tenant-partitioned legal-hold substitute. Production: PostgreSQL with RLS.</summary>
public sealed class InMemoryLegalHoldStore(ITenantContextAccessor tenants) : ILegalHoldStore
{
    private readonly object _gate = new();
    private readonly Dictionary<TenantId, Dictionary<Guid, LegalHold>> _holds = [];

    public Task SaveAsync(LegalHold hold, CancellationToken ct = default)
    {
        var tenant = tenants.Current;
        if (hold.Tenant != tenant) throw new InvalidOperationException("Cross-tenant legal-hold write denied.");
        lock (_gate) BucketFor(tenant)[hold.Id] = hold;
        return Task.CompletedTask;
    }

    public Task<LegalHold?> GetAsync(Guid id, CancellationToken ct = default)
    {
        var tenant = tenants.Current;
        lock (_gate)
        {
            BucketFor(tenant).TryGetValue(id, out var hold);
            return Task.FromResult(hold);
        }
    }

    public Task<IReadOnlyList<LegalHold>> ListAsync(CancellationToken ct = default)
    {
        var tenant = tenants.Current;
        lock (_gate)
            return Task.FromResult<IReadOnlyList<LegalHold>>(
                BucketFor(tenant).Values.OrderByDescending(x => x.PlacedAt).ToList());
    }

    private Dictionary<Guid, LegalHold> BucketFor(TenantId tenant) =>
        _holds.TryGetValue(tenant, out var holds) ? holds : _holds[tenant] = [];
}
