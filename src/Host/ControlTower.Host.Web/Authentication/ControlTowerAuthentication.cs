using System.Security.Claims;
using ControlTower.Platform.Tenancy;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using Microsoft.IdentityModel.Validators;

namespace ControlTower.Host.Web.Authentication;

public sealed record AuthenticatedHuman(
    Guid DirectoryTenantId,
    Guid ObjectId,
    string Subject);

public static class AuthenticatedHumanContext
{
    private static readonly object ItemKey = new();

    internal static void Set(HttpContext context, AuthenticatedHuman human) =>
        context.Items[ItemKey] = human;

    public static bool TryGet(HttpContext context, out AuthenticatedHuman human)
    {
        if (context.Items.TryGetValue(ItemKey, out var value)
            && value is AuthenticatedHuman authenticated)
        {
            human = authenticated;
            return true;
        }

        human = null!;
        return false;
    }

    public static AuthenticatedHuman Require(HttpContext context) =>
        TryGet(context, out var human)
            ? human
            : throw new InvalidOperationException("A validated human request identity is required.");
}

public interface IAllowedTenantDirectory
{
    bool TryResolve(Guid directoryTenantId, out TenantId tenantId);
}

public sealed class ConfigurationAllowedTenantDirectory : IAllowedTenantDirectory
{
    private readonly IReadOnlyDictionary<Guid, TenantId> _mappings;

    public ConfigurationAllowedTenantDirectory(IConfiguration configuration)
    {
        var mappings = new Dictionary<Guid, TenantId>();
        var assignedInternalTenants = new HashSet<Guid>();
        foreach (var entry in configuration
                     .GetSection($"{ControlTowerAuthenticationOptions.SectionName}:TenantMappings")
                     .GetChildren())
        {
            if (!Guid.TryParse(entry.Key, out var directoryTenantId)
                || directoryTenantId == Guid.Empty
                || !Guid.TryParse(entry.Value, out var internalTenantId)
                || internalTenantId == Guid.Empty)
            {
                throw new InvalidOperationException(
                    "Authentication tenant mappings must contain non-empty GUID directory and internal tenant IDs.");
            }

            if (!mappings.TryAdd(directoryTenantId, new TenantId(internalTenantId)))
                throw new InvalidOperationException("Authentication tenant mappings must be unique.");
            if (!assignedInternalTenants.Add(internalTenantId))
            {
                throw new InvalidOperationException(
                    "Each internal tenant may map to only one external directory tenant.");
            }
        }

        _mappings = mappings;
    }

    public bool TryResolve(Guid directoryTenantId, out TenantId tenantId) =>
        _mappings.TryGetValue(directoryTenantId, out tenantId);
}

internal sealed record ControlTowerAuthenticationOptions(
    string Authority,
    string Audience,
    string IssuerTemplate,
    string RequiredScope)
{
    public const string SectionName = "Authentication";

    public static ControlTowerAuthenticationOptions From(IConfiguration configuration)
    {
        var section = configuration.GetSection(SectionName);
        var authority = section["Authority"]?.Trim();
        var audience = section["Audience"]?.Trim();
        var issuerTemplate = section["IssuerTemplate"]?.Trim();
        var requiredScope = section["RequiredScope"]?.Trim();

        if (!Uri.TryCreate(authority, UriKind.Absolute, out var authorityUri)
            || authorityUri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException(
                "Authentication:Authority must be an absolute HTTPS URI.");
        }

        if (string.IsNullOrWhiteSpace(audience))
            throw new InvalidOperationException("Authentication:Audience is required.");

        var issuerParts = issuerTemplate?.Split(
            "{tenantid}",
            StringSplitOptions.None);
        var sampleIssuer = issuerTemplate?.Replace(
            "{tenantid}",
            Guid.Empty.ToString("D"),
            StringComparison.Ordinal);
        if (issuerParts is null
            || issuerParts.Length != 2
            || !Uri.TryCreate(sampleIssuer, UriKind.Absolute, out var issuerUri)
            || issuerUri.Scheme != Uri.UriSchemeHttps
            || !string.Equals(
                authorityUri.Host,
                issuerUri.Host,
                StringComparison.OrdinalIgnoreCase)
            || !string.Equals(
                issuerParts[1],
                "/v2.0",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Authentication:IssuerTemplate must be an authority-hosted HTTPS v2.0 URI "
                + "containing exactly one {tenantid} placeholder.");
        }

        if (string.IsNullOrWhiteSpace(requiredScope)
            || requiredScope.Length > 256
            || requiredScope.Any(char.IsWhiteSpace)
            || requiredScope.Any(char.IsControl))
        {
            throw new InvalidOperationException(
                "Authentication:RequiredScope must be one bounded scope name.");
        }

        return new(
            authorityUri.ToString(),
            audience,
            issuerTemplate!,
            requiredScope);
    }
}

