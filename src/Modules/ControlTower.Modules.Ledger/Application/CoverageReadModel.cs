namespace ControlTower.Modules.Ledger.Application;

/// <summary>
/// Discovery coverage (C1.6) — the honesty surface for the Trust area. Reports what the ledger can and
/// cannot see: connected providers, known assets, last successful sweep. Overstated coverage is worse
/// than none, so this is deliberately conservative until provider connections (C4) exist.
/// </summary>
public sealed record CoverageView
{
    public required int ProvidersConnected { get; init; }
    public required int AssetsKnown { get; init; }
    public DateTimeOffset? LastSuccessfulSweep { get; init; }
    public required string CoverageNote { get; init; }
    public required DateTimeOffset AsOf { get; init; }
    public required IReadOnlyList<ProviderSurfaceCoverageView> Surfaces { get; init; }
}

public sealed record ProviderSurfaceCoverageView
{
    public required string ConnectionRef { get; init; }
    public required string SurfaceId { get; init; }
    public required IReadOnlyList<string> CoveredCapabilities { get; init; }
    public required string State { get; init; }
    public required bool IsFresh { get; init; }
    public DateTimeOffset? LastSuccessfulSweep { get; init; }
    public required int Observed { get; init; }
    public required int New { get; init; }
    public required int Changed { get; init; }
    public required int Suppressed { get; init; }
}

public sealed record ProviderCoverageFact(
    Guid RunId,
    string ConnectionRef,
    string SurfaceId,
    string Capability,
    string Outcome,
    DateTimeOffset CompletedAt,
    TimeSpan FreshnessExpectation,
    int Observed,
    int New,
    int Changed,
    int Suppressed);

/// <summary>Tenant-scoped coverage read model.</summary>
public interface ICoverageReadModel
{
    Task<CoverageView> GetAsync(CancellationToken ct = default);
    Task ProjectAsync(ProviderCoverageFact fact, CancellationToken ct = default);
}
