using ControlTower.Modules.Governance.Domain;

namespace ControlTower.Modules.Governance.Application;

/// <summary>Tenant-scoped store for governance cases and debt. Production: Azure PostgreSQL (DEC-001); dev: in-memory.</summary>
public interface IGovernanceStore
{
    Task SaveCaseAsync(GovernanceCase governanceCase, CancellationToken ct = default);
    Task<GovernanceCase?> GetCaseAsync(GovernanceCaseId id, CancellationToken ct = default);
    Task<IReadOnlyList<GovernanceCase>> CasesAsync(CancellationToken ct = default);
    Task AddDebtAsync(GovernanceDebtItem debt, CancellationToken ct = default);
    Task<IReadOnlyList<GovernanceDebtItem>> DebtAsync(CancellationToken ct = default);
}

public sealed record NativeControlIntent(string Control, string Target, string Reason);

/// <summary>Receipt for a native-control request. <c>Enforced</c> is always false for C2 — enforcement is delegated (ADR-002).</summary>
public sealed record NativeControlReceipt(bool Recorded, bool Enforced, string Note);

/// <summary>
/// Contract-only orchestration seam for native controls (ADR-002). C2 records the request as an intent
/// and hands it to a native platform; C2 itself performs no enforcement. The V1 implementation is a
/// recorder; real invocation via C4.6 adapters is V2.
/// </summary>
public interface INativeControlOrchestrator
{
    Task<NativeControlReceipt> RequestAsync(NativeControlIntent intent, CancellationToken ct = default);
}

public sealed record GovernanceCaseView
{
    public required Guid CaseId { get; init; }
    public required Guid AssetId { get; init; }
    public required string Type { get; init; }
    public required string RiskTier { get; init; }
    public required string Status { get; init; }
    public required IReadOnlyList<string> RequiredReviewers { get; init; }
    public required int DecisionCount { get; init; }
    public string? Outcome { get; init; }
    public required DateTimeOffset DueBy { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }
    public DateTimeOffset? NextRecertDueAt { get; init; }
    public string? ReuseAction { get; init; }
    public required bool SlaBreached { get; init; }
}

public sealed record GovernanceDebtView
{
    public required Guid AssetId { get; init; }
    public required string DebtType { get; init; }
    public required DateTimeOffset RaisedAt { get; init; }
    public required bool IsOpen { get; init; }
}
