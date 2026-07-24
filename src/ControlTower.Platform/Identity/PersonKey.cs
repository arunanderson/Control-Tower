using System.Text.Json.Serialization;

namespace ControlTower.Platform.Identity;

/// <summary>
/// Opaque E19 person reference. Raw directory identity is permitted only behind the PersonKeyMap
/// boundary; every other persisted model refers to a person through this surrogate.
/// </summary>
public readonly record struct PersonKey
{
    [JsonConstructor]
    public PersonKey(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("PersonKey cannot be empty.", nameof(value));

        Value = value;
    }

    public Guid Value { get; }

    [JsonIgnore]
    public bool IsValid => Value != Guid.Empty;

    public static PersonKey New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}
