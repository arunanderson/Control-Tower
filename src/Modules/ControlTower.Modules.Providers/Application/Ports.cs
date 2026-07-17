using ControlTower.Modules.Providers.Domain;

namespace ControlTower.Modules.Providers.Application;

/// <summary>
/// The provider registry — the plug-in point (ADR-007). Providers register their implementation +
/// manifest; the platform discovers and resolves them. Adding a provider never changes the domain model.
/// </summary>
public interface IProviderRegistry
{
    void Register(IProvider provider);
    IProvider? Resolve(string surfaceId);
    IReadOnlyList<ProviderManifest> Discover();
    IReadOnlyList<IProvider> All();
}

/// <summary>Sync scheduling abstraction — cadence derives from the provider's declared freshness (Stage 7 §4).</summary>
public sealed record SyncSchedule(string SurfaceId, TimeSpan Cadence)
{
    public static SyncSchedule ForManifest(ProviderManifest manifest) => new(manifest.SurfaceId, manifest.FreshnessExpectation);
}

/// <summary>Watermark store for idempotent, resumable sweeps (Stage 7 §4). Production: PostgreSQL; dev: in-memory.</summary>
public interface IWatermarkStore
{
    Task<string?> GetAsync(string connectionId, CancellationToken ct = default);
    Task SetAsync(string connectionId, string watermark, CancellationToken ct = default);
}
