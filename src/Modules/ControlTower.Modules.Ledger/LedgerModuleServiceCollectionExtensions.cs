using ControlTower.Modules.Ledger.Application;
using ControlTower.Modules.Ledger.Domain;
using ControlTower.Modules.Ledger.Infrastructure;
using ControlTower.Platform.Events;
using Microsoft.Extensions.DependencyInjection;

namespace ControlTower.Modules.Ledger;

/// <summary>
/// Registers the C1 Asset Ledger. In-memory repository/read model and the permissive authorizer are
/// DEV-001 dev substitutes (behind ports); production swaps the PostgreSQL repository/projection and
/// the C8.2 role-based authorizer by configuration.
/// </summary>
public static class LedgerModuleServiceCollectionExtensions
{
    public static IServiceCollection AddLedgerModule(this IServiceCollection services)
    {
        services.AddSingleton(TaxonomyScheme.Default);
        services.AddSingleton<IAssetRepository, InMemoryAssetRepository>();
        services.AddSingleton<IAssetLedgerReadModel, InMemoryAssetLedgerReadModel>();
        services.AddSingleton<ICoverageReadModel, InMemoryCoverageReadModel>();
        services.AddSingleton<ILedgerAuthorizer, AllowAllLedgerAuthorizer>();
        services.AddSingleton<IMergeCaseStore, InMemoryMergeCaseStore>();
        services.AddSingleton<IMatchClassifier, DeterministicMatchClassifier>();
        services.AddScoped<AssetRegistrationService>();
        services.AddScoped<EntityResolutionService>();
        services.AddScoped<ResolutionWorkbenchReadModel>();
        // The C4→C1 seam: registered as an integration-event handler so the host (with an outbox
        // dispatcher) delivers ObservationIngested to resolution. Providers is never referenced.
        services.AddScoped<IIntegrationEventHandler, ObservationIngestedHandler>();
        services.AddScoped<IIntegrationEventHandler, ProviderCoverageUpdatedHandler>();
        return services;
    }
}
