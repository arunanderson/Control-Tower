using ControlTower.Platform.Events;

namespace ControlTower.Modules.Ledger.Domain;

/// <summary>Base for C1 domain events (Stage 4 §5). Events are the audit trail (ADR-015).</summary>
public abstract record LedgerEvent : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
    public LedgerAssetId AssetId { get; init; }
}

public sealed record AssetDiscovered : LedgerEvent
{
    public required string DisplayName { get; init; }
    public required AssetType Type { get; init; }
}

public sealed record AssetTriaged : LedgerEvent;

public sealed record AssetRegistered : LedgerEvent
{
    public required string BusinessPurpose { get; init; }
}

public sealed record AssetRejected : LedgerEvent
{
    public required string Reason { get; init; }
}

public sealed record AssetRetired : LedgerEvent;

public sealed record LifecycleStateChanged : LedgerEvent
{
    public required OperationalLifecycleState From { get; init; }
    public required OperationalLifecycleState To { get; init; }
}

public sealed record OwnershipAssigned : LedgerEvent
{
    public required PersonRef Person { get; init; }
    public required OwnershipRole Role { get; init; }
}

public sealed record OwnershipLapsed : LedgerEvent
{
    public required PersonRef Person { get; init; }
}

public sealed record OwnershipReassigned : LedgerEvent
{
    public required PersonRef From { get; init; }
    public required PersonRef To { get; init; }
}

public sealed record ResolutionLinkAdded : LedgerEvent
{
    public required Guid LinkId { get; init; }
    public required MatchConfidence LinkConfidence { get; init; }
}

public sealed record ResolutionLinkRemoved : LedgerEvent
{
    public required Guid LinkId { get; init; }
}

public sealed record MatchConfidenceChanged : LedgerEvent
{
    public required MatchConfidence From { get; init; }
    public required MatchConfidence To { get; init; }
}
