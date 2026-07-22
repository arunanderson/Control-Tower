using ControlTower.Platform.Tenancy;

namespace ControlTower.Modules.Providers.Domain;

/// <summary>A tenant's configured provider instance. Credentials are references, never secret values.</summary>
public sealed record ProviderConnection
{
    public required string ConnectionId { get; init; }
    public required TenantId Tenant { get; init; }
    public required string SurfaceId { get; init; }
    public required string CredentialReference { get; init; }
    public required IReadOnlySet<ProviderCapability> Capabilities { get; init; }
    public required TimeSpan Schedule { get; init; }
    public required bool Enabled { get; init; }
    public IReadOnlyDictionary<string, string> Configuration { get; init; } = new Dictionary<string, string>();
}
