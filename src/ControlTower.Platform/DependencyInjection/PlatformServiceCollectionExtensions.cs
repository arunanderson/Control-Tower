using ControlTower.Platform.Events;
using ControlTower.Platform.Tenancy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ControlTower.Platform.DependencyInjection;

/// <summary>Registers the platform shared-kernel services (production-safe; no infrastructure).</summary>
public static class PlatformServiceCollectionExtensions
{
    public static IServiceCollection AddControlTowerPlatform(this IServiceCollection services)
    {
        services.AddSingleton<ITenantContextAccessor, TenantContextAccessor>();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IHashChain, Sha256HashChain>();
        return services;
    }
}
