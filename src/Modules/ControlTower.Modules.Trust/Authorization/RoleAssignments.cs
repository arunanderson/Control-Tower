using ControlTower.Platform.Audit;
using ControlTower.Platform.Events;
using ControlTower.Platform.Identity;
using ControlTower.Platform.Tenancy;

namespace ControlTower.Modules.Trust.Authorization;

public sealed class RoleAssignmentException(string message) : Exception(message);

/// <summary>An evented, tenant-scoped E18 assignment. V1 scope is always TenantWide.</summary>
public sealed class RoleAssignment
{
    public RoleAssignment(
        Guid id,
        TenantId tenant,
        PersonKey subjectPersonKey,
        ControlTowerRole role,
        AuditActor assignedBy,
        DateTimeOffset assignedAt)
        : this(
            id,
            tenant,
            subjectPersonKey,
            role,
            assignedBy,
            assignedAt,
            version: 1,
            revokedAt: null,
            revokedBy: null)
    {
    }

    private RoleAssignment(
        Guid id,
        TenantId tenant,
        PersonKey subjectPersonKey,
        ControlTowerRole role,
        AuditActor assignedBy,
        DateTimeOffset assignedAt,
        long version,
        DateTimeOffset? revokedAt,
        AuditActor? revokedBy)
    {
        Validate(
            id,
            tenant,
            subjectPersonKey,
            role,
            assignedBy,
            assignedAt,
            version,
            revokedAt,
            revokedBy);

        Id = id;
        Tenant = tenant;
        SubjectPersonKey = subjectPersonKey;
        Role = role;
        AssignedBy = assignedBy;
        AssignedAt = assignedAt;
        Version = version;
        RevokedAt = revokedAt;
        RevokedBy = revokedBy;
    }

    public Guid Id { get; }

    public TenantId Tenant { get; }

    public PersonKey SubjectPersonKey { get; }

    public ControlTowerRole Role { get; }

    public OrganizationScope OrganizationScope =>
        OrganizationScope.TenantWide;

    public AuditActor AssignedBy { get; }

    public DateTimeOffset AssignedAt { get; }

    public long Version { get; }

    public DateTimeOffset? RevokedAt { get; }

    public AuditActor? RevokedBy { get; }

    public bool IsActive => RevokedAt is null;

    public static RoleAssignment Rehydrate(
        Guid id,
        TenantId tenant,
        PersonKey subjectPersonKey,
        ControlTowerRole role,
        AuditActor assignedBy,
        DateTimeOffset assignedAt,
        long version,
        DateTimeOffset? revokedAt,
        AuditActor? revokedBy) =>
        new(
            id,
            tenant,
            subjectPersonKey,
            role,
            assignedBy,
            assignedAt,
            version,
            revokedAt,
            revokedBy);

    internal RoleAssignment Revoke(
        AuditActor revokedBy,
        DateTimeOffset revokedAt)
    {
        if (!IsActive)
        {
            throw new RoleAssignmentException(
                "The role assignment is already revoked.");
        }

        return new(
            Id,
            Tenant,
            SubjectPersonKey,
            Role,
            AssignedBy,
            AssignedAt,
            checked(Version + 1),
            revokedAt,
            revokedBy);
    }

    private static void Validate(
        Guid id,
        TenantId tenant,
        PersonKey subjectPersonKey,
        ControlTowerRole role,
        AuditActor assignedBy,
        DateTimeOffset assignedAt,
        long version,
        DateTimeOffset? revokedAt,
        AuditActor? revokedBy)
    {
        if (id == Guid.Empty)
        {
            throw new RoleAssignmentException(
                "A role-assignment ID is required.");
        }
        if (tenant.Value == Guid.Empty)
        {
            throw new RoleAssignmentException(
                "A role-assignment tenant is required.");
        }
        if (!subjectPersonKey.IsValid)
        {
            throw new RoleAssignmentException(
                "A non-empty subject PersonKey is required.");
        }
        if (!Enum.IsDefined(role))
        {
            throw new RoleAssignmentException(
                "The role is not a curated Control Tower role.");
        }
        if (!assignedBy.IsValid)
        {
            throw new RoleAssignmentException(
                "A bounded assigning actor is required.");
        }
        if (assignedAt == default)
        {
            throw new RoleAssignmentException(
                "An assignment time is required.");
        }
        if (revokedAt is null != (revokedBy is null))
        {
            throw new RoleAssignmentException(
                "Revocation time and actor must be supplied together.");
        }
        if (revokedBy is { } actor && !actor.IsValid)
        {
            throw new RoleAssignmentException(
                "A bounded revoking actor is required.");
        }
        if (revokedAt is { } at && at < assignedAt)
        {
            throw new RoleAssignmentException(
                "A revocation cannot precede assignment.");
        }

        var expectedVersion = revokedAt is null ? 1L : 2L;
        if (version != expectedVersion)
        {
            throw new RoleAssignmentException(
                "The role-assignment version is invalid for its state.");
        }
    }
}

