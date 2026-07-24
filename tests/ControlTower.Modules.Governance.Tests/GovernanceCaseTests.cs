using System;
using System.Linq;
using ControlTower.Modules.Governance.Domain;
using ControlTower.Platform.Identity;
using ControlTower.Platform.Tenancy;
using Xunit;

namespace ControlTower.Modules.Governance.Tests;

public class GovernanceCaseTests
{
    private static readonly TenantId Tenant = new(Guid.NewGuid());
    private static readonly AuditActor Gov =
        AuditActor.System("governance-lead");
    private static AuditActor Actor(string id) =>
        AuditActor.System(id);

    private static GovernanceCase Open(RiskTier tier, CaseType type = CaseType.Approval, DateTimeOffset? now = null) =>
        GovernanceCase.Open(Tenant, Guid.NewGuid(), type, tier, now ?? DateTimeOffset.UtcNow);

    [Fact]
    public void Low_risk_intake_auto_approves_with_no_reviewers()
    {
        var c = Open(RiskTier.Low, CaseType.Intake);
        Assert.Equal(CaseStatus.Approved, c.Status);
        Assert.Empty(c.RequiredReviewers);
        Assert.Equal("auto-approved (low risk)", c.Outcome);
    }

    [Fact]
    public void Medium_risk_awaits_review_then_approves_on_governance_sign_off()
    {
        var c = Open(RiskTier.Medium);
        Assert.Equal(CaseStatus.AwaitingReview, c.Status);
        c.RecordDecision(ReviewerRole.Governance, Gov, true, "meets policy", "ev-1", DateTimeOffset.UtcNow);
        Assert.Equal(CaseStatus.Approved, c.Status);
    }

    [Fact]
    public void High_risk_requires_all_reviewers()
    {
        var now = DateTimeOffset.UtcNow;
        var c = Open(RiskTier.High);
        c.RecordDecision(ReviewerRole.Governance, Gov, true, "ok", null, now);
        c.RecordDecision(ReviewerRole.Security, Actor("security"), true, "ok", null, now);
        Assert.Equal(CaseStatus.AwaitingReview, c.Status);
        c.RecordDecision(ReviewerRole.Business, Actor("business"), true, "ok", null, now);
        Assert.Equal(CaseStatus.Approved, c.Status);
    }

    [Fact]
    public void A_single_rejection_rejects_the_case()
    {
        var c = Open(RiskTier.High);
        c.RecordDecision(ReviewerRole.Security, Actor("security"), false, "unacceptable risk", null, DateTimeOffset.UtcNow);
        Assert.Equal(CaseStatus.Rejected, c.Status);
    }

    [Fact]
    public void A_non_required_reviewer_cannot_decide()
    {
        var c = Open(RiskTier.Medium);
        Assert.Throws<GovernanceException>(() =>
            c.RecordDecision(ReviewerRole.Finance, Actor("finance"), true, "x", null, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void A_decision_on_a_closed_case_is_rejected()
    {
        var c = Open(RiskTier.Low, CaseType.Intake); // auto-approved → no longer Open/AwaitingReview
        Assert.Throws<GovernanceException>(() =>
            c.RecordDecision(ReviewerRole.Governance, Gov, true, "x", null, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void A_decision_requires_actor_and_reason()
    {
        var c = Open(RiskTier.Medium);
        Assert.Throws<GovernanceException>(() =>
            c.RecordDecision(ReviewerRole.Governance, Gov, true, "  ", null, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void A_waiver_is_time_bound_and_expires()
    {
        var now = DateTimeOffset.UtcNow;
        var c = Open(RiskTier.Medium);
        c.GrantWaiver(Gov, "temporary exception", now.AddDays(30), now);
        Assert.Equal(CaseStatus.Waived, c.Status);
        Assert.False(c.TryExpire(now.AddDays(1)));
        Assert.True(c.TryExpire(now.AddDays(31)));
        Assert.Equal(CaseStatus.Expired, c.Status);
    }

    [Fact]
    public void Recertification_expires_when_overdue()
    {
        var now = DateTimeOffset.UtcNow;
        var c = Open(RiskTier.Medium);
        c.Recertify(Gov, "annual recertification", now.AddDays(365), now);
        Assert.Equal(CaseStatus.Approved, c.Status);
        Assert.True(c.TryExpire(now.AddDays(366)));
        Assert.Equal(CaseStatus.Expired, c.Status);
    }

    [Fact]
    public void Retirement_moves_the_case_to_retired_and_emits_an_intent()
    {
        var c = Open(RiskTier.Medium);
        c.RequestRetirement(Gov, "decommissioned");
        Assert.Equal(CaseStatus.Retired, c.Status);
        Assert.Contains(c.DequeueEvents(), e => e is RetirementRequested);
    }

    [Fact]
    public void Reuse_decision_records_the_action_and_never_blocks_build_new()
    {
        var c = GovernanceCase.Open(Tenant, Guid.NewGuid(), CaseType.ReuseDecision, RiskTier.Low, DateTimeOffset.UtcNow);
        c.RecordReuseDecision(ReuseAction.BuildNew, "no existing capability fits", Gov, DateTimeOffset.UtcNow);
        Assert.Equal(ReuseAction.BuildNew, c.ReuseAction);
        Assert.Contains(c.DequeueEvents(), e => e is ReuseDecisionRecorded);
    }

    [Fact]
    public void Sla_breach_is_detected_past_the_due_date()
    {
        var now = DateTimeOffset.UtcNow;
        var c = Open(RiskTier.Medium, CaseType.Approval, now);
        Assert.False(c.IsSlaBreached(now));
        Assert.True(c.IsSlaBreached(now.AddDays(3)));
    }
}
