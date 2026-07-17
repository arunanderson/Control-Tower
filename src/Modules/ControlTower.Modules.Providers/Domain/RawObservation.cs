namespace ControlTower.Modules.Providers.Domain;

/// <summary>
/// A provider's raw acquisition output — pre-resolution, provider-shaped. The invariant pipeline
/// (privacy filter, delta-suppress, append) turns these into immutable ProviderObservations (C4/Stage 4
/// §2.1); the resolution engine (C1) maps the native identifiers to the alias model. Every raw
/// observation carries an honest evidence label (e.g. "Self-reported / Manual Import" for CSV, ADR-013).
/// </summary>
public sealed record RawObservation
{
    public required string SurfaceId { get; init; }
    public required ProviderCapability Capability { get; init; }
    public required IReadOnlyList<NativeIdentifier> NativeIdentifiers { get; init; }
    public required IReadOnlyDictionary<string, string> Attributes { get; init; }
    public required DateTimeOffset ObservedAt { get; init; }
    public required string EvidenceLabel { get; init; }
}
