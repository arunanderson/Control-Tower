using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ControlTower.Adapters.InMemory;
using ControlTower.Modules.Ledger.Application;
using ControlTower.Modules.Ledger.Domain;
using ControlTower.Modules.Ledger.Infrastructure;
using ControlTower.Platform.Tenancy;
using ProvidersApp = ControlTower.Modules.Providers.Application;
using ProvidersDomain = ControlTower.Modules.Providers.Domain;
using ProvidersInfra = ControlTower.Modules.Providers.Infrastructure;
using Xunit;

namespace ControlTower.Modules.Ledger.Tests;

public class EntityResolutionTests
{
    private sealed record Rig(
        EntityResolutionService Resolution,
        IAssetRepository Assets,
        IMergeCaseStore MergeCases,
        InMemoryEventStore Events,
        TenantContextAccessor Accessor);

    private static Rig Build(IMatchClassifier? classifier = null)
    {
        var accessor = new TenantContextAccessor();
        var assets = new InMemoryAssetRepository(accessor);
        var readModel = new InMemoryAssetLedgerReadModel(accessor);
        var mergeCases = new InMemoryMergeCaseStore(accessor);
        var events = new InMemoryEventStore(accessor);
        var svc = new EntityResolutionService(assets, readModel, mergeCases, events, classifier ?? new DeterministicMatchClassifier(), accessor, TaxonomyScheme.Default);
        return new Rig(svc, assets, mergeCases, events, accessor);
    }

    private static ObservationDescriptor Obs(string idValue, string system = "csv", string type = "csv:key", string display = "Bot", string assetType = "agent", Guid? observationId = null) =>
        new(observationId ?? Guid.NewGuid(), new NativeIdentifier(system, type, idValue), display, assetType, "Self-reported / Manual Import");

    private async Task<List<string>> EventPayloadsAsync(Rig r)
    {
        var stream = await r.Events.ReadAllAsync();
        return stream.Select(e => Encoding.UTF8.GetString(e.Payload)).ToList();
    }

    [Fact]
    public async Task No_match_creates_a_new_asset_linked_to_the_observation()
    {
        var r = Build();
        using var _ = r.Accessor.BeginScope(new TenantId(Guid.NewGuid()));

        var obs = Obs("bot-1", display: "Sales Copilot", assetType: "agent");
        var result = await r.Resolution.ResolveAsync(obs);

        Assert.Equal(MatchOutcome.NoMatch, result.Outcome);
        var asset = await r.Assets.GetAsync(result.AssetId!.Value);
        Assert.NotNull(asset);
        Assert.Equal("Sales Copilot", asset!.DisplayName);
        Assert.Equal("agent", asset.Type.Value);
        Assert.Single(asset.ActiveResolutionLinks);
        Assert.True(asset.IsLinkedToObservation(obs.ObservationId));
        Assert.Equal(MatchConfidence.High, asset.MatchConfidence);
    }

    [Fact]
    public async Task Deterministic_match_links_to_the_existing_asset_and_is_provider_agnostic()
    {
        var r = Build();
        using var _ = r.Accessor.BeginScope(new TenantId(Guid.NewGuid()));

        // A brand-new, non-Microsoft identifier system — no hardcoded assumptions.
        var first = await r.Resolution.ResolveAsync(Obs("widget-9", system: "acme", type: "acme:id"));
        var second = await r.Resolution.ResolveAsync(Obs("widget-9", system: "acme", type: "acme:id"));

        Assert.Equal(MatchOutcome.NoMatch, first.Outcome);
        Assert.Equal(MatchOutcome.AutoLink, second.Outcome);
        Assert.Equal(first.AssetId, second.AssetId); // same asset — deterministic join
        var asset = await r.Assets.GetAsync(first.AssetId!.Value);
        Assert.Equal(2, asset!.ActiveResolutionLinks.Count);
        Assert.Equal(MatchConfidence.High, asset.MatchConfidence);
    }

    [Fact]
    public async Task Idempotent_replay_of_the_same_observation_does_not_double_link_or_recreate()
    {
        var r = Build();
        using var _ = r.Accessor.BeginScope(new TenantId(Guid.NewGuid()));

        var obs = Obs("bot-1");
        var a = await r.Resolution.ResolveAsync(obs);
        var b = await r.Resolution.ResolveAsync(obs); // exact replay (same ObservationId)

        Assert.Equal(a.AssetId, b.AssetId);
        Assert.Single(await r.Assets.ListAsync());
        var asset = await r.Assets.GetAsync(a.AssetId!.Value);
        Assert.Single(asset!.ActiveResolutionLinks); // not double-linked
    }

