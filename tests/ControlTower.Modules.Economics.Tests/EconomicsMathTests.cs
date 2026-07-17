using System;
using ControlTower.Modules.Economics.Domain;
using Xunit;

namespace ControlTower.Modules.Economics.Tests;

public class EconomicsMathTests
{
    private static EconomicFigure Fig(decimal amount, EvidenceClass evidenceClass) =>
        new(new Money(amount, "EUR"), new Evidence("s", evidenceClass, "m", DateTimeOffset.UtcNow, ValidationState.SystemObserved));

    [Fact]
    public void Weakest_link_returns_the_weakest_class()
    {
        Assert.Equal(EvidenceClass.Inferred,
            EconomicsMath.WeakestLink([EvidenceClass.FinanciallyValidated, EvidenceClass.Inferred, EvidenceClass.Measured]));
    }

    [Fact]
    public void Roi_is_net_over_cost()
    {
        Assert.Equal(1.0m, EconomicsMath.Roi(200m, 100m));
        Assert.Equal(0m, EconomicsMath.Roi(100m, 0m));
    }

    [Fact]
    public void Single_point_roi_is_suppressed_when_more_than_25pct_of_value_is_soft()
    {
        var costs = new[] { Fig(100m, EvidenceClass.Measured) };
        var values = new[] { Fig(50m, EvidenceClass.SelfReported), Fig(50m, EvidenceClass.FinanciallyValidated) }; // 50% soft

        var result = EconomicsMath.Compute(costs, values);

        Assert.True(result.SinglePointSuppressed);
        Assert.Null(result.SinglePointRoi);
        Assert.Equal(EconomicsMath.Roi(50m, 100m), result.ValidatedOnlyRoi); // only the validated 50
        Assert.Equal(EvidenceClass.SelfReported, result.CompositeValueClass); // weakest-material-link
    }

    [Fact]
    public void Single_point_roi_is_shown_when_value_is_mostly_hard()
    {
        var costs = new[] { Fig(100m, EvidenceClass.Measured) };
        var values = new[] { Fig(90m, EvidenceClass.FinanciallyValidated), Fig(10m, EvidenceClass.SelfReported) }; // 10% soft

        var result = EconomicsMath.Compute(costs, values);

        Assert.False(result.SinglePointSuppressed);
        Assert.NotNull(result.SinglePointRoi);
    }

    [Fact]
    public void Payback_uses_the_trailing_12_month_basis()
    {
        var result = EconomicsMath.Compute([Fig(1200m, EvidenceClass.Measured)], [Fig(1200m, EvidenceClass.Measured)]);
        Assert.Equal(12, result.PaybackMonths);
    }
}
