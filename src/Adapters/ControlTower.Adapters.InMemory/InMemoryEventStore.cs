using ControlTower.Platform.Events;
using ControlTower.Platform.Tenancy;

namespace ControlTower.Adapters.InMemory;

/// <summary>
/// DEV-ONLY (DEV-001) in-memory substitute for <see cref="IEventStore"/>. Partitioned by tenant and
/// hash-chained per stream through the production canonical envelope. Production: Azure Database for
/// PostgreSQL append-only partitions with WORM-anchored digests.
/// </summary>
public sealed class InMemoryEventStore : IEventStore
{
    private readonly ITenantContextAccessor _tenants;
    private readonly TimeProvider _clock;
    private readonly IHashChain _chain;
    private readonly Dictionary<TenantId, List<StoredEvent>> _byTenant = [];
    private readonly HashSet<Guid> _eventIds = [];
    private readonly object _gate = new();

    public InMemoryEventStore(ITenantContextAccessor tenants)
        : this(tenants, TimeProvider.System, new Sha256HashChain())
    {
    }

    public InMemoryEventStore(
        ITenantContextAccessor tenants,
        TimeProvider clock,
        IHashChain chain)
    {
        _tenants = tenants ?? throw new ArgumentNullException(nameof(tenants));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _chain = chain ?? throw new ArgumentNullException(nameof(chain));
    }

    public ValueTask<StoredEvent> AppendAsync(
        IDomainEvent @event,
        ReadOnlyMemory<byte> payload,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ct.ThrowIfCancellationRequested();

        var tenant = _tenants.Current;
        var contract = DomainEventContracts.Resolve(@event);
        var eventId = @event.EventId;
        var occurredAt = @event.OccurredAt;
        var ownedPayload = payload.ToArray();

        lock (_gate)
        {
            ct.ThrowIfCancellationRequested();
            _byTenant.TryGetValue(tenant, out var stream);
            if (_eventIds.Contains(eventId))
            {
                throw new EventIntegrityException(
                    "The event ID already exists.");
            }

            var position = (stream?.Count ?? 0) + 1L;
            var previousHash = position == 1
                ? Sha256HashChain.Genesis
                : stream![^1].Hash;
            var prospective = new StoredEvent(
                EventEnvelopeCanonicalizer.CurrentIntegrityFormatVersion,
                position,
                eventId,
                contract.EventType,
                EventEnvelopeCanonicalizer.NormalizeTimestamp(
                    occurredAt),
                EventEnvelopeCanonicalizer.NormalizeTimestamp(
                    _clock.GetUtcNow()),
                tenant,
                contract.Privilege,
                previousHash,
                string.Empty,
                ownedPayload);
            var hash = _chain.ComputeNext(
                previousHash,
                EventEnvelopeCanonicalizer.Canonicalize(prospective));
            var stored = prospective with { Hash = hash };
            if (stream is null)
            {
                stream = [];
                _byTenant.Add(tenant, stream);
            }
            stream.Add(stored);
            _eventIds.Add(eventId);
            return ValueTask.FromResult(stored);
        }
    }

    public ValueTask<IReadOnlyList<StoredEvent>> ReadAllAsync(
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var tenant = _tenants.Current;
        lock (_gate)
        {
            IReadOnlyList<StoredEvent> result =
                _byTenant.TryGetValue(tenant, out var stream)
                    ? stream.ToList()
                    : [];
            return ValueTask.FromResult(result);
        }
    }
}
