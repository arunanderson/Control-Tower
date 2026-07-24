using ControlTower.Platform.Events;
using ControlTower.Platform.Identity;

namespace ControlTower.Modules.Governance.Domain;

/// <summary>Base for C2 domain events (Stage 4 §5). Every governance action is an immutable audit event.</summary>
public abstract record GovernanceEvent : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}

[DomainEventContract("CaseOpened", EventPrivilege.Standard)]
public sealed record CaseOpened : GovernanceEvent
{
    public required GovernanceCaseId CaseId { get; init; }
    public required Guid AssetId { get; init; }
    public required CaseType Type { get; init; }
    public required RiskTier Tier { get; init; }
}

[DomainEventContract("DecisionRecorded", EventPrivilege.Standard)]
public sealed record DecisionRecorded : GovernanceEvent
{
    public required GovernanceCaseId CaseId { get; init; }
    public required ReviewerRole Role { get; init; }
    public required bool Approved { get; init; }
    public required AuditActor Actor { get; init; }
    public required string Reason { get; init; }
}

[DomainEventContract("CaseApproved", EventPrivilege.Standard)]
public sealed record CaseApproved : GovernanceEvent
{
    public required GovernanceCaseId CaseId { get; init; }
}

[DomainEventContract("CaseRejected", EventPrivilege.Standard)]
public sealed record CaseRejected : GovernanceEvent
{
    public required GovernanceCaseId CaseId { get; init; }
    public required string Reason { get; init; }
}

[DomainEventContract("WaiverGranted", EventPrivilege.Standard)]
public sealed record WaiverGranted : GovernanceEvent
{
    public required GovernanceCaseId CaseId { get; init; }
    public required DateTimeOffset ExpiresAt { get; init; }
    public required string Reason { get; init; }
}

[DomainEventContract("CaseExpired", EventPrivilege.Standard)]
public sealed record CaseExpired : GovernanceEvent
{
    public required GovernanceCaseId CaseId { get; init; }
    public required string Reason { get; init; }
}

[DomainEventContract("RecertificationCompleted", EventPrivilege.Standard)]
public sealed record RecertificationCompleted : GovernanceEvent
{
    public required GovernanceCaseId CaseId { get; init; }
    public required DateTimeOffset NextDueAt { get; init; }
}

[DomainEventContract("RetirementRequested", EventPrivilege.Standard)]
public sealed record RetirementRequested : GovernanceEvent
{
    public required GovernanceCaseId CaseId { get; init; }
    public required Guid AssetId { get; init; }
    public required string Reason { get; init; }
}

[DomainEventContract("ReuseDecisionRecorded", EventPrivilege.Standard)]
public sealed record ReuseDecisionRecorded : GovernanceEvent
{
    public required GovernanceCaseId CaseId { get; init; }
    public required Guid AssetId { get; init; }
    public required string Action { get; init; }
    public required string Justification { get; init; }
    public required AuditActor Actor { get; init; }
}

[DomainEventContract("NotificationIntentRaised", EventPrivilege.Standard)]
public sealed record NotificationIntentRaised : GovernanceEvent
{
    public required GovernanceCaseId CaseId { get; init; }
    public required string Recipient { get; init; }
    public required string Reason { get; init; }
    public required string DeepLink { get; init; }
}

/// <summary>An intent to invoke a native control (ADR-002). C2 records the request only — it never enforces.</summary>
[DomainEventContract("NativeControlRequested", EventPrivilege.Standard)]
public sealed record NativeControlRequested : GovernanceEvent
{
    public required GovernanceCaseId CaseId { get; init; }
    public required string Control { get; init; }
    public required string Target { get; init; }
}

[DomainEventContract("GovernanceDebtRaised", EventPrivilege.Standard)]
public sealed record GovernanceDebtRaised : GovernanceEvent
{
    public required Guid DebtId { get; init; }
    public required Guid AssetId { get; init; }
    public required string DebtType { get; init; }
}
