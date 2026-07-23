using System.Reflection;

namespace ControlTower.Platform.Events;

/// <summary>
/// Whether an event belongs to the privileged security/audit surface. The value is persisted in the
/// integrity envelope; changing it is therefore a detectable contract change.
/// </summary>
public enum EventPrivilege : byte
{
    Standard = 0,
    Privileged = 1,
}

/// <summary>
/// Declares the stable persisted name and privilege classification of a concrete domain event.
/// There is intentionally no inherited or implicit default: every concrete event must make both
/// decisions explicitly.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class DomainEventContractAttribute(
    string eventType,
    EventPrivilege privilege) : Attribute
{
    public string EventType { get; } = eventType;
    public EventPrivilege Privilege { get; } = privilege;
}

/// <summary>A validated domain-event persistence contract.</summary>
public sealed record DomainEventContract(string EventType, EventPrivilege Privilege);

/// <summary>Resolves and validates the explicit persistence contract on a concrete domain event.</summary>
public static class DomainEventContracts
{
    public const int MaximumEventTypeLength = 160;

    public static DomainEventContract Resolve(IDomainEvent @event)
    {
        ArgumentNullException.ThrowIfNull(@event);
        return Resolve(@event.GetType());
    }

    public static DomainEventContract Resolve(Type eventType)
    {
        ArgumentNullException.ThrowIfNull(eventType);
        if (eventType.IsAbstract || !typeof(IDomainEvent).IsAssignableFrom(eventType))
        {
            throw new EventIntegrityException(
                "A concrete domain-event type is required.");
        }

        var declaration = eventType.GetCustomAttribute<DomainEventContractAttribute>(
            inherit: false);
        if (declaration is null)
        {
            throw new EventIntegrityException(
                "The domain event has no explicit persistence contract.");
        }

        ValidateEventType(declaration.EventType);
        if (!Enum.IsDefined(declaration.Privilege))
        {
            throw new EventIntegrityException(
                "The domain event has an invalid privilege classification.");
        }

        return new(declaration.EventType, declaration.Privilege);
    }

    internal static void ValidateEventType(string? eventType)
    {
        if (string.IsNullOrWhiteSpace(eventType)
            || eventType.Length > MaximumEventTypeLength
            || !char.IsAsciiLetter(eventType[0])
            || eventType.Any(character =>
                !char.IsAsciiLetterOrDigit(character)
                && character is not '.' and not '-' and not '_'))
        {
            throw new EventIntegrityException(
                "The canonical event type is invalid.");
        }
    }
}
