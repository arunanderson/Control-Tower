using ControlTower.Host.Web.Authentication;
using ControlTower.Platform.Tenancy;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;

namespace ControlTower.Host.Web;

/// <summary>
/// Maps the signed external directory tenant to an onboarded internal tenant and opens the ambient
/// scope. Authentication has already validated signature, issuer, audience, lifetime and human
/// identity claims. Caller-controlled tenant headers are never consulted.
/// </summary>
public sealed class AuthenticatedTenantContextMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(
        HttpContext context,
        ITenantContextAccessor tenants,
        IAllowedTenantDirectory directory)
    {
        if (context.GetEndpoint()?.Metadata.GetMetadata<IAllowAnonymous>() is not null)
        {
            await next(context);
            return;
        }

        if (context.User.Identity?.IsAuthenticated != true)
        {
            await next(context);
            return;
        }

        if (!AuthenticatedHumanContext.TryGet(context, out var human)
            || !directory.TryResolve(human.DirectoryTenantId, out var tenant))
        {
            await context.ChallengeAsync();
            return;
        }

        using var scope = tenants.BeginScope(tenant);
        await next(context);
    }
}
