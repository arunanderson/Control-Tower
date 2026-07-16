using ControlTower.Platform;

namespace ControlTower.Modules.Experience;

/// <summary>C7 — Experience &amp; Insight. The only door out for human-facing views (ADR-009/020).</summary>
public sealed class ExperienceModule : IModule
{
    public string Context => "C7";
}
