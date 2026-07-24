using ControlTower.Platform.Tenancy;
using ControlTower.Platform.Identity;

namespace ControlTower.Modules.Ledger.Domain;

public enum MergeCaseStatus
{
    Open,
    Resolved,
}

/// <summary>
/// The manual merge queue item (Stage 5 E8). Opened when identifiers collide across assets or a match is
/// too weak to auto-link (Low), so nothing is linked automatically — a human decides. Resolving a case is
/// the operator-approved (Manual) decision. A queue entity within C1, not a new aggregate: it records the
/// conflict and its outcome; the actual link/merge changes are applied to the AIAsset aggregate.
/// </summary>
public sealed class MergeCase
{
    private MergeCase(Guid id, TenantId tenant, string reason, MatchConfidence confidence, NativeIdentifierSet identifiers, IReadOnlyList<LedgerAssetId> candidates, Guid? observationRef)
    {
        Id = id;
        Tenant = tenant;
        Reason = reason;
        Confidence = confidence;
        Identifiers = identifiers;
        Candidates = candidates;
        ObservationRef = observationRef;
        Status = MergeCaseStatus.Open;
        OpenedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; }
    public TenantId Tenant { get; }
    public string Reason { get; }
    public MatchConfidence Confidence { get; }
    public NativeIdentifierSet Identifiers { get; }
    public IReadOnlyList<LedgerAssetId> Candidates { get; }
    public Guid? ObservationRef { get; }
    public MergeCaseStatus Status { get; private set; }
    public DateTimeOffset OpenedAt { get; }
    public DateTimeOffset? ResolvedAt { get; private set; }
    public string? Outcome { get; private set; }
    public AuditActor? ResolvedBy { get; private set; }

    public static MergeCase Open(TenantId tenant, string reason, MatchConfidence confidence, NativeIdentifierSet identifiers, IReadOnlyList<LedgerAssetId> candidates, Guid? observationRef) =>
        new(Guid.NewGuid(), tenant, reason, confidence, identifiers, candidates, observationRef);

    public void Resolve(string outcome, AuditActor by)
    {
        if (Status == MergeCaseStatus.Resolved) throw new DomainException("Merge case already resolved.");
        if (!by.IsValid) throw new DomainException("A merge-case decision actor is required.");
        Status = MergeCaseStatus.Resolved;
        Outcome = outcome;
        ResolvedBy = by;
        ResolvedAt = DateTimeOffset.UtcNow;
    }
}
