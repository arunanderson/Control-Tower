using System.Text.Json;
using ControlTower.Modules.Ledger.Domain;
using ControlTower.Platform.Events;
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
    public async Task<LedgerAssetId> DiscoverAsync(string displayName, AssetType type, CancellationToken ct = default)
    {
        Require(LedgerCapability.TriageAssets);
        var asset = AIAsset.Discover(tenants.Current, displayName, type, taxonomy);
        await PersistAsync(asset, ct);
        return asset.Id;
    }

    public async Task TriageAsync(LedgerAssetId id, CancellationToken ct = default)
    {
        Require(LedgerCapability.TriageAssets);
        var asset = await LoadAsync(id, ct);
        asset.Triage();
        await PersistAsync(asset, ct);
    }

    public async Task RegisterAsync(LedgerAssetId id, string businessPurpose, PersonRef owner, CancellationToken ct = default)
    {
        Require(LedgerCapability.RegisterAssets);
        var asset = await LoadAsync(id, ct);
        asset.Register(businessPurpose, owner);
        await PersistAsync(asset, ct);
    }

    public async Task RetireAsync(LedgerAssetId id, CancellationToken ct = default)
    {
        Require(LedgerCapability.RetireAssets);
        var asset = await LoadAsync(id, ct);
        asset.Retire();
        await PersistAsync(asset, ct);
    }

    private async Task<AIAsset> LoadAsync(LedgerAssetId id, CancellationToken ct) =>
        await repository.GetAsync(id, ct) ?? throw new DomainException($"Asset {id} not found in this tenant.");

    private async Task PersistAsync(AIAsset asset, CancellationToken ct)
    {
        await repository.SaveAsync(asset, ct);
        foreach (var domainEvent in asset.DequeueEvents())
        {
            var payload = JsonSerializer.SerializeToUtf8Bytes(domainEvent, domainEvent.GetType());
            await events.AppendAsync(domainEvent, payload, ct);
        }

        await readModel.ProjectAsync(asset, ct);
    }

    private void Require(LedgerCapability capability)
    {
        if (!authorizer.IsAllowed(capability))
            throw new UnauthorizedAccessException($"Missing capability: {capability}.");
    }
}