    [Fact]
    public async Task Identifier_collision_opens_a_merge_case_and_does_not_auto_link()
    {
        var r = Build();
        using var _ = r.Accessor.BeginScope(new TenantId(Guid.NewGuid()));

        var a = await r.Resolution.ResolveAsync(Obs("X", system: "sys", type: "t"));           // asset A ← X
        var b = await r.Resolution.ResolveAsync(Obs("Y", system: "sys", type: "t"));           // asset B ← Y
        await r.Resolution.ApproveManualLinkAsync(b.AssetId!.Value,
            NativeIdentifierSet.Of(new NativeIdentifier("sys", "t", "X")), null, "operator");    // B ← X too (collision seed)

        var result = await r.Resolution.ResolveAsync(Obs("X", system: "sys", type: "t"));       // X now maps to A and B

        Assert.Equal(MatchOutcome.Collision, result.Outcome);
        Assert.NotNull(result.MergeCaseId);
        var open = await r.MergeCases.OpenCasesAsync();
        Assert.Single(open);
        Assert.Equal(2, open[0].Candidates.Count);
        // Neither candidate gained a new link from the colliding observation.
        Assert.Single((await r.Assets.GetAsync(a.AssetId!.Value))!.ActiveResolutionLinks);
        Assert.Equal(2, (await r.Assets.GetAsync(b.AssetId!.Value))!.ActiveResolutionLinks.Count);
    }

    // A classifier that always proposes a weak auto-link — proves the engine's guard, not a real rule.
    private sealed class StubClassifier(MatchConfidence confidence, LedgerAssetId target) : IMatchClassifier
    {
        public MatchDecision Classify(NativeIdentifier primary, IReadOnlyList<AIAsset> candidates) =>
            new(MatchOutcome.AutoLink, confidence, MatchMethod.Heuristic, target, candidates.Select(c => c.Id).ToList(), "weak");
    }

    [Theory]
    [InlineData(MatchConfidence.Low)]
    [InlineData(MatchConfidence.Medium)]
    public async Task Sub_high_confidence_never_auto_links_and_enters_the_merge_queue(MatchConfidence weak)
    {
        var seed = Build();
        using var _ = seed.Accessor.BeginScope(new TenantId(Guid.NewGuid()));
        var a = await seed.Resolution.ResolveAsync(Obs("X", system: "sys", type: "t"));
        var assetId = a.AssetId!.Value;

        // Rebuild the service over the SAME stores but with a classifier that returns the weak confidence.
        var weakSvc = new EntityResolutionService(seed.Assets, new InMemoryAssetLedgerReadModel(seed.Accessor), seed.MergeCases, seed.Events, new StubClassifier(weak, assetId), seed.Accessor, TaxonomyScheme.Default);

        var result = await weakSvc.ResolveAsync(Obs("X", system: "sys", type: "t"));

        Assert.Equal(MatchOutcome.Review, result.Outcome);           // downgraded from AutoLink
        Assert.NotNull(result.MergeCaseId);
        Assert.Single((await seed.Assets.GetAsync(assetId))!.ActiveResolutionLinks); // no auto-link added
    }

    [Fact]
    public async Task Merge_supersedes_source_links_onto_the_target_with_complete_audit()
    {
        var r = Build();
        using var _ = r.Accessor.BeginScope(new TenantId(Guid.NewGuid()));

        var a = (await r.Resolution.ResolveAsync(Obs("X", system: "sys", type: "t"))).AssetId!.Value;
        var b = (await r.Resolution.ResolveAsync(Obs("Y", system: "sys", type: "t"))).AssetId!.Value;

        await r.Resolution.MergeAsync(a, b, "operator");

        var target = await r.Assets.GetAsync(a);
        var source = await r.Assets.GetAsync(b);
        Assert.Equal(2, target!.ActiveResolutionLinks.Count);          // X + superseded-in Y
        Assert.Empty(source!.ActiveResolutionLinks);                   // Y superseded out
        Assert.Equal(RegistrationStatus.Merged, source.RegistrationStatus);
        Assert.Equal(LinkStatus.Superseded, source.ResolutionLinks.Single().Status); // retained, not deleted

        var payloads = await EventPayloadsAsync(r);
        Assert.Contains(payloads, p => p.Contains(nameof(AssetMergedInto)) || p.Contains("TargetAssetId"));
        Assert.Contains(payloads, p => p.Contains("BySupersedingLinkId")); // ResolutionLinkSuperseded
    }

