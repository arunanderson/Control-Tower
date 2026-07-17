namespace ControlTower.Platform.Events;

/// <summary>A message staged for reliable, at-least-once dispatch after the event is committed.</summary>
public sealed record OutboxMessage(long Position, string Topic, byte[] Payload);

/// <summary>
/// Transactional outbox (Stage 9 §2.3): messages are staged in the same store as the event so
/// dispatch survives crashes, then drained by the worker. Production dispatch: Azure Service Bus.
/// </summary>
public interface IOutbox
{
    ValueTask EnqueueAsync(string topic, ReadOnlyMemory<byte> payload, CancellationToken ct = default);

    /// <summary>Reads up to <paramref name="max"/> undispatched messages in position order.</summary>
    ValueTask<IReadOnlyList<OutboxMessage>> DequeueBatchAsync(int max, CancellationToken ct = default);

    /// <summary>Marks a message dispatched so it is not delivered again.</summary>
    ValueTask AcknowledgeAsync(long position, CancellationToken ct = default);
}
