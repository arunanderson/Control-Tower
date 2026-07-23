using ControlTower.Modules.Trust.Authorization;
using ControlTower.Platform.Tenancy;

namespace ControlTower.Modules.Trust.Infrastructure;

/// <summary>
/// DEV-ONLY tenant-partitioned E19 severance point. Production uses field-protected PostgreSQL + RLS.
/// </summary>
public sealed class InMemoryPersonKeyMap(ITenantContextAccessor tenants)
    : IPersonKeyMap
{
    private readonly object _gate = new();
    private readonly Dictionary<TenantId, Dictionary<Guid, PersonKey>> _keys = [];

    public Task<PersonKey?> FindAsync(
        Guid directoryObjectId,
        CancellationToken ct = default)
    {
        if (directoryObjectId == Guid.Empty)
            return Task.FromResult<PersonKey?>(null);

        var tenant = tenants.Current;
        lock (_gate)
        {
            return Task.FromResult<PersonKey?>(
                BucketFor(tenant).TryGetValue(directoryObjectId, out var key)
                    ? key
                    : null);
        }
    }

    public Task<PersonKey> GetOrCreateAsync(
        Guid directoryObjectId,
        CancellationToken ct = default)
    {
        if (directoryObjectId == Guid.Empty)
            throw new RoleAssignmentException(
                "A non-empty subject oid is required.");

        var tenant = tenants.Current;
        lock (_gate)
        {
            var bucket = BucketFor(tenant);
            if (!bucket.TryGetValue(directoryObjectId, out var key))
                bucket[directoryObjectId] = key = new PersonKey(Guid.NewGuid());
            return Task.FromResult(key);
        }
    }

    private Dictionary<Guid, PersonKey> BucketFor(TenantId tenant) =>
        _keys.TryGetValue(tenant, out var keys)
            ? keys
            : _keys[tenant] = [];
}
