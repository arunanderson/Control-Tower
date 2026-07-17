using ControlTower.Platform;

namespace ControlTower.Modules.Audit;

/// <summary>C9 — Audit &amp; Evidence. The immutable event record is the audit trail (ADR-015).</summary>
public sealed class AuditModule : IModule
{
    public string Context => "C9";
}
