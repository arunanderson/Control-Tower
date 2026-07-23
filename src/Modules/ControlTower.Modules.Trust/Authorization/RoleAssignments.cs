using ControlTower.Platform.Events;
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
        RoleAssignmentActor assignedBy,
        DateTimeOffset assignedAt)
        : this(
            id,
            tenant,
            subjectPersonKey,
            role,
            assignedBy,
            assignedAt,
            null,
            null)
    {
    }

    private RoleAssignment(
        Guid id,
        TenantId tenant,
        PersonKey subjectPersonKey,
        ControlTowerRole role,
        RoleAssignmentActor assignedBy,
        DateTimeOffset assignedAt,
        DateTimeOffset? revokedAt,
        RoleAssignmentActor? revokedBy)
    {
        if (id == Guid.Empty)
            throw new RoleAssignmentException("A role-assignment ID is required.");
        if (!subjectPersonKey.IsValid)
            throw new RoleAssignmentException("A non-empty subject person key is required.");
        if (!Enum.IsDefined(role))
            throw new RoleAssignmentException("The role is not a curated Control Tower role.");
        if (!assignedBy.IsValid)
            throw new RoleAssignmentException("A bounded assigning actor is required.");
        if (revokedBy is not null && !revokedBy.Value.IsValid)
            throw new RoleAssignmentException("A bounded revoking actor is required.");

        Id = id;
        Tenant = tenant;
        SubjectPersonKey = subjectPersonKey;
        Role = role;
        AssignedBy = assignedBy;
        AssignedAt = assignedAt;
        RevokedAt = revokedAt;
        RevokedBy = revokedBy;
    }

    public Guid Id { get; }
    public TenantId Tenant { get; }
    public PersonKey SubjectPersonKey { get; }
    public ControlTowerRole Role { get; }
    public OrganizationScope OrganizationScope => OrganizationScope.TenantWide;
    public RoleAssignmentActor AssignedBy { get; }
    public DateTimeOffset AssignedAt { get; }
    public DateTimeOffset? RevokedAt { get; }
    public RoleAssignmentActor? RevokedBy { get; }
    public bool IsActive => RevokedAt is null;

    internal RoleAssignment Revoke(
        RoleAssignmentActor revokedBy,
        DateTimeOffset revokedAt)
    {
        if (!IsActive)
            throw new RoleAssignmentException("The role assignment is already revoked.");
        if (!revokedBy.IsValid)
            throw new RoleAssignmentException("A bounded revoking actor is required.");

        return new(
            Id,
            Tenant,
            SubjectPersonKey,
            Role,
            AssignedBy,
            AssignedAt,
            revokedAt,
            revokedBy);
    }
}

[DomainEventContract("RoleAssignmentChanged", EventPrivilege.Privileged)]
public sealed record RoleAssignmentChanged : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
    public required Guid AssignmentId { get; init; }
    public required Guid SubjectPersonKey { get; init; }
    public required string Role { get; init; }
    public required string OrganizationScope { get; init; }
    public required string Change { get; init; }
    public required string ChangedBy { get; init; }
}

public interface IRoleAssignmentReader
{
    Task<IReadOnlyList<RoleAssignment>> ListForSubjectAsync(
        PersonKey subjectPersonKey,
        CancellationToken ct = default);
}

public interface IRoleAssignmentStore : IRoleAssignmentReader
{
    Task<RoleAssignment?> GetAsync(Guid assignmentId, CancellationToken ct = default);

