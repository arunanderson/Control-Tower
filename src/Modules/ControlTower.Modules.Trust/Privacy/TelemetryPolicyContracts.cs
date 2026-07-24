using ControlTower.Platform.Events;
using ControlTower.Platform.Identity;
using ControlTower.Platform.Privacy;
using ControlTower.Platform.Tenancy;

namespace ControlTower.Modules.Trust.Privacy;

[DomainEventContract(
    "TelemetryPolicyChanged",
    EventPrivilege.Privileged)]
public sealed record TelemetryPolicyChanged : IDomainEvent
{
    private IReadOnlyList<TelemetryPolicyRule> _rules =
        Array.Empty<TelemetryPolicyRule>();

    public Guid EventId { get; init; } = Guid.NewGuid();

    public DateTimeOffset OccurredAt { get; init; } =
        DateTimeOffset.UtcNow;

    public required TenantId Tenant { get; init; }

    public required long Version { get; init; }

    public required DateTimeOffset ValidFrom { get; init; }

    public DateTimeOffset? ValidTo { get; init; }

    public required DateTimeOffset RecordedAt { get; init; }

    public required AuditActor ChangedBy { get; init; }

    public required string Justification { get; init; }

    public required string PolicyFingerprint { get; init; }

    public required int RuleCount { get; init; }

    public required IReadOnlyList<TelemetryPolicyRule> Rules
    {
        get => _rules;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            var materialized = value.ToArray();
            if (materialized.Any(rule => rule is null))
            {
                throw new ArgumentException(
                    "Telemetry-policy event rules cannot contain null.",
                    nameof(value));
            }

            _rules = Array.AsReadOnly(
                materialized
                    .OrderBy(
                        rule => rule.ScopeKey,
                        StringComparer.Ordinal)
                    .ToArray());
        }
    }

    public EventReference? CorrelationReference { get; init; }
}

public sealed record TelemetryPolicyResolutionQuery
{
    public TelemetryPolicyResolutionQuery(
        TelemetryCapabilityRef capability,
        PrivacyApplicability applicability,
        DateTimeOffset validAt,
        DateTimeOffset recordedAt)
    {
        if (!capability.IsValid)
            throw new ArgumentException(
                "A valid telemetry capability is required.",
                nameof(capability));
        ArgumentNullException.ThrowIfNull(applicability);
        PrivacyContractTime.RequireNormalized(
            validAt,
            nameof(validAt));
        PrivacyContractTime.RequireNormalized(
            recordedAt,
            nameof(recordedAt));

        Capability = capability;
        Applicability = applicability;
        ValidAt = validAt;
        RecordedAt = recordedAt;
    }

    public TelemetryCapabilityRef Capability { get; }

    public PrivacyApplicability Applicability { get; }

    public DateTimeOffset ValidAt { get; }

    public DateTimeOffset RecordedAt { get; }
}

public sealed record TelemetryPolicyResolution(
    bool Enabled,
    PrivacyMarking Level,
    long? PolicyVersion,
    int MatchedRuleCount,
    JurisdictionCeilingResolution? Ceiling);

public interface ITelemetryPolicyReader
{
    Task<TelemetryPolicyRevision?> GetAsync(
        long version,
        CancellationToken ct = default);

    Task<IReadOnlyList<TelemetryPolicyRevision>> ListHistoryAsync(
        CancellationToken ct = default);

    Task<TelemetryPolicyRevision?> FindAsOfAsync(
        DateTimeOffset validAt,
        DateTimeOffset recordedAt,
        CancellationToken ct = default);

    Task<TelemetryPolicyResolution> ResolveAsync(
        TelemetryPolicyResolutionQuery query,
        CancellationToken ct = default);
}

public enum TelemetryPolicyCommitStatus
{
    Applied,
    Conflict,
}

public sealed record TelemetryPolicyCommitResult(
    TelemetryPolicyCommitStatus Status,
    TelemetryPolicyRevision? Authoritative);

public interface ITelemetryPolicyStore : ITelemetryPolicyReader
{
    /// <summary>
    /// Atomically appends a privileged E17 event and then installs one immutable policy revision.
    /// Expected version zero creates the aggregate; every later revision advances it by one.
    /// </summary>
    Task<TelemetryPolicyCommitResult> CommitAsync(
        TelemetryPolicyRevision revision,
        TelemetryPolicyChanged changed,
        EventAppendMetadata metadata,
        long expectedVersion,
        CancellationToken ct = default);
}

