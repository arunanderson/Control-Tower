using ControlTower.Platform.Tenancy;

namespace ControlTower.Modules.Providers.Domain;

/// <summary>
/// An immutable log record of one sweep (Stage 5 E3 IngestionRun). It records honestly what a sweep did
/// — how many observations were seen, how many were new/changed, and how many were delta-suppressed —
/// so coverage and freshness can be reported truthfully (never inferred). Append-only; never edited.
/// </summary>
public sealed record IngestionRun
{
    public required Guid RunId { get; init; }
    public required TenantId Tenant { get; init; }
    public required string ConnectionRef { get; init; }
    public required string SurfaceId { get; init; }
    public required ProviderCapability Capability { get; init; }
    public required DateTimeOffset StartedAt { get; init; }
    public required DateTimeOffset CompletedAt { get; init; }
    public required int Observed { get; init; }
    public required int New { get; init; }
    public required int Changed { get; init; }
    public required int Suppressed { get; init; }
    public required string Outcome { get; init; }
}
