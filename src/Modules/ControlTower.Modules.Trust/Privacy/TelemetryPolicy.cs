using System.Security.Cryptography;
using System.Text;
using ControlTower.Platform.Events;
using ControlTower.Platform.Identity;
using ControlTower.Platform.Privacy;
using ControlTower.Platform.Tenancy;

namespace ControlTower.Modules.Trust.Privacy;

public sealed class TelemetryPolicyException(string message)
    : Exception(message);

/// <summary>
/// One immutable E17 capability rule. Jurisdiction and population are opaque applicability
/// references supplied by C5; null means the rule is not narrowed on that dimension.
/// </summary>
public sealed record TelemetryPolicyRule
{
    private const int MaximumPurposeLength = 512;

    public TelemetryPolicyRule(
        TelemetryCapabilityRef capability,
        JurisdictionRef? jurisdiction,
        PopulationRef? population,
        bool enabled,
        PrivacyMarking level,
        string? activationPurpose = null,
        EventReference? approvalReference = null,
        RetentionPolicyRef? retentionPolicy = null,
        DateTimeOffset? timeLimit = null)
    {
        if (!capability.IsValid)
            throw new TelemetryPolicyException(
                "A valid telemetry capability is required.");
        if (jurisdiction is { } jurisdictionValue
            && !jurisdictionValue.IsValid)
        {
            throw new TelemetryPolicyException(
                "The jurisdiction reference is invalid.");
        }
        if (population is { } populationValue
            && !populationValue.IsValid)
        {
            throw new TelemetryPolicyException(
                "The population reference is invalid.");
        }
        if (!Enum.IsDefined(level))
            throw new TelemetryPolicyException(
                "The telemetry level is invalid.");
        if (approvalReference is { } approval
            && !approval.IsValid)
        {
            throw new TelemetryPolicyException(
                "The activation approval reference is invalid.");
        }
        if (retentionPolicy is { } retention
            && !retention.IsValid)
        {
            throw new TelemetryPolicyException(
                "The retention-policy reference is invalid.");
        }
        if (timeLimit is { } limit)
            PrivacyContractTime.RequireNormalized(
                limit,
                nameof(timeLimit));

        var canonicalPurpose = PolicyText.Optional(
            activationPurpose,
            MaximumPurposeLength,
            "activation purpose");
        var requiresActivationEvidence =
            enabled && level >= PrivacyMarking.L2;
        if (requiresActivationEvidence
            && (canonicalPurpose is null
                || approvalReference is null
                || retentionPolicy is null))
        {
            throw new TelemetryPolicyException(
                "Enabled L2 or higher telemetry requires purpose, approval and retention evidence.");
        }
        if (!requiresActivationEvidence
            && (canonicalPurpose is not null
                || approvalReference is not null
                || retentionPolicy is not null))
        {
            throw new TelemetryPolicyException(
                "Activation evidence is valid only for enabled L2 or higher telemetry.");
        }
        if (enabled
            && level == PrivacyMarking.L4
            && timeLimit is null)
        {
            throw new TelemetryPolicyException(
                "Enabled L4 telemetry must be explicitly time-limited.");
        }
        if ((!enabled || level != PrivacyMarking.L4)
            && timeLimit is not null)
        {
            throw new TelemetryPolicyException(
                "A time limit is valid only for enabled L4 telemetry.");
        }

        Capability = capability;
        Jurisdiction = jurisdiction;
        Population = population;
        Enabled = enabled;
        Level = level;
        ActivationPurpose = canonicalPurpose;
        ApprovalReference = approvalReference;
        RetentionPolicy = retentionPolicy;
        TimeLimit = timeLimit;
    }

    public TelemetryCapabilityRef Capability { get; }

    public JurisdictionRef? Jurisdiction { get; }

    public PopulationRef? Population { get; }

    public bool Enabled { get; }

    public PrivacyMarking Level { get; }

    public string? ActivationPurpose { get; }

    public EventReference? ApprovalReference { get; }

    public RetentionPolicyRef? RetentionPolicy { get; }

    public DateTimeOffset? TimeLimit { get; }

