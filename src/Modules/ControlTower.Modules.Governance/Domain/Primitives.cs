namespace ControlTower.Modules.Governance.Domain;

public sealed class GovernanceException(string message) : Exception(message);

public readonly record struct GovernanceCaseId(Guid Value)
{
    public static GovernanceCaseId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}

/// <summary>Typed governance cases (Stage 11 §A2). Each carries decisions that trigger — never duplicate — Ledger transitions.</summary>
public enum CaseType
{
    Intake,
    Approval,
    Review,
    Recertification,
    Exception,
    Retirement,
    ReuseDecision,
}

public enum CaseStatus
{
    Open,
    AwaitingReview,
    Approved,
    Rejected,
    Waived,
    Expired,
    Retired,
    Withdrawn,
}

public enum RiskTier
{
    Low,
    Medium,
    High,
    Exceptional,
}

public enum ReviewerRole
{
    Business,
    Technical,
    Security,
    Privacy,
    Finance,
    Governance,
}

/// <summary>The reuse-vs-build decision recorded at governance intake (Flag-Never-Block; BuildNew is always allowed).</summary>
public enum ReuseAction
{
    Reuse,
    Extend,
    Compose,
    Replace,
    Consolidate,
    Retire,
    BuildNew,
}

public enum DebtType
{
    Ownerless,
    LapsedOwner,
    Unregistered,
    StalePurpose,
}
