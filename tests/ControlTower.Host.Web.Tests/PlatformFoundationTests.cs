using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Xunit;

namespace ControlTower.Host.Web.Tests;

public class PlatformFoundationTests(LocalJwtWebFactory factory)
    : IClassFixture<LocalJwtWebFactory>
{
    [Fact]
    public async Task Health_returns_200_without_a_tenant()
    {
        var client = factory.CreateClient();
        var response = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Ready_returns_200_without_authentication()
    {
        var response = await factory.CreateClient().GetAsync("/ready");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Tenant_scoped_endpoint_is_rejected_without_authentication()
    {
        var client = factory.CreateClient();
        var response = await client.GetAsync("/whoami");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Tenant_header_cannot_authenticate_a_request()
    {
        var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/whoami");
        request.Headers.Add("X-Tenant-Id", Guid.NewGuid().ToString());

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Signed_identity_maps_to_the_internal_tenant_and_canonical_actor()
    {
        var directoryTenantId = Guid.NewGuid();
        var internalTenantId = Guid.NewGuid();
        var objectId = Guid.NewGuid();
        var client = factory.AuthenticatedClient(
            internalTenantId,
            directoryTenantId,
            objectId);

        var response = await client.GetAsync("/whoami");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<WhoAmI>();
        Assert.Equal(internalTenantId.ToString(), body!.Tenant);
        Assert.Equal(directoryTenantId, body.DirectoryTenant);
        Assert.Equal(
            $"entra:{directoryTenantId:D}:{objectId:D}",
            body.Actor);
    }

    [Fact]
    public async Task Tenant_header_cannot_override_the_signed_tenant()
    {
        var directoryTenantId = Guid.NewGuid();
        var internalTenantId = Guid.NewGuid();
        var client = factory.AuthenticatedClient(
            internalTenantId,
            directoryTenantId);
        client.DefaultRequestHeaders.Add(
            "X-Tenant-Id",
            Guid.NewGuid().ToString());

        var body = await client.GetFromJsonAsync<WhoAmI>("/whoami");

        Assert.Equal(internalTenantId.ToString(), body!.Tenant);
        Assert.Equal(directoryTenantId, body.DirectoryTenant);
    }

    private sealed record WhoAmI(
        string Tenant,
        Guid DirectoryTenant,
        string Actor);
}
