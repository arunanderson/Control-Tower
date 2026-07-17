using ControlTower.Modules.Providers.Domain;

namespace ControlTower.Modules.Providers.Application;

public sealed record HarnessResult(bool Passed, IReadOnlyList<string> Failures);

/// <summary>
/// The provider integration test framework: runs ANY provider through the identical contract-conformance
/// suite. This is what makes "treat every provider equally" real — Microsoft, CSV, OpenAI or a future
/// provider all pass (or fail) the same checks, with no provider-specific logic here.
/// </summary>
public static class ProviderTestHarness
{
    public static async Task<HarnessResult> RunAsync(IProvider provider, ProviderConnectionContext context, CancellationToken ct = default)
    {
        var failures = new List<string>();

        var contract = ProviderContractValidator.ValidateProvider(provider);
        if (!contract.IsValid) failures.AddRange(contract.Errors.Select(e => $"contract: {e}"));

        // Health must return without throwing.
        try
        {
            _ = await provider.CheckHealthAsync(context, ct);
        }
        catch (Exception ex)
        {
            failures.Add($"health check threw: {ex.Message}");
        }

        var declaredTypes = provider.Manifest.NativeIdentifierTypes.ToHashSet();

        foreach (var capability in provider.Manifest.Capabilities)
        {
            if (!provider.Supports(capability))
                failures.Add($"manifest declares {capability} but Supports() is false");

            try
            {
                await foreach (var observation in provider.AcquireAsync(context, capability, ct))
                {
                    if (observation.SurfaceId != provider.Manifest.SurfaceId)
                        failures.Add($"{capability}: observation SurfaceId '{observation.SurfaceId}' != manifest '{provider.Manifest.SurfaceId}'");
                    if (string.IsNullOrWhiteSpace(observation.EvidenceLabel))
                        failures.Add($"{capability}: observation has no evidence label");
                    foreach (var id in observation.NativeIdentifiers)
                    {
                        if (!declaredTypes.Contains(id.IdentifierType))
                            failures.Add($"{capability}: undeclared native identifier type '{id.IdentifierType}'");
                    }
                }
            }
            catch (Exception ex)
            {
                failures.Add($"{capability}: acquire threw: {ex.Message}");
            }
        }

        return new HarnessResult(failures.Count == 0, failures);
    }
}
