using ControlTower.Modules.Providers.Domain;

namespace ControlTower.Modules.Providers.Application;

public sealed record ProviderDiagnosticReport(string SurfaceId, bool ContractValid, IReadOnlyList<string> Errors);

/// <summary>Diagnostics over the registered providers: contract validation + health checks.</summary>
public sealed class ProviderDiagnostics(IProviderRegistry registry)
{
    public IReadOnlyList<ProviderDiagnosticReport> ValidateAll() =>
        registry.All()
            .Select(p =>
            {
                var result = ProviderContractValidator.ValidateProvider(p);
                return new ProviderDiagnosticReport(p.Manifest.SurfaceId, result.IsValid, result.Errors);
            })
            .ToList();

    public async Task<ProviderHealth> CheckHealthAsync(string surfaceId, ProviderConnectionContext context, CancellationToken ct = default)
    {
        var provider = registry.Resolve(surfaceId)
            ?? throw new ProviderException($"No provider registered for surface '{surfaceId}'.");
        return await provider.CheckHealthAsync(context, ct);
    }
}
