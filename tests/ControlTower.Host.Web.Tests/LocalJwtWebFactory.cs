using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using ControlTower.Host.Web.Authentication;
using ControlTower.Platform.Tenancy;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace ControlTower.Host.Web.Tests;

public sealed class LocalJwtWebFactory : WebApplicationFactory<Program>
{
    public const string Audience = "api://control-tower-tests";
    public const string Scope = "controltower.access";
    public const string IssuerTemplate = "https://issuer.controltower.test/{tenantid}/v2.0";

    private readonly RSA _rsa = RSA.Create(2048);
    private readonly TestAllowedTenantDirectory _directory = new();
    private readonly RsaSecurityKey _signingKey;
    private readonly string _signingKeyIssuer;

    public LocalJwtWebFactory()
        : this(IssuerTemplate)
    {
    }

    internal LocalJwtWebFactory(string signingKeyIssuer)
    {
        _signingKey = new RsaSecurityKey(_rsa) { KeyId = "control-tower-local-test-key" };
        _signingKeyIssuer = signingKeyIssuer;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting(
            "Authentication:Authority",
            "https://issuer.controltower.test/organizations/v2.0");
        builder.UseSetting("Authentication:Audience", Audience);
        builder.UseSetting("Authentication:IssuerTemplate", IssuerTemplate);
        builder.UseSetting("Authentication:RequiredScope", Scope);

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IAllowedTenantDirectory>();
            services.AddSingleton<IAllowedTenantDirectory>(_directory);
            services.PostConfigure<JwtBearerOptions>(
                JwtBearerDefaults.AuthenticationScheme,
                options =>
                {
                    var publicKey = new RsaSecurityKey(
                        _rsa.ExportParameters(includePrivateParameters: false))
                    {
                        KeyId = _signingKey.KeyId,
                    };
                    var metadataKey =
                        JsonWebKeyConverter.ConvertFromRSASecurityKey(publicKey);
                    metadataKey.Alg = SecurityAlgorithms.RsaSha256;
                    metadataKey.Use = JsonWebKeyUseNames.Sig;
                    metadataKey.AdditionalData[OpenIdProviderMetadataNames.Issuer] =
                        _signingKeyIssuer;
                    var keySet = new JsonWebKeySet();
                    keySet.Keys.Add(metadataKey);
                    var configuration = new OpenIdConnectConfiguration
                    {
                        Issuer = IssuerTemplate,
                        JsonWebKeySet = keySet,
                    };
                    configuration.SigningKeys.Add(metadataKey);
                    options.Authority = null;
                    options.MetadataAddress = string.Empty;
                    options.Configuration = configuration;
                    options.ConfigurationManager =
                        new StaticConfigurationManager<OpenIdConnectConfiguration>(
                            configuration);
                    options.TokenValidationParameters.ValidAudience = Audience;
                });
        });
    }

    public void AllowTenant(Guid directoryTenantId, Guid internalTenantId) =>
        _directory.Allow(directoryTenantId, new TenantId(internalTenantId));

    public HttpClient AuthenticatedClient(
        Guid internalTenantId,
        Guid? directoryTenantId = null,
        Guid? objectId = null,
        string subject = "control-tower-test-user")
    {
        var external = directoryTenantId ?? internalTenantId;
        AllowTenant(external, internalTenantId);
        return ClientWithToken(IssueHumanToken(
            external,
            objectId ?? Guid.NewGuid(),
            subject));
    }

    public HttpClient ClientWithToken(string token)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public string IssueHumanToken(
        Guid directoryTenantId,
        Guid objectId,
        string subject,
        string? issuer = null,
        string? audience = null,
        DateTime? notBefore = null,
        DateTime? expires = null,
        SecurityKey? signingKey = null,
        IEnumerable<Claim>? additionalClaims = null)
    {
        var claims = HumanClaims(directoryTenantId, objectId, subject).ToList();
        if (additionalClaims is not null)
            claims.AddRange(additionalClaims);

        return IssueToken(
            claims,
            issuer ?? IssuerFor(directoryTenantId),
            audience ?? Audience,
            notBefore,
            expires,
            signingKey);
    }

    public string IssueToken(
        IEnumerable<Claim> claims,
        string issuer,
        string audience = Audience,
        DateTime? notBefore = null,
        DateTime? expires = null,
        SecurityKey? signingKey = null)
    {
        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(
            issuer,
            audience,
            claims,
            notBefore ?? now.AddMinutes(-1),
            expires ?? now.AddMinutes(10),
            new SigningCredentials(
                signingKey ?? _signingKey,
                SecurityAlgorithms.RsaSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public static IEnumerable<Claim> HumanClaims(
        Guid directoryTenantId,
        Guid objectId,
        string subject)
    {
        yield return new Claim("tid", directoryTenantId.ToString("D"));
        yield return new Claim("oid", objectId.ToString("D"));
        yield return new Claim("sub", subject);
        yield return new Claim("scp", Scope);
    }

    public static string IssuerFor(Guid directoryTenantId) =>
        IssuerTemplate.Replace(
            "{tenantid}",
            directoryTenantId.ToString("D"),
            StringComparison.Ordinal);

    private sealed class TestAllowedTenantDirectory : IAllowedTenantDirectory
    {
        private readonly ConcurrentDictionary<Guid, TenantId> _mappings = new();

        public void Allow(Guid directoryTenantId, TenantId internalTenantId) =>
            _mappings[directoryTenantId] = internalTenantId;

        public bool TryResolve(Guid directoryTenantId, out TenantId tenantId) =>
            _mappings.TryGetValue(directoryTenantId, out tenantId);
    }
}

public sealed class ConfiguredProductionWebFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.UseSetting(
            "Authentication:Authority",
            "https://issuer.controltower.test/organizations/v2.0");
        builder.UseSetting("Authentication:Audience", LocalJwtWebFactory.Audience);
        builder.UseSetting(
            "Authentication:IssuerTemplate",
            LocalJwtWebFactory.IssuerTemplate);
        builder.UseSetting(
            "Authentication:RequiredScope",
            LocalJwtWebFactory.Scope);
    }
}

public sealed class MissingAudienceWebFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Authentication:Audience", string.Empty);
    }
}
