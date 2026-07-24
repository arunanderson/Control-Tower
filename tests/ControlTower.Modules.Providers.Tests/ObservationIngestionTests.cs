using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Linq;
using System.Threading.Tasks;
using ControlTower.Adapters.InMemory;
using ControlTower.Modules.Providers.Application;
using ControlTower.Modules.Providers.Domain;
using ControlTower.Modules.Providers.Infrastructure;
using ControlTower.Platform.Identity;
using ControlTower.Platform.Privacy;
using ControlTower.Platform.Tenancy;
using Xunit;

namespace ControlTower.Modules.Providers.Tests;

public class ObservationIngestionTests
{
    private sealed class FailingProvider : IProvider
    {
        public ProviderManifest Manifest { get; } = new()
        {
            SurfaceId = "failing",
            DisplayName = "Failing",
            Version = "1.0.0",
            Capabilities = new HashSet<ProviderCapability> { ProviderCapability.Inventory },
            NativeIdentifierTypes = ["test:id"],
            PayloadSchemaVersion = 1,
            Auth = new ProviderAuthRequirement(ProviderAuthKind.None, [], null),
            FreshnessExpectation = TimeSpan.FromHours(1),
        };

        public Task<ProviderHealth> CheckHealthAsync(ProviderConnectionContext context, CancellationToken ct = default) =>
            Task.FromResult(new ProviderHealth(ProviderHealthStatus.Degraded, DateTimeOffset.UtcNow, "test"));

        public async IAsyncEnumerable<RawObservation> AcquireAsync(
            ProviderConnectionContext context,
            ProviderCapability capability,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            await Task.Yield();
            throw new ProviderException("acquisition failed");
#pragma warning disable CS0162
            yield break;
#pragma warning restore CS0162
        }
    }

    private sealed class AuditIdentityProvider(
        string surfaceId,
        string? observationSurfaceId = null)
        : IProvider
    {
        public bool AcquisitionAttempted { get; private set; }

        public ProviderManifest Manifest { get; } = new()
        {
            SurfaceId = surfaceId,
            DisplayName = "Audit identity test provider",
            Version = "1.0.0",
            Capabilities =
                new HashSet<ProviderCapability>
                {
                    ProviderCapability.Inventory,
                },
            NativeIdentifierTypes = ["test:id"],
            PayloadSchemaVersion = 1,
            Auth = new ProviderAuthRequirement(
                ProviderAuthKind.None,
                [],
                null),
            FreshnessExpectation = TimeSpan.FromHours(1),
        };

        public Task<ProviderHealth> CheckHealthAsync(
            ProviderConnectionContext context,
            CancellationToken ct = default) =>
            Task.FromResult(
                new ProviderHealth(
                    ProviderHealthStatus.Degraded,
                    DateTimeOffset.UtcNow,
                    "test"));

        public async IAsyncEnumerable<RawObservation> AcquireAsync(
            ProviderConnectionContext context,
            ProviderCapability capability,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            AcquisitionAttempted = true;
            await Task.Yield();
            if (observationSurfaceId is null)
                yield break;

            yield return new RawObservation
            {
                SurfaceId = observationSurfaceId,
                Capability = capability,
                NativeIdentifiers =
                [
                    new NativeIdentifier(
                        surfaceId,
                        "test:id",
                        "observation-1"),
                ],
                Attributes =
                    new Dictionary<string, string>(),
                ObservedAt = DateTimeOffset.UtcNow,
                EvidenceLabel = "Measured",
            };
        }
    }

    private sealed class ChangingManifestProvider : IProvider
    {
        private readonly ProviderManifest _first =
            ManifestFor("provider:first");
        private readonly ProviderManifest _later =
            ManifestFor("provider:later");

        public int ManifestReads { get; private set; }

        public ProviderManifest Manifest
        {
            get
            {
                ManifestReads++;
                return ManifestReads <= 3
                    ? _first
                    : _later;
            }
        }

        public Task<ProviderHealth> CheckHealthAsync(
            ProviderConnectionContext context,
            CancellationToken ct = default) =>
            Task.FromResult(
                new ProviderHealth(
                    ProviderHealthStatus.Healthy,
                    DateTimeOffset.UtcNow,
                    "test"));

