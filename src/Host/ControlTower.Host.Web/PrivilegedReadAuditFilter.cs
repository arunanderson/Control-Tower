using ControlTower.Modules.Audit;
using ControlTower.Host.Web.Authentication;
using ControlTower.Host.Web.Authorization;
using ControlTower.Platform.Audit;
using ControlTower.Platform.Events;
using ControlTower.Platform.Tenancy;

namespace ControlTower.Host.Web;

public sealed record PrivilegedReadRequirement(string Resource);

public sealed class PrivilegedReadAuditFilter(PrivilegedReadRequirement requirement) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        var human = AuthenticatedHumanContext.Require(http);
        var tenants =
            http.RequestServices
                .GetRequiredService<ITenantContextAccessor>();
        var actor =
            http.RequestServices
                .GetRequiredService<CurrentEffectiveAccess>()
                .RequireActor(
                    tenants.Current,
                    human.ObjectId);
        if (!RequestBusinessContext.TryGetPurpose(http, out var purpose))
            return Results.BadRequest(new { error = "privileged reads require a valid X-Purpose" });

        var result = await next(context);
        var audit = http.RequestServices.GetRequiredService<PrivilegedAccessService>();
        await audit.RecordReadAsync(
            actor,
            purpose,
            new EventReference(
                "read-model",
                requirement.Resource),
            PrivilegedReadPolicy.NotApplicable(),
            new EventReference(
                "http-request",
                http.TraceIdentifier),
            http.RequestAborted);
        return result;
    }
}

public static class PrivilegedReadAuditEndpointExtensions
{
    public static RouteHandlerBuilder AuditPrivilegedRead(this RouteHandlerBuilder builder, string resource) =>
        builder.AddEndpointFilter(new PrivilegedReadAuditFilter(new PrivilegedReadRequirement(resource)));
}
