namespace ControlTower.Modules.Economics.Domain;

/// <summary>The six evidence classes — the permanent platform standard (ADR-025). Ordered weakest→strongest for weakest-material-link composition.</summary>
public enum EvidenceClass
{
    Unknown = 0,
    Inferred = 1,
    SelfReported = 2,
    Estimated = 3,
    Measured = 4,
    FinanciallyValidated = 5,
}

/// <summary>The validation ladder a figure climbs through the Finance workflow (forward-only by default).</summary>
public enum ValidationState
{
    Estimated = 0,
    SystemObserved = 1,
    BusinessValidated = 2,
    FinanceVerified = 3,
}

/// <summary>
/// The provenance every economic figure must carry (ADR-024/025; Stage 10 §1). Constructed only with
/// all fields — there is no way to make evidence without a source, class, methodology, as-of, and
/// validation state. This is the structural spine of "no number without evidence".
/// </summary>
public sealed class Evidence
{
    public Evidence(string source, EvidenceClass evidenceClass, string methodology, DateTimeOffset asOf, ValidationState validationState)
    {
        if (string.IsNullOrWhiteSpace(source)) throw new EconomicsException("Evidence source is required.");
        if (string.IsNullOrWhiteSpace(methodology)) throw new EconomicsException("Methodology reference is required.");
        Source = source;
        Class = evidenceClass;
        Methodology = methodology;
        AsOf = asOf;
        ValidationState = validationState;
    }

    public string Source { get; }
    public EvidenceClass Class { get; }
    public string Methodology { get; }
    public DateTimeOffset AsOf { get; }
    public ValidationState ValidationState { get; }

    public Evidence With(EvidenceClass? evidenceClass = null, ValidationState? validationState = null, DateTimeOffset? asOf = null, string? methodology = null, string? source = null) =>
        new(source ?? Source, evidenceClass ?? Class, methodology ?? Methodology, asOf ?? AsOf, validationState ?? ValidationState);
}

/// <summary>A monetary amount that cannot exist without evidence (ADR-025). The rendering layer can only ever receive one of these.</summary>
public sealed class EconomicFigure
{
    public EconomicFigure(Money amount, Evidence evidence)
    {
        Amount = amount;
        Evidence = evidence ?? throw new EconomicsException("An economic figure cannot exist without evidence.");
    }

    public Money Amount { get; }
    public Evidence Evidence { get; }
    public EvidenceClass Confidence => Evidence.Class;
}
