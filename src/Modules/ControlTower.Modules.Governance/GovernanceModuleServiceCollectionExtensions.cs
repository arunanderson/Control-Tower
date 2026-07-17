using ControlTower.Modules.Governance.Application;
using ControlTower.Modules.Governance.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace ControlTower.Modules.Governance;

/// <summary>
/// Registers C2 Governance Orchestration. Cases trigger — never duplicate — Ledger transitions; native
/// controls are recorded as intents only (no enforcement); notifications are domain intents. The
/// in-memory store is a DEV-001 dev substitute behind the port; production is Azure PostgreSQL.
/// </summary>
public static class GovernanceModuleServiceCollectionExtensions
{
    public static IServiceCollection AddGovernanceModule(this IServiceCollection services)
    {
        services.AddSingleton<IGovernanceStore, InMemoryGovernanceStore>();
        services.AddSingleton<INativeControlOrchestrator, RecordingNativeControlOrchestrator>();
        services.AddScoped<GovernanceService>();
        return services;
    }
}
