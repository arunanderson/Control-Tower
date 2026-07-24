using ControlTower.Platform.Events;
using ControlTower.Platform.Identity;

namespace ControlTower.Modules.Ledger.Domain;

/// <summary>Base for C1 domain events (Stage 4 §5). Events are the audit trail (ADR-015).</summary>
public abstract record LedgerEvent : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
    public LedgerAssetId AssetId { get; init; }
}

[DomainEventContract("AssetDiscovered", EventPrivilege.Standard)]
public sealed record AssetDiscovered : LedgerEvent
{
    public required string DisplayName { get; init; }
    public required AssetType Type { get; init; }
}

[DomainEventContract("AssetTriaged", EventPrivilege.Standard)]
public sealed record AssetTriaged : LedgerEvent;

[DomainEventContract("AssetRegistered", EventPrivilege.Standard)]
public sealed record AssetRegistered : LedgerEvent
{
    public required string BusinessPurpose { get; init; }
}

[DomainEventContract("AssetRejected", EventPrivilege.Standard)]
public sealed record AssetRejected : LedgerEvent
{
    public required string Reason { get; init; }
}

[DomainEventContract("AssetRetired", EventPrivilege.Standard)]
public sealed record AssetRetired : LedgerEvent;

[DomainEventContract("LifecycleStateChanged", EventPrivilege.Standard)]
public sealed record LifecycleStateChanged : LedgerEvent
{
    public required OperationalLifecycleState From { get; init; }
    public required OperationalLifecycleState To { get; init; }
}

[DomainEventContract("OwnershipAssigned", EventPrivilege.Standard)]
public sealed record OwnershipAssigned : LedgerEvent
{
    public required PersonRef Person { get; init; }
    public required OwnershipRole Role { get; init; }
}

[DomainEventContract("OwnershipLapsed", EventPrivilege.Standard)]
public sealed record OwnershipLapsed : LedgerEvent
{
    public required PersonRef Person { get; init; }
}

[DomainEventContract("OwnershipReassigned", EventPrivilege.Standard)]
public sealed record OwnershipReassigned : LedgerEvent
{
    public required PersonRef From { get; init; }
    public required PersonRef To { get; init; }
}

[DomainEventContract("ResolutionLinkAdded", EventPrivilege.Standard)]
public sealed record ResolutionLinkAdded : LedgerEvent
{
    public required Guid LinkId { get; init; }
    public required MatchConfidence LinkConfidence { get; init; }
    public Guid? ObservationRef { get; init; }
}

/// <summary>A link no longer holds — retained with status Severed, never deleted (Stage 5 E6).</summary>
[DomainEventContract("ResolutionLinkRemoved", EventPrivilege.Standard)]
public sealed record ResolutionLinkSevered : LedgerEvent
{
    public required Guid LinkId { get; init; }
    public required AuditActor By { get; init; }
    public required string Reason { get; init; }
}

/// <summary>A link was replaced by a merge/split — retained with status Superseded, never deleted.</summary>
[DomainEventContract("ResolutionLinkSuperseded", EventPrivilege.Standard)]
public sealed record ResolutionLinkSuperseded : LedgerEvent
{
    public required Guid LinkId { get; init; }
    public required Guid BySupersedingLinkId { get; init; }
    public required AuditActor By { get; init; }
}

[DomainEventContract("MatchConfidenceChanged", EventPrivilege.Standard)]
public sealed record MatchConfidenceChanged : LedgerEvent
{
    public required MatchConfidence From { get; init; }
    public required MatchConfidence To { get; init; }
}

/// <summary>This asset (AssetId) was merged into TargetAssetId; its links are superseded there.</summary>
[DomainEventContract("AssetsMerged", EventPrivilege.Standard)]
public sealed record AssetMergedInto : LedgerEvent
{
    public required LedgerAssetId TargetAssetId { get; init; }
    public required AuditActor By { get; init; }
}

/// <summary>Links were split out of this asset (AssetId) into NewAssetId.</summary>
[DomainEventContract("AssetSplit", EventPrivilege.Standard)]
public sealed record AssetSplit : LedgerEvent
{
    public required LedgerAssetId NewAssetId { get; init; }
    public required AuditActor By { get; init; }
}

/// <summary>A manual merge queue item was opened — identifier collision or ambiguous match (Stage 5 E8).</summary>
[DomainEventContract("MergeCaseOpened", EventPrivilege.Standard)]
public sealed record MergeCaseOpened : LedgerEvent
{
    public required Guid MergeCaseId { get; init; }
    public required string Reason { get; init; }
    public required MatchConfidence Confidence { get; init; }
}

/// <summary>An operator resolved a merge case (the operator-approved, Manual, decision).</summary>
[DomainEventContract("MergeCaseDecided", EventPrivilege.Standard)]
public sealed record MergeCaseResolved : LedgerEvent
{
    public required Guid MergeCaseId { get; init; }
    public required string Outcome { get; init; }
    public required AuditActor By { get; init; }
}
