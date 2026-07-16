namespace ControlTower.Platform.Events;

/// <summary>A domain event. Events are the audit trail — one hash-chained, append-only stream (ADR-015/021).</summary>
public interface IDomainEvent
{
    Guid EventId { get; }
    DateTimeOffset OccurredAt { get; }
}

/// <summary>Append-only event sink. There is deliberately no update or delete (ADR-015).</summary>
public interface IEventAppender
{
    /// <summary>Appends an event and returns its monotonically increasing stream position.</summary>
    ValueTask<long> AppendAsync(IDomainEvent @event, CancellationToken ct = default);
}

/// <summary>Hash-chain integrity over the event stream (ADR-021). Anchored to WORM storage periodically.</summary>
public interface IHashChain
{
    string ComputeNext(string previousHash, ReadOnlyMemory<byte> payload);
}
