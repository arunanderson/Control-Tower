using ControlTower.Platform.Tenancy;
using ControlTower.Platform.Identity;

namespace ControlTower.Modules.Governance.Domain;

/// <summary>An approval decision — actor, reason, evidence, timestamp and outcome are always preserved.</summary>
public sealed record ApprovalDecision(ReviewerRole Role, AuditActor Actor, bool Approved, string Reason, string? EvidenceRef, DateTimeOffset At);

/// <summary>
/// C2 GovernanceCase (Stage 4 §10 socket): a typed case that records governance decisions and
/// triggers — never duplicates — Ledger transitions. It holds its own case status only; asset
/// RegistrationStatus/OperationalLifecycle remain owned by C1. Proportional and Flag-Never-Block:
/// low-risk cases auto-approve so registration stays achievable in minutes.
/// </summary>
public sealed class GovernanceCase
{
    private readonly List<ApprovalDecision> _decisions = [];
    private readonly List<GovernanceEvent> _events = [];
    private readonly HashSet<ReviewerRole> _required;

    private GovernanceCase(GovernanceCaseId id, TenantId tenant, Guid assetId, CaseType type, RiskTier tier, DateTimeOffset now)
    {
        Id = id;
        Tenant = tenant;
        AssetId = assetId;
        Type = type;
        RiskTier = tier;
        _required = [.. ApprovalRouting.RequiredReviewers(tier)];
        DueBy = now + ApprovalRouting.Sla(tier);
        Status = CaseStatus.Open;
    }

    public GovernanceCaseId Id { get; }
    public TenantId Tenant { get; }
    public Guid AssetId { get; }
    public CaseType Type { get; }
    public RiskTier RiskTier { get; }
    public CaseStatus Status { get; private set; }
    public DateTimeOffset DueBy { get; }
    public DateTimeOffset? ExpiresAt { get; private set; }
    public DateTimeOffset? NextRecertDueAt { get; private set; }
    public ReuseAction? ReuseAction { get; private set; }
    public string? ReuseJustification { get; private set; }
    public string? Outcome { get; private set; }

    public IReadOnlyList<ApprovalDecision> Decisions => _decisions;
    public IReadOnlyCollection<ReviewerRole> RequiredReviewers => _required;

    public bool IsSlaBreached(DateTimeOffset now) =>
        now > DueBy && Status is CaseStatus.Open or CaseStatus.AwaitingReview;

    public static GovernanceCase Open(TenantId tenant, Guid assetId, CaseType type, RiskTier tier, DateTimeOffset now)
    {
        var c = new GovernanceCase(GovernanceCaseId.New(), tenant, assetId, type, tier, now);
        c.Raise(new CaseOpened { CaseId = c.Id, AssetId = assetId, Type = type, Tier = tier });

        if (ApprovalRouting.AutoApproves(tier) && type is CaseType.Intake or CaseType.Approval)
        {
            c.Status = CaseStatus.Approved;
            c.Outcome = "auto-approved (low risk)";
            c.Raise(new CaseApproved { CaseId = c.Id });
        }
        else if (c._required.Count > 0)
        {
            c.Status = CaseStatus.AwaitingReview;
        }

        return c;
    }

    public void RecordDecision(ReviewerRole role, AuditActor actor, bool approved, string reason, string? evidenceRef, DateTimeOffset at)
    {
        if (Status is not (CaseStatus.Open or CaseStatus.AwaitingReview))
            throw new GovernanceException($"Cannot record a decision on a {Status} case.");
        if (!_required.Contains(role))
            throw new GovernanceException($"{role} is not a required reviewer for this case.");
        Require(actor, reason);

        _decisions.Add(new ApprovalDecision(role, actor, approved, reason, evidenceRef, at));
        Raise(new DecisionRecorded { CaseId = Id, Role = role, Approved = approved, Actor = actor, Reason = reason });

        if (!approved)
        {
            Status = CaseStatus.Rejected;
            Outcome = $"rejected by {role}";
            Raise(new CaseRejected { CaseId = Id, Reason = reason });
            return;
        }

        if (_required.All(r => _decisions.Any(d => d.Role == r && d.Approved)))
        {
            Status = CaseStatus.Approved;
            Outcome = "approved";
            Raise(new CaseApproved { CaseId = Id });
        }
    }

