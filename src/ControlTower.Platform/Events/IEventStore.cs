namespace ControlTower.Platform.Events;

/// <summary>
/// Append-only, hash-chained event store (ADR-015/021). Deliberately exposes no update or delete —
/// immutability is a property of the contract, not merely of an implementation. Production: an
/// append-only partitioned table in Azure Database for PostgreSQL with WORM-anchored digests.
/// </summary>
public interface IEventStore
{
    /// <summary>
    /// Appends an event with its canonical payload and returns the stored, chained record. Opaque
    /// event IDs are globally unique; duplicate appends fail before mutation.
    /// </summary>
    ValueTask<StoredEvent> AppendAsync(IDomainEvent @event, ReadOnlyMemory<byte> payload, CancellationToken ct = default);

    /// <summary>Reads the full stream in order (for projection rebuilds and integrity verification).</summary>
    ValueTask<IReadOnlyList<StoredEvent>> ReadAllAsync(CancellationToken ct = default);
}
