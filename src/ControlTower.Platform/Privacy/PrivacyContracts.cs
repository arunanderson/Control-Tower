using System.Text;
using System.Text.Json.Serialization;

namespace ControlTower.Platform.Privacy;

/// <summary>
/// Privacy marking set once at ingestion. Numeric values preserve the original C4 contract.
/// </summary>
public enum PrivacyMarking
{
    L1 = 0,
    L2 = 1,
    L3 = 2,
    L4 = 3,
}

/// <summary>An opaque jurisdiction identifier; no production taxonomy is embedded in Platform.</summary>
public readonly record struct JurisdictionRef
{
    [JsonConstructor]
    public JurisdictionRef(string value) =>
        Value = PrivacyReferenceText.Require(value, nameof(value));

    public string Value { get; } = string.Empty;

    public bool IsValid => PrivacyReferenceText.IsValid(Value);

    public override string ToString() => IsValid ? Value : string.Empty;
}

/// <summary>An opaque population identifier supplied by an authoritative C5 mapping.</summary>
public readonly record struct PopulationRef
{
    [JsonConstructor]
    public PopulationRef(string value) =>
        Value = PrivacyReferenceText.Require(value, nameof(value));

    public string Value { get; } = string.Empty;

    public bool IsValid => PrivacyReferenceText.IsValid(Value);

    public override string ToString() => IsValid ? Value : string.Empty;
}

/// <summary>
/// An opaque telemetry-policy capability. It is intentionally distinct from provider acquisition
/// capabilities and C8 authorization capabilities.
/// </summary>
public readonly record struct TelemetryCapabilityRef
{
    [JsonConstructor]
    public TelemetryCapabilityRef(string value) =>
        Value = PrivacyReferenceText.Require(value, nameof(value));

    public string Value { get; } = string.Empty;

    public bool IsValid => PrivacyReferenceText.IsValid(Value);

    public override string ToString() => IsValid ? Value : string.Empty;
}

/// <summary>An opaque reference to the retention rule required for enabled L2+ telemetry.</summary>
public readonly record struct RetentionPolicyRef
{
    [JsonConstructor]
    public RetentionPolicyRef(string value) =>
        Value = PrivacyReferenceText.Require(value, nameof(value));

    public string Value { get; } = string.Empty;

    public bool IsValid => PrivacyReferenceText.IsValid(Value);

    public override string ToString() => IsValid ? Value : string.Empty;
}

/// <summary>
/// Already-resolved C5 applicability supplied to policy evaluation. This contract deliberately
/// contains no HR mapping, geography inference or production taxonomy.
/// </summary>
public sealed class PrivacyApplicability
{
    private readonly IReadOnlyList<JurisdictionRef> _jurisdictions;
    private readonly IReadOnlyList<PopulationRef> _populations;

    public PrivacyApplicability(
        IEnumerable<JurisdictionRef> jurisdictions,
        IEnumerable<PopulationRef> populations)
    {
        ArgumentNullException.ThrowIfNull(jurisdictions);
        ArgumentNullException.ThrowIfNull(populations);

        _jurisdictions = Canonicalize(
            jurisdictions,
            reference => reference.IsValid,
            reference => reference.Value,
            "Every jurisdiction reference must be valid.");
        _populations = Canonicalize(
            populations,
            reference => reference.IsValid,
            reference => reference.Value,
            "Every population reference must be valid.");
    }

    public IReadOnlyList<JurisdictionRef> Jurisdictions => _jurisdictions;

    public IReadOnlyList<PopulationRef> Populations => _populations;

    private static IReadOnlyList<T> Canonicalize<T>(
        IEnumerable<T> values,
        Func<T, bool> isValid,
        Func<T, string> key,
        string error)
    {
        var materialized = values.ToArray();
        if (materialized.Any(value => !isValid(value)))
            throw new ArgumentException(error, nameof(values));

        return Array.AsReadOnly(
            materialized
                .Distinct()
                .OrderBy(key, StringComparer.Ordinal)
                .ToArray());
    }
}

