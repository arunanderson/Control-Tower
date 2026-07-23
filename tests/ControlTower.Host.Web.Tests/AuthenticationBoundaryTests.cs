using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ControlTower.Host.Web.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace ControlTower.Host.Web.Tests;

public class AuthenticationBoundaryTests(LocalJwtWebFactory factory)
    : IClassFixture<LocalJwtWebFactory>
{
    [Fact]
    public void Host_without_an_api_audience_fails_closed()
    {
        using var unconfigured = new MissingAudienceWebFactory();

        var exception = Assert.ThrowsAny<Exception>(
            () => unconfigured.CreateClient());

        Assert.Contains(
            "Authentication:Audience is required",
            exception.ToString(),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Every_non_health_endpoint_challenges_anonymous_requests()
    {
        var endpoints = factory.Services
            .GetRequiredService<EndpointDataSource>()
            .Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.RoutePattern.RawText is not null)
            .ToList();

        foreach (var endpoint in endpoints)
        {
            var route = $"/{endpoint.RoutePattern.RawText!.TrimStart('/')}";
            var allowsAnonymous =
                endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null;
            if (route is "/health" or "/ready")
            {
                Assert.True(allowsAnonymous, $"{route} must remain anonymous.");
            }
            else
            {
                Assert.False(allowsAnonymous, $"{route} must not allow anonymous access.");
                var path = Regex.Replace(
                    route,
                    "\\{[^}]+\\}",
                    Guid.NewGuid().ToString("D"));
                var methods = endpoint.Metadata
                    .GetMetadata<IHttpMethodMetadata>()?
                    .HttpMethods
                    ?? ["GET"];
                foreach (var method in methods)
                {
                    using var request = new HttpRequestMessage(
                        new HttpMethod(method),
                        path);
                    if (method is "POST" or "PUT" or "PATCH")
                    {
                        request.Content = new StringContent(
                            "{}",
                            Encoding.UTF8,
                            "application/json");
                    }

                    var response = await factory.CreateClient()
                        .SendAsync(request);
                    await AssertGenericChallenge(response);
                }
            }
        }
    }

    [Fact]
    public async Task Command_api_is_not_mapped_in_production()
    {
        using var production = new ConfiguredProductionWebFactory();
        var client = production.CreateClient();
        var apiEndpoints = production.Services
            .GetRequiredService<EndpointDataSource>()
            .Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint =>
                endpoint.RoutePattern.RawText?.StartsWith(
                    "/api",
                    StringComparison.OrdinalIgnoreCase) == true)
            .ToList();

        Assert.Equal(
            HttpStatusCode.OK,
            (await client.GetAsync("/health")).StatusCode);
        Assert.Empty(apiEndpoints);
    }

    [Fact]
    public async Task Invalid_signature_issuer_audience_and_lifetime_are_rejected()
    {
        var tenant = Guid.NewGuid();
        var actor = Guid.NewGuid();
        factory.AllowTenant(tenant, tenant);

        using var otherRsa = RSA.Create(2048);
        var wrongKey = new RsaSecurityKey(otherRsa) { KeyId = "wrong-key" };
        var tokens = new[]
        {
            factory.IssueHumanToken(tenant, actor, "subject", signingKey: wrongKey),
            factory.IssueHumanToken(
                tenant,
                actor,
                "subject",
                issuer: LocalJwtWebFactory.IssuerFor(Guid.NewGuid())),
            factory.IssueHumanToken(
                tenant,
                actor,
                "subject",
                issuer: $"https://wrong-issuer.test/{tenant:D}/v2.0"),
            factory.IssueHumanToken(
                tenant,
                actor,
                "subject",
                audience: "api://wrong-audience"),
            factory.IssueHumanToken(
                tenant,
                actor,
                "subject",
                notBefore: DateTime.UtcNow.AddMinutes(-20),
                expires: DateTime.UtcNow.AddMinutes(-10)),
            factory.IssueHumanToken(
                tenant,
                actor,
                "subject",
                notBefore: DateTime.UtcNow.AddMinutes(10),
                expires: DateTime.UtcNow.AddMinutes(20)),
        };

        foreach (var token in tokens)
        {
            var response = await factory.ClientWithToken(token).GetAsync("/whoami");
            await AssertGenericChallenge(response);
            Assert.DoesNotContain(tenant.ToString(), await response.Content.ReadAsStringAsync());
        }
    }

    [Fact]
    public async Task Missing_malformed_or_duplicate_identity_claims_are_rejected()
    {
        var tenant = Guid.NewGuid();
        var actor = Guid.NewGuid();
        factory.AllowTenant(tenant, tenant);
        var valid = LocalJwtWebFactory.HumanClaims(tenant, actor, "subject").ToList();

        var claimSets = new List<IReadOnlyCollection<Claim>>
        {
            valid.Where(claim => claim.Type != "tid").ToList(),
            valid.Where(claim => claim.Type != "oid").ToList(),
            valid.Where(claim => claim.Type != "sub").ToList(),
            valid.Where(claim => claim.Type != "scp").ToList(),
            valid.Select(claim => claim.Type == "tid" ? new Claim("tid", Guid.Empty.ToString()) : claim).ToList(),
            valid.Select(claim => claim.Type == "oid" ? new Claim("oid", Guid.Empty.ToString()) : claim).ToList(),
            valid.Select(claim => claim.Type == "sub" ? new Claim("sub", " ") : claim).ToList(),
            valid.Select(claim => claim.Type == "tid" ? new Claim("tid", "not-a-guid") : claim).ToList(),
            valid.Select(claim => claim.Type == "oid" ? new Claim("oid", "not-a-guid") : claim).ToList(),
            valid.Select(claim => claim.Type == "sub" ? new Claim("sub", "bad\u0001subject") : claim).ToList(),
            valid.Select(claim => claim.Type == "sub" ? new Claim("sub", new string('x', 257)) : claim).ToList(),
            valid.Select(claim => claim.Type == "scp" ? new Claim("scp", "other.scope") : claim).ToList(),
            valid.Append(new Claim("tid", tenant.ToString())).ToList(),
            valid.Append(new Claim("oid", actor.ToString())).ToList(),
            valid.Append(new Claim("sub", "second-subject")).ToList(),
        };

        foreach (var claims in claimSets)
        {
            var token = factory.IssueToken(
                claims,
                LocalJwtWebFactory.IssuerFor(tenant));
            var response = await factory.ClientWithToken(token).GetAsync("/whoami");
            await AssertGenericChallenge(response);
        }
    }

    [Fact]
    public async Task Cryptographically_valid_but_unonboarded_tenant_is_rejected()
    {
        var tenant = Guid.NewGuid();
        var token = factory.IssueHumanToken(
            tenant,
            Guid.NewGuid(),
            "subject");

        var response = await factory.ClientWithToken(token).GetAsync("/whoami");

        await AssertGenericChallenge(response);
        Assert.DoesNotContain(tenant.ToString(), await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task App_only_and_wrong_scope_tokens_are_rejected()
    {
        var tenant = Guid.NewGuid();
        var objectId = Guid.NewGuid();
        factory.AllowTenant(tenant, tenant);
        var appOnlyClaims = new[]
        {
            new Claim("tid", tenant.ToString("D")),
            new Claim("oid", objectId.ToString("D")),
            new Claim("sub", objectId.ToString("D")),
            new Claim("azp", Guid.NewGuid().ToString("D")),
            new Claim("roles", "ControlTower.Read.All"),
        };
        var wrongScopeClaims = LocalJwtWebFactory
            .HumanClaims(tenant, objectId, "subject")
            .Select(claim => claim.Type == "scp"
                ? new Claim("scp", "unrelated.scope")
                : claim);

        foreach (var claims in new[] { appOnlyClaims, wrongScopeClaims })
        {
            var response = await factory.ClientWithToken(factory.IssueToken(
                    claims,
                    LocalJwtWebFactory.IssuerFor(tenant)))
                .GetAsync("/whoami");
            await AssertGenericChallenge(response);
        }
    }

    [Fact]
    public async Task Issuer_must_name_the_same_tenant_as_the_signed_tid()
    {
        var tenant = Guid.NewGuid();
        factory.AllowTenant(tenant, tenant);
        var token = factory.IssueHumanToken(
            tenant,
            Guid.NewGuid(),
            "subject",
            issuer: LocalJwtWebFactory.IssuerFor(Guid.NewGuid()));

        var response = await factory.ClientWithToken(token).GetAsync("/whoami");

        await AssertGenericChallenge(response);
    }

    [Fact]
    public async Task Signing_key_metadata_issuer_is_bound_to_the_token_tenant()
    {
        var keyTenant = Guid.NewGuid();
        var tokenTenant = Guid.NewGuid();
        using var mismatched = new LocalJwtWebFactory(
            LocalJwtWebFactory.IssuerFor(keyTenant));
        mismatched.AllowTenant(tokenTenant, tokenTenant);
        var mismatchedToken = mismatched.IssueHumanToken(
            tokenTenant,
            Guid.NewGuid(),
            "subject");

        var rejected = await mismatched
            .ClientWithToken(mismatchedToken)
            .GetAsync("/whoami");

        await AssertGenericChallenge(rejected);

        using var matched = new LocalJwtWebFactory(
            LocalJwtWebFactory.IssuerFor(tokenTenant));
        var actor = Guid.NewGuid();
        var accepted = matched.AuthenticatedClient(
            tokenTenant,
            tokenTenant,
            actor);

        await AssertIdentity(
            accepted,
            tokenTenant,
            tokenTenant,
            actor);
    }

    [Fact]
    public void Two_directories_cannot_map_to_one_internal_tenant()
    {
        var internalTenant = Guid.NewGuid();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"Authentication:TenantMappings:{Guid.NewGuid():D}"] =
                    internalTenant.ToString("D"),
                [$"Authentication:TenantMappings:{Guid.NewGuid():D}"] =
                    internalTenant.ToString("D"),
            })
            .Build();

        var exception = Assert.Throws<InvalidOperationException>(
            () => new ConfigurationAllowedTenantDirectory(configuration));

        Assert.Contains("only one external directory", exception.Message);
    }

    [Fact]
    public async Task Parallel_requests_keep_tenant_and_actor_contexts_isolated()
    {
        var directoryA = Guid.NewGuid();
        var directoryB = Guid.NewGuid();
        var internalA = Guid.NewGuid();
        var internalB = Guid.NewGuid();
        var actorA = Guid.NewGuid();
        var actorB = Guid.NewGuid();
        var clientA = factory.AuthenticatedClient(
            internalA,
            directoryA,
            actorA);
        var clientB = factory.AuthenticatedClient(
            internalB,
            directoryB,
            actorB);

        var requests = Enumerable.Range(0, 20)
            .SelectMany(_ => new[]
            {
                AssertIdentity(clientA, internalA, directoryA, actorA),
                AssertIdentity(clientB, internalB, directoryB, actorB),
            });

        await Task.WhenAll(requests);
    }

    [Fact]
    public async Task Purpose_is_bounded_business_context_not_identity()
    {
        var client = factory.AuthenticatedClient(Guid.NewGuid());
        client.DefaultRequestHeaders.Add("X-Purpose", new string('x', 513));

        var response = await client.GetAsync("/api/trust/privileged-access");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var controlCharacterClient = factory.AuthenticatedClient(Guid.NewGuid());
        controlCharacterClient.DefaultRequestHeaders.TryAddWithoutValidation(
            "X-Purpose",
            "invalid\u0001purpose");
        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await controlCharacterClient.GetAsync(
                "/api/trust/privileged-access")).StatusCode);
    }

    [Fact]
    public async Task Approval_reference_is_bounded_single_business_context()
    {
        var client = factory.AuthenticatedClient(Guid.NewGuid());
        var placed = await client.PostAsJsonAsync(
            "/api/trust/legal-holds",
            new { dataClass = "All", reason = "Investigation" });
        var holdId = (await JsonDocument.ParseAsync(
                await placed.Content.ReadAsStreamAsync()))
            .RootElement
            .GetProperty("holdId")
            .GetGuid();

        var invalidHeaders = new[]
        {
            new[] { new string('x', 257) },
            new[] { "invalid\u0001reference" },
            new[] { "approval:1", "approval:2" },
        };
        foreach (var values in invalidHeaders)
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"/api/trust/legal-holds/{holdId}/release")
            {
                Content = JsonContent.Create(new { reason = "Matter closed" }),
            };
            request.Headers.TryAddWithoutValidation(
                "X-Approval-Reference",
                values);

            var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }

    private static async Task AssertIdentity(
        HttpClient client,
        Guid internalTenant,
        Guid directoryTenant,
        Guid objectId)
    {
        var identity = await client.GetFromJsonAsync<WhoAmI>("/whoami");
        Assert.Equal(internalTenant.ToString(), identity!.Tenant);
        Assert.Equal(directoryTenant, identity.DirectoryTenant);
        Assert.Equal(
            $"entra:{directoryTenant:D}:{objectId:D}",
            identity.Actor);
    }

    private static async Task AssertGenericChallenge(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var challenge = Assert.Single(response.Headers.WwwAuthenticate);
        Assert.Equal("Bearer", challenge.Scheme);
        Assert.True(string.IsNullOrEmpty(challenge.Parameter));
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("tid", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("oid", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("issuer", body, StringComparison.OrdinalIgnoreCase);
    }

    private sealed record WhoAmI(
        string Tenant,
        Guid DirectoryTenant,
        string Actor);
}
