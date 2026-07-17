namespace ControlTower.Modules.Providers.Domain;

public sealed class ProviderException(string message) : Exception(message);

/// <summary>Capabilities a provider may offer (Stage 7 §8). A provider declares a subset in its manifest.</summary>
public enum ProviderCapability
{
    Inventory,
    Usage,
    Cost,
    Identity,
    Control,
}

public enum ProviderAuthKind
{
    None,
    ApiKey,
    OAuthClientCredentials,
    OAuthDelegated,
    Certificate,
}

public enum ProviderHealthStatus
{
    Healthy,
    Degraded,
    Disconnected,
    Unknown,
}

public enum ProviderConnectionState
{
    Configured,
    Connected,
    Degraded,
    Disconnected,
}

/// <summary>A native identifier a provider surface contributes (feeds the alias model, C1). Provider-local — the resolution pipeline maps these to the ledger later.</summary>
public sealed record NativeIdentifier(string System, string IdentifierType, string Value);

/// <summary>A recorded provider-side error (provider error model).</summary>
public sealed record ProviderError(string SurfaceId, string Code, string Message, DateTimeOffset At);
