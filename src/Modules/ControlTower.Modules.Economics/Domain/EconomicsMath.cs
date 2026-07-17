namespace ControlTower.Modules.Economics.Domain;

/// <summary>The result of an ROI computation, honesty rules applied (Stage 10 §5).</summary>
public sealed record RoiResult(
    decimal TotalCost,
    decimal TotalValue,
    decimal NetBenefit,
    decimal? SinglePointRoi,
    decimal ValidatedOnlyRoi,
    decimal LowRoi,
    decimal HighRoi,
    int? PaybackMonths,
    bool SinglePointSuppressed,
    EvidenceClass CompositeValueClass,
    IReadOnlyDictionary<EvidenceClass, int> ConfidenceMix);

/// <summary>
/// Pure economics calculations. ROI presentation obeys Stage 10 §5: no single-point ROI when more than
/// 25% of the value side is Self-reported/Inferred (a range + confidence mix is shown instead), and a
/// validated-only ROI is always available alongside. Composite confidence is weakest-material-link.
/// </summary>
public static class EconomicsMath
{
    private const decimal SoftLabelThreshold = 0.25m;

    public static EvidenceClass WeakestLink(IEnumerable<EvidenceClass> classes)
    {
        var list = classes.ToList();
        return list.Count == 0 ? EvidenceClass.Unknown : list.Min();
    }

    public static IReadOnlyDictionary<EvidenceClass, int> ConfidenceMix(IEnumerable<EvidenceClass> classes) =>
        classes.GroupBy(c => c).ToDictionary(g => g.Key, g => g.Count());

    public static decimal Roi(decimal value, decimal cost) => cost == 0m ? 0m : (value - cost) / cost;

    public static RoiResult Compute(IReadOnlyList<EconomicFigure> costs, IReadOnlyList<EconomicFigure> values)
    {
        var totalCost = costs.Sum(c => c.Amount.Amount);
        var totalValue = values.Sum(v => v.Amount.Amount);
        var net = totalValue - totalCost;

        var softValue = values
            .Where(v => v.Confidence is EvidenceClass.SelfReported or EvidenceClass.Inferred)
            .Sum(v => v.Amount.Amount);
        var softShare = totalValue > 0m ? softValue / totalValue : 0m;
        var suppressed = softShare > SoftLabelThreshold;

        var validatedValue = values
            .Where(v => v.Confidence == EvidenceClass.FinanciallyValidated)
            .Sum(v => v.Amount.Amount);

        var singlePoint = Roi(totalValue, totalCost);
        var validatedOnly = Roi(validatedValue, totalCost);

        // Trailing-12-month payback: months to recover cost from the annualised benefit.
        int? payback = totalCost == 0m
            ? 0
            : totalValue > 0m ? (int)Math.Ceiling(totalCost / (totalValue / 12m)) : null;

        return new RoiResult(
            totalCost,
            totalValue,
            net,
            suppressed ? null : singlePoint,
            validatedOnly,
            LowRoi: validatedOnly,
            HighRoi: singlePoint,
            payback,
            suppressed,
            WeakestLink(values.Select(v => v.Confidence)),
            ConfidenceMix(values.Select(v => v.Confidence)));
    }
}