/// <summary>A version of an E16 profile that contributed to an effective ceiling.</summary>
public readonly record struct JurisdictionProfileVersionRef
{
    public JurisdictionProfileVersionRef(
        JurisdictionRef jurisdiction,
        long version)
    {
        if (!jurisdiction.IsValid)
            throw new ArgumentException(
                "A valid jurisdiction reference is required.",
                nameof(jurisdiction));
        if (version <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(version),
                "A positive jurisdiction-profile version is required.");

        Jurisdiction = jurisdiction;
        Version = version;
    }

    public JurisdictionRef Jurisdiction { get; }

    public long Version { get; }
}

/// <summary>Tenant-ambient query for the current E16 ceiling.</summary>
public sealed record JurisdictionCeilingQuery
{
    public JurisdictionCeilingQuery(PrivacyApplicability applicability)
    {
        ArgumentNullException.ThrowIfNull(applicability);

        Applicability = applicability;
    }

    public PrivacyApplicability Applicability { get; }
}

/// <summary>
/// Effective E16 result. A non-authoritative result is always L1 and may retain partial evidence
/// solely to explain why resolution failed closed.
/// </summary>
public sealed class JurisdictionCeilingResolution
{
    private readonly IReadOnlyList<JurisdictionProfileVersionRef> _matchedVersions;

    public JurisdictionCeilingResolution(
        PrivacyMarking ceiling,
        bool isAuthoritative,
        IEnumerable<JurisdictionProfileVersionRef> matchedVersions)
    {
        if (!Enum.IsDefined(ceiling))
            throw new ArgumentOutOfRangeException(
                nameof(ceiling),
                "The jurisdiction ceiling is invalid.");
        if (!isAuthoritative && ceiling != PrivacyMarking.L1)
            throw new ArgumentException(
                "A non-authoritative jurisdiction result must fail closed to L1.",
                nameof(ceiling));
        ArgumentNullException.ThrowIfNull(matchedVersions);

        var canonical = matchedVersions
            .Distinct()
            .OrderBy(reference => reference.Jurisdiction.Value, StringComparer.Ordinal)
            .ThenBy(reference => reference.Version)
            .ToArray();
        if (canonical.Any(reference =>
                !reference.Jurisdiction.IsValid
                || reference.Version <= 0))
        {
            throw new ArgumentException(
                "Every matched jurisdiction-profile version must be valid.",
                nameof(matchedVersions));
        }
        if (isAuthoritative && canonical.Length == 0)
            throw new ArgumentException(
                "An authoritative ceiling requires matched profile evidence.",
                nameof(matchedVersions));

        Ceiling = ceiling;
        IsAuthoritative = isAuthoritative;
        _matchedVersions = Array.AsReadOnly(canonical);
    }

    public PrivacyMarking Ceiling { get; }

    public bool IsAuthoritative { get; }

    public IReadOnlyList<JurisdictionProfileVersionRef> MatchedVersions =>
        _matchedVersions;
}

/// <summary>
/// Cross-context E16 port. The implementation is owned by C5; consumers remain module-independent.
/// Tenant authority is ambient and therefore cannot be supplied by callers.
/// </summary>
public interface IJurisdictionCeilingResolver
{
    Task<JurisdictionCeilingResolution> ResolveAsync(
        JurisdictionCeilingQuery query,
        CancellationToken ct = default);
}

public static class PrivacyContractTime
{
    public static bool IsNormalized(DateTimeOffset value) =>
        value != default
        && value.Offset == TimeSpan.Zero
        && value.UtcTicks % TimeSpan.TicksPerMicrosecond == 0;

    public static void RequireNormalized(
        DateTimeOffset value,
        string parameterName)
    {
        if (!IsNormalized(value))
            throw new ArgumentException(
                "A normalized UTC microsecond timestamp is required.",
                parameterName);
    }
}

internal static class PrivacyReferenceText
{
    private const int MaximumLength = 256;
    private static readonly Encoding StrictUtf8 =
        new UTF8Encoding(
            encoderShouldEmitUTF8Identifier: false,
            throwOnInvalidBytes: true);

    public static string Require(
        string value,
        string parameterName)
    {
        if (!IsValid(value))
            throw new ArgumentException(
                "A bounded canonical opaque privacy reference is required.",
                parameterName);

        return value;
    }

    public static bool IsValid(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Length > MaximumLength
            || !string.Equals(value, value.Trim(), StringComparison.Ordinal)
            || value.Any(char.IsControl))
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
}
