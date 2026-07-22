using ControlTower.Modules.Ledger.Application;
using ControlTower.Modules.Ledger.Domain;
using ControlTower.Platform.Tenancy;

namespace ControlTower.Modules.Ledger.Infrastructure;

/// <summary>DEV-ONLY (DEV-001) tenant-partitioned in-memory merge-case store. Production: Azure PostgreSQL (DEC-001).</summary>
public sealed class InMemoryMergeCaseStore(ITenantContextAccessor tenants) : IMergeCaseStore
{
    private readonly Dictionary<TenantId, Dictionary<Guid, MergeCase>> _byTenant = [];
    private readonly object _gate = new();

    public Task SaveAsync(MergeCase mergeCase, CancellationToken ct = default)
    {
        var tenant = tenants.Current;
        if (mergeCase.Tenant != tenant) throw new InvalidOperationException("Cross-tenant merge-case write rejected.");
        lock (_gate) Bucket(tenant)[mergeCase.Id] = mergeCase;
        return Task.CompletedTask;
    }

    public Task<MergeCase?> GetAsync(Guid id, CancellationToken ct = default)
    {
        var tenant = tenants.Current;
        lock (_gate)
        {
            Bucket(tenant).TryGetValue(id, out var mergeCase);
            return Task.FromResult(mergeCase);
        }
    }

    public Task<IReadOnlyList<MergeCase>> OpenCasesAsync(CancellationToken ct = default)
    {
        var tenant = tenants.Current;
        lock (_gate)
        {
            IReadOnlyList<MergeCase> open = Bucket(tenant).Values.Where(c => c.Status == MergeCaseStatus.Open).ToList();
            return Task.FromResult(open);
        }
    }

    public Task<MergeCase?> FindOpenForAsync(NativeIdentifier identifier, Guid? observationRef, CancellationToken ct = default)
    {
        var tenant = tenants.Current;
        lock (_gate)
        {
            var match = Bucket(tenant).Values.FirstOrDefault(c =>
                c.Status == MergeCaseStatus.Open &&
                c.ObservationRef == observationRef &&
                c.Identifiers.Identifiers.Any(i => i == identifier));
            return Task.FromResult(match);
        }
    }

    private Dictionary<Guid, MergeCase> Bucket(TenantId tenant) =>
        _byTenant.TryGetValue(tenant, out var bucket) ? bucket : _byTenant[tenant] = [];
}
