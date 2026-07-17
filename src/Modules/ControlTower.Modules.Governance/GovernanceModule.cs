using ControlTower.Platform;

namespace ControlTower.Modules.Governance;

/// <summary>C2 — Governance Orchestration (V1.5). Intake, approval, lifecycle gates, attestation.</summary>
public sealed class GovernanceModule : IModule
{
    public string Context => "C2";
}
