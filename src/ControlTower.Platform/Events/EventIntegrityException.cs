namespace ControlTower.Platform.Events;

/// <summary>A bounded failure in event-envelope construction or verification.</summary>
public sealed class EventIntegrityException(string message) : Exception(message);
