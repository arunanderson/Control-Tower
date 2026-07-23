namespace ControlTower.Modules.Trust.Authorization;

/// <summary>
/// Opaque C8 E19 reference used everywhere outside the severable person-key map. It carries no
/// directory identifier and remains safe to retain after the map entry is severed.
/// </summary>
public readonly record struct PersonKey
{
    public PersonKey(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("PersonKey cannot be empty.", nameof(value));
        Value = value;
    }

    public Guid Value { get; }
    public bool IsValid => Value != Guid.Empty;
    public override string ToString() => Value.ToString("D");
}

public interface IPersonKeyReader
{
    Task<PersonKey?> FindAsync(
        Guid directoryObjectId,
        CancellationToken ct = default);
}

/// <summary>
/// Minimal C8 E19 lookup seam used to keep raw directory identity out of E18. Raw identity is
/// permitted only behind this boundary. The durable E19 lifecycle must additionally provide
/// field protection, O(1) severance and privileged auditing for every read and write.
/// </summary>
public interface IPersonKeyMap : IPersonKeyReader
{
    Task<PersonKey> GetOrCreateAsync(
        Guid directoryObjectId,
        CancellationToken ct = default);
}

/// <summary>Production-safe default until the field-protected durable E19 adapter is composed.</summary>
public sealed class DenyAllPersonKeyReader : IPersonKeyReader
{
    public Task<PersonKey?> FindAsync(
        Guid directoryObjectId,
        CancellationToken ct = default) =>
        Task.FromResult<PersonKey?>(null);
}

/// <summary>An auditable actor reference that never embeds an Entra object ID.</summary>
public readonly record struct RoleAssignmentActor
{
    private RoleAssignmentActor(string value) => Value = value;

    public string Value { get; }
    internal bool IsValid =>
        !string.IsNullOrWhiteSpace(Value)
        && Value.Length <= 160
        && !Value.Any(char.IsControl)
        && (Value.StartsWith("person:", StringComparison.Ordinal)
            || Value.StartsWith("system:", StringComparison.Ordinal));

    public static RoleAssignmentActor Person(PersonKey personKey)
    {
        if (!personKey.IsValid)
            throw new RoleAssignmentException(
                "A non-empty person key is required.");
        return new($"person:{personKey}");
    }

    public static RoleAssignmentActor System(string systemId)
    {
        var value = systemId?.Trim();
        if (string.IsNullOrWhiteSpace(value)
            || value.Length > 128
            || value.Any(char.IsControl)
            || value.Any(character =>
                !char.IsAsciiLetterOrDigit(character)
                && character is not '-' and not '_' and not '.'))
        {
            throw new RoleAssignmentException(
                "A bounded system actor identifier is required.");
        }

        return new($"system:{value}");
    }

    public override string ToString() => Value ?? string.Empty;
}
