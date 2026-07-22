using ControlTower.Modules.Providers.Application;
using ControlTower.Modules.Providers.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using ControlTower.Platform.Events;

namespace ControlTower.Modules.Providers;

/// <summary>
/// Registers the C4 provider framework (ADR-007). The registry admits providers only after C4.5 contract
/// validation; the manual CSV provider (ADR-013) is registered as an ordinary provider. Microsoft (and
/// any other) providers register the same way, behind the same contracts, when built.
/// </summary>
public static class ProvidersModuleServiceCollectionExtensions
{
    public static IServiceCollection AddProviderFramework(this IServiceCollection services)
    {
        services.AddSingleton<IProviderRegistry>(_ =>
        {
            var registry = new ProviderRegistry();
            registry.Register(new CsvManualImportProvider());
            return registry;
        });
        services.AddSingleton<IWatermarkStore, InMemoryWatermarkStore>();
        services.AddSingleton<IObservationStore, InMemoryObservationStore>();
        services.AddSingleton<IProviderConnectionStore, InMemoryProviderConnectionStore>();
        services.AddSingleton<IProviderJobReceiptStore, InMemoryProviderJobReceiptStore>();
        services.AddSingleton<ProviderDiagnostics>();
        services.AddScoped<ObservationIngestionService>();
        services.AddScoped<ProviderSweepRequestService>();
        services.AddScoped<IIntegrationEventHandler, ProviderSweepRequestedHandler>();
        return services;
    }
}
