using System.Text;
using ControlTower.Adapters.InMemory;
using ControlTower.Modules.Providers.Application;
using ControlTower.Modules.Providers.Domain;
using ControlTower.Modules.Providers.Infrastructure;
using ControlTower.Platform.Tenancy;
using Xunit;

namespace ControlTower.Modules.Providers.Tests;

public class ProviderSweepJobTests
{
    private sealed record Rig(
        TenantContextAccessor Tenants,
        IProviderConnectionStore Connections,
        IObservationStore Observations,
        InMemoryEventStore Events,
        InMemoryOutbox Outbox,
        ProviderSweepRequestService Requests,
        ProviderSweepRequestedHandler Handler);

    private static Rig Build(bool registerCsv = true)
    {
        var tenants = new TenantContextAccessor();
        var connections = new InMemoryProviderConnectionStore(tenants);
        var observations = new InMemoryObservationStore(tenants);
        var events = new InMemoryEventStore(tenants);
        var outbox = new InMemoryOutbox();
        var registry = new ProviderRegistry();
        if (registerCsv) registry.Register(new CsvManualImportProvider());
        var ingestion = new ObservationIngestionService(
            observations, events, outbox, new InMemoryWatermarkStore(), tenants);
        var requests = new ProviderSweepRequestService(connections, events, outbox, tenants);
        var handler = new ProviderSweepRequestedHandler(
            connections, registry, new InMemoryProviderJobReceiptStore(tenants), ingestion, tenants);
        return new(tenants, connections, observations, events, outbox, requests, handler);
    }

    private static ProviderConnection Connection(TenantId tenant, string surface = "manual-csv") => new()
    {
        ConnectionId = "conn-1",
        Tenant = tenant,
        SurfaceId = surface,
        CredentialReference = "vault://tenant/provider/csv",
        Capabilities = new HashSet<ProviderCapability> { ProviderCapability.Inventory },
        Schedule = TimeSpan.FromDays(30),
        Enabled = true,
        Configuration = new Dictionary<string, string>
        {
            ["csv"] = "key,displayName,assetType\nbot-1,Sales Copilot,agent\nbot-2,HR Flow,flow",
            ["private-setting"] = "must-not-enter-job",
        },
    };

    [Fact]
    public async Task Request_is_secret_free_and_worker_executes_the_invariant_ingestion_pipeline()
    {
        var r = Build();
        var tenant = new TenantId(Guid.NewGuid());
        using var _ = r.Tenants.BeginScope(tenant);
        await r.Connections.SaveAsync(Connection(tenant));

        var jobId = await r.Requests.RequestAsync("conn-1", ProviderCapability.Inventory);
        var requestedEvent = Assert.Single(await r.Events.ReadAllAsync());
        Assert.Equal(jobId, requestedEvent.EventId);
        Assert.NotEqual(requestedEvent.PreviousHash, requestedEvent.Hash);
        var job = Assert.Single(await r.Outbox.DequeueBatchAsync(100), x => x.Topic == ProviderSweepRequestService.Topic);
        var json = Encoding.UTF8.GetString(job.Payload);
        Assert.Contains(jobId.ToString(), json);
        Assert.DoesNotContain("vault://", json);
        Assert.DoesNotContain("must-not-enter-job", json);

        await r.Handler.HandleAsync(job.Payload);
        Assert.Equal(2, (await r.Observations.ObservationsAsync()).Count);
        Assert.Single(await r.Observations.RunsAsync());

        await r.Handler.HandleAsync(job.Payload); // completed replay is ignored
        Assert.Single(await r.Observations.RunsAsync());
    }

    [Fact]
    public async Task Failed_job_releases_its_claim_for_external_retry()
    {
        var r = Build(registerCsv: false);
        var tenant = new TenantId(Guid.NewGuid());
        using var _ = r.Tenants.BeginScope(tenant);
        await r.Connections.SaveAsync(Connection(tenant));
        await r.Requests.RequestAsync("conn-1", ProviderCapability.Inventory);
        var job = Assert.Single(await r.Outbox.DequeueBatchAsync(100));

        await Assert.ThrowsAsync<ProviderException>(() => r.Handler.HandleAsync(job.Payload));
        await Assert.ThrowsAsync<ProviderException>(() => r.Handler.HandleAsync(job.Payload));
    }

    [Fact]
    public async Task Connection_lookup_is_tenant_isolated()
    {
        var r = Build();
        var tenantA = new TenantId(Guid.NewGuid());
        var tenantB = new TenantId(Guid.NewGuid());
        using (r.Tenants.BeginScope(tenantA)) await r.Connections.SaveAsync(Connection(tenantA));
        using (r.Tenants.BeginScope(tenantB))
            await Assert.ThrowsAsync<ProviderException>(() =>
                r.Requests.RequestAsync("conn-1", ProviderCapability.Inventory));
    }
}