        public async IAsyncEnumerable<RawObservation>
            AcquireAsync(
                ProviderConnectionContext context,
                ProviderCapability capability,
                [EnumeratorCancellation]
                CancellationToken ct = default)
        {
            await Task.Yield();
            yield return new RawObservation
            {
                SurfaceId = _later.SurfaceId,
                Capability = capability,
                NativeIdentifiers =
                [
                    new NativeIdentifier(
                        _later.SurfaceId,
                        "test:id",
                        "observation-1"),
                ],
                Attributes =
                    new Dictionary<string, string>(),
                ObservedAt = DateTimeOffset.UtcNow,
                EvidenceLabel = "Measured",
            };
        }

        private static ProviderManifest ManifestFor(
            string surfaceId) =>
            new()
            {
                SurfaceId = surfaceId,
                DisplayName = surfaceId,
                Version = "1.0.0",
                Capabilities =
                    new HashSet<ProviderCapability>
                    {
                        ProviderCapability.Inventory,
                    },
                NativeIdentifierTypes = ["test:id"],
                PayloadSchemaVersion = 1,
                Auth = new ProviderAuthRequirement(
                    ProviderAuthKind.None,
                    [],
                    null),
                FreshnessExpectation =
                    TimeSpan.FromHours(1),
            };
    }

    private sealed record Rig(
        ObservationIngestionService Service,
        IObservationStore Store,
        InMemoryEventStore Events,
        InMemoryOutbox Outbox,
        TenantContextAccessor Accessor);

    private static Rig Build()
    {
        var accessor = new TenantContextAccessor();
        var store = new InMemoryObservationStore(accessor);
        var events = new InMemoryEventStore(accessor);
        var outbox = new InMemoryOutbox();
        var watermarks = new InMemoryWatermarkStore();
        return new Rig(new ObservationIngestionService(store, events, outbox, watermarks, accessor), store, events, outbox, accessor);
    }

    private static ProviderConnectionContext CsvContext(string csv) =>
        new("conn-1", "", new Dictionary<string, string> { ["csv"] = csv });

    private const string TwoRows = "key,displayName,assetType\nbot-1,Sales Copilot,agent\nbot-2,HR Flow,flow";

    [Fact]
    public async Task It_appends_immutable_observations_from_a_provider()
    {
        var r = Build();
        using var _ = r.Accessor.BeginScope(new TenantId(Guid.NewGuid()));

        var result = await r.Service.IngestAsync(new CsvManualImportProvider(), CsvContext(TwoRows), ProviderCapability.Inventory);

        Assert.Equal(2, result.Observed);
        Assert.Equal(2, result.New);
        Assert.Equal(0, result.Suppressed);

        var observations = await r.Store.ObservationsAsync();
        Assert.Equal(2, observations.Count);
        Assert.All(observations, o => Assert.Equal(ObservationKind.Inventory, o.Kind));
        Assert.All(observations, o => Assert.Equal(PrivacyMarking.L1, o.PrivacyMarking));                 // Gate 1 default
        Assert.All(observations, o => Assert.Equal(CsvManualImportProvider.ManualImportLabel, o.EvidenceLabel)); // ADR-013 honesty
        Assert.All(observations, o => Assert.Equal(DeltaStatus.New, o.DeltaStatus));
        Assert.Contains(observations, o => o.NativeIdentifiers.Any(n => n.IdentifierType == "csv:key" && n.Value == "bot-1"));
    }

    [Fact]
    public async Task Re_ingesting_identical_data_is_fully_delta_suppressed()
    {
        var r = Build();
        using var _ = r.Accessor.BeginScope(new TenantId(Guid.NewGuid()));

        await r.Service.IngestAsync(new CsvManualImportProvider(), CsvContext(TwoRows), ProviderCapability.Inventory);
        var second = await r.Service.IngestAsync(new CsvManualImportProvider(), CsvContext(TwoRows), ProviderCapability.Inventory);

        Assert.Equal(2, second.Observed);
        Assert.Equal(0, second.New);
        Assert.Equal(0, second.Changed);
        Assert.Equal(2, second.Suppressed);

        // Nothing new appended: the store still holds only the first sweep's two observations.
        Assert.Equal(2, (await r.Store.ObservationsAsync()).Count);
    }

    [Fact]
    public async Task A_changed_attribute_is_recorded_as_a_changed_observation()
    {
        var r = Build();
        using var _ = r.Accessor.BeginScope(new TenantId(Guid.NewGuid()));

        await r.Service.IngestAsync(new CsvManualImportProvider(), CsvContext(TwoRows), ProviderCapability.Inventory);
        const string changed = "key,displayName,assetType\nbot-1,Sales Copilot RENAMED,agent\nbot-2,HR Flow,flow";
        var second = await r.Service.IngestAsync(new CsvManualImportProvider(), CsvContext(changed), ProviderCapability.Inventory);

        Assert.Equal(1, second.Changed);
        Assert.Equal(1, second.Suppressed); // the unchanged row
        var observations = await r.Store.ObservationsAsync();
        Assert.Equal(3, observations.Count);
        Assert.Contains(observations, o => o.DeltaStatus == DeltaStatus.Changed);
    }

