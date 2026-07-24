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
        var tenant = tenants.Current;
        if (assignment.Tenant != tenant)
        {
            throw new InvalidOperationException(
                "Cross-tenant role-assignment write denied.");
        }
        RoleAssignmentCommitSemantics.Validate(
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
            if (!RoleAssignmentCommitSemantics.IsTransitionFrom(
                    current,
                    assignment))
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

    private Dictionary<Guid, RoleAssignment> BucketFor(
        TenantId tenant) =>
        _assignments.TryGetValue(
            tenant,
            out var assignments)
            ? assignments
            : _assignments[tenant] = [];
}
