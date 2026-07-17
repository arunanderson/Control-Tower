namespace ControlTower.Modules.Governance.Domain;

/// <summary>
/// Proportional, risk-tiered approval routing (Stage 11 §A2). Low risk auto-approves so registration
/// stays achievable in minutes (Flag-Never-Block); higher tiers require more reviewers. SLA windows
/// scale with tier. This is a rule table, not a workflow engine.
/// </summary>
public static class ApprovalRouting
{
    public static IReadOnlyList<ReviewerRole> RequiredReviewers(RiskTier tier) => tier switch
    {
        RiskTier.Low => [],
        RiskTier.Medium => [ReviewerRole.Governance],
        RiskTier.High => [ReviewerRole.Governance, ReviewerRole.Security, ReviewerRole.Business],
        RiskTier.Exceptional => [ReviewerRole.Governance, ReviewerRole.Security, ReviewerRole.Privacy, ReviewerRole.Finance],
        _ => [ReviewerRole.Governance],
    };

    public static bool AutoApproves(RiskTier tier) => tier == RiskTier.Low;

    public static TimeSpan Sla(RiskTier tier) => tier switch
    {
        RiskTier.Low => TimeSpan.FromMinutes(5),
        RiskTier.Medium => TimeSpan.FromDays(2),
        RiskTier.High => TimeSpan.FromDays(5),
        RiskTier.Exceptional => TimeSpan.FromDays(10),
        _ => TimeSpan.FromDays(2),
    };
}
