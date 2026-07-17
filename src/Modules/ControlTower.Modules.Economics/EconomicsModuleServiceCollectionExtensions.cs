using ControlTower.Modules.Economics.Application;
using ControlTower.Modules.Economics.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace ControlTower.Modules.Economics;

/// <summary>
/// Registers C3 Cost &amp; Value Intelligence. One semantic model (cost/usage/value) with an ingestion
/// service and a projection service that produces every read model — asset, agent, department,
/// business unit, portfolio, executive. No separate Agent/Department/Portfolio ROI modules. The
/// in-memory store is a DEV-001 dev substitute behind the port; production is Azure PostgreSQL.
/// </summary>
public static class EconomicsModuleServiceCollectionExtensions
{
    public static IServiceCollection AddEconomicsModule(this IServiceCollection services)
    {
        services.AddSingleton<IEconomicsStore, InMemoryEconomicsStore>();
        services.AddScoped<EconomicsIngestionService>();
        services.AddScoped<EconomicsProjectionService>();
        return services;
    }
}
