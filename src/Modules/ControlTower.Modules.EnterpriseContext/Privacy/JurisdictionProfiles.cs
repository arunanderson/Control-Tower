using System.Text;
using ControlTower.Platform.Events;
using ControlTower.Platform.Identity;
using ControlTower.Platform.Privacy;
using ControlTower.Platform.Tenancy;

namespace ControlTower.Modules.EnterpriseContext.Privacy;

public sealed class JurisdictionProfileException(string message)
    : Exception(message);

/// <summary>
/// Opaque, tenant-configured regulatory regime marker. C5 owns only the bounded identifier; no
/// production legal taxonomy is embedded in the domain.
/// </summary>
public readonly record struct RegulatoryRegimeMarker
{
    private const int MaximumLength = 128;
    private static readonly Encoding StrictUtf8 =
        new UTF8Encoding(
            encoderShouldEmitUTF8Identifier: false,
            throwOnInvalidBytes: true);

    public RegulatoryRegimeMarker(string value)
    {
        if (!IsCanonical(value))
        {
            throw new ArgumentException(
                "A bounded canonical regulatory-regime marker is required.",
                nameof(value));
        }

        Value = value;
    }

    public string Value { get; } = string.Empty;

    public bool IsValid => IsCanonical(Value);

    public override string ToString() => IsValid ? Value : string.Empty;

    private static bool IsCanonical(string? value)
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

/// <summary>One immutable revision in the simple versioned E16 event history.</summary>
public sealed class JurisdictionProfile
{
    private const int MaximumReasonLength = 2048;
    private static readonly Encoding StrictUtf8 =
        new UTF8Encoding(
            encoderShouldEmitUTF8Identifier: false,
            throwOnInvalidBytes: true);
    private readonly IReadOnlyList<RegulatoryRegimeMarker> _regimeMarkers;

    public JurisdictionProfile(
        Guid id,
        TenantId tenant,
        JurisdictionRef jurisdiction,
        long version,
        PrivacyMarking telemetryCeiling,
        IEnumerable<RegulatoryRegimeMarker> regimeMarkers,
        DateTimeOffset changedAt,
        AuditActor changedBy,
        string changeReason)
    {
        ArgumentNullException.ThrowIfNull(regimeMarkers);
        var canonicalMarkers = regimeMarkers
            .Distinct()
            .OrderBy(marker => marker.Value, StringComparer.Ordinal)
            .ToArray();

        Validate(
            id,
            tenant,
            jurisdiction,
            version,
            telemetryCeiling,
            canonicalMarkers,
            changedAt,
            changedBy,
            changeReason);

        Id = id;
        Tenant = tenant;
        Jurisdiction = jurisdiction;
        Version = version;
        TelemetryCeiling = telemetryCeiling;
        _regimeMarkers = Array.AsReadOnly(canonicalMarkers);
        ChangedAt = changedAt;
        ChangedBy = changedBy;
        ChangeReason = changeReason;
    }

    public Guid Id { get; }

    public TenantId Tenant { get; }

    public JurisdictionRef Jurisdiction { get; }

    public long Version { get; }

    public PrivacyMarking TelemetryCeiling { get; }

    public IReadOnlyList<RegulatoryRegimeMarker> RegimeMarkers =>
        _regimeMarkers;

    public DateTimeOffset ChangedAt { get; }

    public AuditActor ChangedBy { get; }

    public string ChangeReason { get; }

    private static void Validate(
        Guid id,
        TenantId tenant,
        JurisdictionRef jurisdiction,
        long version,
        PrivacyMarking telemetryCeiling,
        IReadOnlyList<RegulatoryRegimeMarker> regimeMarkers,
        DateTimeOffset changedAt,
        AuditActor changedBy,
        string changeReason)
    {
        if (id == Guid.Empty)
            throw Invalid("A jurisdiction-profile ID is required.");
        if (tenant.Value == Guid.Empty)
            throw Invalid("A jurisdiction-profile tenant is required.");
        if (!jurisdiction.IsValid)
            throw Invalid("A valid jurisdiction reference is required.");
        if (version <= 0)
            throw Invalid("A positive jurisdiction-profile version is required.");
        if (!Enum.IsDefined(telemetryCeiling))
            throw Invalid("The telemetry ceiling is invalid.");
        if (regimeMarkers.Count == 0
            || regimeMarkers.Any(marker => !marker.IsValid))
        {
            throw Invalid(
                "At least one valid regulatory-regime marker is required.");
        }
        if (regimeMarkers.Select(marker => marker.Value)
            .Distinct(StringComparer.Ordinal)
            .Count() != regimeMarkers.Count)
        {
            throw Invalid(
                "Regulatory-regime markers must be unique.");
        }

        RequireTime(changedAt, "A normalized changed time is required.");
        if (!changedBy.IsValid)
            throw Invalid("A valid opaque changing actor is required.");
        if (!IsReason(changeReason))
            throw Invalid("A bounded canonical change reason is required.");
    }

    private static void RequireTime(
        DateTimeOffset value,
        string message)
    {
        if (!PrivacyContractTime.IsNormalized(value))
            throw Invalid(message);
    }

    private static JurisdictionProfileException Invalid(string message) =>
        new(message);

    private static bool IsReason(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Length > MaximumReasonLength
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

[DomainEventContract(
    "JurisdictionProfileChanged",
    EventPrivilege.Standard)]
public sealed class JurisdictionProfileChanged : IDomainEvent
{
    private readonly IReadOnlyList<RegulatoryRegimeMarker> _regimeMarkers;

    public JurisdictionProfileChanged(
        Guid eventId,
        DateTimeOffset occurredAt,
        Guid profileId,
        JurisdictionRef jurisdiction,
        long version,
        PrivacyMarking telemetryCeiling,
        IEnumerable<RegulatoryRegimeMarker> regimeMarkers,
        AuditActor changedBy,
        string changeReason,
        EventReference? correlationReference = null)
    {
        ArgumentNullException.ThrowIfNull(regimeMarkers);
        EventId = eventId;
        OccurredAt = occurredAt;
        ProfileId = profileId;
        Jurisdiction = jurisdiction;
        Version = version;
        TelemetryCeiling = telemetryCeiling;
        _regimeMarkers = Array.AsReadOnly(
            regimeMarkers.ToArray());
        ChangedBy = changedBy;
        ChangeReason = changeReason;
        CorrelationReference = correlationReference;
    }

    public Guid EventId { get; }

    public DateTimeOffset OccurredAt { get; }

    public Guid ProfileId { get; }

    public JurisdictionRef Jurisdiction { get; }

    public long Version { get; }

    public PrivacyMarking TelemetryCeiling { get; }

    public IReadOnlyList<RegulatoryRegimeMarker> RegimeMarkers =>
        _regimeMarkers;

    public AuditActor ChangedBy { get; }

    public string ChangeReason { get; }

    public EventReference? CorrelationReference { get; }
}

public enum JurisdictionProfileCommitStatus
{
    Applied,
    Conflict,
}

public sealed record JurisdictionProfileCommitResult(
    JurisdictionProfileCommitStatus Status,
    JurisdictionProfile? Authoritative);

public interface IJurisdictionProfileStore
{
    Task<JurisdictionProfile?> GetExactAsync(
        Guid profileId,
        long version,
        CancellationToken ct = default);

    Task<JurisdictionProfile?> GetCurrentAsync(
        Guid profileId,
        CancellationToken ct = default);

    Task<IReadOnlyList<JurisdictionProfile>> GetHistoryAsync(
        Guid profileId,
        CancellationToken ct = default);

    /// <summary>
    /// Atomically appends the canonical event and then makes the immutable E16 revision visible.
    /// Expected version zero creates version one; later revisions use optimistic concurrency.
    /// </summary>
    Task<JurisdictionProfileCommitResult> CommitAsync(
        JurisdictionProfile profile,
        JurisdictionProfileChanged changed,
        EventAppendMetadata metadata,
        long expectedVersion,
        CancellationToken ct = default);
}

/// <summary>Exact state/event/audit tuple rules shared by development and future durable adapters.</summary>
public static class JurisdictionProfileCommitSemantics
{
    public static JurisdictionProfileChanged Changed(
        JurisdictionProfile profile,
        EventReference? correlationReference = null)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (correlationReference is { } correlation
            && !correlation.IsValid)
        {
            throw new ArgumentException(
                "The jurisdiction-profile correlation is invalid.",
                nameof(correlationReference));
        }

        return new(
            Guid.NewGuid(),
            profile.ChangedAt,
            profile.Id,
            profile.Jurisdiction,
            profile.Version,
            profile.TelemetryCeiling,
            profile.RegimeMarkers,
            profile.ChangedBy,
            profile.ChangeReason,
            correlationReference);
    }

    public static EventAppendMetadata Metadata(
        JurisdictionProfile profile,
        EventReference? correlationReference = null)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return new(
            AggregateReference(profile.Id),
            profile.ChangedBy,
            profile.ChangeReason,
            correlationReference);
    }

    public static EventReference AggregateReference(Guid profileId) =>
        EventReference.For(
            "jurisdiction-profile",
            profileId);

    public static void Validate(
        JurisdictionProfile profile,
        JurisdictionProfileChanged changed,
        EventAppendMetadata metadata,
        long expectedVersion)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(changed);
        ArgumentNullException.ThrowIfNull(metadata);
        if (expectedVersion < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(expectedVersion),
                "The expected version cannot be negative.");
        }

        if (changed.EventId == Guid.Empty
            || !PrivacyContractTime.IsNormalized(changed.OccurredAt)
            || !changed.OccurredAt.EqualsExact(profile.ChangedAt)
            || changed.ProfileId != profile.Id
            || changed.Jurisdiction != profile.Jurisdiction
            || changed.Version != profile.Version
            || changed.TelemetryCeiling != profile.TelemetryCeiling
            || !changed.RegimeMarkers.SequenceEqual(profile.RegimeMarkers)
            || changed.ChangedBy != profile.ChangedBy
            || changed.ChangeReason != profile.ChangeReason
            || changed.CorrelationReference
                != metadata.CorrelationReference
            || metadata.AggregateReference
                != AggregateReference(profile.Id)
            || metadata.Actor != profile.ChangedBy
            || metadata.Reason != profile.ChangeReason
            || changed.CorrelationReference is { } correlation
                && !correlation.IsValid)
        {
            throw new InvalidOperationException(
                "Jurisdiction-profile state, event and audit metadata do not match.");
        }

        if (expectedVersion == 0)
        {
            if (profile.Version != 1)
            {
                throw new InvalidOperationException(
                    "A jurisdiction-profile create must produce version 1.");
            }
        }
        else if (profile.Version != checked(expectedVersion + 1))
        {
            throw new InvalidOperationException(
                "A jurisdiction-profile revision has an invalid version.");
        }
    }

    public static bool IsRevisionOf(
        JurisdictionProfile current,
        JurisdictionProfile next)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(next);
        return current.Id == next.Id
            && current.Tenant == next.Tenant
            && current.Jurisdiction == next.Jurisdiction
            && next.Version == checked(current.Version + 1)
            && next.ChangedAt > current.ChangedAt;
    }
}
