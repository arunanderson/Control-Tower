using System.Text.Json;
using ControlTower.Modules.Trust.Authorization;
using ControlTower.Platform.Events;
using ControlTower.Platform.Identity;
using ControlTower.Platform.Tenancy;

namespace ControlTower.Modules.Trust.Infrastructure;

/// <summary>
/// DEV-ONLY tenant-partitioned E18 substitute. A serialized commit gate models the durable
/// transaction's active-grant uniqueness and optimistic-concurrency guarantees.
/// </summary>
public sealed class InMemoryRoleAssignmentStore(
    ITenantContextAccessor tenants,
    IEventStore events)
    : IRoleAssignmentStore
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<
        TenantId,
        Dictionary<Guid, RoleAssignment>> _assignments = [];

    public async Task<RoleAssignment?> GetAsync(
        Guid assignmentId,
        CancellationToken ct = default)
    {
        var tenant = tenants.Current;
        await _gate.WaitAsync(ct);
        try
        {
            BucketFor(tenant).TryGetValue(
                assignmentId,
                out var assignment);
            return assignment;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<RoleAssignment>>
        ListForSubjectAsync(
            PersonKey subjectPersonKey,
            CancellationToken ct = default)
    {
        var tenant = tenants.Current;
        await _gate.WaitAsync(ct);
        try
        {
            return BucketFor(tenant).Values
                .Where(assignment =>
                    assignment.SubjectPersonKey
                    == subjectPersonKey)
                .OrderBy(assignment =>
                    assignment.AssignedAt)
                .ToList();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<RoleAssignmentCommitResult> CommitAsync(
        RoleAssignment assignment,
        RoleAssignmentChanged changed,
        EventAppendMetadata metadata,
        long expectedVersion,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(assignment);
        ArgumentNullException.ThrowIfNull(changed);
        ArgumentNullException.ThrowIfNull(metadata);
        if (expectedVersion < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(expectedVersion),
                "The expected version cannot be negative.");
        }

        var tenant = tenants.Current;
        if (assignment.Tenant != tenant)
        {
            throw new InvalidOperationException(
                "Cross-tenant role-assignment write denied.");
        }
        ValidateConsistency(
            assignment,
            changed,
            metadata,
            expectedVersion);

        await _gate.WaitAsync(ct);
        try
        {
            var bucket = BucketFor(tenant);
            if (expectedVersion == 0)
            {
                var active = bucket.Values.SingleOrDefault(
                    current =>
                        current.SubjectPersonKey
                        == assignment.SubjectPersonKey
                        && current.Role == assignment.Role
                        && current.IsActive);
                if (active is not null)
                {
                    return new(
                        RoleAssignmentCommitStatus.AlreadyActive,
                        active);
                }

                if (bucket.TryGetValue(
                        assignment.Id,
                        out var duplicateId))
                {
                    return new(
                        RoleAssignmentCommitStatus.Conflict,
                        duplicateId);
                }

                await AppendAsync(
                    changed,
                    metadata,
                    ct);
                bucket.Add(assignment.Id, assignment);
                return new(
                    RoleAssignmentCommitStatus.Applied,
                    assignment);
            }

            if (!bucket.TryGetValue(
                    assignment.Id,
                    out var current))
            {
                return new(
                    RoleAssignmentCommitStatus.Conflict,
                    null);
            }
            if (current.Version != expectedVersion
                || !current.IsActive)
            {
                return new(
                    RoleAssignmentCommitStatus.Conflict,
                    current);
            }
            if (!IsTransitionFrom(current, assignment))
            {
                throw new InvalidOperationException(
                    "The role-assignment transition does not match the current state.");
            }

            await AppendAsync(changed, metadata, ct);
            bucket[assignment.Id] = assignment;
            return new(
                RoleAssignmentCommitStatus.Applied,
                assignment);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task AppendAsync(
        RoleAssignmentChanged changed,
        EventAppendMetadata metadata,
        CancellationToken ct) =>
        await events.AppendAsync(
            changed,
            metadata,
            JsonSerializer.SerializeToUtf8Bytes(changed),
            ct);

    private static void ValidateConsistency(
        RoleAssignment assignment,
        RoleAssignmentChanged changed,
        EventAppendMetadata metadata,
        long expectedVersion)
    {
        var expectedChange =
            assignment.IsActive ? "Assigned" : "Revoked";
        var expectedActor = assignment.IsActive
            ? assignment.AssignedBy
            : assignment.RevokedBy!.Value;
        var expectedOccurredAt = assignment.IsActive
            ? assignment.AssignedAt
            : assignment.RevokedAt!.Value;
        if (changed.AssignmentId != assignment.Id
            || changed.EventId == Guid.Empty
            || changed.SubjectPersonKey
            != assignment.SubjectPersonKey
            || changed.Role
            != ControlTowerAccessCatalog.Name(assignment.Role)
            || changed.OrganizationScope
            != assignment.OrganizationScope.ToString()
            || changed.Change != expectedChange
            || changed.ChangedBy != expectedActor
            || changed.Version != assignment.Version
            || changed.OccurredAt != expectedOccurredAt
            || metadata.AggregateReference
            != EventReference.For(
                "role-assignment",
                assignment.Id)
            || metadata.Actor != expectedActor
            || metadata.Reason is not null
            || metadata.CorrelationReference
                is not { Kind: "role-assignment-command" }
                    correlation
            || !Guid.TryParseExact(
                correlation.Value,
                "D",
                out var correlationId)
            || correlationId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "Role-assignment state, event and audit metadata do not match.");
        }

        if (expectedVersion == 0)
        {
            if (!assignment.IsActive
                || assignment.Version != 1)
            {
                throw new InvalidOperationException(
                    "A role-assignment create must produce active version 1.");
            }
        }
        else if (assignment.IsActive
                 || assignment.Version
                 != checked(expectedVersion + 1))
        {
            throw new InvalidOperationException(
                "A role-assignment transition has an invalid version.");
        }
    }

    private static bool IsTransitionFrom(
        RoleAssignment current,
        RoleAssignment next) =>
        current.Id == next.Id
        && current.Tenant == next.Tenant
        && current.SubjectPersonKey == next.SubjectPersonKey
        && current.Role == next.Role
        && current.OrganizationScope
        == next.OrganizationScope
        && current.AssignedBy == next.AssignedBy
        && current.AssignedAt == next.AssignedAt
        && next.Version == current.Version + 1
        && !next.IsActive;

    private Dictionary<Guid, RoleAssignment> BucketFor(
        TenantId tenant) =>
        _assignments.TryGetValue(
            tenant,
            out var assignments)
            ? assignments
            : _assignments[tenant] = [];
}
