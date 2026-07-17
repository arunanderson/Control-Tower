namespace ControlTower.Platform.Events;

/// <summary>A domain event. Events are the audit trail — one hash-chained, append-only stream (ADR-015/021).</summary>
public interface IDomainEvent
{
    Guid EventId { get; }
    DateTimeOffset OccurredAt { get; }
}

/// <summary>Hash-chain integrity over the event stream (ADR-021). Anchored to WORM storage periodically.</summary>
public interface IHashChain
{
    /// <summary>Deterministic next hash from the previous hash and this record's canonical payload.</summary>
    string ComputeNext(string previousHash, ReadOnlyMemory<byte> payload);
}
