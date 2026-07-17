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
}

/// <summary>Tenant-scoped coverage read model.</summary>
public interface ICoverageReadModel
{
    Task<CoverageView> GetAsync(CancellationToken ct = default);
}
