using ControlTower.Modules.Ledger.Domain;

namespace ControlTower.Modules.Ledger.Application;

/// <summary>A native identifier as shown in the workbench.</summary>
public sealed record IdentifierView(string System, string IdentifierType, string Value);

/// <summary>An identity alias on the graph (provider-scoped, with provenance).</summary>
public sealed record AliasView(string System, string IdentifierType, string Value, string Provenance);

/// <summary>One resolution link — active or closed — with the fields the workbench needs for stewardship + audit.</summary>
public sealed record ResolutionLinkView
{
    public required Guid LinkId { get; init; }
    public required IReadOnlyList<IdentifierView> Identifiers { get; init; }
    public required string Method { get; init; }
    public required string Confidence { get; init; }
    public required string Status { get; init; }
    public Guid? ObservationRef { get; init; }
    public required string LinkedBy { get; init; }
    public required DateTimeOffset LinkedAt { get; init; }
    public Guid? SupersededByLinkId { get; init; }
}

/// <summary>An asset's resolution slice: its rolled-up confidence, its alias graph, and its full link history.</summary>
public sealed record AssetResolutionView
{
    public required Guid AssetId { get; init; }
    public required string DisplayName { get; init; }
    public required string MatchConfidence { get; init; }
    public required IReadOnlyList<AliasView> Aliases { get; init; }
    public required IReadOnlyList<ResolutionLinkView> Links { get; init; }
}

/// <summary>An open manual-merge-queue item.</summary>
public sealed record MergeCaseView
{
    public required Guid MergeCaseId { get; init; }
    public required string Reason { get; init; }
    public required string Confidence { get; init; }
    public required IReadOnlyList<IdentifierView> Identifiers { get; init; }
    public required IReadOnlyList<Guid> CandidateAssetIds { get; init; }
    public Guid? ObservationRef { get; init; }
    public required DateTimeOffset OpenedAt { get; init; }
}

/// <summary>
/// Read-model-only projection for the Resolution &amp; Merge Workbench (C7/I4). It reads the tenant's
/// assets and merge cases and shapes them into view DTOs — no calculation, no domain objects leave the
/// module, no writes. Operator actions go through <see cref="EntityResolutionService"/>, not here.
/// </summary>
public sealed class ResolutionWorkbenchReadModel(IAssetRepository assets, IMergeCaseStore mergeCases)
{
    public async Task<IReadOnlyList<MergeCaseView>> OpenMergeCasesAsync(CancellationToken ct = default)
    {
        var open = await mergeCases.OpenCasesAsync(ct);
        return open.Select(c => new MergeCaseView
        {
            MergeCaseId = c.Id,
            Reason = c.Reason,
            Confidence = c.Confidence.ToString(),
            Identifiers = c.Identifiers.Identifiers.Select(i => new IdentifierView(i.System, i.IdentifierType, i.Value)).ToList(),
            CandidateAssetIds = c.Candidates.Select(a => a.Value).ToList(),
            ObservationRef = c.ObservationRef,
            OpenedAt = c.OpenedAt,
        }).ToList();
    }

    public async Task<AssetResolutionView?> AssetResolutionAsync(LedgerAssetId assetId, CancellationToken ct = default)
    {
        var asset = await assets.GetAsync(assetId, ct);
        if (asset is null) return null;

        return new AssetResolutionView
        {
            AssetId = asset.Id.Value,
            DisplayName = asset.DisplayName,
            MatchConfidence = asset.MatchConfidence.ToString(),
            Aliases = asset.Aliases.Select(a => new AliasView(a.System, a.IdentifierType, a.Value, a.Provenance.ToString())).ToList(),
            Links = asset.ResolutionLinks.Select(l => new ResolutionLinkView
            {
                LinkId = l.Id,
                Identifiers = l.Identifiers.Identifiers.Select(i => new IdentifierView(i.System, i.IdentifierType, i.Value)).ToList(),
                Method = l.Method.ToString(),
                Confidence = l.Confidence.ToString(),
                Status = l.Status.ToString(),
                ObservationRef = l.ObservationRef,
                LinkedBy = l.LinkedBy,
                LinkedAt = l.LinkedAt,
                SupersededByLinkId = l.SupersededByLinkId,
            }).ToList(),
        };
    }
}
