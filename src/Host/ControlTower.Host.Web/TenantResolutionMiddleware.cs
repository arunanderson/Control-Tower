using ControlTower.Platform.Tenancy;

namespace ControlTower.Host.Web;

/// <summary>
/// Opens an ambient tenant scope for the request when a valid tenant is presented. In V1 this is a
/// dev/header source; production resolves the tenant from the validated Entra token (later phase).
/// Endpoints that require a tenant read <see cref="ITenantContextAccessor.Current"/> (throws if absent).
/// </summary>
public sealed class TenantResolutionMiddleware(RequestDelegate next)
{
    public const string TenantHeader = "X-Tenant-Id";

    public async Task InvokeAsync(HttpContext context, ITenantContextAccessor tenants)
    {
        if (context.Request.Headers.TryGetValue(TenantHeader, out var raw)
            && Guid.TryParse(raw.ToString(), out var id)
            && id != Guid.Empty)
        {
            using var scope = tenants.BeginScope(new TenantId(id));
            await next(context);
        }
        else
        {
            await next(context);
        }
    }
}