    [Fact]
    public async Task Each_appended_observation_emits_one_ingested_event_and_one_outbox_message()
    {
        var r = Build();
        using var _ = r.Accessor.BeginScope(new TenantId(Guid.NewGuid()));

        await r.Service.IngestAsync(new CsvManualImportProvider(), CsvContext(TwoRows), ProviderCapability.Inventory);

        // Two observation events plus one self-contained coverage fact for the completed run.
        var stream = await r.Events.ReadAllAsync();
        Assert.Equal(3, stream.Count);
        var payloads = stream.Select(e => System.Text.Encoding.UTF8.GetString(e.Payload)).ToList();
        Assert.Contains(payloads, p => p.Contains("bot-1"));
        Assert.Contains(payloads, p => p.Contains("bot-2"));

        var messages = await r.Outbox.DequeueBatchAsync(100);
        Assert.Equal(2, messages.Count(m => m.Topic == ObservationIngestionService.ObservationIngestedTopic));
        Assert.Single(messages, m => m.Topic == ObservationIngestionService.ProviderCoverageUpdatedTopic);
    }

    [Fact]
    public async Task It_records_an_honest_ingestion_run()
    {
        var r = Build();
        using var _ = r.Accessor.BeginScope(new TenantId(Guid.NewGuid()));

        await r.Service.IngestAsync(new CsvManualImportProvider(), CsvContext(TwoRows), ProviderCapability.Inventory);

        var runs = await r.Store.RunsAsync();
        var run = Assert.Single(runs);
        Assert.Equal("manual-csv", run.SurfaceId);
        Assert.Equal(2, run.Observed);
        Assert.Equal(2, run.New);
        Assert.Equal(0, run.Suppressed);
        Assert.Equal("Completed", run.Outcome);
    }

    [Fact]
    public async Task Failed_acquisition_records_and_emits_degraded_coverage_before_rethrowing()
    {
        var r = Build();
        using var _ = r.Accessor.BeginScope(new TenantId(Guid.NewGuid()));

        await Assert.ThrowsAsync<ProviderException>(() =>
            r.Service.IngestAsync(new FailingProvider(), CsvContext(TwoRows), ProviderCapability.Inventory));

        var run = Assert.Single(await r.Store.RunsAsync());
        Assert.Equal("Degraded", run.Outcome);
        var message = Assert.Single(await r.Outbox.DequeueBatchAsync(100),
            m => m.Topic == ObservationIngestionService.ProviderCoverageUpdatedTopic);
        Assert.Contains("Degraded", System.Text.Encoding.UTF8.GetString(message.Payload));
    }

    [Fact]
    public async Task An_undeclared_capability_is_rejected()
    {
        var r = Build();
        using var _ = r.Accessor.BeginScope(new TenantId(Guid.NewGuid()));

        await Assert.ThrowsAsync<ProviderException>(() =>
            r.Service.IngestAsync(new CsvManualImportProvider(), CsvContext(TwoRows), ProviderCapability.Identity));
    }

    [Fact]
    public async Task Provider_surface_identity_is_preserved_as_the_audit_actor()
    {
        const string surfaceId = "provider:custom/surface";
        var r = Build();
        var provider = new AuditIdentityProvider(surfaceId);
        using var _ = r.Accessor.BeginScope(
            new TenantId(Guid.NewGuid()));

        await r.Service.IngestAsync(
            provider,
            CsvContext(TwoRows),
            ProviderCapability.Inventory);

        Assert.True(provider.AcquisitionAttempted);
        var events = await r.Events.ReadAllAsync();
        Assert.NotEmpty(events);
        Assert.All(
            events,
            stored => Assert.Equal(
                AuditActor.Provider(surfaceId),
                stored.Actor));
    }

