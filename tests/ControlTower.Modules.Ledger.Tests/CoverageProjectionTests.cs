using System.Text.Json;
using ControlTower.Modules.Ledger.Application;
using ControlTower.Modules.Ledger.Infrastructure;
using ControlTower.Platform.Tenancy;
using Xunit;

namespace ControlTower.Modules.Ledger.Tests;

public class CoverageProjectionTests
{
    private static readonly TenantId TenantA = new(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
    private static readonly TenantId TenantB = new(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));

    private static (TenantContextAccessor Accessor, ICoverageReadModel Coverage, ProviderCoverageUpdatedHandler Handler) Build()
    {
        var accessor = new TenantContextAccessor();
        var coverage = new InMemoryCoverageReadModel(new InMemoryAssetLedgerReadModel(accessor), accessor);
        return (accessor, coverage, new ProviderCoverageUpdatedHandler(coverage, accessor));
    }

    private static byte[] Payload(TenantId tenant, Guid runId, DateTimeOffset completedAt, string capability = "Inventory") =>
        JsonSerializer.SerializeToUtf8Bytes(new
        {
            RunId = runId,
            Tenant = tenant.ToString(),
            ConnectionRef = "conn-1",
            SurfaceId = "manual-csv",
            Capability = capability,
            Outcome = "Completed",
            CompletedAt = completedAt,
            FreshnessExpectationSeconds = 2_592_000d,
            Observed = 2,
            New = 2,
            Changed = 0,
            Suppressed = 0,
        });

    [Fact]
    public async Task Completed_run_projects_honest_surface_coverage_and_replay_is_idempotent()
    {
        var r = Build();
        var run = Guid.NewGuid();
        var completed = DateTimeOffset.UtcNow.AddMinutes(-1);

        await r.Handler.HandleAsync(Payload(TenantA, run, completed));
        await r.Handler.HandleAsync(Payload(TenantA, run, completed));

        using var _ = r.Accessor.BeginScope(TenantA);
        var view = await r.Coverage.GetAsync();
        var surface = Assert.Single(view.Surfaces);
        Assert.Equal(1, view.ProvidersConnected);
        Assert.True(surface.IsFresh);
        Assert.Equal("Connected", surface.State);
        Assert.Equal(["Inventory"], surface.CoveredCapabilities);
        Assert.Equal(2, surface.Observed);
    }

    [Fact]
    public async Task Older_replay_cannot_regress_a_newer_fact_and_tenants_are_isolated()
    {
        var r = Build();
        var now = DateTimeOffset.UtcNow;
        await r.Handler.HandleAsync(Payload(TenantA, Guid.NewGuid(), now, "Inventory"));
        await r.Handler.HandleAsync(Payload(TenantA, Guid.NewGuid(), now.AddDays(-1), "Inventory"));
        await r.Handler.HandleAsync(Payload(TenantB, Guid.NewGuid(), now, "Cost"));

        using (r.Accessor.BeginScope(TenantA))
        {
            var a = await r.Coverage.GetAsync();
            Assert.Equal(now, Assert.Single(a.Surfaces).LastSuccessfulSweep);
            Assert.Equal(["Inventory"], a.Surfaces[0].CoveredCapabilities);
        }
        using (r.Accessor.BeginScope(TenantB))
            Assert.Equal(["Cost"], Assert.Single((await r.Coverage.GetAsync()).Surfaces).CoveredCapabilities);
    }

    [Fact]
    public async Task No_runs_is_an_explicit_unknown_coverage_state()
    {
        var r = Build();
        using var _ = r.Accessor.BeginScope(TenantA);
        var view = await r.Coverage.GetAsync();
        Assert.Empty(view.Surfaces);
        Assert.Equal(0, view.ProvidersConnected);
        Assert.Null(view.LastSuccessfulSweep);
        Assert.Contains("not yet established", view.CoverageNote);
    }
}
