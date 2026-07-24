using System.Text;
using System.Text.Json.Serialization;
using ControlTower.Platform.Identity;

namespace ControlTower.Platform.Events;

/// <summary>
/// Bounded, technology-neutral reference used for E20 aggregate and correlation identities.
/// </summary>
public readonly record struct EventReference
{
    private const int MaximumKindLength = 64;
    private const int MaximumValueLength = 256;

    [JsonConstructor]
    public EventReference(string kind, string value)
    {
        var normalizedKind = kind?.Trim();
        var normalizedValue = value?.Trim();
        if (!string.Equals(
                kind,
                normalizedKind,
                StringComparison.Ordinal)
            || !IsKind(normalizedKind))
            throw new ArgumentException(
                "A bounded canonical event-reference kind is required.",
                nameof(kind));
        if (!string.Equals(
                value,
                normalizedValue,
                StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(normalizedValue)
            || normalizedValue.Length > MaximumValueLength
            || normalizedValue.Any(char.IsControl)
            || !EventText.IsWellFormedUnicode(normalizedValue))
        {
            throw new ArgumentException(
                "A bounded event-reference value is required.",
                nameof(value));
        }

        Kind = normalizedKind!;
        Value = normalizedValue!;
    }

    public string Kind { get; } = string.Empty;

    public string Value { get; } = string.Empty;

    public bool IsValid
    {
        get
        {
            try
            {
                _ = new EventReference(Kind, Value);
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }
    }

    public static EventReference For(string kind, Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException(
                "An event reference cannot contain an empty identifier.",
                nameof(value));

        return new(kind, value.ToString("D"));
    }

    public override string ToString() =>
        IsValid ? $"{Kind}:{Value}" : string.Empty;

    private static bool IsKind(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Length <= MaximumKindLength
        && char.IsAsciiLetter(value[0])
        && value.All(character =>
            char.IsAsciiLetterOrDigit(character)
            || character == '-')
        && string.Equals(value, value.ToLowerInvariant(), StringComparison.Ordinal);
}

/// <summary>
/// The caller-owned semantic portion of E20. Tenant, stream position, event contract, recorded time,
/// privilege and integrity links remain store-controlled.
/// </summary>
public sealed record EventAppendMetadata
{
    private const int MaximumReasonLength = 2048;

    public EventAppendMetadata(
        EventReference aggregateReference,
        AuditActor actor,
        string? reason = null,
        EventReference? correlationReference = null)
    {
        if (!aggregateReference.IsValid)
            throw new ArgumentException(
                "A valid aggregate reference is required.",
                nameof(aggregateReference));
        if (!actor.IsValid)
            throw new ArgumentException(
                "A valid opaque audit actor is required.",
                nameof(actor));
        if (correlationReference is { } correlation
            && !correlation.IsValid)
        {
            throw new ArgumentException(
                "The correlation reference is invalid.",
                nameof(correlationReference));
        }

        AggregateReference = aggregateReference;
        Actor = actor;
        Reason = ValidateReason(reason);
        CorrelationReference = correlationReference;
    }

    public EventReference AggregateReference { get; }

    public AuditActor Actor { get; }

    public string? Reason { get; }

    public EventReference? CorrelationReference { get; }

    private static string? ValidateReason(string? reason)
    {
        if (reason is null)
            return null;

        var normalized = reason.Trim();
        if (!string.Equals(
                reason,
                normalized,
                StringComparison.Ordinal)
            || normalized.Length == 0
            || normalized.Length > MaximumReasonLength
            || normalized.Any(char.IsControl)
            || !EventText.IsWellFormedUnicode(normalized))
        {
            throw new ArgumentException(
                "A present event reason must be bounded non-empty text.",
                nameof(reason));
        }

        return normalized;
    }
}

internal static class EventText
{
    private static readonly Encoding StrictUtf8 =
        new UTF8Encoding(
            encoderShouldEmitUTF8Identifier: false,
            throwOnInvalidBytes: true);

    public static bool IsWellFormedUnicode(string value)
    {
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

    public static byte[] EncodeUtf8(string value) =>
        StrictUtf8.GetBytes(value);
}