    /// <summary>
    /// Atomically persists the E18 state and its canonical audit event. The durable adapter must use
    /// one database transaction; an assignment may never exist without the matching event. It must
    /// also enforce one active assignment per tenant/person/role and use optimistic concurrency for
    /// revocation so concurrent commands cannot leave duplicate grants or audit events.
    /// </summary>
    Task CommitAsync(
        RoleAssignment assignment,
        RoleAssignmentChanged changed,
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
        CancellationToken ct = default)
    {
        if (subjectObjectId == Guid.Empty)
            return ControlTowerAccessCatalog.Resolve([]);

        var subjectPersonKey = await personKeys.FindAsync(subjectObjectId, ct);
        if (subjectPersonKey is null || !subjectPersonKey.Value.IsValid)
            return ControlTowerAccessCatalog.Resolve([]);

        var tenant = tenants.Current;
        var active = await assignments.ListForSubjectAsync(
            subjectPersonKey.Value,
            ct);
        return ControlTowerAccessCatalog.Resolve(
            active.Where(assignment =>
                    assignment.Tenant == tenant
                    && assignment.SubjectPersonKey == subjectPersonKey.Value
                    && assignment.IsActive)
                .Select(assignment => assignment.Role));
    }
}

/// <summary>C8 assignment lifecycle. Mutations append RoleAssignmentChanged to the audit stream.</summary>
public sealed class RoleAssignmentService(
    IPersonKeyMap personKeys,
    IRoleAssignmentStore store,
    ITenantContextAccessor tenants)
{
    public async Task<Guid> AssignAsync(
        Guid subjectObjectId,
        ControlTowerRole role,
        RoleAssignmentActor changedBy,
        CancellationToken ct = default)
    {
        if (subjectObjectId == Guid.Empty)
            throw new RoleAssignmentException("A non-empty subject oid is required.");
        if (!Enum.IsDefined(role))
            throw new RoleAssignmentException("The role is not a curated Control Tower role.");
        if (!changedBy.IsValid)
            throw new RoleAssignmentException("A bounded assigning actor is required.");

        var tenant = tenants.Current;
        var subjectPersonKey = await personKeys.GetOrCreateAsync(
            subjectObjectId,
            ct);
        if (!subjectPersonKey.IsValid)
        {
            throw new RoleAssignmentException(
                "The person-key map returned an invalid subject.");
        }
        var existing = (await store.ListForSubjectAsync(subjectPersonKey, ct))
            .SingleOrDefault(assignment =>
                assignment.Tenant == tenant
                && assignment.SubjectPersonKey == subjectPersonKey
                && assignment.IsActive
                && assignment.Role == role);
        if (existing is not null)
            return existing.Id;

        var now = DateTimeOffset.UtcNow;
        var assignment = new RoleAssignment(
            Guid.NewGuid(),
            tenant,
            subjectPersonKey,
            role,
            changedBy,
            now);
        await store.CommitAsync(
            assignment,
            Changed(assignment, "Assigned", changedBy, now),
            ct);
        return assignment.Id;
    }

    public async Task RevokeAsync(
        Guid assignmentId,
        RoleAssignmentActor changedBy,
        CancellationToken ct = default)
    {
        var assignment = await store.GetAsync(assignmentId, ct)
            ?? throw NotFound();
        if (assignment.Id != assignmentId
            || assignment.Tenant != tenants.Current)
            throw NotFound();

        var now = DateTimeOffset.UtcNow;
        var revoked = assignment.Revoke(changedBy, now);
        await store.CommitAsync(
            revoked,
            Changed(revoked, "Revoked", changedBy, now),
            ct);
    }

    private static RoleAssignmentChanged Changed(
        RoleAssignment assignment,
        string change,
        RoleAssignmentActor changedBy,
        DateTimeOffset occurredAt)
    {
        return new RoleAssignmentChanged
        {
            AssignmentId = assignment.Id,
            SubjectPersonKey = assignment.SubjectPersonKey.Value,
            Role = ControlTowerAccessCatalog.Name(assignment.Role),
            OrganizationScope = assignment.OrganizationScope.ToString(),
            Change = change,
            ChangedBy = changedBy.ToString(),
            OccurredAt = occurredAt,
        };
    }

    private static RoleAssignmentException NotFound() =>
        new("Role assignment not found in this tenant.");
}
