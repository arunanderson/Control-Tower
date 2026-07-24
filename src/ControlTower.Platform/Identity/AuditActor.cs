using System.Text;
using System.Text.Json.Serialization;

namespace ControlTower.Platform.Identity;

/// <summary>The three actor classes named by the frozen shared-kernel model.</summary>
public enum AuditActorKind : byte
{
    Human = 1,
    System = 2,
    Provider = 3,
}

/// <summary>
/// Canonical, opaque audit actor. A human is represented only by an E19 PersonKey; raw directory
/// tenant, object, subject, display or email identity cannot be constructed as an actor.
/// </summary>
public readonly record struct AuditActor
{
    private const int MaximumWorkloadIdLength = 128;
    private static readonly Encoding StrictUtf8 =
        new UTF8Encoding(
            encoderShouldEmitUTF8Identifier: false,
            throwOnInvalidBytes: true);

    [JsonConstructor]
    public AuditActor(
        AuditActorKind kind,
        PersonKey? personKey,
        string? workloadId)
    {
        if (!Enum.IsDefined(kind))
            throw new ArgumentOutOfRangeException(
                nameof(kind),
                "The audit actor kind is invalid.");

        if (kind == AuditActorKind.Human)
        {
            if (personKey is null
                || !personKey.Value.IsValid
                || workloadId is not null)
            {
                throw new ArgumentException(
                    "A human audit actor requires only an opaque PersonKey.",
                    nameof(personKey));
            }
        }
        else if (personKey is not null)
        {
            throw new ArgumentException(
                "A workload audit actor cannot contain a PersonKey.",
                nameof(personKey));
        }
        else if (workloadId is null
            || !string.Equals(
                workloadId,
                workloadId.Trim(),
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "A canonical workload audit actor identifier is required.",
                nameof(workloadId));
        }
        else if (kind == AuditActorKind.System
            && !IsBoundedSystemId(workloadId))
        {
            throw new ArgumentException(
                "A bounded workload audit actor identifier is required.",
                nameof(workloadId));
        }
        else if (kind == AuditActorKind.Provider
            && !IsBoundedProviderId(workloadId))
        {
            throw new ArgumentException(
                "A bounded provider audit actor identifier is required.",
                nameof(workloadId));
        }

        Kind = kind;
        PersonKey = personKey;
        WorkloadId = workloadId;
    }

    public AuditActorKind Kind { get; }

    public PersonKey? PersonKey { get; }

    public string? WorkloadId { get; }

    [JsonIgnore]
    public string OpaqueId =>
        Kind == AuditActorKind.Human
            ? PersonKey?.ToString() ?? string.Empty
            : WorkloadId ?? string.Empty;

    [JsonIgnore]
    public bool IsValid
    {
        get
        {
            try
            {
                _ = new AuditActor(
                    Kind,
                    PersonKey,
                    WorkloadId);
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }
    }

    public static AuditActor Person(PersonKey personKey)
    {
        if (!personKey.IsValid)
            throw new ArgumentException(
                "A human audit actor requires an opaque PersonKey.",
                nameof(personKey));

        return new(
            AuditActorKind.Human,
            personKey,
            workloadId: null);
    }

    public static AuditActor System(string systemId) =>
        new(
            AuditActorKind.System,
            personKey: null,
            workloadId: systemId);

    public static AuditActor Provider(string providerId) =>
        new(
            AuditActorKind.Provider,
            personKey: null,
            workloadId: providerId);

    public override string ToString()
    {
        if (!IsValid)
            return string.Empty;

        var prefix = Kind switch
        {
            AuditActorKind.Human => "person",
            AuditActorKind.System => "system",
            AuditActorKind.Provider => "provider",
            _ => string.Empty,
        };
        return $"{prefix}:{OpaqueId}";
    }

    private static bool IsBoundedSystemId(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Length <= MaximumWorkloadIdLength
        && !ContainsDirectoryIdentityShape(value)
        && value.All(character =>
            char.IsAsciiLetterOrDigit(character)
            || character is '-' or '_' or '.');

    private static bool IsBoundedProviderId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Length > MaximumWorkloadIdLength
            || value.Any(char.IsControl)
            || ContainsDirectoryIdentityShape(value))
        {
            return false;
        }

        try
        {
            _ = StrictUtf8.GetByteCount(value);
            return true;
        }
        catch (EncoderFallbackException)
        {
            return false;
        }
    }

    private static bool ContainsDirectoryIdentityShape(
        string value)
    {
        if (value.Contains('@')
            || value.StartsWith(
                "entra:",
                StringComparison.OrdinalIgnoreCase)
            || value.StartsWith(
                "oid:",
                StringComparison.OrdinalIgnoreCase)
            || value.StartsWith(
                "object-id:",
                StringComparison.OrdinalIgnoreCase)
            || value.StartsWith(
                "directory-object:",
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        for (var index = 0;
             index <= value.Length - 36;
             index++)
        {
            if (Guid.TryParseExact(
                    value.AsSpan(index, 36),
                    "D",
                    out _))
            {
                return true;
            }
        }

        for (var index = 0;
             index <= value.Length - 32;
             index++)
        {
            if (Guid.TryParseExact(
                    value.AsSpan(index, 32),
                    "N",
                    out _))
            {
                return true;
            }
        }

        return false;
    }
}
