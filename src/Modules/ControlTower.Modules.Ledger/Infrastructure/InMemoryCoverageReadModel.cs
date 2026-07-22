using ControlTower.Modules.Ledger.Application;
using ControlTower.Platform.Tenancy;

namespace ControlTower.Modules.Ledger.Infrastructure;

/// <summary>
/// DEV-ONLY (DEV-001) coverage read model. With no provider connections yet (C4 is a later, tenant-gated
/// train), it honestly reports zero connected providers and no sweep — the inventory is manual
/// registration only. Production derives this from live ProviderConnection health.
/// </summary>
public sealed class InMemoryCoverageReadModel(IAssetLedgerReadModel assets, ITenantContextAccessor tenants) : ICoverageReadModel
{
    private readonly object _gate = new();
    private readonly Dictionary<TenantId, Dictionary<string, Dictionary<string, ProviderCoverageFact>>> _facts = [];

    public async Task<CoverageView> GetAsync(CancellationToken ct = default)
    {
        var tenant = tenants.Current;
        var known = (await assets.QueryAsync(ct)).Count;
        List<ProviderCoverageFact> facts;
        lock (_gate)
            facts = _facts.TryGetValue(tenant, out var byConnection)
                ? byConnection.Values.SelectMany(x => x.Values).ToList()
                : [];

        var now = DateTimeOffset.UtcNow;
        var surfaces = facts.GroupBy(x => new { x.ConnectionRef, x.SurfaceId })
            .Select(group =>
            {
                var latest = group.OrderByDescending(x => x.CompletedAt).First();
                var successful = group.Where(x => x.Outcome == "Completed").OrderByDescending(x => x.CompletedAt).FirstOrDefault();
                return new ProviderSurfaceCoverageView
                {
                    ConnectionRef = group.Key.ConnectionRef,
                    SurfaceId = group.Key.SurfaceId,
                    CoveredCapabilities = group.Where(x => x.Outcome == "Completed").Select(x => x.Capability).Distinct().Order().ToList(),
                    State = latest.Outcome == "Completed" ? "Connected" : "Degraded",
                    IsFresh = successful is not null && now - successful.CompletedAt <= successful.FreshnessExpectation,
                    LastSuccessfulSweep = successful?.CompletedAt,
                    Observed = latest.Observed,
                    New = latest.New,
                    Changed = latest.Changed,
                    Suppressed = latest.Suppressed,
                };
            })
            .OrderBy(x => x.SurfaceId)
            .ThenBy(x => x.ConnectionRef)
            .ToList();

        var lastSweep = surfaces.Max(x => x.LastSuccessfulSweep);
        return new CoverageView
        {
            ProvidersConnected = surfaces.Count(x => x.State == "Connected"),
            AssetsKnown = known,
            LastSuccessfulSweep = lastSweep,
            CoverageNote = surfaces.Count == 0
                ? "No provider sweeps recorded — automated discovery coverage is not yet established."
                : $"Coverage is evidenced by {surfaces.Count} provider connection(s); stale or degraded surfaces remain visible.",
            AsOf = now,
            Surfaces = surfaces,
        };
    }

    public Task ProjectAsync(ProviderCoverageFact fact, CancellationToken ct = default)
    {
        var tenant = tenants.Current;
        lock (_gate)
        {
            if (!_facts.TryGetValue(tenant, out var byConnection))
                _facts[tenant] = byConnection = [];
            if (!byConnection.TryGetValue(fact.ConnectionRef, out var byCapability))
                byConnection[fact.ConnectionRef] = byCapability = [];
            if (!byCapability.TryGetValue(fact.Capability, out var current) ||
                fact.CompletedAt > current.CompletedAt ||
                (fact.CompletedAt == current.CompletedAt && fact.RunId == current.RunId))
                byCapability[fact.Capability] = fact;
        }
        return Task.CompletedTask;
    }
}
