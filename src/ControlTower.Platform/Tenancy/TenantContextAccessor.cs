namespace ControlTower.Platform.Tenancy;

/// <summary>
/// AsyncLocal-backed tenant context. There is no way to obtain a tenant without opening a scope, and
/// no way to read one when none is set — the unforgeable, by-construction guarantee of ADR-021.
/// </summary>
public sealed class TenantContextAccessor : ITenantContextAccessor
{
    private static readonly AsyncLocal<TenantId?> Ambient = new();

    public bool HasTenant => Ambient.Value is not null;

    public TenantId Current =>
        Ambient.Value
        ?? throw new InvalidOperationException(
            "No tenant context is set. Every operation must execute within a tenant scope "
            + "(ADR-021: tenant scoping by construction, not developer discipline).");

    public IDisposable BeginScope(TenantId tenant)
    {
        var previous = Ambient.Value;
        Ambient.Value = tenant;
        return new Scope(() => Ambient.Value = previous);
    }

    private sealed class Scope(Action onDispose) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            onDispose();
        }
    }
}
