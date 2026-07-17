using ControlTower.Modules.Economics.Domain;

namespace ControlTower.Modules.Economics.Application;

/// <summary>
/// Read side: one semantic model → many read models (asset, agent, department, business unit,
/// portfolio, executive). Agent ROI, department ROI, etc. are projections/filters over the same model,
/// not separate modules. Projections are computed as-of a timestamp and are reproducible for any
/// historical reporting period because they read only immutable facts (costs) and revision chains
/// (declarations) up to the as-of instant.
/// </summary>
public sealed class EconomicsProjectionService(IEconomicsStore store)
{
    public async Task<IReadOnlyList<AssetEconomicsView>> AssetEconomicsAsync(DateTimeOffset asOf, CancellationToken ct = default)
    {
        var model = await LoadAsync(asOf, ct);
        return model.AssetIds
            .Select(assetId => BuildAssetView(assetId, model, asOf))
            .OrderBy(v => v.AssetId)
            .ToList();
    }

    public async Task<RoiView> AgentRoiAsync(DateTimeOffset asOf, CancellationToken ct = default)
    {
        var model = await LoadAsync(asOf, ct);
        var agents = model.AssetIds.Where(id => string.Equals(model.AssetType(id), "agent", StringComparison.OrdinalIgnoreCase)).ToList();
        return BuildScopedRoi("agent-portfolio", agents, model, asOf);
    }

    public async Task<IReadOnlyList<RoiView>> DepartmentRoiAsync(DateTimeOffset asOf, CancellationToken ct = default) =>
        await GroupedRoiAsync(asOf, model => model.Department, "department", ct);

    public async Task<IReadOnlyList<RoiView>> BusinessUnitRoiAsync(DateTimeOffset asOf, CancellationToken ct = default) =>
        await GroupedRoiAsync(asOf, model => model.BusinessUnit, "business-unit", ct);

    public async Task<RoiView> PortfolioRoiAsync(DateTimeOffset asOf, CancellationToken ct = default)
    {
        var model = await LoadAsync(asOf, ct);
        return BuildScopedRoi("portfolio", model.AssetIds.ToList(), model, asOf);
    }

    public async Task<ExecutiveEconomicsView> ExecutiveAsync(DateTimeOffset asOf, CancellationToken ct = default)
    {
        var model = await LoadAsync(asOf, ct);
        var currency = model.Currency;
        var allCosts = model.AssetIds.SelectMany(model.CostFigures).ToList();
        var allValues = model.AssetIds.SelectMany(model.ValueFigures).ToList();

        var totalCost = allCosts.Sum(c => c.Amount.Amount);
        var declaredValue = allValues.Sum(v => v.Amount.Amount);
        var validatedValue = allValues.Where(v => v.Confidence == EvidenceClass.FinanciallyValidated).Sum(v => v.Amount.Amount);
        var unattributedCosts = model.AssetIds.Where(id => model.Department(id) is null).SelectMany(model.CostFigures).ToList();
        var unattributed = unattributedCosts.Sum(c => c.Amount.Amount);

        return new ExecutiveEconomicsView
        {
            TotalSpend = EconomicAmount.Composite(totalCost, currency, allCosts, $"{allCosts.Count} cost observation(s)", "sum; weakest-material-link (ADR-025)", asOf),
            DeclaredValue = EconomicAmount.Composite(declaredValue, currency, allValues, $"{allValues.Count} value declaration(s)", "sum; weakest-material-link (ADR-025)", asOf),
            ValidatedValue = EconomicAmount.Composite(validatedValue, currency, allValues.Where(v => v.Confidence == EvidenceClass.FinanciallyValidated).ToList(), "financially validated value", "sum of Financially validated figures", asOf),
            ValidatedToDeclaredRatio = declaredValue == 0m ? 0m : validatedValue / declaredValue,
            UnattributedCost = EconomicAmount.Composite(unattributed, currency, unattributedCosts, "cost with no department attribution", "Unattributed — never spread (ADR-025)", asOf),
            UnattributedPercent = totalCost == 0m ? 0m : unattributed / totalCost,
            ConfidenceMix = EconomicsMath.ConfidenceMix(allValues.Select(v => v.Confidence)).ToDictionary(k => k.Key.ToString(), v => v.Value),
            AsOf = asOf,
        };
    }

    private async Task<IReadOnlyList<RoiView>> GroupedRoiAsync(DateTimeOffset asOf, Func<Model, Func<Guid, string?>> dimensionSelector, string scopePrefix, CancellationToken ct)
    {
        var model = await LoadAsync(asOf, ct);
        var dimension = dimensionSelector(model);
        return model.AssetIds
            .GroupBy(id => dimension(id) ?? "Unattributed")
            .OrderBy(g => g.Key)
            .Select(g => BuildScopedRoi($"{scopePrefix}:{g.Key}", g.ToList(), model, asOf))
            .ToList();
    }

