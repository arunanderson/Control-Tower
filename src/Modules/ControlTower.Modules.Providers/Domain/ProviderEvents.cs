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
[DomainEventContract("ObservationIngested", EventPrivilege.Standard)]
public sealed record ObservationIngested : ProviderEvent
{
    public required Guid ObservationId { get; init; }

    /// <summary>The tenant the observation belongs to — carried so off-outbox delivery re-enters the right scope (ADR-021).</summary>
    public required string Tenant { get; init; }
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

    /// <summary>
    /// A provider-suggested descriptor for the resolution engine to name a newly-created asset (the engine
    /// reconstructs identity from the event, never by reading the observation store — I3). Populated from
    /// the generic well-known attributes "displayName"/"assetType" when present, else sensible fallbacks.
    /// </summary>
    public required string DisplayName { get; init; }
    public required string AssetType { get; init; }
}

/// <summary>
/// Self-contained coverage fact emitted after an ingestion run is durably recorded. C1.6 consumes
/// this through the outbox; it never reads the provider store (I3).
/// </summary>
[DomainEventContract("ProviderCoverageUpdated", EventPrivilege.Standard)]
public sealed record ProviderCoverageUpdated : ProviderEvent
{
    public required Guid RunId { get; init; }
    public required string Tenant { get; init; }
    public required string ConnectionRef { get; init; }
    public required string SurfaceId { get; init; }
    public required string Capability { get; init; }
    public required string Outcome { get; init; }
    public required DateTimeOffset CompletedAt { get; init; }
    public required double FreshnessExpectationSeconds { get; init; }
    public required int Observed { get; init; }
    public required int New { get; init; }
    public required int Changed { get; init; }
    public required int Suppressed { get; init; }
}

/// <summary>Self-contained, secret-free request for the worker to execute a configured provider sweep.</summary>
[DomainEventContract("ProviderSweepRequested", EventPrivilege.Standard)]
public sealed record ProviderSweepRequested : ProviderEvent
{
    public required Guid JobId { get; init; }
    public required string Tenant { get; init; }
    public required string ConnectionId { get; init; }
    public required string SurfaceId { get; init; }
    public required string Capability { get; init; }
}