public static class ControlTowerAuthenticationExtensions
{
    public static IServiceCollection AddControlTowerAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var settings = ControlTowerAuthenticationOptions.From(configuration);
        services.AddSingleton<IAllowedTenantDirectory>(
            new ConfigurationAllowedTenantDirectory(configuration));

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.IncludeErrorDetails = false;
                options.Authority = settings.Authority;
                options.Audience = settings.Audience;
                options.RequireHttpsMetadata = true;
                options.SaveToken = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ClockSkew = TimeSpan.FromMinutes(2),
                    RequireExpirationTime = true,
                    RequireSignedTokens = true,
                    ValidateAudience = true,
                    ValidateIssuer = true,
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
                    IssuerValidator = (issuer, _, _) =>
                        ValidateIssuerShape(issuer, settings.IssuerTemplate),
                };
                options.TokenValidationParameters.EnableAadSigningKeyIssuerValidation();
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = context =>
                    {
                        if (!TryCreateHuman(
                                context.Principal,
                                context.SecurityToken.Issuer,
                                settings.IssuerTemplate,
                                settings.RequiredScope,
                                out var human))
                        {
                            context.Fail("The access token does not establish a valid Control Tower human identity.");
                            return Task.CompletedTask;
                        }

                        AuthenticatedHumanContext.Set(context.HttpContext, human);
                        return Task.CompletedTask;
                    },
                };
            });

        services
            .AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build());

        return services;
    }

    private static string ValidateIssuerShape(string issuer, string issuerTemplate)
    {
        var parts = issuerTemplate.Split(
            "{tenantid}",
            StringSplitOptions.None);
        if (parts.Length != 2
            || !issuer.StartsWith(parts[0], StringComparison.OrdinalIgnoreCase)
            || !issuer.EndsWith(parts[1], StringComparison.OrdinalIgnoreCase))
        {
            throw new SecurityTokenInvalidIssuerException("The token issuer is not an accepted Entra tenant issuer.");
        }

        var tenantPartLength = issuer.Length - parts[0].Length - parts[1].Length;
        if (tenantPartLength <= 0
            || !Guid.TryParse(
                issuer.AsSpan(parts[0].Length, tenantPartLength),
                out var issuerTenant)
            || issuerTenant == Guid.Empty)
        {
            throw new SecurityTokenInvalidIssuerException("The token issuer does not contain a valid tenant.");
        }

        return issuer;
    }

    private static bool TryCreateHuman(
        ClaimsPrincipal? principal,
        string issuer,
        string issuerTemplate,
        string requiredScope,
        out AuthenticatedHuman human)
    {
        human = null!;
        if (principal is null
            || !TryGetSingleClaim(principal, "tid", out var rawTenant)
            || !Guid.TryParse(rawTenant, out var directoryTenantId)
            || directoryTenantId == Guid.Empty
            || !TryGetSingleClaim(principal, "oid", out var rawObject)
            || !Guid.TryParse(rawObject, out var objectId)
            || objectId == Guid.Empty
            || !TryGetSingleClaim(principal, "sub", out var subject)
            || !IsBoundedText(subject, 256)
            || !TryGetSingleClaim(principal, "scp", out var scopes)
            || !IsBoundedText(scopes, 2048)
            || !scopes.Split(
                    ' ',
                    StringSplitOptions.RemoveEmptyEntries
                    | StringSplitOptions.TrimEntries)
                .Contains(requiredScope, StringComparer.Ordinal))
        {
            return false;
        }

        var expectedIssuer = issuerTemplate.Replace(
            "{tenantid}",
            directoryTenantId.ToString("D"),
            StringComparison.Ordinal);
        if (!string.Equals(issuer, expectedIssuer, StringComparison.OrdinalIgnoreCase))
            return false;

        human = new AuthenticatedHuman(directoryTenantId, objectId, subject);
        return true;
    }

    private static bool TryGetSingleClaim(
        ClaimsPrincipal principal,
        string claimType,
        out string value)
    {
        var claims = principal.FindAll(claimType).ToArray();
        if (claims.Length == 1 && !string.IsNullOrWhiteSpace(claims[0].Value))
        {
            value = claims[0].Value.Trim();
            return true;
        }

        value = string.Empty;
        return false;
    }

    internal static bool IsBoundedText(string value, int maximumLength) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Length <= maximumLength
        && value.All(character => !char.IsControl(character));
}
