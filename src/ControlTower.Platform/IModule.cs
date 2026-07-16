namespace ControlTower.Platform;

/// <summary>Marker for a bounded-context module (ADR-020 modular monolith). One per C1..C9.</summary>
public interface IModule
{
    /// <summary>Bounded-context code, e.g. "C1" (Ledger).</summary>
    string Context { get; }
}
