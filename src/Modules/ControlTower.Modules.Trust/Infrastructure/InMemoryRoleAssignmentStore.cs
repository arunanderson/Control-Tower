using System.Text.Json;
using ControlTower.Modules.Trust.Authorization;
using ControlTower.Platform.Events;
using ControlTower.Platform.Tenancy;

namespace ControlTower.Modules.Trust.Infrastructure;

/// <summary>
/// DEV-ONLY tenant-partitioned E18 substitute. Production replaces this port with PostgreSQL + RLS.
/// </summary>
public sealed class InMemoryRoleAssignmentStore(
    ITenantContextAccessor tenants,
    IEventStore events)
    : IRoleAssignmentStore
{
    private readonly object _gate = new();
    private readonly Dictionary<TenantId, Dictionary<Guid, RoleAssignment>> _assignments = [];

    public Task<RoleAssignment?> GetAsync(
        Guid assignmentId,
        CancellationToken ct = default)
    {
        var tenant = tenants.Current;
        lock (_gate)
        {
            BucketFor(tenant).TryGetValue(assignmentId, out var assignment);
            return Task.FromResult(assignment);
        }
    }

    public Task<IReadOnlyList<RoleAssignment>> ListForSubjectAsync(
        PersonKey subjectPersonKey,
        CancellationToken ct = default)
    {
        var tenant = tenants.Current;
        lock (_gate)
        {
            return Task.FromResult<IReadOnlyList<RoleAssignment>>(
                BucketFor(tenant).Values
                    .Where(assignment =>
                        assignment.SubjectPersonKey == subjectPersonKey)
                    .OrderBy(assignment => assignment.AssignedAt)
                    .ToList());
        }
    }

    public async Task CommitAsync(
        RoleAssignment assignment,
        RoleAssignmentChanged changed,
        CancellationToken ct = default)
    {
        var tenant = tenants.Current;
        if (assignment.Tenant != tenant)
            throw new InvalidOperationException("Cross-tenant role-assignment write denied.");
        var expectedChange = assignment.IsActive ? "Assigned" : "Revoked";
        var expectedActor = assignment.IsActive
            ? assignment.AssignedBy
            : assignment.RevokedBy!.Value;
        var expectedOccurredAt = assignment.IsActive
            ? assignment.AssignedAt
            : assignment.RevokedAt!.Value;
        if (changed.AssignmentId != assignment.Id
            || changed.EventId == Guid.Empty
            || changed.SubjectPersonKey != assignment.SubjectPersonKey.Value
            || changed.Role != ControlTowerAccessCatalog.Name(assignment.Role)
            || changed.OrganizationScope != assignment.OrganizationScope.ToString()
            || changed.Change != expectedChange
            || changed.ChangedBy != expectedActor.ToString()
            || changed.OccurredAt != expectedOccurredAt)
        {
            throw new InvalidOperationException(
                "Role-assignment state and audit event do not match.");
        }

        // Append first: if the development event store rejects the write, state remains unchanged.
        // The production implementation owns one transaction for both records.
        await events.AppendAsync(
            changed,
            JsonSerializer.SerializeToUtf8Bytes(changed),
            ct);
        lock (_gate)
            BucketFor(tenant)[assignment.Id] = assignment;
    }

    private Dictionary<Guid, RoleAssignment> BucketFor(TenantId tenant) =>
        _assignments.TryGetValue(tenant, out var assignments)
            ? assignments
            : _assignments[tenant] = [];
}
