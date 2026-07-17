using ControlTower.Platform.Events;
using ControlTower.Platform.Tenancy;

namespace ControlTower.Adapters.InMemory;

/// <summary>
/// DEV-ONLY (DEV-001) in-memory substitute for <see cref="IEventStore"/>. Partitioned by tenant and
/// hash-chained per stream. Production: Azure Database for PostgreSQL append-only partitions with
/// WORM-anchored digests. Never register this in a production composition.
/// </summary>
public sealed class InMemoryEventStore(ITenantContextAccessor tenants) : IEventStore
{
    private readonly Dictionary<TenantId, List<StoredEvent>> _byTenant = [];
    private readonly IHashChain _chain = new Sha256HashChain();
    private readonly object _gate = new();

    public ValueTask<StoredEvent> AppendAsync(IDomainEvent @event, ReadOnlyMemory<byte> payload, CancellationToken ct = default)
    {
        var tenant = tenants.Current; // throws without a tenant scope — isolation by construction
        lock (_gate)
        {
            var stream = _byTenant.TryGetValue(tenant, out var existing) ? existing : _byTenant[tenant] = [];
            var previous = stream.Count == 0 ? Sha256HashChain.Genesis : stream[^1].Hash;
            var stored = new StoredEvent(
                stream.Count + 1, @event.EventId, @event.OccurredAt, tenant,
                previous, _chain.ComputeNext(previous, payload), payload.ToArray());
            stream.Add(stored);
            return ValueTask.FromResult(stored);
        }
    }

    public ValueTask<IReadOnlyList<StoredEvent>> ReadAllAsync(CancellationToken ct = default)
    {
        var tenant = tenants.Current;
        lock (_gate)
        {
            IReadOnlyList<StoredEvent> result =
                _byTenant.TryGetValue(tenant, out var stream) ? stream.ToList() : [];
            return ValueTask.FromResult(result);
        }
    }
}
