using System.Text.Json;
using ControlTower.Modules.Ledger.Domain;
using ControlTower.Platform.Events;
using ControlTower.Platform.Identity;
using ControlTower.Platform.Tenancy;

namespace ControlTower.Modules.Ledger.Application;

/// <summary>A normalized observation the resolution engine acts on — reconstructed from the C4 event, never from the store.</summary>
public sealed record ObservationDescriptor(
    Guid ObservationId,
    NativeIdentifier PrimaryIdentifier,
    string DisplayName,
    string AssetType,
    string EvidenceLabel);

/// <summary>The outcome of resolving one observation (for callers/tests).</summary>
public sealed record ResolutionResult(MatchOutcome Outcome, LedgerAssetId? AssetId, Guid? MergeCaseId);

/// <summary>
/// The C1 entity-resolution engine (Stage 4 §2.3 / Stage 7 §resolution, ADR-012). A stateless domain
/// service — not an aggregate. It consumes an observation descriptor (reconstructed from the C4
/// <c>ObservationIngested</c> event), looks up candidates on the alias graph, applies the (pluggable,
/// PoC-gated) confidence rule, and either auto-links (High only), creates a new asset (no match), or opens
/// a manual merge case (collision or too-weak). It never modifies provider observations; it only points
/// links at them, and links are severed/superseded, never deleted. Idempotent: replaying the same
/// observation neither double-links nor opens duplicate cases.
/// </summary>
public sealed class EntityResolutionService(
    IAssetRepository repository,
    IAssetLedgerReadModel readModel,
    IMergeCaseStore mergeCases,
    IEventStore events,
    IMatchClassifier classifier,
    ITenantContextAccessor tenants,
    TaxonomyScheme taxonomy)
{
    private static readonly AuditActor ResolutionActor =
        AuditActor.System("entity-resolution");

    public async Task<ResolutionResult> ResolveAsync(ObservationDescriptor descriptor, CancellationToken ct = default)
    {
        var observationCorrelation = RequiredReference(
            "provider-observation",
            descriptor.ObservationId);
        ValidateContext(
            EventReference.For(
                "ai-asset",
                Guid.NewGuid()),
            ResolutionActor,
            reason: null,
            observationCorrelation);

        var primary = descriptor.PrimaryIdentifier;
        var idSet = NativeIdentifierSet.Of(primary);
        var candidates = await repository.FindByNativeIdentifierAsync(primary, ct);

        // Idempotent replay: this observation is already linked to a matched asset — no-op.
        var alreadyLinked = candidates.FirstOrDefault(c => c.IsLinkedToObservation(descriptor.ObservationId));
        if (alreadyLinked is not null)
            return new ResolutionResult(MatchOutcome.AutoLink, alreadyLinked.Id, null);

        var decision = classifier.Classify(primary, candidates);

        // Auto-link is permitted ONLY for High confidence; anything weaker goes to the manual queue.
        if (decision.Outcome == MatchOutcome.AutoLink && decision.Confidence != MatchConfidence.High)
            decision = decision with { Outcome = MatchOutcome.Review };

        switch (decision.Outcome)
        {
            case MatchOutcome.NoMatch:
                {
                    var asset = AIAsset.Discover(tenants.Current, descriptor.DisplayName, TypeOrDefault(descriptor.AssetType), taxonomy);
                    asset.AddResolutionLink(idSet, decision.Method, decision.Confidence, ResolutionActor, descriptor.ObservationId);
                    await PersistAssetAsync(
                        asset,
                        ResolutionActor,
                        observationCorrelation,
                        ct);
                    return new ResolutionResult(MatchOutcome.NoMatch, asset.Id, null);
                }

            case MatchOutcome.AutoLink:
                {
                    var asset = await repository.GetAsync(decision.Target!.Value, ct)
                        ?? throw new DomainException("Matched asset not found in this tenant.");
                    asset.AddResolutionLink(idSet, decision.Method, decision.Confidence, ResolutionActor, descriptor.ObservationId);
                    await PersistAssetAsync(
                        asset,
                        ResolutionActor,
                        observationCorrelation,
                        ct);
                    return new ResolutionResult(MatchOutcome.AutoLink, asset.Id, null);
                }

            default: // Review (too weak) or Collision — never auto-link; open a manual merge case.
                {
                    var existing = await mergeCases.FindOpenForAsync(primary, descriptor.ObservationId, ct);
                    if (existing is not null)
                        return new ResolutionResult(decision.Outcome, null, existing.Id); // idempotent

                    var mergeCase = MergeCase.Open(tenants.Current, decision.Reason, decision.Confidence, idSet, decision.Candidates, descriptor.ObservationId);
                    var @event = new MergeCaseOpened
                    {
                        AssetId = decision.Candidates.Count > 0 ? decision.Candidates[0] : default,
                        MergeCaseId = mergeCase.Id,
                        Reason = decision.Reason,
                        Confidence = decision.Confidence,
                    };
                    var prepared = PrepareEvent(
                        @event,
                        ResolutionActor,
                        observationCorrelation);

                    await mergeCases.SaveAsync(mergeCase, ct);
                    await AppendAsync(prepared, ct);
                    return new ResolutionResult(decision.Outcome, null, mergeCase.Id);
                }
        }
    }

    /// <summary>Operator-approved (Manual) link — the resolution of ambiguity by a human.</summary>
    public async Task<LedgerAssetId> ApproveManualLinkAsync(LedgerAssetId assetId, NativeIdentifierSet identifiers, Guid? observationRef, AuditActor by, CancellationToken ct = default)
    {
        var correlation = observationRef is { } observation
            ? RequiredReference(
                "provider-observation",
                observation)
            : (EventReference?)null;
        ValidateContext(
            RequiredReference(
                "ai-asset",
                assetId.Value),
            by,
            reason: null,
            correlation);

        var asset = await LoadAsync(assetId, ct);
        asset.AddResolutionLink(identifiers, MatchMethod.Manual, MatchConfidence.Manual, by, observationRef);
        await PersistAssetAsync(
            asset,
            by,
            correlation,
            ct);
        return asset.Id;
    }

    /// <summary>Merge <paramref name="sourceId"/> into <paramref name="targetId"/>: source links are superseded onto the target; observations are untouched.</summary>
    public async Task MergeAsync(LedgerAssetId targetId, LedgerAssetId sourceId, AuditActor by, CancellationToken ct = default)
    {
        if (targetId.Equals(sourceId)) throw new DomainException("Cannot merge an asset into itself.");
        var targetReference = RequiredReference(
            "ai-asset",
            targetId.Value);
        var sourceReference = RequiredReference(
            "ai-asset",
            sourceId.Value);
        ValidateContext(
            targetReference,
            by,
            reason: null,
            sourceReference);
        ValidateContext(
            sourceReference,
            by,
            reason: null,
            targetReference);

        var target = await LoadAsync(targetId, ct);
        var source = await LoadAsync(sourceId, ct);

        foreach (var link in source.ActiveResolutionLinks.ToList())
        {
            var superseding = target.AddResolutionLink(link.Identifiers, link.Method, link.Confidence, by, link.ObservationRef);
            source.SupersedeResolutionLink(link.Id, superseding.Id, by);
        }

        source.MarkMergedInto(target.Id, by);
        await PersistAssetAsync(
            target,
            by,
            sourceReference,
            ct);
        await PersistAssetAsync(
            source,
            by,
            targetReference,
            ct);
    }

    /// <summary>Split the given links out of <paramref name="assetId"/> into a new asset; observations are untouched.</summary>
    public async Task<LedgerAssetId> SplitAsync(LedgerAssetId assetId, IReadOnlyList<Guid> linkIds, string newDisplayName, string newAssetType, AuditActor by, CancellationToken ct = default)
    {
        var assetReference = RequiredReference(
            "ai-asset",
            assetId.Value);
        ValidateContext(
            assetReference,
            by,
            reason: null,
            correlation: null);

        var asset = await LoadAsync(assetId, ct);
        var toMove = asset.ActiveResolutionLinks.Where(l => linkIds.Contains(l.Id)).ToList();
        if (toMove.Count == 0) throw new DomainException("No active links match the split selection.");

        var newAsset = AIAsset.Discover(tenants.Current, newDisplayName, TypeOrDefault(newAssetType), taxonomy);
        foreach (var link in toMove)
        {
            var moved = newAsset.AddResolutionLink(link.Identifiers, link.Method, link.Confidence, by, link.ObservationRef);
            asset.SupersedeResolutionLink(link.Id, moved.Id, by);
        }

        asset.RecordSplit(newAsset.Id, by);
        await PersistAssetAsync(
            newAsset,
            by,
            assetReference,
            ct);
        await PersistAssetAsync(
            asset,
            by,
            RequiredReference(
                "ai-asset",
                newAsset.Id.Value),
            ct);
        return newAsset.Id;
    }

    public async Task ResolveMergeCaseAsync(Guid mergeCaseId, string outcome, AuditActor by, CancellationToken ct = default)
    {
        var mergeCase = await mergeCases.GetAsync(mergeCaseId, ct) ?? throw new DomainException("Merge case not found in this tenant.");
        var @event = new MergeCaseResolved
        {
            AssetId = mergeCase.Candidates.Count > 0 ? mergeCase.Candidates[0] : default,
            MergeCaseId = mergeCase.Id,
            Outcome = outcome,
            By = by,
        };
        var prepared = PrepareEvent(
            @event,
            by,
            correlation: null);

        mergeCase.Resolve(outcome, by);
        await mergeCases.SaveAsync(mergeCase, ct);
        await AppendAsync(prepared, ct);
    }

    private AssetType TypeOrDefault(string raw)
    {
        if (!string.IsNullOrWhiteSpace(raw))
        {
            var type = new AssetType(raw);
            if (taxonomy.IsValid(type)) return type;
        }

        return new AssetType("external-ai-service"); // generic catch-all; provider-agnostic
    }

    private async Task<AIAsset> LoadAsync(LedgerAssetId id, CancellationToken ct) =>
        await repository.GetAsync(id, ct) ?? throw new DomainException($"Asset {id} not found in this tenant.");

    private async Task PersistAssetAsync(
        AIAsset asset,
        AuditActor actor,
        EventReference? correlation,
        CancellationToken ct)
    {
        var prepared = asset
            .DequeueEvents()
            .Select(domainEvent =>
                PrepareEvent(
                    domainEvent,
                    actor,
                    correlation))
            .ToList();

        await repository.SaveAsync(asset, ct);
        foreach (var pending in prepared)
            await AppendAsync(pending, ct);
        await readModel.ProjectAsync(asset, ct);
    }

    private static PreparedEvent PrepareEvent(
        LedgerEvent domainEvent,
        AuditActor actor,
        EventReference? correlation)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(domainEvent, domainEvent.GetType());
        var aggregate = domainEvent switch
        {
            MergeCaseOpened opened =>
                EventReference.For(
                    "merge-case",
                    opened.MergeCaseId),
            MergeCaseResolved resolved =>
                EventReference.For(
                    "merge-case",
                    resolved.MergeCaseId),
            _ when domainEvent.AssetId.Value != Guid.Empty =>
                EventReference.For(
                    "ai-asset",
                    domainEvent.AssetId.Value),
            _ => throw new DomainException(
                "The ledger event has no aggregate reference."),
        };
        var reason = domainEvent switch
        {
            AssetRejected rejected => rejected.Reason,
            ResolutionLinkSevered severed =>
                severed.Reason,
            MergeCaseOpened opened => opened.Reason,
            MergeCaseResolved resolved =>
                resolved.Outcome,
            _ => null,
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
            throw new DomainException(
                "The entity-resolution evidence context is invalid.");
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

    private static EventReference RequiredReference(
        string kind,
        Guid value)
    {
        try
        {
            return EventReference.For(kind, value);
        }
        catch (ArgumentException)
        {
            throw new DomainException(
                "The entity-resolution evidence context is invalid.");
        }
    }

    private static void ValidateContext(
        EventReference aggregate,
        AuditActor actor,
        string? reason,
        EventReference? correlation)
    {
        try
        {
            _ = new EventAppendMetadata(
                aggregate,
                actor,
                reason,
                correlation);
        }
        catch (ArgumentException)
        {
            throw new DomainException(
                "The entity-resolution evidence context is invalid.");
        }
    }

    private sealed record PreparedEvent(
        LedgerEvent Event,
        EventAppendMetadata Metadata,
        byte[] Payload);
}
