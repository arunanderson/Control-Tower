using ControlTower.Platform.Identity;

namespace ControlTower.Modules.Ledger.Domain;

/// <summary>
/// A link from the asset to a provider observation (Stage 4 §2.3 / Stage 5 E6). The set of active links
/// and their native-identifier sets is the alias graph. Resolution never rewrites source observations; it
/// only points at them. A link is never deleted or rewritten — it is <see cref="LinkStatus.Severed"/> when
/// it no longer holds, or <see cref="LinkStatus.Superseded"/> when a merge/split replaces it. Only active
/// links roll up to the asset's <see cref="MatchConfidence"/>.
/// </summary>
public sealed class ResolutionLink
{
    public ResolutionLink(
        NativeIdentifierSet identifiers,
        MatchMethod method,
        MatchConfidence confidence,
        AuditActor linkedBy,
        DateTimeOffset linkedAt,
        Guid? observationRef = null)
    {
        Id = Guid.NewGuid();
        Identifiers = identifiers;
        Method = method;
        Confidence = confidence;
        if (!linkedBy.IsValid)
            throw new DomainException("A link actor is required.");
        LinkedBy = linkedBy;
        LinkedAt = linkedAt;
        ObservationRef = observationRef;
        Status = LinkStatus.Active;
    }

    public Guid Id { get; }

    public NativeIdentifierSet Identifiers { get; }

    public MatchMethod Method { get; }

    public MatchConfidence Confidence { get; }

    public AuditActor LinkedBy { get; }

    public DateTimeOffset LinkedAt { get; }

    /// <summary>The observation this link points at (Stage 5 E6). Null for operator links not tied to one observation.</summary>
    public Guid? ObservationRef { get; }

    public LinkStatus Status { get; private set; }

    public DateTimeOffset? ClosedAt { get; private set; }

    /// <summary>When superseded by a merge/split, the link that replaces this one.</summary>
    public Guid? SupersededByLinkId { get; private set; }

    public bool IsActive => Status == LinkStatus.Active;

    /// <summary>The identity aliases this link contributes to the graph (provider-reported).</summary>
    public IReadOnlyList<IdentityAlias> Aliases =>
        Identifiers.Identifiers.Select(IdentityAlias.Observed).ToList();

    internal void Sever(DateTimeOffset at)
    {
        if (Status != LinkStatus.Active) return;
        Status = LinkStatus.Severed;
        ClosedAt = at;
    }

    internal void SupersedeWith(Guid supersedingLinkId, DateTimeOffset at)
    {
        if (Status != LinkStatus.Active) return;
        Status = LinkStatus.Superseded;
        SupersededByLinkId = supersedingLinkId;
        ClosedAt = at;
    }
}
