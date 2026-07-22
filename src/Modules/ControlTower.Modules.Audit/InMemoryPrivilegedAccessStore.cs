using ControlTower.Platform.Tenancy;

namespace ControlTower.Modules.Audit;

/// <summary>DEV-001 tenant-partitioned append-only substitute. Production: PostgreSQL projection.</summary>
public sealed class InMemoryPrivilegedAccessProjection(ITenantContextAccessor tenants) : IPrivilegedAccessProjection
{
    private readonly object _gate = new();
    private readonly Dictionary<TenantId, List<PrivilegedAccessLogEntry>> _records = [];

    public Task ProjectAsync(PrivilegedAccessLogEntry entry, CancellationToken ct = default)
    {
        var tenant = tenants.Current;
        if (entry.Record.Tenant != tenant) throw new InvalidOperationException("Cross-tenant audit projection denied.");
        lock (_gate)
        {
            if (!_records.TryGetValue(tenant, out var records)) _records[tenant] = records = [];
            records.Add(entry);
        }
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<PrivilegedAccessLogEntry>> ListAsync(CancellationToken ct = default)
    {
        var tenant = tenants.Current;
        lock (_gate)
            return Task.FromResult<IReadOnlyList<PrivilegedAccessLogEntry>>(
                _records.TryGetValue(tenant, out var records) ? records.OrderByDescending(x => x.Record.OccurredAt).ToList() : []);
    }
}
