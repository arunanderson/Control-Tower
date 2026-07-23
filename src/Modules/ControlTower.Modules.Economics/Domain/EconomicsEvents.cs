using ControlTower.Platform.Events;

namespace ControlTower.Modules.Economics.Domain;

/// <summary>Base for C3 domain events (Stage 4 §5). Appended to the immutable stream — nothing is overwritten.</summary>
public abstract record EconomicsEvent : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}

[DomainEventContract("CostFactIngested", EventPrivilege.Standard)]
public sealed record CostObserved : EconomicsEvent
{
    public required Guid AssetId { get; init; }
    public required decimal Amount { get; init; }
    public required string Currency { get; init; }
    public required string EvidenceClass { get; init; }
}

[DomainEventContract("ValueDeclared", EventPrivilege.Standard)]
public sealed record ValueDeclared : EconomicsEvent
{
    public required Guid AssetId { get; init; }
    public required decimal Amount { get; init; }
    public required string Currency { get; init; }
    public required string EvidenceClass { get; init; }
    public required string ValidationState { get; init; }
}

[DomainEventContract("ValueRevised", EventPrivilege.Standard)]
public sealed record ValueRevisedEvent : EconomicsEvent
{
    public required Guid AssetId { get; init; }
    public required string ToValidationState { get; init; }
    public required string Reason { get; init; }
}

[DomainEventContract("ReportingPeriodFrozen", EventPrivilege.Standard)]
public sealed record ReportingPeriodFrozen : EconomicsEvent
{
    public required Guid PeriodId { get; init; }
    public required Guid SnapshotId { get; init; }
    public required int Version { get; init; }
    public required string InputBasisHash { get; init; }
    public required string SignedBy { get; init; }
}

[DomainEventContract("ReportingPeriodRestated", EventPrivilege.Standard)]
public sealed record ReportingPeriodRestated : EconomicsEvent
{
    public required Guid PeriodId { get; init; }
    public required Guid SnapshotId { get; init; }
    public required int Version { get; init; }
    public required Guid SupersedesSnapshotId { get; init; }
    public required string InputBasisHash { get; init; }
    public required string SignedBy { get; init; }
    public required string Reason { get; init; }
}
