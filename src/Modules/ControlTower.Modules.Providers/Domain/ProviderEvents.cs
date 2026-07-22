using ControlTower.Platform.Events;

namespace ControlTower.Modules.Providers.Domain;

/// <summary>Base for C4 provider-integration domain events — the audit trail, one hash-chained stream (ADR-015).</summary>
public abstract record ProviderEvent : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Emitted when an observation is appended to the store (Stage 7 §ingestion). This is the only signal
/// the C1 resolution pipeline consumes — it carries a self-contained serialization contract (no shared
/// types cross the module boundary), so the resolution engine reconstructs the native identifier
/// independently. Unchanged (suppressed) observations do not emit this event.
/// </summary>
public sealed record ObservationIngested : ProviderEvent
{
    public required Guid ObservationId { get; init; }
    public required string ConnectionRef { get; init; }
    public required string SurfaceId { get; init; }
    public required string Kind { get; init; }
    public required string DeltaStatus { get; init; }
    public required string PrivacyMarking { get; init; }

    /// <summary>The primary native identifier, flattened for the resolution contract.</summary>
    public required string PrimaryIdentifierSystem { get; init; }
    public required string PrimaryIdentifierType { get; init; }
    public required string PrimaryIdentifierValue { get; init; }

    public required string EvidenceLabel { get; init; }
    public required DateTimeOffset ObservedAt { get; init; }
}
