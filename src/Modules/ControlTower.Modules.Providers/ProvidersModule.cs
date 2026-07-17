using ControlTower.Platform;

namespace ControlTower.Modules.Providers;

/// <summary>C4 — Provider Integration. The only door in/out for external signals (ADR-009/020).</summary>
public sealed class ProvidersModule : IModule
{
    public string Context => "C4";
}
