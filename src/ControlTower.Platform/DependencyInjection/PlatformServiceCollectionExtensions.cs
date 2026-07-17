using ControlTower.Platform.Tenancy;
using Microsoft.Extensions.DependencyInjection;

namespace ControlTower.Platform.DependencyInjection;

/// <summary>Registers the platform shared-kernel services (production-safe; no infrastructure).</summary>
public static class PlatformServiceCollectionExtensions
{
    public static IServiceCollection AddControlTowerPlatform(this IServiceCollection services)
    {
        services.AddSingleton<ITenantContextAccessor, TenantContextAccessor>();
        return services;
    }
}
