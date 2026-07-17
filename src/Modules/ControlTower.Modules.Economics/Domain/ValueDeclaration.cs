using ControlTower.Platform.Tenancy;

namespace ControlTower.Modules.Economics.Domain;

public enum BenefitType
{
    TimeSaved,
    CostAvoided,
    RevenueEnabled,
    RiskReduced,
    QualityImproved,
}

/// <summary>One immutable point in a declaration's revision chain — nothing is ever overwritten (Stage 4 §2.6).</summary>
public sealed record ValueRevision(EconomicFigure Figure, string Reason, string By, DateTimeOffset At);

/// <summary>
/// A declared benefit for an asset (C3 aggregate, Stage 4 §2.6). Climbs the Finance validation ladder
/// (Estimated → SystemObserved → BusinessValidated → FinanceVerified) forward-only, each step recorded
/// as a new revision. The honest-data principle is a domain invariant here, not a UI convention.
/// </summary>
public sealed class ValueDeclaration
{
    private readonly List<ValueRevision> _revisions = [];

    public ValueDeclaration(Guid id, TenantId tenant, Guid assetId, string assetType, BenefitType benefit, EconomicFigure declared, string declaredBy)
    {
        if (string.IsNullOrWhiteSpace(assetType)) throw new EconomicsException("Asset type is required for attribution.");
        Id = id;
        Tenant = tenant;
        AssetId = assetId;
        AssetType = assetType;
        Benefit = benefit;
        DeclaredBy = declaredBy;
        _revisions.Add(new ValueRevision(declared, "initial declaration", declaredBy, DateTimeOffset.UtcNow));
    }

    public Guid Id { get; }
    public TenantId Tenant { get; }
    public Guid AssetId { get; }
    public string AssetType { get; }
    public BenefitType Benefit { get; }
    public string DeclaredBy { get; }

    public EconomicFigure Current => _revisions[^1].Figure;
    public ValidationState State => Current.Evidence.ValidationState;
    public IReadOnlyList<ValueRevision> Revisions => _revisions;

    /// <summary>Append a new revision (e.g. a Finance validation step). Forward-only; never overwrites history.</summary>
    public void Revise(EconomicFigure updated, string reason, string by)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new EconomicsException("A revision reason is required.");
        if (updated.Evidence.ValidationState < State)
            throw new EconomicsException("Validation state cannot move backward (forward-only; use a formal restatement).");
        _revisions.Add(new ValueRevision(updated, reason, by, DateTimeOffset.UtcNow));
    }
}