    [Fact]
    public async Task Split_moves_links_to_a_new_asset_with_complete_audit()
    {
        var r = Build();
        using var _ = r.Accessor.BeginScope(new TenantId(Guid.NewGuid()));

        var a = (await r.Resolution.ResolveAsync(Obs("X", system: "sys", type: "t"))).AssetId!.Value;
        await r.Resolution.ApproveManualLinkAsync(a, NativeIdentifierSet.Of(new NativeIdentifier("sys", "t", "Y")), null, "operator");

        var asset = await r.Assets.GetAsync(a);
        var yLink = asset!.ActiveResolutionLinks.Single(l => l.Identifiers.Identifiers.Any(i => i.Value == "Y"));

        var newId = await r.Resolution.SplitAsync(a, [yLink.Id], "Split Out", "agent", "operator");

        var original = await r.Assets.GetAsync(a);
        var created = await r.Assets.GetAsync(newId);
        Assert.Single(original!.ActiveResolutionLinks);                // only X remains active
        Assert.Equal(LinkStatus.Superseded, original.ResolutionLinks.Single(l => l.Id == yLink.Id).Status);
        Assert.Single(created!.ActiveResolutionLinks);                 // Y moved here

        var payloads = await EventPayloadsAsync(r);
        Assert.Contains(payloads, p => p.Contains(nameof(AssetSplit)) || p.Contains("NewAssetId"));
    }

    [Fact]
    public async Task Resolution_is_tenant_isolated()
    {
        var r = Build();
        var tenantA = new TenantId(Guid.NewGuid());
        var tenantB = new TenantId(Guid.NewGuid());

        using (r.Accessor.BeginScope(tenantA))
            await r.Resolution.ResolveAsync(Obs("shared-id"));

        using (r.Accessor.BeginScope(tenantB))
        {
            Assert.Empty(await r.Assets.ListAsync());                  // A's asset is invisible in B
            await r.Resolution.ResolveAsync(Obs("shared-id"));         // same id, different tenant
            Assert.Single(await r.Assets.ListAsync());                 // a separate B-owned asset
        }

        using (r.Accessor.BeginScope(tenantA))
            Assert.Single(await r.Assets.ListAsync());
    }

    [Fact]
    public void Ledger_and_Providers_modules_do_not_reference_each_other()
    {
        var ledger = typeof(EntityResolutionService).Assembly;
        var providers = typeof(ProvidersDomain.RawObservation).Assembly;

        Assert.DoesNotContain(ledger.GetReferencedAssemblies(), a => a.Name == providers.GetName().Name);
        Assert.DoesNotContain(providers.GetReferencedAssemblies(), a => a.Name == ledger.GetName().Name);
    }

    [Fact]
    public async Task End_to_end_csv_ingestion_resolves_into_the_ledger_without_a_tenant()
    {
        var accessor = new TenantContextAccessor();

        // C4 ingestion side.
        var observationStore = new ProvidersInfra.InMemoryObservationStore(accessor);
        var events = new InMemoryEventStore(accessor);
        var outbox = new InMemoryOutbox();
        var watermarks = new ProvidersInfra.InMemoryWatermarkStore();
        var ingestion = new ProvidersApp.ObservationIngestionService(observationStore, events, outbox, watermarks, accessor);

        // C1 resolution side (shares the same tenant accessor; separate stores, like separate hosts).
        var assets = new InMemoryAssetRepository(accessor);
        var resolution = new EntityResolutionService(assets, new InMemoryAssetLedgerReadModel(accessor), new InMemoryMergeCaseStore(accessor), events, new DeterministicMatchClassifier(), accessor, TaxonomyScheme.Default);
        var handler = new ObservationIngestedHandler(resolution, accessor);

        const string csv = "key,displayName,assetType\nbot-1,Sales Copilot,agent\nbot-2,HR Flow,flow";
        var ctx = new ProvidersDomain.ProviderConnectionContext("conn-1", "", new Dictionary<string, string> { ["csv"] = csv });

        using var _ = accessor.BeginScope(new TenantId(Guid.NewGuid()));

        await ingestion.IngestAsync(new ProvidersInfra.CsvManualImportProvider(), ctx, ProvidersDomain.ProviderCapability.Inventory);

        // Host-composed delivery: pump the outbox to the topic's handler (what OutboxDispatcher does).
        async Task PumpAsync()
        {
            var batch = await outbox.DequeueBatchAsync(100);
            foreach (var m in batch.Where(m => m.Topic == handler.Topic))
                await handler.HandleAsync(m.Payload);
        }

        await PumpAsync();

        var ledger = await assets.ListAsync();
        Assert.Equal(2, ledger.Count);
        Assert.Contains(ledger, a => a.DisplayName == "Sales Copilot" && a.Type.Value == "agent");
        Assert.Contains(ledger, a => a.DisplayName == "HR Flow" && a.Type.Value == "flow");
        Assert.All(ledger, a => Assert.Equal(MatchConfidence.High, a.MatchConfidence));

        // Idempotent replay end-to-end: re-delivering the same messages changes nothing.
        await PumpAsync();
        Assert.Equal(2, (await assets.ListAsync()).Count);
        Assert.All(await assets.ListAsync(), a => Assert.Single(a.ActiveResolutionLinks));

        // Resolution never modified the provider observations.
        Assert.Equal(2, (await observationStore.ObservationsAsync()).Count);
    }
}
