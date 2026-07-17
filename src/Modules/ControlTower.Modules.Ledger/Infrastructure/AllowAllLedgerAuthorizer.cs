using ControlTower.Modules.Ledger.Application;

namespace ControlTower.Modules.Ledger.Infrastructure;

/// <summary>
/// DEV-ONLY permissive authorizer. Replaced by the C8.2 delegated role model, which maps the
/// authenticated principal's roles to <see cref="LedgerCapability"/>. Never use in production.
/// </summary>
public sealed class AllowAllLedgerAuthorizer : ILedgerAuthorizer
{
    public bool IsAllowed(LedgerCapability capability) => true;
}