    public void GrantWaiver(AuditActor actor, string reason, DateTimeOffset expiresAt, DateTimeOffset now)
    {
        Require(actor, reason);
        if (expiresAt <= now) throw new GovernanceException("A waiver must expire in the future (time-bound).");
        Status = CaseStatus.Waived;
        ExpiresAt = expiresAt;
        Outcome = "waived";
        Raise(new WaiverGranted { CaseId = Id, ExpiresAt = expiresAt, Reason = reason });
    }

    public void Recertify(AuditActor actor, string reason, DateTimeOffset nextDueAt, DateTimeOffset now)
    {
        Require(actor, reason);
        if (nextDueAt <= now) throw new GovernanceException("Next recertification date must be in the future.");
        Status = CaseStatus.Approved;
        NextRecertDueAt = nextDueAt;
        Outcome = "recertified";
        Raise(new RecertificationCompleted { CaseId = Id, NextDueAt = nextDueAt });
    }

    public void RequestRetirement(AuditActor actor, string reason)
    {
        Require(actor, reason);
        Status = CaseStatus.Retired;
        Outcome = "retirement requested";
        Raise(new RetirementRequested { CaseId = Id, AssetId = AssetId, Reason = reason });
    }

    public void RecordReuseDecision(ReuseAction action, string justification, AuditActor actor, DateTimeOffset at)
    {
        Require(actor, justification);
        ReuseAction = action;
        ReuseJustification = justification;
        Outcome = $"reuse:{action}";
        Raise(new ReuseDecisionRecorded { CaseId = Id, AssetId = AssetId, Action = action.ToString(), Justification = justification, Actor = actor });
        // Flag-Never-Block: BuildNew is recorded like any other action; nothing is blocked.
    }

    public bool TryExpire(DateTimeOffset now)
    {
        if (Status == CaseStatus.Waived && ExpiresAt is { } waiverEnd && now > waiverEnd)
        {
            Status = CaseStatus.Expired;
            Outcome = "waiver expired";
            Raise(new CaseExpired { CaseId = Id, Reason = "waiver expired" });
            return true;
        }

        if (Status != CaseStatus.Expired && NextRecertDueAt is { } recertDue && now > recertDue)
        {
            Status = CaseStatus.Expired;
            Outcome = "recertification overdue";
            Raise(new CaseExpired { CaseId = Id, Reason = "recertification overdue" });
            return true;
        }

        return false;
    }

    public void RaiseNotificationIntent(string recipient, string reason, string deepLink)
    {
        if (string.IsNullOrWhiteSpace(recipient)) throw new GovernanceException("Notification recipient is required.");
        Raise(new NotificationIntentRaised { CaseId = Id, Recipient = recipient, Reason = reason, DeepLink = deepLink });
    }

    public void RaiseNativeControlIntent(string control, string target)
    {
        // Intent only — C2 records the request; enforcement is delegated to native platforms (ADR-002).
        Raise(new NativeControlRequested { CaseId = Id, Control = control, Target = target });
    }

    public IReadOnlyList<GovernanceEvent> DequeueEvents()
    {
        var copy = _events.ToList();
        _events.Clear();
        return copy;
    }

    private void Raise(GovernanceEvent domainEvent) => _events.Add(domainEvent);

    private static void Require(AuditActor actor, string reason)
    {
        if (!actor.IsValid) throw new GovernanceException("An actor is required.");
        if (string.IsNullOrWhiteSpace(reason)) throw new GovernanceException("A reason is required.");
    }
}
