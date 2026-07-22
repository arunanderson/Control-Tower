namespace ControlTower.Platform.Events;

/// <summary>
/// A handler for an integration event delivered off the outbox (Stage 9 §2.3). The delivery seam is
/// composed by the host, not by the modules — a producing module stages a message on the outbox and a
/// consuming module registers a handler for its topic, so no module ever references another module. The
/// payload is the producer's canonical JSON; the handler reconstructs its own contract from it (no type
/// crosses the module boundary). Handlers must be idempotent — a message may be delivered more than once.
/// </summary>
public interface IIntegrationEventHandler
{
    /// <summary>The outbox topic this handler consumes.</summary>
    string Topic { get; }

    /// <summary>Handles one message. Must be idempotent and must establish its own tenant scope.</summary>
    Task HandleAsync(ReadOnlyMemory<byte> payload, CancellationToken ct = default);
}
