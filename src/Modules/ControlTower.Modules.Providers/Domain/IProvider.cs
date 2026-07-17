namespace ControlTower.Modules.Providers.Domain;

/// <summary>
/// A configured connection's context: which connection, the credential *reference* (never the secret —
/// the credential boundary, Stage 7 §7), and provider settings. Credentials are resolved via the
/// platform secret port outside the provider's domain logic.
/// </summary>
public sealed record ProviderConnectionContext(
    string ConnectionId,
    string CredentialRef,
    IReadOnlyDictionary<string, string> Settings);

/// <summary>
/// The one contract every provider implements — Microsoft, CSV, OpenAI, Anthropic, Google, ServiceNow
/// or any future surface (ADR-007). A provider is acquisition logic + its manifest; it performs no
/// business logic beyond acquiring raw observations for the capabilities it declares.
/// </summary>
public interface IProvider
{
    ProviderManifest Manifest { get; }

    Task<ProviderHealth> CheckHealthAsync(ProviderConnectionContext context, CancellationToken ct = default);

    IAsyncEnumerable<RawObservation> AcquireAsync(ProviderConnectionContext context, ProviderCapability capability, CancellationToken ct = default);
}

/// <summary>Capability negotiation — a provider supports exactly the capabilities its manifest declares.</summary>
public static class ProviderCapabilityNegotiation
{
    public static bool Supports(this IProvider provider, ProviderCapability capability) =>
        provider.Manifest.Capabilities.Contains(capability);
}