    internal bool AppliesToCell(
        TelemetryCapabilityRef capability,
        JurisdictionRef? jurisdiction,
        PopulationRef? population,
        DateTimeOffset validAt) =>
        Capability == capability
        && (Jurisdiction is null
            || jurisdiction is { } cellJurisdiction
                && Jurisdiction.Value
                    == cellJurisdiction)
        && (Population is null
            || population is { } cellPopulation
                && Population.Value
                    == cellPopulation)
        && (TimeLimit is null || validAt < TimeLimit.Value);

    internal PrivacyApplicability CeilingApplicability() =>
        new(
            Jurisdiction is { } jurisdiction
                ? [jurisdiction]
                : [],
            Population is { } population
                ? [population]
                : []);

    internal string ScopeKey =>
        string.Join(
            '\u001f',
            Capability.Value,
            Jurisdiction?.Value ?? string.Empty,
            Population?.Value ?? string.Empty);
}

/// <summary>
/// An immutable, tenant-wide E17 revision. Valid time and record time remain independent so
/// restatements can answer both "what was true?" and "what did we know?".
/// </summary>
public sealed class TelemetryPolicyRevision
{
    private const int MaximumJustificationLength = 2048;
    private readonly IReadOnlyList<TelemetryPolicyRule> _rules;

    public TelemetryPolicyRevision(
        TenantId tenant,
        long version,
        DateTimeOffset validFrom,
        DateTimeOffset? validTo,
        DateTimeOffset recordedAt,
        AuditActor changedBy,
        string justification,
        IEnumerable<TelemetryPolicyRule> rules)
    {
        if (tenant.Value == Guid.Empty)
            throw new TelemetryPolicyException(
                "A telemetry-policy tenant is required.");
        if (version <= 0)
            throw new TelemetryPolicyException(
                "A positive telemetry-policy version is required.");
        PrivacyContractTime.RequireNormalized(
            validFrom,
            nameof(validFrom));
        PrivacyContractTime.RequireNormalized(
            recordedAt,
            nameof(recordedAt));
        if (validTo is { } end)
        {
            PrivacyContractTime.RequireNormalized(
                end,
                nameof(validTo));
            if (end <= validFrom)
            {
                throw new TelemetryPolicyException(
                    "Policy valid-to must follow valid-from.");
            }
        }
        if (!changedBy.IsValid)
            throw new TelemetryPolicyException(
                "A valid opaque policy-change actor is required.");

        ArgumentNullException.ThrowIfNull(rules);
        var materialized = rules.ToArray();
        if (materialized.Any(rule => rule is null))
            throw new TelemetryPolicyException(
                "Telemetry-policy rules cannot contain null.");
        var canonical = materialized
            .OrderBy(rule => rule.ScopeKey, StringComparer.Ordinal)
            .ToArray();
        if (canonical
            .GroupBy(rule => rule.ScopeKey, StringComparer.Ordinal)
            .Any(group => group.Count() != 1))
        {
            throw new TelemetryPolicyException(
                "A policy revision cannot contain duplicate capability and applicability scopes.");
        }
        foreach (var rule in canonical)
        {
            if (rule.TimeLimit is { } limit
                && (limit <= validFrom
                    || validTo is { } revisionEnd
                        && limit > revisionEnd))
            {
                throw new TelemetryPolicyException(
                    "An L4 time limit must fall within the policy validity interval.");
            }
        }

        Tenant = tenant;
        Version = version;
        ValidFrom = validFrom;
        ValidTo = validTo;
        RecordedAt = recordedAt;
        ChangedBy = changedBy;
        Justification = PolicyText.Required(
            justification,
            MaximumJustificationLength,
            "policy-change justification");
        _rules = Array.AsReadOnly(canonical);
        Fingerprint = TelemetryPolicyFingerprint.Compute(this);
    }

    public TenantId Tenant { get; }

    public long Version { get; }

    public DateTimeOffset ValidFrom { get; }

    public DateTimeOffset? ValidTo { get; }

    public DateTimeOffset RecordedAt { get; }

    public AuditActor ChangedBy { get; }

