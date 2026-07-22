using ControlTower.Platform;
using Microsoft.Extensions.DependencyInjection;

namespace ControlTower.Modules.Audit;

/// <summary>C9 — Audit &amp; Evidence. The immutable event record is the audit trail (ADR-015).</summary>
public sealed class AuditModule : IModule
{
    public string Context => "C9";
}

public static class AuditModuleServiceCollectionExtensions
{
    public static IServiceCollection AddAuditModule(this IServiceCollection services)
    {
        services.AddSingleton<IPrivilegedAccessProjection, InMemoryPrivilegedAccessProjection>();
        services.AddScoped<PrivilegedAccessService>();
        return services;
    }
}
