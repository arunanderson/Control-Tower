using ControlTower.Host.Web.Authentication;
using ControlTower.Adapters.InMemory;
using ControlTower.Modules.Audit;
using ControlTower.Modules.Ledger.Application;
using ControlTower.Modules.Trust.Authorization;
using ControlTower.Modules.Trust.Infrastructure;
using ControlTower.Platform.Audit;
using ControlTower.Platform.Events;
using ControlTower.Platform.Identity;
using ControlTower.Platform.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ControlTower.Host.Web.Authorization;

public sealed record RequiredControlTowerCapability(
    ControlTowerCapability Capability);

public sealed record ControlTowerCapabilityRequirement(
    ControlTowerCapability Capability) : IAuthorizationRequirement;

public sealed class CurrentEffectiveAccess
{
    public TenantId? Tenant { get; private set; }
    public Guid? SubjectObjectId { get; private set; }
    public PersonKey? SubjectPersonKey { get; private set; }
    public EffectiveAccess? Access { get; private set; }

    public void Set(
        TenantId tenant,
        Guid subjectObjectId,
        EffectiveAccess access)
    {
        Tenant = tenant;
        SubjectObjectId = subjectObjectId;
        SubjectPersonKey = access.SubjectPersonKey;
        Access = access;
    }

    public bool TryGet(
        TenantId tenant,
        Guid subjectObjectId,
        out EffectiveAccess access)
    {
        if (Tenant == tenant
            && SubjectObjectId == subjectObjectId
            && Access is { } current)
        {
            access = current;
            return true;
        }

        access = null!;
        return false;
    }

    public AuditActor RequireActor(
        TenantId tenant,
        Guid subjectObjectId)
    {
        if (Tenant != tenant
            || SubjectObjectId != subjectObjectId
            || SubjectPersonKey is not { } personKey
            || !personKey.IsValid)
        {
            throw new InvalidOperationException(
                "A server-resolved request PersonKey is required.");
        }

        return AuditActor.Person(personKey);
    }
}

public sealed class ControlTowerCapabilityHandler(
    IEffectiveAccessResolver accessResolver,
    ITenantContextAccessor tenants,
    CurrentEffectiveAccess current)
    : AuthorizationHandler<ControlTowerCapabilityRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ControlTowerCapabilityRequirement requirement)
    {
        if (context.Resource is not HttpContext http
            || !tenants.HasTenant
            || !AuthenticatedHumanContext.TryGet(http, out var human))
        {
            return;
        }

        if (!current.TryGet(
                tenants.Current,
                human.ObjectId,
                out var access))
        {
            access = await accessResolver.ResolveAsync(
                human.ObjectId,
                new PersonKeyAccessContext(
                    AuditActor.System("host-authorization"),
                    "resolve effective access",
                    new EventReference(
                        "http-request",
                        http.TraceIdentifier),
                    PrivilegedReadPolicy.NotApplicable()),
                http.RequestAborted);
            current.Set(
                tenants.Current,
                human.ObjectId,
                access);
        }
        if (access.OrganizationScope is OrganizationScope.TenantWide
            && access.Allows(requirement.Capability))
        {
            context.Succeed(requirement);
        }
    }
}

/// <summary>
/// Host composition bridge from the existing C1 authorization port to the current request's C8
/// decision. The modules remain independent and there is no second role matrix.
/// </summary>
public sealed class HttpContextLedgerAuthorizer(
    IHttpContextAccessor httpContext,
    ITenantContextAccessor tenants,
    CurrentEffectiveAccess current) : ILedgerAuthorizer
{
    public bool IsAllowed(LedgerCapability capability)
    {
        var http = httpContext.HttpContext;
        if (http is null
            || !tenants.HasTenant
            || !AuthenticatedHumanContext.TryGet(http, out var human)
            || current.Tenant != tenants.Current
            || current.SubjectObjectId != human.ObjectId
            || current.Access is null)
        {
            return false;
        }

        return capability switch
        {
            LedgerCapability.TriageAssets
                or LedgerCapability.RegisterAssets
                or LedgerCapability.RetireAssets =>
                current.Access.Allows(ControlTowerCapability.LedgerManage),
            _ => false,
        };
    }
}

public static class ControlTowerAuthorizationExtensions
{
    public static IServiceCollection AddControlTowerAuthorization(
        this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddSingleton<IPersonKeyReader, DenyAllPersonKeyReader>();
        services.AddSingleton<IRoleAssignmentReader, DenyAllRoleAssignmentReader>();
        services.AddScoped<IEffectiveAccessResolver, EffectiveAccessResolver>();
        services.AddScoped<CurrentEffectiveAccess>();
        services.AddScoped<IAuthorizationHandler, ControlTowerCapabilityHandler>();

        var authorization = services.AddAuthorizationBuilder();
        foreach (var capability in Enum.GetValues<ControlTowerCapability>())
        {
            authorization.AddPolicy(
                PolicyName(capability),
                policy => policy
                    .RequireAuthenticatedUser()
                    .AddRequirements(
                        new ControlTowerCapabilityRequirement(capability)));
        }

        return services;
    }

    public static IServiceCollection AddDevelopmentControlTowerAuthorization(
        this IServiceCollection services)
    {
        services.RemoveAll<IPersonKeyReader>();
        services.RemoveAll<IPersonKeyMap>();
        services.RemoveAll<IRoleAssignmentReader>();
        services.RemoveAll<IRoleAssignmentStore>();
        services.RemoveAll<ILedgerAuthorizer>();
        services.RemoveAll<IPrivilegedReadAuditor>();
        services.AddSingleton<
            InMemoryPrivilegedReadAuditor>();
        services.AddSingleton<IPrivilegedReadRecordSink>(
            provider =>
                provider.GetRequiredService<
                    InMemoryPrivilegedReadAuditor>());
        services.AddSingleton<IPrivilegedReadAuditor>(
            provider =>
                new PrivilegedReadEvidenceAuditor(
                    provider.GetRequiredService<
                        IPrivilegedReadRecordSink>(),
                    provider.GetRequiredService<
                        IPrivilegedAccessProjection>(),
                    provider.GetRequiredService<IEventStore>(),
                    provider.GetRequiredService<
                        ITenantContextAccessor>()));
        services.AddSingleton<InMemoryPersonKeyMap>();
        services.AddSingleton<IPersonKeyReader>(
            provider => provider.GetRequiredService<InMemoryPersonKeyMap>());
        services.AddSingleton<IPersonKeyMap>(
            provider => provider.GetRequiredService<InMemoryPersonKeyMap>());
        services.AddSingleton<InMemoryRoleAssignmentStore>();
        services.AddSingleton<IRoleAssignmentReader>(
            provider => provider.GetRequiredService<InMemoryRoleAssignmentStore>());
        services.AddSingleton<IRoleAssignmentStore>(
            provider => provider.GetRequiredService<InMemoryRoleAssignmentStore>());
        services.AddScoped<RoleAssignmentService>();
        services.AddScoped<ILedgerAuthorizer, HttpContextLedgerAuthorizer>();
        return services;
    }

    public static RouteHandlerBuilder RequireControlTowerCapability(
        this RouteHandlerBuilder builder,
        ControlTowerCapability capability) =>
        builder
            .WithMetadata(new RequiredControlTowerCapability(capability))
            .RequireAuthorization(PolicyName(capability));

    public static string PolicyName(ControlTowerCapability capability) =>
        $"ControlTower.Capability.{capability}";
}