    [Fact]
    public async Task Invalid_provider_audit_identity_is_rejected_before_acquisition_or_state()
    {
        var r = Build();
        var provider =
            new AuditIdentityProvider(new string('x', 129));
        using var _ = r.Accessor.BeginScope(
            new TenantId(Guid.NewGuid()));

        var exception =
            await Assert.ThrowsAsync<ProviderException>(
                () => r.Service.IngestAsync(
                    provider,
                    CsvContext(TwoRows),
                    ProviderCapability.Inventory));

        Assert.Contains(
            "invalid audit identity",
            exception.Message,
            StringComparison.Ordinal);
        Assert.False(provider.AcquisitionAttempted);
        Assert.Empty(await r.Store.ObservationsAsync());
        Assert.Empty(await r.Store.RunsAsync());
        Assert.Empty(await r.Events.ReadAllAsync());
        Assert.Empty(await r.Outbox.DequeueBatchAsync(100));
    }

    [Fact]
    public async Task Observation_surface_must_match_manifest_before_observation_state()
    {
        var r = Build();
        var provider = new AuditIdentityProvider(
            "provider:manifest",
            "provider:other");
        using var _ = r.Accessor.BeginScope(
            new TenantId(Guid.NewGuid()));

        var exception =
            await Assert.ThrowsAsync<ProviderException>(
                () => r.Service.IngestAsync(
                    provider,
                    CsvContext(TwoRows),
                    ProviderCapability.Inventory));

        Assert.Contains(
            "manifest surface identity",
            exception.Message,
            StringComparison.Ordinal);
        Assert.True(provider.AcquisitionAttempted);
        Assert.Empty(await r.Store.ObservationsAsync());
        var run = Assert.Single(await r.Store.RunsAsync());
        Assert.Equal("Degraded", run.Outcome);
        Assert.Equal(0, run.Observed);
        var events = await r.Events.ReadAllAsync();
        Assert.DoesNotContain(
            events,
            stored =>
                stored.EventType
                == "ObservationIngested");
        Assert.Single(
            events,
            stored =>
                stored.EventType
                == "ProviderCoverageUpdated");
        var messages =
            await r.Outbox.DequeueBatchAsync(100);
        Assert.DoesNotContain(
            messages,
            message =>
                message.Topic
                == ObservationIngestionService
                    .ObservationIngestedTopic);
    }

    [Fact]
    public async Task Provider_manifest_is_snapshotted_once_for_attribution()
    {
        var r = Build();
        var provider = new ChangingManifestProvider();
        using var _ = r.Accessor.BeginScope(
            new TenantId(Guid.NewGuid()));

        await Assert.ThrowsAsync<ProviderException>(
            () => r.Service.IngestAsync(
                provider,
                CsvContext(TwoRows),
                ProviderCapability.Inventory));

        Assert.Equal(1, provider.ManifestReads);
        Assert.Empty(await r.Store.ObservationsAsync());
        var run = Assert.Single(await r.Store.RunsAsync());
        Assert.Equal("provider:first", run.SurfaceId);
        Assert.Equal("Degraded", run.Outcome);
        var coverage = Assert.Single(
            await r.Events.ReadAllAsync());
        Assert.Equal(
            AuditActor.Provider("provider:first"),
            coverage.Actor);
        Assert.DoesNotContain(
            "provider:later",
            System.Text.Encoding.UTF8.GetString(
                coverage.Payload),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Invalid_connection_evidence_identity_is_rejected_before_acquisition_or_state()
    {
        var r = Build();
        using var _ = r.Accessor.BeginScope(
            new TenantId(Guid.NewGuid()));

        foreach (var invalidConnectionId in new[]
                 {
                     " padded-connection ",
                     new string('x', 257),
                     "invalid\u0001connection",
                     "invalid\uD800connection",
                 })
        {
            var provider =
                new AuditIdentityProvider(
                    "provider:custom/surface");
            await Assert.ThrowsAsync<ProviderException>(
                () => r.Service.IngestAsync(
                    provider,
                    new ProviderConnectionContext(
                        invalidConnectionId,
                        string.Empty,
                        new Dictionary<string, string>()),
                    ProviderCapability.Inventory));

            Assert.False(provider.AcquisitionAttempted);
            Assert.Empty(
                await r.Store.ObservationsAsync());
            Assert.Empty(await r.Store.RunsAsync());
            Assert.Empty(await r.Events.ReadAllAsync());
            Assert.Empty(
                await r.Outbox.DequeueBatchAsync(
                    100));
        }
    }

    [Fact]
    public async Task Ingestion_requires_a_tenant_scope()
    {
        var r = Build();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            r.Service.IngestAsync(new CsvManualImportProvider(), CsvContext(TwoRows), ProviderCapability.Inventory));
    }
}
