using ControlTower.Modules.Providers.Application;
using ControlTower.Modules.Providers.Domain;

namespace ControlTower.Modules.Providers.Infrastructure;

/// <summary>
/// In-process provider registry. Providers are validated against the C4.5 contract on registration and
/// keyed by surface id — the same admission path for every provider. This is process-level (providers
/// are types registered at composition); per-tenant connections are a separate concern.
/// </summary>
public sealed class ProviderRegistry : IProviderRegistry
{
    private readonly Dictionary<string, IProvider> _providers = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();

    public void Register(IProvider provider)
    {
        var validation = ProviderContractValidator.ValidateProvider(provider);
        if (!validation.IsValid)
            throw new ProviderException($"Provider '{provider.Manifest.SurfaceId}' failed contract validation: {string.Join("; ", validation.Errors)}");

        lock (_gate)
        {
            if (!_providers.TryAdd(provider.Manifest.SurfaceId, provider))
                throw new ProviderException($"Provider '{provider.Manifest.SurfaceId}' is already registered.");
        }
    }

    public IProvider? Resolve(string surfaceId)
    {
        lock (_gate)
        {
            _providers.TryGetValue(surfaceId, out var provider);
            return provider;
        }
    }

    public IReadOnlyList<ProviderManifest> Discover()
    {
        lock (_gate) return _providers.Values.Select(p => p.Manifest).ToList();
    }

    public IReadOnlyList<IProvider> All()
    {
        lock (_gate) return _providers.Values.ToList();
    }
}
