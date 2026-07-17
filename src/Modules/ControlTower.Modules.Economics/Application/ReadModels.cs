using ControlTower.Modules.Economics.Domain;

namespace ControlTower.Modules.Economics.Application;

/// <summary>
/// A monetary value on a read model. It cannot be constructed without the mandated evidence fields
/// (ADR-025; Stage 10 §1): amount+currency, evidence class, source, methodology, as-of, validation
/// state. This is the "no number without evidence" guarantee at the presentation boundary.
/// </summary>
public sealed record EconomicAmount
{
    public required decimal Amount { get; init; }
    public required string Currency { get; init; }
    public required string EvidenceClass { get; init; }
    public required string Source { get; init; }
    public required string Methodology { get; init; }
    public required DateTimeOffset AsOf { get; init; }
    public required string ValidationState { get; init; }

    public static EconomicAmount From(EconomicFigure figure) => new()
    {
        Amount = figure.Amount.Amount,
        Currency = figure.Amount.Currency,
        EvidenceClass = figure.Confidence.ToString(),
        Source = figure.Evidence.Source,
        Methodology = figure.Evidence.Methodology,
        AsOf = figure.Evidence.AsOf,
        ValidationState = figure.Evidence.ValidationState.ToString(),
    };

    /// <summary>A derived/aggregated amount: class = weakest-material-link, validation = lowest of contributors.</summary>
    public static EconomicAmount Composite(decimal amount, string currency, IReadOnlyList<EconomicFigure> contributing, string source, string methodology, DateTimeOffset asOf)
    {
        var evidenceClass = EconomicsMath.WeakestLink(contributing.Select(f => f.Confidence));
        var validation = contributing.Count == 0
            ? Domain.ValidationState.Estimated
            : (Domain.ValidationState)contributing.Min(f => (int)f.Evidence.ValidationState);
        return new EconomicAmount
        {
            Amount = amount,
            Currency = currency,
            EvidenceClass = evidenceClass.ToString(),
            Source = source,
            Methodology = methodology,
            AsOf = asOf,
            ValidationState = validation.ToString(),
        };
    }
}

/// <summary>Per-asset economics (the base projection all ROI views are built from).</summary>
public sealed record AssetEconomicsView
{
    public required Guid AssetId { get; init; }
    public required string AssetType { get; init; }
    public string? Department { get; init; }
    public string? BusinessUnit { get; init; }
    public required EconomicAmount Cost { get; init; }
    public required EconomicAmount Value { get; init; }
    public required EconomicAmount NetBenefit { get; init; }
    public required RoiView Roi { get; init; }
    public required DateTimeOffset AsOf { get; init; }
}

/// <summary>An ROI view for any scope — asset, agent portfolio, department, business unit, or portfolio.</summary>
public sealed record RoiView
{
    public required string Scope { get; init; }
    public required EconomicAmount Cost { get; init; }
    public required EconomicAmount Value { get; init; }
    public required EconomicAmount NetBenefit { get; init; }
    public decimal? SinglePointRoi { get; init; }
    public required decimal ValidatedOnlyRoi { get; init; }
    public required decimal LowRoi { get; init; }
    public required decimal HighRoi { get; init; }
    public int? PaybackMonths { get; init; }
    public required bool SinglePointSuppressed { get; init; }
    public required string CompositeEvidenceClass { get; init; }
    public required IReadOnlyDictionary<string, int> ConfidenceMix { get; init; }
    public required DateTimeOffset AsOf { get; init; }
}

/// <summary>The executive economics dashboard (Stage 10 §9 KPIs).</summary>
public sealed record ExecutiveEconomicsView
{
    public required EconomicAmount TotalSpend { get; init; }
    public required EconomicAmount DeclaredValue { get; init; }
    public required EconomicAmount ValidatedValue { get; init; }
    public required decimal ValidatedToDeclaredRatio { get; init; }
    public required EconomicAmount UnattributedCost { get; init; }
    public required decimal UnattributedPercent { get; init; }
    public required IReadOnlyDictionary<string, int> ConfidenceMix { get; init; }
    public required DateTimeOffset AsOf { get; init; }
}
