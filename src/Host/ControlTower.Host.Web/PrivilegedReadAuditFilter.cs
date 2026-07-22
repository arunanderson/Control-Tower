using ControlTower.Modules.Audit;

namespace ControlTower.Host.Web;

public sealed record PrivilegedReadRequirement(string Resource);

public sealed class PrivilegedReadAuditFilter(PrivilegedReadRequirement requirement) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var request = context.HttpContext.Request;
        var actor = request.Headers["X-Actor"].ToString();
        var purpose = request.Headers["X-Purpose"].ToString();
        if (string.IsNullOrWhiteSpace(actor) || string.IsNullOrWhiteSpace(purpose))
            return Results.BadRequest(new { error = "privileged reads require X-Actor and X-Purpose" });

        var result = await next(context);
        var audit = context.HttpContext.RequestServices.GetRequiredService<PrivilegedAccessService>();
        await audit.RecordReadAsync(actor, purpose, requirement.Resource,
            context.HttpContext.TraceIdentifier, context.HttpContext.RequestAborted);
        return result;
    }
}

public static class PrivilegedReadAuditEndpointExtensions
{
    public static RouteHandlerBuilder AuditPrivilegedRead(this RouteHandlerBuilder builder, string resource) =>
        builder.AddEndpointFilter(new PrivilegedReadAuditFilter(new PrivilegedReadRequirement(resource)));
}
