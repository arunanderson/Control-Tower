using System.Text.Json;
using ControlTower.Platform.Events;
using ControlTower.Platform.Identity;
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
    public LegalHold(Guid id, TenantId tenant, LegalHoldScope scope, string reason, AuditActor placedBy, DateTimeOffset placedAt)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new LegalHoldException("A legal-hold reason is required.");
        if (!placedBy.IsValid) throw new LegalHoldException("An authorised operator is required.");
        if (scope is null) throw new LegalHoldException("A legal-hold scope is required.");
        if (scope.ResourceReference is not null && string.IsNullOrWhiteSpace(scope.ResourceReference))
            throw new LegalHoldException("A resource reference cannot be blank.");
        Id = id;
        Tenant = tenant;
        Scope = scope with { ResourceReference = scope.ResourceReference?.Trim() };
        Reason = reason.Trim();
        PlacedBy = placedBy;
        PlacedAt = placedAt;
    }

    public Guid Id { get; }
    public TenantId Tenant { get; }
    public LegalHoldScope Scope { get; }
    public string Reason { get; }
    public AuditActor PlacedBy { get; }
    public DateTimeOffset PlacedAt { get; }
    public DateTimeOffset? ReleasedAt { get; private set; }
    public AuditActor? ReleasedBy { get; private set; }
    public string? ReleaseReason { get; private set; }
    public string? ApprovalReference { get; private set; }
    public bool IsActive => ReleasedAt is null;

    public void Release(AuditActor releasedBy, string reason, string approvalReference, DateTimeOffset releasedAt)
    {
        if (!IsActive) throw new LegalHoldException("The legal hold has already been released.");
        if (!releasedBy.IsValid) throw new LegalHoldException("An authorised operator is required.");
        if (string.IsNullOrWhiteSpace(reason)) throw new LegalHoldException("A release reason is required.");
        if (string.IsNullOrWhiteSpace(approvalReference)) throw new LegalHoldException("An approval reference is required to release a legal hold.");
        ReleasedAt = releasedAt;
        ReleasedBy = releasedBy;
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
    public required AuditActor PlacedBy { get; init; }
}

[DomainEventContract("LegalHoldReleased", EventPrivilege.Privileged)]
public sealed record LegalHoldReleased : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
    public required Guid HoldId { get; init; }
    public required AuditActor ReleasedBy { get; init; }
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
public sealed class LegalHoldService(
    ILegalHoldStore store,
    IEventStore events,
    ITenantContextAccessor tenants,
    TimeProvider clock)
{
    public async Task<Guid> PlaceAsync(
        LegalHoldScope scope,
        string reason,
        AuditActor placedBy,
        CancellationToken ct = default)
    {
        var now = EventEnvelopeCanonicalizer.NormalizeTimestamp(
            clock.GetUtcNow());
        var hold = new LegalHold(Guid.NewGuid(), tenants.Current, scope, reason, placedBy, now);
        var @event = new LegalHoldPlaced
        {
            HoldId = hold.Id,
            DataClass = hold.Scope.DataClass.ToString(),
            ResourceReference = hold.Scope.ResourceReference,
            Reason = hold.Reason,
            PlacedBy = hold.PlacedBy,
            OccurredAt = now,
        };
        var prepared = PrepareEvent(
            @event,
            placedBy,
            reason,
            correlation: null);

        await store.SaveAsync(hold, ct);
        await AppendAsync(prepared, ct);
        return hold.Id;
    }

    public async Task ReleaseAsync(
        Guid holdId,
        AuditActor releasedBy,
        string reason,
        string approvalReference,
        CancellationToken ct = default)
    {
        var hold = await store.GetAsync(holdId, ct) ?? throw new LegalHoldException("Legal hold not found in this tenant.");
        var now = EventEnvelopeCanonicalizer.NormalizeTimestamp(
            clock.GetUtcNow());
        PreparedEvent prepared;
        try
        {
            var approval = new EventReference(
                "approval",
                approvalReference);
            var @event = new LegalHoldReleased
            {
                HoldId = hold.Id,
                ReleasedBy = releasedBy,
                Reason = reason,
                ApprovalReference = approvalReference,
                OccurredAt = now,
            };
            prepared = PrepareEvent(
                @event,
                releasedBy,
                reason,
                approval);
        }
        catch (ArgumentException)
        {
            throw new LegalHoldException(
                "The legal-hold release evidence context is invalid.");
        }

        hold.Release(releasedBy, reason, approvalReference, now);
        await store.SaveAsync(hold, ct);
        await AppendAsync(prepared, ct);
    }

    public async Task<bool> IsProtectedAsync(RetentionSubject subject, CancellationToken ct = default) =>
        (await store.ListAsync(ct)).Any(x => x.Protects(subject));

    public async Task<IReadOnlyList<LegalHoldView>> ListAsync(CancellationToken ct = default) =>
        (await store.ListAsync(ct)).Select(ToView).ToList();

    private static PreparedEvent PrepareEvent(
        IDomainEvent domainEvent,
        AuditActor actor,
        string? reason,
        EventReference? correlation)
    {
        try
        {
            var metadata = new EventAppendMetadata(
                EventReference.For(
                    "legal-hold",
                    domainEvent switch
                    {
                        LegalHoldPlaced placed => placed.HoldId,
                        LegalHoldReleased released =>
                            released.HoldId,
                        _ => throw new LegalHoldException(
                            "Unknown legal-hold event."),
                    }),
                actor,
                reason,
                correlation);
            return new PreparedEvent(
                domainEvent,
                metadata,
                JsonSerializer.SerializeToUtf8Bytes(
                    domainEvent,
                    domainEvent.GetType()));
        }
        catch (ArgumentException)
        {
            throw new LegalHoldException(
                "The legal-hold evidence context is invalid.");
        }
    }

    private async Task AppendAsync(
        PreparedEvent prepared,
        CancellationToken ct) =>
        await events.AppendAsync(
            prepared.Event,
            prepared.Metadata,
            prepared.Payload,
            ct);

    private sealed record PreparedEvent(
        IDomainEvent Event,
        EventAppendMetadata Metadata,
        byte[] Payload);

    private static LegalHoldView ToView(LegalHold hold) =>
        new(hold.Id, hold.Scope.DataClass, hold.Scope.ResourceReference, hold.Reason, hold.PlacedBy.ToString(),
            hold.PlacedAt, hold.IsActive, hold.ReleasedAt, hold.ReleasedBy?.ToString(), hold.ReleaseReason, hold.ApprovalReference);
}