    public string Justification { get; }

    public IReadOnlyList<TelemetryPolicyRule> Rules => _rules;

    public string Fingerprint { get; }

    public bool IsValidAt(DateTimeOffset validAt) =>
        ValidFrom <= validAt
        && (ValidTo is null || validAt < ValidTo.Value);
}

internal static class TelemetryPolicyFingerprint
{
    public static string Compute(TelemetryPolicyRevision revision)
    {
        ArgumentNullException.ThrowIfNull(revision);
        var canonical = new StringBuilder();
        Append(canonical, revision.Tenant.Value.ToString("D"));
        Append(canonical, revision.Version.ToString(
            System.Globalization.CultureInfo.InvariantCulture));
        Append(canonical, revision.ValidFrom.UtcTicks.ToString(
            System.Globalization.CultureInfo.InvariantCulture));
        AppendOptional(
            canonical,
            revision.ValidTo?.UtcTicks.ToString(
                System.Globalization.CultureInfo.InvariantCulture));
        Append(canonical, revision.RecordedAt.UtcTicks.ToString(
            System.Globalization.CultureInfo.InvariantCulture));
        Append(
            canonical,
            ((byte)revision.ChangedBy.Kind).ToString(
                System.Globalization.CultureInfo.InvariantCulture));
        Append(canonical, revision.ChangedBy.OpaqueId);
        Append(canonical, revision.Justification);
        Append(
            canonical,
            revision.Rules.Count.ToString(
                System.Globalization.CultureInfo.InvariantCulture));
        foreach (var rule in revision.Rules)
        {
            Append(canonical, rule.Capability.Value);
            AppendOptional(
                canonical,
                rule.Jurisdiction?.Value);
            AppendOptional(
                canonical,
                rule.Population?.Value);
            Append(canonical, rule.Enabled ? "1" : "0");
            Append(
                canonical,
                ((byte)rule.Level).ToString(
                    System.Globalization.CultureInfo.InvariantCulture));
            AppendOptional(
                canonical,
                rule.ActivationPurpose);
            AppendOptional(
                canonical,
                rule.ApprovalReference?.Kind);
            AppendOptional(
                canonical,
                rule.ApprovalReference?.Value);
            AppendOptional(
                canonical,
                rule.RetentionPolicy?.Value);
            AppendOptional(
                canonical,
                rule.TimeLimit?.UtcTicks.ToString(
                    System.Globalization.CultureInfo.InvariantCulture));
        }

        return Convert.ToHexString(
                SHA256.HashData(
                    Encoding.UTF8.GetBytes(canonical.ToString())))
            .ToLowerInvariant();
    }

    private static void Append(
        StringBuilder destination,
        string value) =>
        destination.Append(value.Length)
            .Append(':')
            .Append(value)
            .Append(';');

    private static void AppendOptional(
        StringBuilder destination,
        string? value)
    {
        Append(destination, value is null ? "absent" : "present");
        if (value is not null)
            Append(destination, value);
    }
}

internal static class PolicyText
{
    private static readonly Encoding StrictUtf8 =
        new UTF8Encoding(
            encoderShouldEmitUTF8Identifier: false,
            throwOnInvalidBytes: true);

    public static string Required(
        string? value,
        int maximumLength,
        string description) =>
        Optional(value, maximumLength, description)
        ?? throw new TelemetryPolicyException(
            $"A bounded {description} is required.");

    public static string? Optional(
        string? value,
        int maximumLength,
        string description)
    {
        if (value is null)
            return null;

        if (string.IsNullOrWhiteSpace(value)
            || value.Length > maximumLength
            || !string.Equals(
                value,
                value.Trim(),
                StringComparison.Ordinal)
            || value.Any(char.IsControl))
        {
            throw new TelemetryPolicyException(
                $"A present {description} must be bounded canonical text.");
        }

        try
        {
            _ = StrictUtf8.GetByteCount(value);
            return value;
        }
        catch (EncoderFallbackException)
        {
            throw new TelemetryPolicyException(
                $"A present {description} must be well-formed Unicode.");
        }
    }
}
