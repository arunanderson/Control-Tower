using System.Text.Json;
using ControlTower.Modules.Ledger.Domain;
using ControlTower.Platform.Events;
using ControlTower.Platform.Identity;
using ControlTower.Platform.Tenancy;

namespace ControlTower.Modules.Ledger.Application;

/// <summary>
/// The asset registration workflow (Stage 6 J1, "Discovered → Governed"). Applies authorization,
/// mutates the aggregate through guarded commands, appends the emitted domain events to the immutable
/// event stream (ADR-015), and updates the read model — all within the ambient tenant scope.
/// </summary>
public sealed class AssetRegistrationService(
    IAssetRepository repository,
    IAssetLedgerReadModel readModel,
    IEventStore events,
    ILedgerAuthorizer authorizer,
    ITenantContextAccessor tenants,
    TaxonomyScheme taxonomy)
{
    public async Task<LedgerAssetId> DiscoverAsync(
        string displayName,
        AssetType type,
        AuditActor actor,
        EventReference? correlation = null,
        CancellationToken ct = default)
    {
        Require(LedgerCapability.TriageAssets);
        RequireActor(actor);
        var asset = AIAsset.Discover(tenants.Current, displayName, type, taxonomy);
        ValidateContext(
            asset.Id,
            actor,
            correlation);
        await PersistAsync(asset, actor, correlation, ct);
        return asset.Id;
    }

    public async Task TriageAsync(
        LedgerAssetId id,
        AuditActor actor,
        EventReference? correlation = null,
        CancellationToken ct = default)
    {
        Require(LedgerCapability.TriageAssets);
        RequireActor(actor);
        ValidateContext(
            id,
            actor,
            correlation);
        var asset = await LoadAsync(id, ct);
        asset.Triage();
        await PersistAsync(asset, actor, correlation, ct);
    }

    public async Task RegisterAsync(
        LedgerAssetId id,
        string businessPurpose,
        PersonRef owner,
        AuditActor actor,
        EventReference? correlation = null,
        CancellationToken ct = default)
    {
        Require(LedgerCapability.RegisterAssets);
        RequireActor(actor);
        ValidateContext(
            id,
            actor,
            correlation);
        var asset = await LoadAsync(id, ct);
        asset.Register(businessPurpose, owner);
        await PersistAsync(asset, actor, correlation, ct);
    }

    public async Task RetireAsync(
        LedgerAssetId id,
        AuditActor actor,
        EventReference? correlation = null,
        CancellationToken ct = default)
    {
        Require(LedgerCapability.RetireAssets);
        RequireActor(actor);
        ValidateContext(
            id,
            actor,
            correlation);
        var asset = await LoadAsync(id, ct);
        asset.Retire();
        await PersistAsync(asset, actor, correlation, ct);
    }

    private async Task<AIAsset> LoadAsync(LedgerAssetId id, CancellationToken ct) =>
        await repository.GetAsync(id, ct) ?? throw new DomainException($"Asset {id} not found in this tenant.");

    private async Task PersistAsync(
        AIAsset asset,
        AuditActor actor,
        EventReference? correlation,
        CancellationToken ct)
    {
        var prepared = asset
            .DequeueEvents()
            .Select(domainEvent =>
            {
                var payload =
                    JsonSerializer.SerializeToUtf8Bytes(
                        domainEvent,
                        domainEvent.GetType());
                try
                {
                    return new PreparedEvent(
                        domainEvent,
                        new EventAppendMetadata(
                            EventReference.For(
                                "ai-asset",
                                domainEvent.AssetId.Value),
                            actor,
                            domainEvent is AssetRejected rejected
                                ? rejected.Reason
                                : null,
                            correlation),
                        payload);
                }
                catch (ArgumentException)
                {
                    throw new DomainException(
                        "The asset evidence context is invalid.");
                }
            })
            .ToList();

        await repository.SaveAsync(asset, ct);
        foreach (var pending in prepared)
        {
            await events.AppendAsync(
                pending.Event,
                pending.Metadata,
                pending.Payload,
                ct);
        }

        await readModel.ProjectAsync(asset, ct);
    }

    private void Require(LedgerCapability capability)
    {
        if (!authorizer.IsAllowed(capability))
            throw new UnauthorizedAccessException($"Missing capability: {capability}.");
    }

    private static void RequireActor(AuditActor actor)
    {
        if (!actor.IsValid)
            throw new DomainException("An audit actor is required.");
    }

    private static void ValidateContext(
        LedgerAssetId id,
        AuditActor actor,
        EventReference? correlation)
    {
        try
        {
            _ = new EventAppendMetadata(
                EventReference.For(
                    "ai-asset",
                    id.Value),
                actor,
                reason: null,
                correlation);
        }
        catch (ArgumentException)
        {
            throw new DomainException(
                "The asset evidence context is invalid.");
        }
    }

    private sealed record PreparedEvent(
        LedgerEvent Event,
        EventAppendMetadata Metadata,
        byte[] Payload);
}
