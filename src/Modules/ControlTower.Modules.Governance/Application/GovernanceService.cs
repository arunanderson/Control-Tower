using System.Text.Json;
using ControlTower.Modules.Governance.Domain;
using ControlTower.Platform.Events;
using ControlTower.Platform.Identity;
using ControlTower.Platform.Tenancy;

namespace ControlTower.Modules.Governance.Application;

/// <summary>
/// C2 governance orchestration. Records typed cases, decisions, waivers, recertifications, retirements,
/// reuse decisions and governance debt — every action an immutable audit event. Native controls are
/// requested as intents only (never enforced by C2). Notifications are raised as domain intents, not
/// delivered. No workflow engine, no security enforcement, no Ledger lifecycle duplication.
/// </summary>
public sealed class GovernanceService(
    IGovernanceStore store,
    IEventStore events,
    ITenantContextAccessor tenants,
    INativeControlOrchestrator controls)
{
    public async Task<GovernanceCaseId> OpenCaseAsync(Guid assetId, CaseType type, RiskTier tier, DateTimeOffset now, AuditActor actor, CancellationToken ct = default)
    {
        ValidateContext(
            EventReference.For(
                "governance-case",
                Guid.NewGuid()),
            actor,
            reason: null,
            "ai-asset",
            assetId);
        var governanceCase = GovernanceCase.Open(tenants.Current, assetId, type, tier, now);
        await PersistAsync(governanceCase, actor, null, ct);
        return governanceCase.Id;
    }

    public async Task RecordDecisionAsync(GovernanceCaseId id, ReviewerRole role, AuditActor actor, bool approved, string reason, string? evidenceRef, DateTimeOffset at, CancellationToken ct = default)
    {
        var governanceCase = await LoadAsync(id, ct);
        ValidateCaseContext(
            governanceCase,
            actor,
            reason);
        governanceCase.RecordDecision(role, actor, approved, reason, evidenceRef, at);
        await PersistAsync(governanceCase, actor, reason, ct);
    }

    public async Task GrantWaiverAsync(GovernanceCaseId id, AuditActor actor, string reason, DateTimeOffset expiresAt, DateTimeOffset now, CancellationToken ct = default)
    {
        var governanceCase = await LoadAsync(id, ct);
        ValidateCaseContext(
            governanceCase,
            actor,
            reason);
        governanceCase.GrantWaiver(actor, reason, expiresAt, now);
        await PersistAsync(governanceCase, actor, reason, ct);
    }

    public async Task RecertifyAsync(GovernanceCaseId id, AuditActor actor, string reason, DateTimeOffset nextDueAt, DateTimeOffset now, CancellationToken ct = default)
    {
        var governanceCase = await LoadAsync(id, ct);
        ValidateCaseContext(
            governanceCase,
            actor,
            reason);
        governanceCase.Recertify(actor, reason, nextDueAt, now);
        await PersistAsync(governanceCase, actor, reason, ct);
    }

    public async Task RequestRetirementAsync(GovernanceCaseId id, AuditActor actor, string reason, CancellationToken ct = default)
    {
        var governanceCase = await LoadAsync(id, ct);
        ValidateCaseContext(
            governanceCase,
            actor,
            reason);
        governanceCase.RequestRetirement(actor, reason);
        await PersistAsync(governanceCase, actor, reason, ct);
    }

    public async Task RecordReuseDecisionAsync(GovernanceCaseId id, ReuseAction action, string justification, AuditActor actor, DateTimeOffset at, CancellationToken ct = default)
    {
        var governanceCase = await LoadAsync(id, ct);
        ValidateCaseContext(
            governanceCase,
            actor,
            justification);
        governanceCase.RecordReuseDecision(action, justification, actor, at);
        await PersistAsync(governanceCase, actor, justification, ct);
    }

    public async Task ExpireDueCasesAsync(DateTimeOffset now, CancellationToken ct = default)
    {
        foreach (var governanceCase in await store.CasesAsync(ct))
        {
            ValidateCaseContext(
                governanceCase,
                AuditActor.System("governance-expiry"),
                "governance deadline expired");
            if (governanceCase.TryExpire(now))
            {
                await PersistAsync(
                    governanceCase,
                    AuditActor.System("governance-expiry"),
                    "governance deadline expired",
                    ct);
            }
        }
    }

    public async Task RaiseDebtAsync(Guid assetId, DebtType type, DateTimeOffset now, CancellationToken ct = default)
    {
        var debt = new GovernanceDebtItem(Guid.NewGuid(), tenants.Current, assetId, type, now);
        ValidateContext(
            EventReference.For(
                "governance-debt",
                debt.Id),
            AuditActor.System("governance-debt"),
            reason: null,
            "ai-asset",
            assetId);
        var @event = new GovernanceDebtRaised
        {
            DebtId = debt.Id,
            AssetId = assetId,
            DebtType = type.ToString(),
        };
        var prepared = PrepareEvent(
            @event,
            AuditActor.System("governance-debt"),
            reason: null,
            EventReference.For(
                "ai-asset",
                assetId));

        await store.AddDebtAsync(debt, ct);
        await AppendAsync(prepared, ct);
    }

    /// <summary>Requests a native control as an intent. C2 records it and delegates; it never enforces.</summary>
    public async Task<NativeControlReceipt> RequestNativeControlAsync(GovernanceCaseId id, string control, string target, string reason, AuditActor actor, CancellationToken ct = default)
    {
        var governanceCase = await LoadAsync(id, ct);
        ValidateCaseContext(
            governanceCase,
            actor,
            reason);
        governanceCase.RaiseNativeControlIntent(control, target);
        await PersistAsync(governanceCase, actor, reason, ct);
        return await controls.RequestAsync(new NativeControlIntent(control, target, reason), ct);
    }

    public async Task<IReadOnlyList<GovernanceCaseView>> CasesAsync(DateTimeOffset now, CancellationToken ct = default) =>
        (await store.CasesAsync(ct)).Select(c => ToView(c, now)).ToList();

    public async Task<IReadOnlyList<GovernanceDebtView>> DebtAsync(CancellationToken ct = default) =>
        (await store.DebtAsync(ct))
        .Select(d => new GovernanceDebtView { AssetId = d.AssetId, DebtType = d.Type.ToString(), RaisedAt = d.RaisedAt, IsOpen = d.IsOpen })
        .ToList();

    private static GovernanceCaseView ToView(GovernanceCase c, DateTimeOffset now) => new()
    {
        CaseId = c.Id.Value,
        AssetId = c.AssetId,
        Type = c.Type.ToString(),
        RiskTier = c.RiskTier.ToString(),
        Status = c.Status.ToString(),
        RequiredReviewers = c.RequiredReviewers.Select(r => r.ToString()).ToList(),
        DecisionCount = c.Decisions.Count,
        Outcome = c.Outcome,
        DueBy = c.DueBy,
        ExpiresAt = c.ExpiresAt,
        NextRecertDueAt = c.NextRecertDueAt,
        ReuseAction = c.ReuseAction?.ToString(),
        SlaBreached = c.IsSlaBreached(now),
    };

    private async Task<GovernanceCase> LoadAsync(GovernanceCaseId id, CancellationToken ct) =>
        await store.GetCaseAsync(id, ct) ?? throw new GovernanceException("Governance case not found in this tenant.");

    private async Task PersistAsync(
        GovernanceCase governanceCase,
        AuditActor actor,
        string? commandReason,
        CancellationToken ct)
    {
        var prepared = governanceCase
            .DequeueEvents()
            .Select(domainEvent =>
                PrepareEvent(
                    domainEvent,
                    actor,
                    ReasonFor(domainEvent)
                        ?? commandReason,
                    EventReference.For(
                        "ai-asset",
                        governanceCase.AssetId)))
            .ToList();

        await store.SaveCaseAsync(governanceCase, ct);
        foreach (var pending in prepared)
            await AppendAsync(pending, ct);
    }

    private static PreparedEvent PrepareEvent(
        GovernanceEvent domainEvent,
        AuditActor actor,
        string? reason,
        EventReference? correlation)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(domainEvent, domainEvent.GetType());
        var aggregate = domainEvent switch
        {
            GovernanceDebtRaised debt =>
                EventReference.For(
                    "governance-debt",
                    debt.DebtId),
            _ => EventReference.For(
                "governance-case",
                domainEvent switch
                {
                    CaseOpened value => value.CaseId.Value,
                    DecisionRecorded value => value.CaseId.Value,
                    CaseApproved value => value.CaseId.Value,
                    CaseRejected value => value.CaseId.Value,
                    WaiverGranted value => value.CaseId.Value,
                    CaseExpired value => value.CaseId.Value,
                    RecertificationCompleted value => value.CaseId.Value,
                    RetirementRequested value => value.CaseId.Value,
                    ReuseDecisionRecorded value => value.CaseId.Value,
                    NotificationIntentRaised value => value.CaseId.Value,
                    NativeControlRequested value => value.CaseId.Value,
                    _ => throw new GovernanceException(
                        "Unknown governance event."),
                }),
        };
        try
        {
            return new PreparedEvent(
                domainEvent,
                new EventAppendMetadata(
                    aggregate,
                    actor,
                    reason,
                    correlation),
                payload);
        }
        catch (ArgumentException)
        {
            throw new GovernanceException(
                "The governance evidence context is invalid.");
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

    private static void ValidateCaseContext(
        GovernanceCase governanceCase,
        AuditActor actor,
        string? reason) =>
        ValidateContext(
            EventReference.For(
                "governance-case",
                governanceCase.Id.Value),
            actor,
            reason,
            "ai-asset",
            governanceCase.AssetId);

    private static void ValidateContext(
        EventReference aggregate,
        AuditActor actor,
        string? reason,
        string correlationKind,
        Guid correlationId)
    {
        try
        {
            _ = new EventAppendMetadata(
                aggregate,
                actor,
                reason,
                EventReference.For(
                    correlationKind,
                    correlationId));
        }
        catch (ArgumentException)
        {
            throw new GovernanceException(
                "The governance evidence context is invalid.");
        }
    }

    private sealed record PreparedEvent(
        GovernanceEvent Event,
        EventAppendMetadata Metadata,
        byte[] Payload);

    private static string? ReasonFor(
        GovernanceEvent domainEvent) =>
        domainEvent switch
        {
            DecisionRecorded value => value.Reason,
            CaseRejected value => value.Reason,
            WaiverGranted value => value.Reason,
            CaseExpired value => value.Reason,
            RetirementRequested value => value.Reason,
            ReuseDecisionRecorded value =>
                value.Justification,
            NotificationIntentRaised value => value.Reason,
            _ => null,
        };

}
