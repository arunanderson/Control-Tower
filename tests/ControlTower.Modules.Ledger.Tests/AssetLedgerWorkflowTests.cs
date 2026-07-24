using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ControlTower.Adapters.InMemory;
using ControlTower.Modules.Ledger.Application;
using ControlTower.Modules.Ledger.Domain;
using ControlTower.Modules.Ledger.Infrastructure;
using ControlTower.Platform.Events;
using ControlTower.Platform.Identity;
using ControlTower.Platform.Tenancy;
using Xunit;

namespace ControlTower.Modules.Ledger.Tests;

public class AssetLedgerWorkflowTests
{
    private static readonly AuditActor Actor =
        AuditActor.System("ledger-workflow-test");

    private static ILedgerAuthorizer AllowAll { get; } =
        new TestAuthorizer(LedgerCapability.TriageAssets, LedgerCapability.RegisterAssets, LedgerCapability.RetireAssets);

    private static Harness Build(ILedgerAuthorizer authorizer)
    {
        var accessor = new TenantContextAccessor();
        var events = new InMemoryEventStore(accessor);
        var repository = new InMemoryAssetRepository(accessor);
        var readModel = new InMemoryAssetLedgerReadModel(accessor);
        var service = new AssetRegistrationService(repository, readModel, events, authorizer, accessor, TaxonomyScheme.Default);
        return new Harness(service, accessor, repository, readModel, events);
    }

    [Fact]
    public async Task Registration_flow_persists_the_asset_the_events_and_the_read_model()
    {
        var h = Build(AllowAll);
        var tenant = new TenantId(Guid.NewGuid());

        using (h.Accessor.BeginScope(tenant))
        {
            var id = await h.Service.DiscoverAsync("Sales Copilot", new AssetType("agent"), Actor);
            await h.Service.TriageAsync(id, Actor);
            await h.Service.RegisterAsync(
                id,
                "Drafts sales emails",
                new PersonRef(PersonKey.New()),
                Actor);

            var view = await h.ReadModel.GetAsync(id);
            Assert.NotNull(view);
            Assert.Equal("Registered", view!.RegistrationStatus);
            Assert.False(view.IsOwnerless);
            Assert.Equal("Assigned", view.OwnerDisplayName);

            var stream = await h.Events.ReadAllAsync();
            Assert.True(stream.Count >= 4); // discovered, triaged, registered, ownership assigned
        }
    }

    [Fact]
    public async Task The_ledger_is_tenant_isolated()
    {
        var h = Build(AllowAll);
        var tenantA = new TenantId(Guid.NewGuid());
        var tenantB = new TenantId(Guid.NewGuid());

        LedgerAssetId id;
        using (h.Accessor.BeginScope(tenantA))
        {
            id = await h.Service.DiscoverAsync("A-asset", new AssetType("agent"), Actor);
            await h.Service.TriageAsync(id, Actor);
            await h.Service.RegisterAsync(
                id,
                "purpose",
                new PersonRef(PersonKey.New()),
                Actor);
        }

        using (h.Accessor.BeginScope(tenantB))
        {
            Assert.Empty(await h.ReadModel.QueryAsync());
            Assert.Empty(await h.Repository.ListAsync());
            Assert.Null(await h.ReadModel.GetAsync(id));
        }

        using (h.Accessor.BeginScope(tenantA))
        {
            Assert.Single(await h.ReadModel.QueryAsync());
        }
    }

    [Fact]
    public async Task Registration_without_the_capability_is_rejected_and_leaves_the_asset_triaged()
    {
        var h = Build(new TestAuthorizer(LedgerCapability.TriageAssets)); // no RegisterAssets
        var tenant = new TenantId(Guid.NewGuid());

        using (h.Accessor.BeginScope(tenant))
        {
            var id = await h.Service.DiscoverAsync("X", new AssetType("agent"), Actor);
            await h.Service.TriageAsync(id, Actor);

            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => h.Service.RegisterAsync(
                    id,
                    "purpose",
                    new PersonRef(PersonKey.New()),
                    Actor));

            var view = await h.ReadModel.GetAsync(id);
            Assert.Equal("Triaged", view!.RegistrationStatus);
        }
    }

    [Fact]
    public async Task Invalid_event_context_is_rejected_before_asset_mutation()
    {
        var h = Build(AllowAll);
        using var _ = h.Accessor.BeginScope(
            new TenantId(Guid.NewGuid()));
        var id = await h.Service.DiscoverAsync(
            "Evidence boundary",
            new AssetType("agent"),
            Actor);
        EventReference? invalidCorrelation =
            default(EventReference);
        Assert.True(invalidCorrelation.HasValue);
        var baselineEventCount =
            (await h.Events.ReadAllAsync()).Count;

        await Assert.ThrowsAsync<DomainException>(
            () => h.Service.TriageAsync(
                id,
                Actor,
                invalidCorrelation));

        Assert.Equal(
            RegistrationStatus.Discovered,
            (await h.Repository.GetAsync(id))!
            .RegistrationStatus);
        Assert.Equal(
            "Discovered",
            (await h.ReadModel.GetAsync(id))!
            .RegistrationStatus);
        Assert.Equal(
            baselineEventCount,
            (await h.Events.ReadAllAsync()).Count);
    }

    private sealed record Harness(
        AssetRegistrationService Service,
        ITenantContextAccessor Accessor,
        IAssetRepository Repository,
        IAssetLedgerReadModel ReadModel,
        IEventStore Events);

    private sealed class TestAuthorizer(params LedgerCapability[] allowed) : ILedgerAuthorizer
    {
        private readonly HashSet<LedgerCapability> _allowed = [.. allowed];
        public bool IsAllowed(LedgerCapability capability) => _allowed.Contains(capability);
    }
}
