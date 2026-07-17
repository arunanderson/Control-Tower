using ControlTower.Modules.Ledger.Domain;

namespace ControlTower.Modules.Ledger.Application;

/// <summary>
/// Tenant-scoped persistence port for the ledger. Implementations resolve the tenant from the ambient
/// tenant context (ADR-021) — callers never pass a tenant, and cross-tenant access is impossible by
/// construction. Production: Azure Database for PostgreSQL with RLS (DEC-001); dev: in-memory.
/// </summary>
public interface IAssetRepository
{
    Task<AIAsset?> GetAsync(LedgerAssetId id, CancellationToken ct = default);
    Task SaveAsync(AIAsset asset, CancellationToken ct = default);
    Task<IReadOnlyList<AIAsset>> ListAsync(CancellationToken ct = default);
}

/// <summary>The polymorphic ledger view row (the C7 read model; one shape for all asset types).</summary>
public sealed record AssetLedgerView
{
    public required Guid AssetId { get; init; }
    public required string DisplayName { get; init; }
    public required string AssetType { get; init; }
    public required string RegistrationStatus { get; init; }
    public required string OperationalLifecycleState { get; init; }
    public required string MatchConfidence { get; init; }
    public required bool IsOwnerless { get; init; }
    public string? OwnerDisplayName { get; init; }
    public string? BusinessPurpose { get; init; }
    public required int ResolutionLinkCount { get; init; }
}

/// <summary>Tenant-scoped read model for the ledger (disposable projection, Stage 7 §5).</summary>
public interface IAssetLedgerReadModel
{
    Task ProjectAsync(AIAsset asset, CancellationToken ct = default);
    Task<IReadOnlyList<AssetLedgerView>> QueryAsync(CancellationToken ct = default);
    Task<AssetLedgerView?> GetAsync(LedgerAssetId id, CancellationToken ct = default);
}

/// <summary>Capabilities gating ledger operations (foundation for the C8.2 role model).</summary>
public enum LedgerCapability
{
    TriageAssets,
    RegisterAssets,
    RetireAssets,
}

/// <summary>
/// Authorization seam for ledger operations. A thin foundation replaced by the C8.2 delegated role
/// model later; here it lets the workflow enforce capability checks and be tested.
/// </summary>
public interface ILedgerAuthorizer
{
    bool IsAllowed(LedgerCapability capability);
}
