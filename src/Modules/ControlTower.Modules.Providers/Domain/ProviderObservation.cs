using ControlTower.Platform.Privacy;
using ControlTower.Platform.Tenancy;

namespace ControlTower.Modules.Providers.Domain;

/// <summary>
/// An immutable, append-only, pre-resolution observation (Stage 4 §2.1 / Stage 5 E2, ADR-015). It is
/// what a raw acquisition becomes once it has passed the C4 invariant pipeline: contract-validated,
/// privacy-marked (Gate 1), delta-classified, and appended. Observations know nothing about AIAssets —
/// the C1 resolution engine points links <em>at</em> them, never the reverse. Nothing here is ever
/// edited or deleted; a later reading of the same entity is a new observation.
/// </summary>
public sealed record ProviderObservation
{
    public required Guid ObservationId { get; init; }
    public required TenantId Tenant { get; init; }

    /// <summary>The connection this came through (the credentialled surface instance).</summary>
    public required string ConnectionRef { get; init; }
    public required string SurfaceId { get; init; }
    public required ObservationKind Kind { get; init; }

    /// <summary>The provider-local native identifiers this observation contributes to the alias model (C1).</summary>
    public required IReadOnlyList<NativeIdentifier> NativeIdentifiers { get; init; }

    /// <summary>The provider-shaped payload (denormalized attributes). Read only via the pipeline (I3/I4).</summary>
    public required IReadOnlyDictionary<string, string> Payload { get; init; }

    public required DateTimeOffset ObservedAt { get; init; }
    public required DateTimeOffset IngestedAt { get; init; }

    /// <summary>Set once at ingestion (ADR-014 Gate 1); immutable thereafter.</summary>
    public required PrivacyMarking PrivacyMarking { get; init; }

    public required DeltaStatus DeltaStatus { get; init; }

    /// <summary>The honest evidence label carried from acquisition (e.g. "Self-reported / Manual Import", ADR-013).</summary>
    public required string EvidenceLabel { get; init; }

    /// <summary>Stable content hash used for delta classification and idempotent replay.</summary>
    public required string ContentHash { get; init; }

    /// <summary>
    /// Natural key for storage identity (Stage 5 E2): connection + primary native identifier + observedAt.
    /// Two sweeps of an unchanged entity differ only by observedAt, so each remains individually addressable.
    /// </summary>
    public string NaturalKey =>
        $"{ConnectionRef}|{(NativeIdentifiers.Count > 0 ? $"{NativeIdentifiers[0].System}:{NativeIdentifiers[0].IdentifierType}:{NativeIdentifiers[0].Value}" : "(none)")}|{ObservedAt:O}";
}
