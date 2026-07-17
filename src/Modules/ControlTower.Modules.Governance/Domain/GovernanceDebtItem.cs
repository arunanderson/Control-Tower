using ControlTower.Platform.Tenancy;

namespace ControlTower.Modules.Governance.Domain;

/// <summary>
/// Governance debt — a governance-visible gap (ownerless/lapsed-owner/unregistered/stale-purpose).
/// Populated from asset/ownership signals (C1 events in production). The ledger records ungoverned
/// reality as debt (ADR-018 Flag-Never-Block); debt is surfaced, never used to block.
/// </summary>
public sealed class GovernanceDebtItem
{
    public GovernanceDebtItem(Guid id, TenantId tenant, Guid assetId, DebtType type, DateTimeOffset raisedAt)
    {
        Id = id;
        Tenant = tenant;
        AssetId = assetId;
        Type = type;
        RaisedAt = raisedAt;
    }

    public Guid Id { get; }
    public TenantId Tenant { get; }
    public Guid AssetId { get; }
    public DebtType Type { get; }
    public DateTimeOffset RaisedAt { get; }
    public DateTimeOffset? ResolvedAt { get; private set; }

    public bool IsOpen => ResolvedAt is null;

    public void Resolve(DateTimeOffset at)
    {
        if (ResolvedAt is not null) throw new GovernanceException("Debt item is already resolved.");
        ResolvedAt = at;
    }
}
