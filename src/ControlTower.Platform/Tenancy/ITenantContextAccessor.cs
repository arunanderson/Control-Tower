namespace ControlTower.Platform.Tenancy;

/// <summary>
/// Ambient tenant context. Reading <see cref="Current"/> outside a scope throws — tenant scoping is
/// enforced by construction, not by developer discipline (ADR-021).
/// </summary>
public interface ITenantContextAccessor
{
    bool HasTenant { get; }
    TenantId Current { get; }
    IDisposable BeginScope(TenantId tenant);
}
