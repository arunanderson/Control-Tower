using System;
using System.Collections.Generic;
using ControlTower.Modules.Providers.Domain;
using ControlTower.Modules.Providers.Infrastructure;
using Xunit;

namespace ControlTower.Modules.Providers.Tests;

public class ProviderFrameworkTests
{
    private static ProviderManifest ValidManifest() => new()
    {
        SurfaceId = "x",
        DisplayName = "X",
        Version = "1.0.0",
        Capabilities = new HashSet<ProviderCapability> { ProviderCapability.Inventory },
        NativeIdentifierTypes = ["x:id"],
        PayloadSchemaVersion = 1,
        Auth = new ProviderAuthRequirement(ProviderAuthKind.None, [], null),
        FreshnessExpectation = TimeSpan.FromHours(1),
    };

    [Fact]
    public void A_valid_manifest_passes_contract_validation()
    {
        Assert.True(ProviderContractValidator.ValidateManifest(ValidManifest()).IsValid);
    }

    [Fact]
    public void A_manifest_without_capabilities_fails()
    {
        var manifest = ValidManifest() with { Capabilities = new HashSet<ProviderCapability>() };
        Assert.False(ProviderContractValidator.ValidateManifest(manifest).IsValid);
    }

    [Fact]
    public void A_manifest_with_a_non_semver_version_fails()
    {
        var manifest = ValidManifest() with { Version = "v1" };
        Assert.False(ProviderContractValidator.ValidateManifest(manifest).IsValid);
    }

    [Fact]
    public void The_registry_registers_resolves_and_discovers()
    {
        var registry = new ProviderRegistry();
        var provider = new CsvManualImportProvider();
        registry.Register(provider);
        Assert.Same(provider, registry.Resolve("manual-csv"));
        Assert.Contains(registry.Discover(), m => m.SurfaceId == "manual-csv");
    }

    [Fact]
    public void The_registry_rejects_a_duplicate_surface()
    {
        var registry = new ProviderRegistry();
        registry.Register(new CsvManualImportProvider());
        Assert.Throws<ProviderException>(() => registry.Register(new CsvManualImportProvider()));
    }

    [Fact]
    public void Freshness_is_stale_when_never_synced_or_overdue()
    {
        var now = DateTimeOffset.UtcNow;
        Assert.True(new ProviderFreshness(TimeSpan.FromHours(1), null).IsStale(now));
        Assert.True(new ProviderFreshness(TimeSpan.FromHours(1), now.AddHours(-2)).IsStale(now));
        Assert.False(new ProviderFreshness(TimeSpan.FromHours(1), now).IsStale(now));
    }

    [Fact]
    public void Capability_negotiation_matches_the_manifest()
    {
        var provider = new CsvManualImportProvider();
        Assert.True(provider.Supports(ProviderCapability.Inventory));
        Assert.False(provider.Supports(ProviderCapability.Identity));
    }
}