    private static AssetEconomicsView BuildAssetView(Guid assetId, Model model, DateTimeOffset asOf)
    {
        var roi = BuildScopedRoi($"asset:{assetId}", [assetId], model, asOf);
        return new AssetEconomicsView
        {
            AssetId = assetId,
            AssetType = model.AssetType(assetId),
            Department = model.Department(assetId),
            BusinessUnit = model.BusinessUnit(assetId),
            Cost = roi.Cost,
            Value = roi.Value,
            NetBenefit = roi.NetBenefit,
            Roi = roi,
            AsOf = asOf,
        };
    }

    private static RoiView BuildScopedRoi(string scope, IReadOnlyList<Guid> assetIds, Model model, DateTimeOffset asOf)
    {
        var costFigures = assetIds.SelectMany(model.CostFigures).ToList();
        var valueFigures = assetIds.SelectMany(model.ValueFigures).ToList();
        var currency = model.Currency;
        var roi = EconomicsMath.Compute(costFigures, valueFigures);

        return new RoiView
        {
            Scope = scope,
            Cost = EconomicAmount.Composite(roi.TotalCost, currency, costFigures, $"{costFigures.Count} cost observation(s)", "sum; weakest-material-link (ADR-025)", asOf),
            Value = EconomicAmount.Composite(roi.TotalValue, currency, valueFigures, $"{valueFigures.Count} value declaration(s)", "sum; weakest-material-link (ADR-025)", asOf),
            NetBenefit = EconomicAmount.Composite(roi.NetBenefit, currency, [.. costFigures, .. valueFigures], "net benefit (value − cost)", "value − cost; weakest-material-link (ADR-025)", asOf),
            SinglePointRoi = roi.SinglePointRoi,
            ValidatedOnlyRoi = roi.ValidatedOnlyRoi,
            LowRoi = roi.LowRoi,
            HighRoi = roi.HighRoi,
            PaybackMonths = roi.PaybackMonths,
            SinglePointSuppressed = roi.SinglePointSuppressed,
            CompositeEvidenceClass = roi.CompositeValueClass.ToString(),
            ConfidenceMix = roi.ConfidenceMix.ToDictionary(k => k.Key.ToString(), v => v.Value),
            AsOf = asOf,
        };
    }

    private async Task<Model> LoadAsync(DateTimeOffset asOf, CancellationToken ct)
    {
        var costs = (await store.CostsAsync(ct)).Where(c => c.PeriodEnd <= asOf).ToList();
        var declarations = await store.DeclarationsAsync(ct);
        return new Model(costs, declarations, asOf);
    }

    /// <summary>An as-of view of the immutable model: costs (period-filtered) and declaration figures (revision as-of).</summary>
    private sealed class Model
    {
        private readonly ILookup<Guid, CostObservation> _costs;
        private readonly Dictionary<Guid, List<EconomicFigure>> _values = [];
        private readonly Dictionary<Guid, (string Type, string? Dept, string? Bu)> _dims = [];

        public Model(IReadOnlyList<CostObservation> costs, IReadOnlyList<ValueDeclaration> declarations, DateTimeOffset asOf)
        {
            _costs = costs.ToLookup(c => c.AssetId);
            foreach (var c in costs)
                _dims.TryAdd(c.AssetId, (c.AssetType, c.Department, c.BusinessUnit));

            foreach (var d in declarations)
            {
                var revision = d.Revisions.Where(r => r.At <= asOf).OrderBy(r => r.At).LastOrDefault();
                if (revision is null) continue;
                if (!_values.TryGetValue(d.AssetId, out var list)) _values[d.AssetId] = list = [];
                list.Add(revision.Figure);
                _dims.TryAdd(d.AssetId, (d.AssetType, null, null));
            }

            Currency = costs.Select(c => c.Cost.Amount.Currency)
                .Concat(_values.Values.SelectMany(v => v).Select(f => f.Amount.Currency))
                .FirstOrDefault() ?? "EUR";
        }

        public string Currency { get; }
        public IEnumerable<Guid> AssetIds => _dims.Keys;
        public string AssetType(Guid id) => _dims.TryGetValue(id, out var d) ? d.Type : "unknown";
        public string? Department(Guid id) => _dims.TryGetValue(id, out var d) ? d.Dept : null;
        public string? BusinessUnit(Guid id) => _dims.TryGetValue(id, out var d) ? d.Bu : null;
        public IReadOnlyList<EconomicFigure> CostFigures(Guid id) => _costs[id].Select(c => c.Cost).ToList();
        public IReadOnlyList<EconomicFigure> ValueFigures(Guid id) => _values.TryGetValue(id, out var v) ? v : [];
    }
}
