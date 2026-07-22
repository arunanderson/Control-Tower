using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ControlTower.Adapters.InMemory;
using ControlTower.Modules.Providers.Application;
using ControlTower.Modules.Providers.Domain;
using ControlTower.Modules.Providers.Infrastructure;
using ControlTower.Platform.Tenancy;
using Xunit;

namespace ControlTower.Modules.Providers.Tests;

public class ObservationIngestionTests
{
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

        // Only ingestion runs in this rig, so every appended event is an ObservationIngested.
        var stream = await r.Events.ReadAllAsync();
        Assert.Equal(2, stream.Count);
        var payloads = stream.Select(e => System.Text.Encoding.UTF8.GetString(e.Payload)).ToList();
        Assert.Contains(payloads, p => p.Contains("bot-1"));
        Assert.Contains(payloads, p => p.Contains("bot-2"));

        var messages = await r.Outbox.DequeueBatchAsync(100);
        Assert.Equal(2, messages.Count(m => m.Topic == ObservationIngestionService.ObservationIngestedTopic));
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
    public async Task An_undeclared_capability_is_rejected()
    {
        var r = Build();
        using var _ = r.Accessor.BeginScope(new TenantId(Guid.NewGuid()));

        await Assert.ThrowsAsync<ProviderException>(() =>
            r.Service.IngestAsync(new CsvManualImportProvider(), CsvContext(TwoRows), ProviderCapability.Identity));
    }

    [Fact]
    public async Task Ingestion_requires_a_tenant_scope()
    {
        var r = Build();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            r.Service.IngestAsync(new CsvManualImportProvider(), CsvContext(TwoRows), ProviderCapability.Inventory));
    }
}
