using System.Text.Json.Serialization;
using ControlTower.Platform.Events;
using ControlTower.Platform.Identity;
using ControlTower.Platform.Tenancy;

namespace ControlTower.Platform.Audit;

public enum PrivilegedReadPolicyKind : byte
{
    NotApplicable = 1,
    Applied = 2,
}

/// <summary>
/// Honest policy context for a privileged read. Administrative privileged-zone access can be
/// explicitly non-policy-scoped; a policy-governed read must name the exact policy version.
/// </summary>
public readonly record struct PrivilegedReadPolicy
{
    [JsonConstructor]
    public PrivilegedReadPolicy(
        PrivilegedReadPolicyKind kind,
        EventReference? version)
    {
        if (!Enum.IsDefined(kind))
            throw new ArgumentOutOfRangeException(
                nameof(kind),
                "The privileged-read policy kind is invalid.");
        if (kind == PrivilegedReadPolicyKind.Applied
            && (version is null || !version.Value.IsValid))
        {
            throw new ArgumentException(
                "A policy-governed privileged read requires a policy version.",
                nameof(version));
        }
        if (kind == PrivilegedReadPolicyKind.NotApplicable
            && version is not null)
        {
            throw new ArgumentException(
                "A non-policy-scoped privileged read cannot name a policy version.",
                nameof(version));
        }

        Kind = kind;
        Version = version;
    }

    public PrivilegedReadPolicyKind Kind { get; }

    public EventReference? Version { get; }

    public bool IsValid
    {
        get
        {
            try
            {
                _ = new PrivilegedReadPolicy(Kind, Version);
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }
    }

    public static PrivilegedReadPolicy NotApplicable() =>
        new(PrivilegedReadPolicyKind.NotApplicable, null);

    public static PrivilegedReadPolicy Applied(
        EventReference version) =>
        new(PrivilegedReadPolicyKind.Applied, version);
}

/// <summary>
/// Immutable E20 privileged-read evidence. Actor and resource identities are opaque; correlation is
/// mandatory; policy applicability is represented explicitly rather than with a fabricated version.
/// </summary>
public sealed record PrivilegedReadRecord
{
    private const int MaximumPurposeLength = 512;

    [JsonConstructor]
    public PrivilegedReadRecord(
        Guid accessId,
        TenantId tenant,
        AuditActor actor,
        EventReference resource,
        string purpose,
        PrivilegedReadPolicy policy,
        EventReference correlationReference,
        DateTimeOffset occurredAt)
    {
        if (accessId == Guid.Empty)
            throw new ArgumentException(
                "A privileged-read access ID is required.",
                nameof(accessId));
        if (tenant.Value == Guid.Empty)
            throw new ArgumentException(
                "A privileged-read tenant is required.",
                nameof(tenant));
        if (!actor.IsValid)
            throw new ArgumentException(
                "A valid opaque actor is required.",
                nameof(actor));
        if (!resource.IsValid)
            throw new ArgumentException(
                "A valid privileged-read resource is required.",
                nameof(resource));
        if (!policy.IsValid)
            throw new ArgumentException(
                "A valid privileged-read policy context is required.",
                nameof(policy));
        if (!correlationReference.IsValid)
            throw new ArgumentException(
                "A valid privileged-read correlation is required.",
                nameof(correlationReference));

        var canonicalPurpose = purpose?.Trim();
        if (!string.Equals(
                purpose,
                canonicalPurpose,
                StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(canonicalPurpose)
            || canonicalPurpose.Length > MaximumPurposeLength
            || canonicalPurpose.Any(char.IsControl)
            || !EventText.IsWellFormedUnicode(canonicalPurpose))
        {
            throw new ArgumentException(
                "A bounded privileged-read purpose is required.",
                nameof(purpose));
        }
        if (occurredAt == default
            || occurredAt.Offset != TimeSpan.Zero
            || occurredAt.UtcTicks % TimeSpan.TicksPerMicrosecond != 0)
        {
            throw new ArgumentException(
                "A normalized privileged-read occurrence time is required.",
                nameof(occurredAt));
        }

        AccessId = accessId;
        Tenant = tenant;
        Actor = actor;
        Resource = resource;
        Purpose = canonicalPurpose;
        Policy = policy;
        CorrelationReference = correlationReference;
        OccurredAt = occurredAt;
    }

    public Guid AccessId { get; }

    public TenantId Tenant { get; }

    public AuditActor Actor { get; }

    public EventReference Resource { get; }

    public string ResourceKind => Resource.Kind;

    public string ResourceId => Resource.Value;

    public string Purpose { get; }

    public PrivilegedReadPolicy Policy { get; }

    public EventReference CorrelationReference { get; }

    public DateTimeOffset OccurredAt { get; }
}

/// <summary>Records every privileged read (ADR-015.9). Enabled by default.</summary>
public interface IPrivilegedReadAuditor
{
    ValueTask RecordAsync(
        PrivilegedReadRecord record,
        CancellationToken ct = default);
}

/// <summary>
/// Adapter-owned storage sink used by the C9 evidence recorder after the integrity event succeeds.
/// Callers that release protected values depend on <see cref="IPrivilegedReadAuditor"/>, never this
/// lower-level persistence seam.
/// </summary>
public interface IPrivilegedReadRecordSink
{
    ValueTask StoreAsync(
        PrivilegedReadRecord record,
        CancellationToken ct = default);
}
