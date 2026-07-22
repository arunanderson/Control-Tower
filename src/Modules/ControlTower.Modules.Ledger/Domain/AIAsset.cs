using ControlTower.Platform.Tenancy;

namespace ControlTower.Modules.Ledger.Domain;

/// <summary>
/// C1 — the AI asset the whole product hangs on (Stage 4 §2.3). One aggregate for all asset types
/// (Stage 4 §11.1). Business-context attributes mutate only through commands that emit domain events;
/// native/technical attributes live in observations and are projected, never edited here.
/// </summary>
public sealed class AIAsset
{
    private static readonly Dictionary<RegistrationStatus, RegistrationStatus[]> RegistrationTransitions = new()
    {
        [RegistrationStatus.Discovered] = [RegistrationStatus.Triaged, RegistrationStatus.Rejected],
        [RegistrationStatus.Triaged] = [RegistrationStatus.Registered, RegistrationStatus.Rejected],
        [RegistrationStatus.Registered] = [RegistrationStatus.Retired],
    };

    private static readonly Dictionary<OperationalLifecycleState, OperationalLifecycleState[]> LifecycleTransitions = new()
    {
        [OperationalLifecycleState.Draft] = [OperationalLifecycleState.Pilot, OperationalLifecycleState.Retired],
        [OperationalLifecycleState.Pilot] = [OperationalLifecycleState.Production, OperationalLifecycleState.Retired],
        [OperationalLifecycleState.Production] = [OperationalLifecycleState.UnderReview, OperationalLifecycleState.Suspended, OperationalLifecycleState.Retired],
        [OperationalLifecycleState.UnderReview] = [OperationalLifecycleState.Production, OperationalLifecycleState.Suspended, OperationalLifecycleState.Retired],
        [OperationalLifecycleState.Suspended] = [OperationalLifecycleState.Production, OperationalLifecycleState.Retired],
    };

    private readonly List<OwnershipAssignment> _ownerships = [];
    private readonly List<ResolutionLink> _links = [];
    private readonly List<LedgerEvent> _pendingEvents = [];

    private AIAsset(LedgerAssetId id, TenantId tenant, string displayName, AssetType type)
    {
        Id = id;
        Tenant = tenant;
        DisplayName = displayName;
        Type = type;
        RegistrationStatus = RegistrationStatus.Discovered;
        OperationalLifecycleState = OperationalLifecycleState.Draft;
        MatchConfidence = MatchConfidence.Manual;
    }

    public LedgerAssetId Id { get; }
    public TenantId Tenant { get; }
    public string DisplayName { get; private set; }
    public AssetType Type { get; private set; }
    public string? BusinessPurpose { get; private set; }
    public RegistrationStatus RegistrationStatus { get; private set; }
    public OperationalLifecycleState OperationalLifecycleState { get; private set; }
    public MatchConfidence MatchConfidence { get; private set; }

    public IReadOnlyList<OwnershipAssignment> Ownerships => _ownerships;
    public IReadOnlyList<ResolutionLink> ResolutionLinks => _links;

    /// <summary>The material links — only active links contribute to identity and confidence roll-up.</summary>
    public IReadOnlyList<ResolutionLink> ActiveResolutionLinks => _links.Where(l => l.IsActive).ToList();

    /// <summary>The asset's slice of the alias graph: every alias on its active links (Stage 5 E5).</summary>
    public IReadOnlyList<IdentityAlias> Aliases => ActiveResolutionLinks.SelectMany(l => l.Aliases).Distinct().ToList();

    /// <summary>No current Owner-role assignment — a first-class, queryable condition (Stage 1 §8.11).</summary>
    public bool IsOwnerless => !_ownerships.Any(o => o.IsCurrent && o.Role == OwnershipRole.Owner);

    public static AIAsset Discover(TenantId tenant, string displayName, AssetType type, TaxonomyScheme taxonomy)
    {
        if (string.IsNullOrWhiteSpace(displayName)) throw new DomainException("DisplayName is required.");
        if (!taxonomy.IsValid(type)) throw new DomainException($"Asset type '{type}' is not in taxonomy '{taxonomy.SchemeId}'.");

        var asset = new AIAsset(LedgerAssetId.New(), tenant, displayName.Trim(), type);
        asset.Raise(new AssetDiscovered { AssetId = asset.Id, DisplayName = asset.DisplayName, Type = type });
        return asset;
    }

    public void Triage() =>
        TransitionRegistration(RegistrationStatus.Triaged, () => Raise(new AssetTriaged { AssetId = Id }));

    public void Reject(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new DomainException("A rejection reason is required.");
        TransitionRegistration(RegistrationStatus.Rejected, () => Raise(new AssetRejected { AssetId = Id, Reason = reason.Trim() }));
    }

    public void Retire() =>
        TransitionRegistration(RegistrationStatus.Retired, () => Raise(new AssetRetired { AssetId = Id }));

    /// <summary>Register with the minimal mandatory set (ADR-018): business purpose + an owner.</summary>
    public void Register(string businessPurpose, PersonRef owner)
    {
        if (string.IsNullOrWhiteSpace(businessPurpose))
            throw new DomainException("Business purpose is required to register.");

        TransitionRegistration(RegistrationStatus.Registered, () =>
        {
            BusinessPurpose = businessPurpose.Trim();
            Raise(new AssetRegistered { AssetId = Id, BusinessPurpose = BusinessPurpose });
            AssignOwnership(owner, OwnershipRole.Owner);
        });
    }

    public void TransitionLifecycle(OperationalLifecycleState to)
    {
        if (!LifecycleTransitions.TryGetValue(OperationalLifecycleState, out var allowed) || !allowed.Contains(to))
            throw new DomainException($"Illegal lifecycle transition {OperationalLifecycleState} → {to}.");

        var from = OperationalLifecycleState;
        OperationalLifecycleState = to;
        Raise(new LifecycleStateChanged { AssetId = Id, From = from, To = to });
    }

