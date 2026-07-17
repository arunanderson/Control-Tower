using System.Text.Json;
using ControlTower.Modules.Governance.Domain;
using ControlTower.Platform.Events;
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
    public async Task<GovernanceCaseId> OpenCaseAsync(Guid assetId, CaseType type, RiskTier tier, DateTimeOffset now, CancellationToken ct = default)
    {
        var governanceCase = GovernanceCase.Open(tenants.Current, assetId, type, tier, now);
        await PersistAsync(governanceCase, ct);
        return governanceCase.Id;
    }

    public async Task RecordDecisionAsync(GovernanceCaseId id, ReviewerRole role, ActorRef actor, bool approved, string reason, string? evidenceRef, DateTimeOffset at, CancellationToken ct = default)
    {
        var governanceCase = await LoadAsync(id, ct);
        governanceCase.RecordDecision(role, actor, approved, reason, evidenceRef, at);
        await PersistAsync(governanceCase, ct);
    }

    public async Task GrantWaiverAsync(GovernanceCaseId id, ActorRef actor, string reason, DateTimeOffset expiresAt, DateTimeOffset now, CancellationToken ct = default)
    {
        var governanceCase = await LoadAsync(id, ct);
        governanceCase.GrantWaiver(actor, reason, expiresAt, now);
        await PersistAsync(governanceCase, ct);
    }

    public async Task RecertifyAsync(GovernanceCaseId id, ActorRef actor, string reason, DateTimeOffset nextDueAt, DateTimeOffset now, CancellationToken ct = default)
    {
        var governanceCase = await LoadAsync(id, ct);
        governanceCase.Recertify(actor, reason, nextDueAt, now);
        await PersistAsync(governanceCase, ct);
    }

    public async Task RequestRetirementAsync(GovernanceCaseId id, ActorRef actor, string reason, CancellationToken ct = default)
    {
        var governanceCase = await LoadAsync(id, ct);
        governanceCase.RequestRetirement(actor, reason);
        await PersistAsync(governanceCase, ct);
    }

    public async Task RecordReuseDecisionAsync(GovernanceCaseId id, ReuseAction action, string justification, ActorRef actor, DateTimeOffset at, CancellationToken ct = default)
    {
        var governanceCase = await LoadAsync(id, ct);
        governanceCase.RecordReuseDecision(action, justification, actor, at);
        await PersistAsync(governanceCase, ct);
    }

    public async Task ExpireDueCasesAsync(DateTimeOffset now, CancellationToken ct = default)
    {
        foreach (var governanceCase in await store.CasesAsync(ct))
        {
            if (governanceCase.TryExpire(now)) await PersistAsync(governanceCase, ct);
        }
    }

    public async Task RaiseDebtAsync(Guid assetId, DebtType type, DateTimeOffset now, CancellationToken ct = default)
    {
        var debt = new GovernanceDebtItem(Guid.NewGuid(), tenants.Current, assetId, type, now);
        await store.AddDebtAsync(debt, ct);
        await AppendAsync(new GovernanceDebtRaised { AssetId = assetId, DebtType = type.ToString() }, ct);
    }

    /// <summary>Requests a native control as an intent. C2 records it and delegates; it never enforces.</summary>
    public async Task<NativeControlReceipt> RequestNativeControlAsync(GovernanceCaseId id, string control, string target, string reason, CancellationToken ct = default)
    {
        var governanceCase = await LoadAsync(id, ct);
        governanceCase.RaiseNativeControlIntent(control, target);
        await PersistAsync(governanceCase, ct);
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

    private async Task PersistAsync(GovernanceCase governanceCase, CancellationToken ct)
    {
        await store.SaveCaseAsync(governanceCase, ct);
        foreach (var domainEvent in governanceCase.DequeueEvents())
            await AppendAsync(domainEvent, ct);
    }

    private async Task AppendAsync(GovernanceEvent domainEvent, CancellationToken ct)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(domainEvent, domainEvent.GetType());
        await events.AppendAsync(domainEvent, payload, ct);
    }
}