[DomainEventContract("RoleAssignmentChanged", EventPrivilege.Privileged)]
public sealed record RoleAssignmentChanged : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();

    public DateTimeOffset OccurredAt { get; init; } =
        DateTimeOffset.UtcNow;

    public required Guid AssignmentId { get; init; }

    public required PersonKey SubjectPersonKey { get; init; }

    public required string Role { get; init; }

    public required string OrganizationScope { get; init; }

    public required string Change { get; init; }

    public required AuditActor ChangedBy { get; init; }

    public required long Version { get; init; }
}

public interface IRoleAssignmentReader
{
    Task<IReadOnlyList<RoleAssignment>> ListForSubjectAsync(
        PersonKey subjectPersonKey,
        CancellationToken ct = default);
}

public enum RoleAssignmentCommitStatus
{
    Applied,
    AlreadyActive,
    Conflict,
}

public sealed record RoleAssignmentCommitResult(
    RoleAssignmentCommitStatus Status,
    RoleAssignment? Authoritative);

public interface IRoleAssignmentStore : IRoleAssignmentReader
{
    Task<RoleAssignment?> GetAsync(
        Guid assignmentId,
        CancellationToken ct = default);

    /// <summary>
    /// Atomically persists E18 state and its canonical audit event. Expected version zero creates an
    /// assignment; a positive value applies a state transition. The durable adapter owns one database
    /// transaction and the same typed outcomes.
    /// </summary>
    Task<RoleAssignmentCommitResult> CommitAsync(
        RoleAssignment assignment,
        RoleAssignmentChanged changed,
        EventAppendMetadata metadata,
        long expectedVersion,
        CancellationToken ct = default);
}

