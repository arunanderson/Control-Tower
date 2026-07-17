using ControlTower.Platform.Audit;
using ControlTower.Platform.Events;
using ControlTower.Platform.Ports;
using Microsoft.Extensions.DependencyInjection;

namespace ControlTower.Adapters.InMemory;

/// <summary>
/// DEV-ONLY (DEV-001). Registers in-memory implementations of the platform ports for local
/// development and tests. Must never be called from a production composition — the
/// production-readiness CI gate + the dev-substitute registry guard this.
/// </summary>
public static class InMemoryAdapterServiceCollectionExtensions
{
    public static IServiceCollection AddInMemoryAdapters(this IServiceCollection services)
    {
        services.AddSingleton<IEventStore, InMemoryEventStore>();
        services.AddSingleton<IOutbox, InMemoryOutbox>();
        services.AddSingleton<IPrivilegedReadAuditor, InMemoryPrivilegedReadAuditor>();
        services.AddSingleton<ISecretProvider>(_ => new InMemorySecretProvider());
        return services;
    }
}
