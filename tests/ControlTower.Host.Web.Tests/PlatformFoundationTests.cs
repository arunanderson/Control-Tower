using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace ControlTower.Host.Web.Tests;

public class PlatformFoundationTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Health_returns_200_without_a_tenant()
    {
        var client = factory.CreateClient();
        var response = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Tenant_scoped_endpoint_is_rejected_without_a_tenant_header()
    {
        var client = factory.CreateClient();
        var response = await client.GetAsync("/whoami");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Tenant_scoped_endpoint_succeeds_within_a_tenant_scope()
    {
        var client = factory.CreateClient();
        var tenantId = Guid.NewGuid();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/whoami");
        request.Headers.Add("X-Tenant-Id", tenantId.ToString());

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<WhoAmI>();
        Assert.Equal(tenantId.ToString(), body!.Tenant);
    }

    private sealed record WhoAmI(string Tenant);
}
