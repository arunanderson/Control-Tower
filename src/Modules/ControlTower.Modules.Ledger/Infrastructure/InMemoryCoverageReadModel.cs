using ControlTower.Modules.Ledger.Application;

namespace ControlTower.Modules.Ledger.Infrastructure;

/// <summary>
/// DEV-ONLY (DEV-001) coverage read model. With no provider connections yet (C4 is a later, tenant-gated
/// train), it honestly reports zero connected providers and no sweep — the inventory is manual
/// registration only. Production derives this from live ProviderConnection health.
/// </summary>
public sealed class InMemoryCoverageReadModel(IAssetLedgerReadModel assets) : ICoverageReadModel
{
    public async Task<CoverageView> GetAsync(CancellationToken ct = default)
    {
        var known = (await assets.QueryAsync(ct)).Count;
        return new CoverageView
        {
            ProvidersConnected = 0,
            AssetsKnown = known,
            LastSuccessfulSweep = null,
            CoverageNote = "No provider connections yet — inventory is manual-registration only; automated discovery coverage is not yet established.",
            AsOf = DateTimeOffset.UtcNow,
        };
    }
}
