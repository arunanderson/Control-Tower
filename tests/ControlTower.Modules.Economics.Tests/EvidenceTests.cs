using System;
using ControlTower.Modules.Economics.Domain;
using Xunit;

namespace ControlTower.Modules.Economics.Tests;

public class EvidenceTests
{
    [Fact]
    public void Evidence_requires_a_source()
    {
        Assert.Throws<EconomicsException>(() =>
            new Evidence("", EvidenceClass.Measured, "method", DateTimeOffset.UtcNow, ValidationState.SystemObserved));
    }

    [Fact]
    public void Evidence_requires_a_methodology()
    {
        Assert.Throws<EconomicsException>(() =>
            new Evidence("source", EvidenceClass.Measured, "  ", DateTimeOffset.UtcNow, ValidationState.SystemObserved));
    }

    [Fact]
    public void An_economic_figure_cannot_exist_without_evidence()
    {
        Assert.Throws<EconomicsException>(() => new EconomicFigure(new Money(1m, "EUR"), null!));
    }

    [Fact]
    public void Money_requires_a_currency()
    {
        Assert.Throws<EconomicsException>(() => new Money(1m, ""));
    }

    [Fact]
    public void A_valid_figure_exposes_its_full_evidence()
    {
        var figure = new EconomicFigure(
            new Money(100m, "EUR"),
            new Evidence("Azure Cost Management", EvidenceClass.Measured, "billing meter", DateTimeOffset.UtcNow, ValidationState.SystemObserved));

        Assert.Equal(EvidenceClass.Measured, figure.Confidence);
        Assert.Equal("Azure Cost Management", figure.Evidence.Source);
        Assert.Equal("billing meter", figure.Evidence.Methodology);
    }
}
