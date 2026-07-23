using System.Text.Json;
using ControlTower.Platform.Events;
using ControlTower.Platform.Tenancy;

namespace ControlTower.Modules.Audit;

public sealed class LegalHoldException(string message) : Exception(message);

/// <summary>The retention classes fixed by Stage 5 section 9.</summary>
public enum RetentionDataClass
{
    All,
    InventoryObservations,
    UsageCostObservations,
    DomainEvents,
    PrivilegedReadRecords,
    FrozenSnapshots,
    PersonalKeyMap,
}

public sealed record LegalHoldScope(RetentionDataClass DataClass, string? ResourceReference = null);

public sealed record RetentionSubject(RetentionDataClass DataClass, string? ResourceReference = null);

/// <summary>A reason-bound hold marker. Release progresses state; the marker is never deleted (ADR-021).</summary>
public sealed class LegalHold
{
    public LegalHold(Guid id, TenantId tenant, LegalHoldScope scope, string reason, string placedBy, DateTimeOffset placedAt)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new LegalHoldException("A legal-hold reason is required.");
        if (string.IsNullOrWhiteSpace(placedBy)) throw new LegalHoldException("An authorised operator is required.");
        if (scope is null) throw new LegalHoldException("A legal-hold scope is required.");
        if (scope.ResourceReference is not null && string.IsNullOrWhiteSpace(scope.ResourceReference))
            throw new LegalHoldException("A resource reference cannot be blank.");
        Id = id;
        Tenant = tenant;
        Scope = scope with { ResourceReference = scope.ResourceReference?.Trim() };
        Reason = reason.Trim();
        PlacedBy = placedBy.Trim();
        PlacedAt = placedAt;
    }

    public Guid Id { get; }
    public TenantId Tenant { get; }
    public LegalHoldScope Scope { get; }
    public string Reason { get; }
    public string PlacedBy { get; }
    public DateTimeOffset PlacedAt { get; }
    public DateTimeOffset? ReleasedAt { get; private set; }
    public string? ReleasedBy { get; private set; }
    public string? ReleaseReason { get; private set; }
    public string? ApprovalReference { get; private set; }
    public bool IsActive => ReleasedAt is null;

    public void Release(string releasedBy, string reason, string approvalReference, DateTimeOffset releasedAt)
    {
        if (!IsActive) throw new LegalHoldException("The legal hold has already been released.");
        if (string.IsNullOrWhiteSpace(releasedBy)) throw new LegalHoldException("An authorised operator is required.");
        if (string.IsNullOrWhiteSpace(reason)) throw new LegalHoldException("A release reason is required.");
        if (string.IsNullOrWhiteSpace(approvalReference)) throw new LegalHoldException("An approval reference is required to release a legal hold.");
        ReleasedAt = releasedAt;
        ReleasedBy = releasedBy.Trim();
        ReleaseReason = reason.Trim();
        ApprovalReference = approvalReference.Trim();
    }

    public bool Protects(RetentionSubject subject)
    {
        if (!IsActive) return false;
        if (Scope.DataClass is not RetentionDataClass.All && Scope.DataClass != subject.DataClass) return false;
        return Scope.ResourceReference is null ||
            string.Equals(Scope.ResourceReference, subject.ResourceReference, StringComparison.Ordinal);
    }
}

public sealed record LegalHoldView(
    Guid Id,
    RetentionDataClass DataClass,
    string? ResourceReference,
    string Reason,
    string PlacedBy,
    DateTimeOffset PlacedAt,
    bool IsActive,
    DateTimeOffset? ReleasedAt,
    string? ReleasedBy,
    string? ReleaseReason,
    string? ApprovalReference);

[DomainEventContract("LegalHoldPlaced", EventPrivilege.Privileged)]
public sealed record LegalHoldPlaced : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
    public required Guid HoldId { get; init; }
    public required string DataClass { get; init; }
    public string? ResourceReference { get; init; }
    public required string Reason { get; init; }
    public required string PlacedBy { get; init; }
}

[DomainEventContract("LegalHoldReleased", EventPrivilege.Privileged)]
public sealed record LegalHoldReleased : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
    public required Guid HoldId { get; init; }
    public required string ReleasedBy { get; init; }
    public required string Reason { get; init; }
    public required string ApprovalReference { get; init; }
}

public interface ILegalHoldStore
{
    Task SaveAsync(LegalHold hold, CancellationToken ct = default);
    Task<LegalHold?> GetAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<LegalHold>> ListAsync(CancellationToken ct = default);
}

/// <summary>C9 legal-hold lifecycle and the mandatory protection decision consumed by retention.</summary>
public sealed class LegalHoldService(ILegalHoldStore store, IEventStore events, ITenantContextAccessor tenants)
{
    public async Task<Guid> PlaceAsync(LegalHoldScope scope, string reason, string placedBy, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var hold = new LegalHold(Guid.NewGuid(), tenants.Current, scope, reason, placedBy, now);
        await store.SaveAsync(hold, ct);
        await AppendAsync(new LegalHoldPlaced
        {
            HoldId = hold.Id,
            DataClass = hold.Scope.DataClass.ToString(),
            ResourceReference = hold.Scope.ResourceReference,
            Reason = hold.Reason,
            PlacedBy = hold.PlacedBy,
            OccurredAt = now,
        }, ct);
        return hold.Id;
    }

    public async Task ReleaseAsync(
        Guid holdId,
        string releasedBy,
        string reason,
        string approvalReference,
        CancellationToken ct = default)
    {
        var hold = await store.GetAsync(holdId, ct) ?? throw new LegalHoldException("Legal hold not found in this tenant.");
        var now = DateTimeOffset.UtcNow;
        hold.Release(releasedBy, reason, approvalReference, now);
        await store.SaveAsync(hold, ct);
        await AppendAsync(new LegalHoldReleased
        {
            HoldId = hold.Id,
            ReleasedBy = hold.ReleasedBy!,
            Reason = hold.ReleaseReason!,
            ApprovalReference = hold.ApprovalReference!,
            OccurredAt = now,
        }, ct);
    }

    public async Task<bool> IsProtectedAsync(RetentionSubject subject, CancellationToken ct = default) =>
        (await store.ListAsync(ct)).Any(x => x.Protects(subject));

    public async Task<IReadOnlyList<LegalHoldView>> ListAsync(CancellationToken ct = default) =>
        (await store.ListAsync(ct)).Select(ToView).ToList();

    private async Task AppendAsync(IDomainEvent domainEvent, CancellationToken ct) =>
        await events.AppendAsync(domainEvent, JsonSerializer.SerializeToUtf8Bytes(domainEvent, domainEvent.GetType()), ct);

    private static LegalHoldView ToView(LegalHold hold) =>
        new(hold.Id, hold.Scope.DataClass, hold.Scope.ResourceReference, hold.Reason, hold.PlacedBy,
            hold.PlacedAt, hold.IsActive, hold.ReleasedAt, hold.ReleasedBy, hold.ReleaseReason, hold.ApprovalReference);
}
