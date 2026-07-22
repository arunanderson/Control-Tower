using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ControlTower.Modules.Ledger.Application;
using ControlTower.Modules.Ledger.Domain;
using ControlTower.Platform.Tenancy;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ControlTower.Host.Web.Tests;

/// <summary>Runs the host in Development so the Experience API (backed by dev module wiring) is mapped.</summary>
public sealed class DevWebFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseEnvironment("Development");
}

public class ExperienceApiTests(DevWebFactory factory) : IClassFixture<DevWebFactory>
{
    private HttpClient TenantClient()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", Guid.NewGuid().ToString());
        return client;
    }

    [Theory]
    [InlineData("/api/portfolio/assets")]
    [InlineData("/api/economics/executive")]
    [InlineData("/api/governance/cases")]
    [InlineData("/api/trust/coverage")]
    [InlineData("/api/resolution/merge-cases")]
    public async Task Api_requires_a_tenant(string path)
    {
        var response = await factory.CreateClient().GetAsync(path);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/portfolio/assets")]
    [InlineData("/api/economics/executive")]
    [InlineData("/api/economics/portfolio")]
    [InlineData("/api/economics/departments")]
    [InlineData("/api/economics/agents")]
    [InlineData("/api/governance/cases")]
    [InlineData("/api/governance/debt")]
    [InlineData("/api/trust/coverage")]
    [InlineData("/api/admin/summary")]
    [InlineData("/api/resolution/merge-cases")]
    public async Task Api_returns_200_within_a_tenant_scope(string path)
    {
        var response = await TenantClient().GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Economics_executive_carries_evidence_fields()
    {
        var body = await TenantClient().GetStringAsync("/api/economics/executive");
        Assert.Contains("evidenceClass", body);
        Assert.Contains("methodology", body);
        Assert.Contains("asOf", body);
        Assert.Contains("validationState", body);
    }

    [Fact]
    public async Task Trust_coverage_is_reported_honestly()
    {
        var body = await TenantClient().GetStringAsync("/api/trust/coverage");
        Assert.Contains("providersConnected", body);
        Assert.Contains("coverageNote", body);
    }

    [Fact]
    public async Task Asset_record_returns_404_for_an_unknown_asset()
    {
        var response = await TenantClient().GetAsync($"/api/portfolio/assets/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---- Resolution & Merge Workbench (P6-T04) ----

    private HttpClient ClientFor(Guid tenant)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenant.ToString());
        return client;
    }

    private static ObservationDescriptor Obs(string value) =>
        new(Guid.NewGuid(), new NativeIdentifier("sys", "t", value), "Seeded", "agent", "Self-reported / Manual Import");

    /// <summary>Seeds two assets sharing identifier X (a collision) via the resolution service, in the tenant scope.</summary>
    private async Task<(Guid a, Guid b)> SeedCollisionAsync(Guid tenant)
    {
        using var scope = factory.Services.CreateScope();
        var accessor = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
        using var _ = accessor.BeginScope(new TenantId(tenant));
        var svc = scope.ServiceProvider.GetRequiredService<EntityResolutionService>();

        var a = (await svc.ResolveAsync(Obs("X"))).AssetId!.Value;
        var b = (await svc.ResolveAsync(Obs("Y"))).AssetId!.Value;
        await svc.ApproveManualLinkAsync(b, NativeIdentifierSet.Of(new NativeIdentifier("sys", "t", "X")), null, "seed");
        await svc.ResolveAsync(Obs("X")); // X now maps to A and B → collision → merge case
        return (a.Value, b.Value);
    }

    private sealed record CaseDto(Guid MergeCaseId, string Confidence, string Reason);

    [Fact]
    public async Task Merge_case_queue_lists_collisions_and_an_operator_can_resolve_them()
    {
        var tenant = Guid.NewGuid();
        await SeedCollisionAsync(tenant);
        var client = ClientFor(tenant);

        var cases = await client.GetFromJsonAsync<List<CaseDto>>("/api/resolution/merge-cases");
        Assert.NotNull(cases);
        var mergeCase = Assert.Single(cases!);
        Assert.Equal("Low", mergeCase.Confidence); // a collision is never auto-linked

        var resolve = await client.PostAsJsonAsync($"/api/resolution/merge-cases/{mergeCase.MergeCaseId}/resolve", new { outcome = "kept-separate" });
        Assert.Equal(HttpStatusCode.OK, resolve.StatusCode);

        var after = await client.GetFromJsonAsync<List<CaseDto>>("/api/resolution/merge-cases");
        Assert.Empty(after!);
    }

    [Fact]
    public async Task Operator_merge_supersedes_source_links_visible_in_the_resolution_view()
    {
        var tenant = Guid.NewGuid();
        var (a, b) = await SeedCollisionAsync(tenant); // A←X, B←(Y,X)
        var client = ClientFor(tenant);

        var merge = await client.PostAsJsonAsync("/api/resolution/merge", new { targetId = a, sourceId = b });
        Assert.Equal(HttpStatusCode.OK, merge.StatusCode);

        // Source's links are superseded (retained, not deleted); target absorbed them.
        var sourceView = await client.GetStringAsync($"/api/resolution/assets/{b}");
        Assert.Contains("Superseded", sourceView);
        var targetView = await client.GetStringAsync($"/api/resolution/assets/{a}");
        Assert.Contains("Active", targetView);
    }

    [Fact]
    public async Task Resolution_view_is_404_for_an_unknown_asset()
    {
        var response = await ClientFor(Guid.NewGuid()).GetAsync($"/api/resolution/assets/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