    public void AssignOwnership(PersonRef person, OwnershipRole role)
    {
        _ownerships.Add(new OwnershipAssignment(person, role, DateTimeOffset.UtcNow));
        Raise(new OwnershipAssigned { AssetId = Id, Person = person, Role = role });
    }

    public void LapseOwnership(PersonRef person)
    {
        var current = _ownerships.FirstOrDefault(o => o.IsCurrent && o.Person == person)
            ?? throw new DomainException("No current assignment for that person.");
        current.Lapse(DateTimeOffset.UtcNow);
        Raise(new OwnershipLapsed { AssetId = Id, Person = person });
    }

    public void ReassignOwnership(PersonRef from, PersonRef to, OwnershipRole role)
    {
        LapseOwnership(from);
        AssignOwnership(to, role);
        Raise(new OwnershipReassigned { AssetId = Id, From = from, To = to });
    }

    public ResolutionLink AddResolutionLink(NativeIdentifierSet identifiers, MatchMethod method, MatchConfidence confidence, string linkedBy, Guid? observationRef = null)
    {
        var link = new ResolutionLink(identifiers, method, confidence, linkedBy, DateTimeOffset.UtcNow, observationRef);
        _links.Add(link);
        Raise(new ResolutionLinkAdded { AssetId = Id, LinkId = link.Id, LinkConfidence = confidence, ObservationRef = observationRef });
        RecomputeConfidence();
        return link;
    }

    /// <summary>True once an active link already points at this observation — the basis for idempotent replay.</summary>
    public bool IsLinkedToObservation(Guid observationRef) =>
        _links.Any(l => l.IsActive && l.ObservationRef == observationRef);

    /// <summary>Sever a link (it no longer holds). The link is retained with status Severed — never deleted.</summary>
    public void SeverResolutionLink(Guid linkId, string by, string reason)
    {
        var link = _links.FirstOrDefault(l => l.Id == linkId) ?? throw new DomainException("Resolution link not found.");
        if (!link.IsActive) return;
        link.Sever(DateTimeOffset.UtcNow);
        Raise(new ResolutionLinkSevered { AssetId = Id, LinkId = linkId, By = by, Reason = reason });
        RecomputeConfidence();
    }

    /// <summary>Mark a link superseded by a new link created on another asset (merge/split). Retained, never deleted.</summary>
    public void SupersedeResolutionLink(Guid linkId, Guid supersedingLinkId, string by)
    {
        var link = _links.FirstOrDefault(l => l.Id == linkId) ?? throw new DomainException("Resolution link not found.");
        if (!link.IsActive) return;
        link.SupersedeWith(supersedingLinkId, DateTimeOffset.UtcNow);
        Raise(new ResolutionLinkSuperseded { AssetId = Id, LinkId = linkId, BySupersedingLinkId = supersedingLinkId, By = by });
        RecomputeConfidence();
    }

    /// <summary>This asset was merged into <paramref name="target"/> (its links have been superseded there).</summary>
    public void MarkMergedInto(LedgerAssetId target, string by)
    {
        if (RegistrationStatus == RegistrationStatus.Retired)
            throw new DomainException("A retired asset cannot be merged.");
        RegistrationStatus = RegistrationStatus.Merged;
        Raise(new AssetMergedInto { AssetId = Id, TargetAssetId = target, By = by });
    }

    /// <summary>Some of this asset's links were split out into <paramref name="newAsset"/>.</summary>
    public void RecordSplit(LedgerAssetId newAsset, string by) =>
        Raise(new AssetSplit { AssetId = Id, NewAssetId = newAsset, By = by });

    /// <summary>
    /// Roll up asset confidence as the weakest material link (ADR-024/025; Stage 5 §confidence): the lowest
    /// graded confidence among active links wins, so a strong link never masks a weak one. Operator (Manual)
    /// links are authoritative and do not weaken the roll-up. No active links ⇒ unresolved (Manual). The
    /// per-link classification rule table itself is PoC-gated (PoC-1/2); this roll-up is not.
    /// </summary>
    public static MatchConfidence RollUp(IEnumerable<MatchConfidence> confidences)
    {
        var list = confidences.ToList();
        return list.Count == 0 ? MatchConfidence.Manual : list.OrderBy(Rank).First();
    }

    private static int Rank(MatchConfidence confidence) => confidence switch
    {
        MatchConfidence.Low => 0,
        MatchConfidence.Medium => 1,
        MatchConfidence.High => 2,
        MatchConfidence.Manual => 3, // operator-approved: authoritative, never the weakest link
        _ => 0,
    };

    public IReadOnlyList<LedgerEvent> DequeueEvents()
    {
        var copy = _pendingEvents.ToList();
        _pendingEvents.Clear();
        return copy;
    }

    private void RecomputeConfidence()
    {
        var next = RollUp(_links.Where(l => l.IsActive).Select(l => l.Confidence));
        if (next == MatchConfidence) return;
        var from = MatchConfidence;
        MatchConfidence = next;
        Raise(new MatchConfidenceChanged { AssetId = Id, From = from, To = next });
    }

    private void TransitionRegistration(RegistrationStatus to, Action apply)
    {
        if (!RegistrationTransitions.TryGetValue(RegistrationStatus, out var allowed) || !allowed.Contains(to))
            throw new DomainException($"Illegal registration transition {RegistrationStatus} → {to}.");
        RegistrationStatus = to;
        apply();
    }

    private void Raise(LedgerEvent domainEvent) => _pendingEvents.Add(domainEvent);
}