/// <summary>Canonical state/event/E20 tuple rules shared by all E17 adapters.</summary>
public static class TelemetryPolicyCommitSemantics
{
    public static TelemetryPolicyChanged Changed(
        TelemetryPolicyRevision revision,
        EventReference? correlationReference = null)
    {
        ArgumentNullException.ThrowIfNull(revision);
        if (correlationReference is { } correlation
            && !correlation.IsValid)
        {
            throw new ArgumentException(
                "The policy-change correlation is invalid.",
                nameof(correlationReference));
        }

        return new TelemetryPolicyChanged
        {
            Tenant = revision.Tenant,
            Version = revision.Version,
            ValidFrom = revision.ValidFrom,
            ValidTo = revision.ValidTo,
            RecordedAt = revision.RecordedAt,
            ChangedBy = revision.ChangedBy,
            Justification = revision.Justification,
            PolicyFingerprint = revision.Fingerprint,
            RuleCount = revision.Rules.Count,
            Rules = revision.Rules,
            CorrelationReference = correlationReference,
            OccurredAt = revision.RecordedAt,
        };
    }

    public static EventAppendMetadata Metadata(
        TelemetryPolicyRevision revision,
        EventReference? correlationReference = null)
    {
        ArgumentNullException.ThrowIfNull(revision);
        return new(
            AggregateReference(revision.Tenant),
            revision.ChangedBy,
            revision.Justification,
            correlationReference);
    }

    public static void Validate(
        TelemetryPolicyRevision revision,
        TelemetryPolicyChanged changed,
        EventAppendMetadata metadata,
        long expectedVersion)
    {
        ArgumentNullException.ThrowIfNull(revision);
        ArgumentNullException.ThrowIfNull(changed);
        ArgumentNullException.ThrowIfNull(metadata);
        if (expectedVersion < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(expectedVersion),
                "The expected version cannot be negative.");
        }

        if (changed.EventId == Guid.Empty
            || changed.Tenant != revision.Tenant
            || changed.Version != revision.Version
            || !Exact(changed.ValidFrom, revision.ValidFrom)
            || !Exact(changed.ValidTo, revision.ValidTo)
            || !Exact(changed.RecordedAt, revision.RecordedAt)
            || !Exact(changed.OccurredAt, revision.RecordedAt)
            || changed.ChangedBy != revision.ChangedBy
            || changed.Justification != revision.Justification
            || changed.PolicyFingerprint != revision.Fingerprint
            || changed.RuleCount != revision.Rules.Count
            || !RulesExact(
                changed.Rules,
                revision.Rules)
            || changed.CorrelationReference
                != metadata.CorrelationReference
            || metadata.AggregateReference
                != AggregateReference(revision.Tenant)
            || metadata.Actor != revision.ChangedBy
            || metadata.Reason != revision.Justification
            || !ValidCorrelation(
                changed.CorrelationReference))
        {
            throw new InvalidOperationException(
                "Telemetry-policy state, event and audit metadata do not match.");
        }

        if (expectedVersion == 0)
        {
            if (revision.Version != 1)
            {
                throw new InvalidOperationException(
                    "A telemetry-policy create must produce version 1.");
            }
        }
        else if (revision.Version
                 != checked(expectedVersion + 1))
        {
            throw new InvalidOperationException(
                "A telemetry-policy revision has an invalid version.");
        }
    }

    public static EventReference AggregateReference(
        TenantId tenant) =>
        EventReference.For(
            "telemetry-policy",
            tenant.Value);

    private static bool Exact(
        DateTimeOffset left,
        DateTimeOffset right) =>
        PrivacyContractTime.IsNormalized(left)
        && PrivacyContractTime.IsNormalized(right)
        && left.EqualsExact(right);

    private static bool Exact(
        DateTimeOffset? left,
        DateTimeOffset? right) =>
        left is null && right is null
        || left is { } leftValue
            && right is { } rightValue
            && Exact(leftValue, rightValue);

    private static bool ValidCorrelation(
        EventReference? correlationReference)
    {
        if (correlationReference is null)
            return true;

        return correlationReference.Value.IsValid;
    }

    private static bool RulesExact(
        IReadOnlyList<TelemetryPolicyRule> changed,
        IReadOnlyList<TelemetryPolicyRule> revision)
    {
        if (changed.Count != revision.Count)
            return false;

        for (var index = 0; index < changed.Count; index++)
        {
            if (changed[index] != revision[index])
                return false;
        }

        return true;
    }
}
