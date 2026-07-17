using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
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
}