/// <summary>Production-safe default until a durable C8 adapter is composed: no assignment, no access.</summary>
public sealed class DenyAllRoleAssignmentReader : IRoleAssignmentReader
{
    public Task<IReadOnlyList<RoleAssignment>> ListForSubjectAsync(
        PersonKey subjectPersonKey,
        CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<RoleAssignment>>([]);
}

public interface IEffectiveAccessResolver
{
    Task<EffectiveAccess> ResolveAsync(
        Guid subjectObjectId,
        PersonKeyAccessContext access,
        CancellationToken ct = default);
}

public sealed class EffectiveAccessResolver(
    IPersonKeyReader personKeys,
    IRoleAssignmentReader assignments,
    ITenantContextAccessor tenants)
    : IEffectiveAccessResolver
{
    public async Task<EffectiveAccess> ResolveAsync(
        Guid subjectObjectId,
        PersonKeyAccessContext access,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(access);
        if (subjectObjectId == Guid.Empty)
            return ControlTowerAccessCatalog.Resolve(null, []);

        var subjectPersonKey = await personKeys.FindAsync(
            subjectObjectId,
            access,
            ct);
        if (subjectPersonKey is null
            || !subjectPersonKey.Value.IsValid)
        {
            return ControlTowerAccessCatalog.Resolve(null, []);
        }

        var tenant = tenants.Current;
        var active = await assignments.ListForSubjectAsync(
            subjectPersonKey.Value,
            ct);
        return ControlTowerAccessCatalog.Resolve(
            subjectPersonKey.Value,
            active.Where(assignment =>
                    assignment.Tenant == tenant
                    && assignment.SubjectPersonKey
                    == subjectPersonKey.Value
                    && assignment.IsActive)
                .Select(assignment => assignment.Role));
    }
}

/// <summary>C8 assignment lifecycle. Mutations append RoleAssignmentChanged to the audit stream.</summary>
public sealed class RoleAssignmentService(
    IPersonKeyMap personKeys,
    IRoleAssignmentStore store,
    ITenantContextAccessor tenants,
    TimeProvider clock)
{
    public async Task<Guid> AssignAsync(
        Guid subjectObjectId,
        ControlTowerRole role,
        AuditActor changedBy,
        CancellationToken ct = default)
    {
        if (subjectObjectId == Guid.Empty)
        {
            throw new RoleAssignmentException(
                "A non-empty subject oid is required.");
        }
        if (!Enum.IsDefined(role))
        {
            throw new RoleAssignmentException(
                "The role is not a curated Control Tower role.");
        }
        if (!changedBy.IsValid)
        {
            throw new RoleAssignmentException(
                "A bounded assigning actor is required.");
        }

        var correlation = EventReference.For(
            "role-assignment-command",
            Guid.NewGuid());
        var personResult = await personKeys.GetOrCreateAsync(
            new DirectoryIdentitySnapshot(subjectObjectId),
            new PersonKeyAccessContext(
                changedBy,
                "assign role",
                correlation,
                PrivilegedReadPolicy.NotApplicable()),
            ct);
        if (!personResult.PersonKey.IsValid)
        {
            throw new RoleAssignmentException(
                "The PersonKey map returned an invalid subject.");
        }

        var now = EventEnvelopeCanonicalizer.NormalizeTimestamp(
            clock.GetUtcNow());
        var assignment = new RoleAssignment(
            Guid.NewGuid(),
            tenants.Current,
            personResult.PersonKey,
            role,
            changedBy,
            now);
        var changed = Changed(
            assignment,
            "Assigned",
            changedBy,
            now);
        var result = await store.CommitAsync(
            assignment,
            changed,
            Metadata(assignment, changedBy, correlation),
            expectedVersion: 0,
            ct);

        if (result.Status
                is RoleAssignmentCommitStatus.Applied
                or RoleAssignmentCommitStatus.AlreadyActive
            && IsAuthoritativeActive(
                result.Authoritative,
                tenants.Current,
                personResult.PersonKey,
                role))
        {
            return result.Authoritative!.Id;
        }

        throw new RoleAssignmentException(
            "The role assignment conflicted with a concurrent change.");
    }

    public async Task RevokeAsync(
        Guid assignmentId,
        AuditActor changedBy,
        CancellationToken ct = default)
    {
        if (!changedBy.IsValid)
        {
            throw new RoleAssignmentException(
                "A bounded revoking actor is required.");
        }

        var assignment = await store.GetAsync(assignmentId, ct)
            ?? throw NotFound();
        if (assignment.Id != assignmentId
            || assignment.Tenant != tenants.Current)
        {
            throw NotFound();
        }
        if (!assignment.IsActive)
            return;

        var now = EventEnvelopeCanonicalizer.NormalizeTimestamp(
            clock.GetUtcNow());
        var revoked = assignment.Revoke(changedBy, now);
        var correlation = EventReference.For(
            "role-assignment-command",
            Guid.NewGuid());
        var changed = Changed(
            revoked,
            "Revoked",
            changedBy,
            now);
        var result = await store.CommitAsync(
            revoked,
            changed,
            Metadata(revoked, changedBy, correlation),
            assignment.Version,
            ct);

        if (result.Status == RoleAssignmentCommitStatus.Applied)
            return;
        if (result.Authoritative is { } authoritative
            && authoritative.Id == assignmentId
            && authoritative.Tenant == tenants.Current
            && !authoritative.IsActive)
        {
            return;
        }

        throw new RoleAssignmentException(
            "The role assignment conflicted with a concurrent change.");
    }

    private static RoleAssignmentChanged Changed(
        RoleAssignment assignment,
        string change,
        AuditActor changedBy,
        DateTimeOffset occurredAt) =>
        new()
        {
            AssignmentId = assignment.Id,
            SubjectPersonKey = assignment.SubjectPersonKey,
            Role = ControlTowerAccessCatalog.Name(
                assignment.Role),
            OrganizationScope =
                assignment.OrganizationScope.ToString(),
            Change = change,
            ChangedBy = changedBy,
            Version = assignment.Version,
            OccurredAt = occurredAt,
        };

    private static EventAppendMetadata Metadata(
        RoleAssignment assignment,
        AuditActor actor,
        EventReference correlation) =>
        new(
            EventReference.For(
                "role-assignment",
                assignment.Id),
            actor,
            reason: null,
            correlation);

    private static bool IsAuthoritativeActive(
        RoleAssignment? assignment,
        TenantId tenant,
        PersonKey subject,
        ControlTowerRole role) =>
        assignment is not null
        && assignment.Tenant == tenant
        && assignment.SubjectPersonKey == subject
        && assignment.Role == role
        && assignment.IsActive;

    private static RoleAssignmentException NotFound() =>
        new("Role assignment not found in this tenant.");
}
