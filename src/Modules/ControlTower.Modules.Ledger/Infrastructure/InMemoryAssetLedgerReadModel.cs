using ControlTower.Modules.Ledger.Application;
using ControlTower.Modules.Ledger.Domain;
using ControlTower.Platform.Tenancy;

namespace ControlTower.Modules.Ledger.Infrastructure;

/// <summary>DEV-ONLY (DEV-001) tenant-partitioned in-memory ledger read model. Production: a PostgreSQL projection.</summary>
public sealed class InMemoryAssetLedgerReadModel(ITenantContextAccessor tenants) : IAssetLedgerReadModel
{
    private readonly Dictionary<TenantId, Dictionary<LedgerAssetId, AssetLedgerView>> _byTenant = [];
    private readonly object _gate = new();

    public Task ProjectAsync(AIAsset asset, CancellationToken ct = default)
    {
        var tenant = tenants.Current;
        // Display identity lives only behind E19 and cannot enter this general L1 read model.
        var hasOwner = asset.Ownerships.Any(
            assignment =>
                assignment.IsCurrent
                && assignment.Role == OwnershipRole.Owner);
        var view = new AssetLedgerView
        {
            AssetId = asset.Id.Value,
            DisplayName = asset.DisplayName,
            AssetType = asset.Type.Value,
            RegistrationStatus = asset.RegistrationStatus.ToString(),
            OperationalLifecycleState = asset.OperationalLifecycleState.ToString(),
            MatchConfidence = asset.MatchConfidence.ToString(),
            IsOwnerless = asset.IsOwnerless,
            OwnerDisplayName = hasOwner ? "Assigned" : null,
            BusinessPurpose = asset.BusinessPurpose,
            ResolutionLinkCount = asset.ActiveResolutionLinks.Count, // honest: severed/superseded links don't count
        };
        lock (_gate)
        {
            Bucket(tenant)[asset.Id] = view;
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<AssetLedgerView>> QueryAsync(CancellationToken ct = default)
    {
        var tenant = tenants.Current;
        lock (_gate)
        {
            IReadOnlyList<AssetLedgerView> result = Bucket(tenant).Values.ToList();
            return Task.FromResult(result);
        }
    }

    public Task<AssetLedgerView?> GetAsync(LedgerAssetId id, CancellationToken ct = default)
    {
        var tenant = tenants.Current;
        lock (_gate)
        {
            Bucket(tenant).TryGetValue(id, out var view);
            return Task.FromResult(view);
        }
    }

    private Dictionary<LedgerAssetId, AssetLedgerView> Bucket(TenantId tenant) =>
        _byTenant.TryGetValue(tenant, out var bucket) ? bucket : _byTenant[tenant] = [];
}
