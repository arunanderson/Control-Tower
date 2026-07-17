using ControlTower.Platform.Events;

namespace ControlTower.Modules.Governance.Domain;

/// <summary>Base for C2 domain events (Stage 4 §5). Every governance action is an immutable audit event.</summary>
public abstract record GovernanceEvent : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record CaseOpened : GovernanceEvent
{
    public required GovernanceCaseId CaseId { get; init; }
    public required Guid AssetId { get; init; }
    public required CaseType Type { get; init; }
    public required RiskTier Tier { get; init; }
}

public sealed record DecisionRecorded : GovernanceEvent
{
    public required GovernanceCaseId CaseId { get; init; }
    public required ReviewerRole Role { get; init; }
    public required bool Approved { get; init; }
    public required string Actor { get; init; }
    public required string Reason { get; init; }
}

public sealed record CaseApproved : GovernanceEvent
{
    public required GovernanceCaseId CaseId { get; init; }
}

public sealed record CaseRejected : GovernanceEvent
{
    public required GovernanceCaseId CaseId { get; init; }
    public required string Reason { get; init; }
}

public sealed record WaiverGranted : GovernanceEvent
{
    public required GovernanceCaseId CaseId { get; init; }
    public required DateTimeOffset ExpiresAt { get; init; }
    public required string Reason { get; init; }
}

public sealed record CaseExpired : GovernanceEvent
{
    public required GovernanceCaseId CaseId { get; init; }
    public required string Reason { get; init; }
}

public sealed record RecertificationCompleted : GovernanceEvent
{
    public required GovernanceCaseId CaseId { get; init; }
    public required DateTimeOffset NextDueAt { get; init; }
}

public sealed record RetirementRequested : GovernanceEvent
{
    public required GovernanceCaseId CaseId { get; init; }
    public required Guid AssetId { get; init; }
    public required string Reason { get; init; }
}

public sealed record ReuseDecisionRecorded : GovernanceEvent
{
    public required GovernanceCaseId CaseId { get; init; }
    public required Guid AssetId { get; init; }
    public required string Action { get; init; }
    public required string Justification { get; init; }
    public required string Actor { get; init; }
}

public sealed record NotificationIntentRaised : GovernanceEvent
{
    public required GovernanceCaseId CaseId { get; init; }
    public required string Recipient { get; init; }
    public required string Reason { get; init; }
    public required string DeepLink { get; init; }
}

/// <summary>An intent to invoke a native control (ADR-002). C2 records the request only — it never enforces.</summary>
public sealed record NativeControlRequested : GovernanceEvent
{
    public required GovernanceCaseId CaseId { get; init; }
    public required string Control { get; init; }
    public required string Target { get; init; }
}

public sealed record GovernanceDebtRaised : GovernanceEvent
{
    public required Guid AssetId { get; init; }
    public required string DebtType { get; init; }
}
