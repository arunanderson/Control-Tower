using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using ControlTower.Modules.Providers.Application;
using ControlTower.Modules.Providers.Domain;
using ControlTower.Modules.Providers.Infrastructure;
using Xunit;

namespace ControlTower.Modules.Providers.Tests;

public class ProviderTestHarnessTests
{
    private static ProviderConnectionContext Ctx(Dictionary<string, string>? settings = null) =>
        new("c", "", settings ?? new Dictionary<string, string>());

    [Fact]
    public async Task A_conforming_provider_passes_the_harness()
    {
        var result = await ProviderTestHarness.RunAsync(new FakeProvider(), Ctx());
        Assert.True(result.Passed, string.Join("; ", result.Failures));
    }

    [Fact]
    public async Task The_csv_provider_passes_the_same_harness()
    {
        var result = await ProviderTestHarness.RunAsync(
            new CsvManualImportProvider(), Ctx(new Dictionary<string, string> { ["csv"] = "key\nbot-1" }));
        Assert.True(result.Passed, string.Join("; ", result.Failures));
    }

    [Fact]
    public async Task A_nonconforming_provider_fails_the_harness()
    {
        var result = await ProviderTestHarness.RunAsync(new BrokenProvider(), Ctx());
        Assert.False(result.Passed);
        Assert.NotEmpty(result.Failures);
    }

    // A well-formed provider: manifest valid, observations conform to the declared contract.
    private sealed class FakeProvider : IProvider
    {
        public ProviderManifest Manifest { get; } = new()
        {
            SurfaceId = "fake",
            DisplayName = "Fake",
            Version = "1.0.0",
            Capabilities = new HashSet<ProviderCapability> { ProviderCapability.Inventory },
            NativeIdentifierTypes = ["fake:id"],
            PayloadSchemaVersion = 1,
            Auth = new ProviderAuthRequirement(ProviderAuthKind.ApiKey, ["read"], null),
            FreshnessExpectation = TimeSpan.FromHours(1),
        };

        public Task<ProviderHealth> CheckHealthAsync(ProviderConnectionContext context, CancellationToken ct = default) =>
            Task.FromResult(new ProviderHealth(ProviderHealthStatus.Healthy, DateTimeOffset.UtcNow, "ok"));

        public async IAsyncEnumerable<RawObservation> AcquireAsync(
            ProviderConnectionContext context, ProviderCapability capability, [EnumeratorCancellation] CancellationToken ct = default)
        {
            await Task.CompletedTask;
            if (capability != ProviderCapability.Inventory) yield break;
            yield return new RawObservation
            {
                SurfaceId = "fake",
                Capability = capability,
                NativeIdentifiers = [new NativeIdentifier("fake", "fake:id", "1")],
                Attributes = new Dictionary<string, string>(),
                ObservedAt = DateTimeOffset.UtcNow,
                EvidenceLabel = "Measured",
            };
        }
    }

    // Manifest is valid (so it is admitted) but acquisition violates the contract: wrong SurfaceId and
    // an undeclared native identifier type. The harness must catch both.
    private sealed class BrokenProvider : IProvider
    {
        public ProviderManifest Manifest { get; } = new()
        {
            SurfaceId = "broken",
            DisplayName = "Broken",
            Version = "1.0.0",
            Capabilities = new HashSet<ProviderCapability> { ProviderCapability.Inventory },
            NativeIdentifierTypes = ["broken:id"],
            PayloadSchemaVersion = 1,
            Auth = new ProviderAuthRequirement(ProviderAuthKind.None, [], null),
            FreshnessExpectation = TimeSpan.FromHours(1),
        };

        public Task<ProviderHealth> CheckHealthAsync(ProviderConnectionContext context, CancellationToken ct = default) =>
            Task.FromResult(new ProviderHealth(ProviderHealthStatus.Healthy, DateTimeOffset.UtcNow, "ok"));

        public async IAsyncEnumerable<RawObservation> AcquireAsync(
            ProviderConnectionContext context, ProviderCapability capability, [EnumeratorCancellation] CancellationToken ct = default)
        {
            await Task.CompletedTask;
            yield return new RawObservation
            {
                SurfaceId = "someone-else", // contract violation: != manifest SurfaceId
                Capability = capability,
                NativeIdentifiers = [new NativeIdentifier("x", "undeclared:id", "1")], // undeclared type
                Attributes = new Dictionary<string, string>(),
                ObservedAt = DateTimeOffset.UtcNow,
                EvidenceLabel = "Measured",
            };
        }
    }
}
