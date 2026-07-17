namespace ControlTower.Modules.Providers.Domain;

/// <summary>Authentication + authorization the provider requires (auth/authz abstraction). Credentials themselves live in connection scope, never here.</summary>
public sealed record ProviderAuthRequirement(ProviderAuthKind Kind, IReadOnlyList<string> Scopes, string? Note);

/// <summary>
/// A provider's self-declaration (C4.5, ADR-007): surface identity, capabilities offered, native
/// identifier types contributed, payload schema version, auth requirements, freshness expectation,
/// health semantics. The platform supplies the invariant services; a provider is only acquisition
/// logic + this manifest. Every provider — Microsoft, CSV, OpenAI, ServiceNow — declares one of these.
/// </summary>
public sealed record ProviderManifest
{
    public required string SurfaceId { get; init; }
    public required string DisplayName { get; init; }
    public required string Version { get; init; }
    public required IReadOnlySet<ProviderCapability> Capabilities { get; init; }
    public required IReadOnlyList<string> NativeIdentifierTypes { get; init; }
    public required int PayloadSchemaVersion { get; init; }
    public required ProviderAuthRequirement Auth { get; init; }
    public required TimeSpan FreshnessExpectation { get; init; }
    public string? HealthNote { get; init; }
}

/// <summary>Provider health (health model).</summary>
public sealed record ProviderHealth(ProviderHealthStatus Status, DateTimeOffset CheckedAt, string? Detail);

/// <summary>Freshness model — is the last successful sync within the expected interval?</summary>
public sealed record ProviderFreshness(TimeSpan Expected, DateTimeOffset? LastSuccessfulSync)
{
    public bool IsStale(DateTimeOffset now) => LastSuccessfulSync is null || now - LastSuccessfulSync.Value > Expected;
}

/// <summary>Coverage model — which capabilities a connected provider actually covers, stated honestly.</summary>
public sealed record ProviderCoverage(string SurfaceId, IReadOnlySet<ProviderCapability> Covered, string Note);
