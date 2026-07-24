using System.Data;
using System.Text.Json;
using ControlTower.Adapters.PostgreSql;
using ControlTower.Modules.Trust.Authorization;
using ControlTower.Platform.Events;
using ControlTower.Platform.Identity;
using ControlTower.Platform.Tenancy;
using Npgsql;
using NpgsqlTypes;

namespace ControlTower.Adapters.PostgreSql.Trust;

/// <summary>
/// Durable C8 E18 store. The normal runtime identity receives only bounded role-assignment
/// functions; state and its canonical privileged event commit in one tenant-bound transaction.
/// </summary>
public sealed class PostgreSqlRoleAssignmentStore
    : IRoleAssignmentStore
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly ITenantContextAccessor _tenants;
    private readonly PostgreSqlEventTransactionAppender _events;

    public PostgreSqlRoleAssignmentStore(
        NpgsqlDataSource dataSource,
        ITenantContextAccessor tenants,
        PostgreSqlEventTransactionAppender events)
    {
        _dataSource = dataSource
            ?? throw new ArgumentNullException(nameof(dataSource));
        _tenants = tenants
            ?? throw new ArgumentNullException(nameof(tenants));
        _events = events
            ?? throw new ArgumentNullException(nameof(events));
    }

    public async Task<RoleAssignment?> GetAsync(
        Guid assignmentId,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var capture = PostgreSqlTenantCapture.Capture(_tenants);
        if (assignmentId == Guid.Empty)
            return null;

        try
        {
            await using var connection =
                await _dataSource.OpenConnectionAsync(ct)
                    .ConfigureAwait(false);
            await using var transaction =
                await connection.BeginTransactionAsync(
                        IsolationLevel.ReadCommitted,
                        ct)
                    .ConfigureAwait(false);
            var assignment = await ReadByIdAsync(
                    connection,
                    transaction,
                    capture,
                    assignmentId,
                    lockRow: false,
                    ct)
                .ConfigureAwait(false);
            await PostgreSqlTrustDb.CommitAsync(
                    transaction,
                    _tenants,
                    capture,
                    ct)
                .ConfigureAwait(false);
            return assignment;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (PostgreSqlTrustException)
        {
            throw;
        }
        catch (Exception exception)
            when (exception is NpgsqlException
                or EventIntegrityException)
        {
            throw Rejected();
        }
    }

    public async Task<IReadOnlyList<RoleAssignment>>
        ListForSubjectAsync(
            PersonKey subjectPersonKey,
            CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var capture = PostgreSqlTenantCapture.Capture(_tenants);
        if (!subjectPersonKey.IsValid)
            return [];

        try
        {
            await using var connection =
                await _dataSource.OpenConnectionAsync(ct)
                    .ConfigureAwait(false);
            await using var transaction =
                await connection.BeginTransactionAsync(
                        IsolationLevel.ReadCommitted,
                        ct)
                    .ConfigureAwait(false);
            _ = await PostgreSqlTrustDb.BindAsync(
                    connection,
                    transaction,
                    capture,
                    ct)
                .ConfigureAwait(false);
            await using var command = new NpgsqlCommand(
                """
                SELECT *
                FROM trust_store.list_role_assignments(
                    @tenant_id,
                    @subject_person_key);
                """,
                connection,
                transaction);
            PostgreSqlTrustDb.AddTenant(
                command,
                capture.Tenant);
            command.Parameters.Add(
                    "subject_person_key",
                    NpgsqlDbType.Uuid)
                .Value = subjectPersonKey.Value;

            var assignments = new List<RoleAssignment>();
            await using (var reader =
                await command.ExecuteReaderAsync(ct)
                    .ConfigureAwait(false))
            {
                while (await reader.ReadAsync(ct)
                    .ConfigureAwait(false))
                {
                    assignments.Add(
                        ReadAssignment(
                            reader,
                            capture.Tenant));
                }
            }

            await PostgreSqlTrustDb.CommitAsync(
                    transaction,
                    _tenants,
                    capture,
                    ct)
                .ConfigureAwait(false);
            return assignments;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (PostgreSqlTrustException)
        {
            throw;
        }
        catch (Exception exception)
            when (exception is NpgsqlException
                or EventIntegrityException)
        {
            throw Rejected();
        }
    }

    public async Task<RoleAssignmentCommitResult> CommitAsync(
        RoleAssignment assignment,
        RoleAssignmentChanged changed,
        EventAppendMetadata metadata,
        long expectedVersion,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var tenantCapture =
            PostgreSqlTenantCapture.Capture(_tenants);
        RoleAssignmentCommitSemantics.Validate(
            assignment,
            changed,
            metadata,
            expectedVersion);
        if (assignment.Tenant != tenantCapture.Tenant)
        {
            throw new InvalidOperationException(
                "Cross-tenant role-assignment write denied.");
        }

        var write = RoleAssignmentWrite.Capture(
            assignment,
            changed,
            metadata,
            expectedVersion);
        try
        {
            await using var connection =
                await _dataSource.OpenConnectionAsync(ct)
                    .ConfigureAwait(false);
            await using var transaction =
                await connection.BeginTransactionAsync(
                        IsolationLevel.ReadCommitted,
                        ct)
                    .ConfigureAwait(false);

            var result = expectedVersion == 0
                ? await CreateAsync(
                        connection,
                        transaction,
                        tenantCapture,
                        write,
                        ct)
                    .ConfigureAwait(false)
                : await RevokeAsync(
                        connection,
                        transaction,
                        tenantCapture,
                        write,
                        ct)
                    .ConfigureAwait(false);
            await PostgreSqlTrustDb.CommitAsync(
                    transaction,
                    _tenants,
                    tenantCapture,
                    ct)
                .ConfigureAwait(false);
            return result;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (PostgreSqlTrustException)
        {
            throw;
        }
        catch (Exception exception)
            when (exception is NpgsqlException
                or EventIntegrityException)
        {
            throw Rejected();
        }
    }

    private async Task<RoleAssignmentCommitResult> CreateAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        PostgreSqlTenantCapture tenantCapture,
        RoleAssignmentWrite write,
        CancellationToken ct)
    {
        var active = await LockActiveAsync(
                connection,
                transaction,
                tenantCapture,
                write.SubjectPersonKey,
                write.Role,
                ct)
            .ConfigureAwait(false);
        if (active is not null)
        {
            return new(
                RoleAssignmentCommitStatus.AlreadyActive,
                active);
        }

        var inserted = await InsertAsync(
                connection,
                transaction,
                tenantCapture,
                write,
                ct)
            .ConfigureAwait(false);
        if (!inserted)
        {
            var duplicate = await ReadByIdAsync(
                    connection,
                    transaction,
                    tenantCapture,
                    write.AssignmentId,
                    lockRow: false,
                    ct)
                .ConfigureAwait(false);
            return new(
                RoleAssignmentCommitStatus.Conflict,
                duplicate);
        }

        var bound = await PostgreSqlTrustDb.BindAsync(
                connection,
                transaction,
                tenantCapture,
                ct)
            .ConfigureAwait(false);
        _ = await _events.AppendWithinTransactionAsync(
                bound,
                write.EventRequest,
                ct)
            .ConfigureAwait(false);
        return new(
            RoleAssignmentCommitStatus.Applied,
            write.Assignment);
    }

    private async Task<RoleAssignmentCommitResult> RevokeAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        PostgreSqlTenantCapture tenantCapture,
        RoleAssignmentWrite write,
        CancellationToken ct)
    {
        var current = await ReadByIdAsync(
                connection,
                transaction,
                tenantCapture,
                write.AssignmentId,
                lockRow: true,
                ct)
            .ConfigureAwait(false);
        if (current is null
            || current.Version != write.ExpectedVersion
            || !current.IsActive)
        {
            return new(
                RoleAssignmentCommitStatus.Conflict,
                current);
        }
        if (!RoleAssignmentCommitSemantics.IsTransitionFrom(
                current,
                write.Assignment))
        {
            throw new InvalidOperationException(
                "The role-assignment transition does not match the current state.");
        }

        var updated = await RevokeRowAsync(
                connection,
                transaction,
                tenantCapture,
                write,
                ct)
            .ConfigureAwait(false);
        if (!updated)
            throw Rejected();

        var bound = await PostgreSqlTrustDb.BindAsync(
                connection,
                transaction,
                tenantCapture,
                ct)
            .ConfigureAwait(false);
        _ = await _events.AppendWithinTransactionAsync(
                bound,
                write.EventRequest,
                ct)
            .ConfigureAwait(false);
        return new(
            RoleAssignmentCommitStatus.Applied,
            write.Assignment);
    }

    private static async Task<RoleAssignment?> LockActiveAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        PostgreSqlTenantCapture capture,
        PersonKey subject,
        ControlTowerRole role,
        CancellationToken ct)
    {
        _ = await PostgreSqlTrustDb.BindAsync(
                connection,
                transaction,
                capture,
                ct)
            .ConfigureAwait(false);
        await using var command = new NpgsqlCommand(
            """
            SELECT *
            FROM trust_store.lock_active_role_assignment(
                @tenant_id,
                @subject_person_key,
                @role);
            """,
            connection,
            transaction);
        PostgreSqlTrustDb.AddTenant(command, capture.Tenant);
        command.Parameters.Add(
                "subject_person_key",
                NpgsqlDbType.Uuid)
            .Value = subject.Value;
        command.Parameters.Add(
                "role",
                NpgsqlDbType.Smallint)
            .Value = (short)role;
        return await ReadSingleAsync(
                command,
                capture.Tenant,
                ct)
            .ConfigureAwait(false);
    }

    private static async Task<RoleAssignment?> ReadByIdAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        PostgreSqlTenantCapture capture,
        Guid assignmentId,
        bool lockRow,
        CancellationToken ct)
    {
        _ = await PostgreSqlTrustDb.BindAsync(
                connection,
                transaction,
                capture,
                ct)
            .ConfigureAwait(false);
        await using var command = new NpgsqlCommand(
            lockRow
                ? """
                  SELECT *
                  FROM trust_store.lock_role_assignment(
                      @tenant_id,
                      @assignment_id);
                  """
                : """
                  SELECT *
                  FROM trust_store.read_role_assignment(
                      @tenant_id,
                      @assignment_id);
                  """,
            connection,
            transaction);
        PostgreSqlTrustDb.AddTenant(command, capture.Tenant);
        command.Parameters.Add(
                "assignment_id",
                NpgsqlDbType.Uuid)
            .Value = assignmentId;
        return await ReadSingleAsync(
                command,
                capture.Tenant,
                ct)
            .ConfigureAwait(false);
    }

    private static async Task<RoleAssignment?> ReadSingleAsync(
        NpgsqlCommand command,
        TenantId tenant,
        CancellationToken ct)
    {
        await using var reader =
            await command.ExecuteReaderAsync(ct)
                .ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false))
            return null;

        var assignment = ReadAssignment(reader, tenant);
        if (await reader.ReadAsync(ct).ConfigureAwait(false))
            throw Rejected();
        return assignment;
    }

    private static async Task<bool> InsertAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        PostgreSqlTenantCapture capture,
        RoleAssignmentWrite write,
        CancellationToken ct)
    {
        _ = await PostgreSqlTrustDb.BindAsync(
                connection,
                transaction,
                capture,
                ct)
            .ConfigureAwait(false);
        await using var command = new NpgsqlCommand(
            """
            SELECT trust_store.insert_role_assignment(
                @tenant_id,
                @assignment_id,
                @subject_person_key,
                @role,
                @organization_scope,
                @assigned_by_kind,
                @assigned_by_person_key,
                @assigned_by_workload_id,
                @assigned_at,
                @event_id);
            """,
            connection,
            transaction);
        PostgreSqlTrustDb.AddTenant(command, capture.Tenant);
        command.Parameters.Add(
                "assignment_id",
                NpgsqlDbType.Uuid)
            .Value = write.AssignmentId;
        command.Parameters.Add(
                "subject_person_key",
                NpgsqlDbType.Uuid)
            .Value = write.SubjectPersonKey.Value;
        command.Parameters.Add(
                "role",
                NpgsqlDbType.Smallint)
            .Value = (short)write.Role;
        command.Parameters.Add(
                "organization_scope",
                NpgsqlDbType.Smallint)
            .Value = (short)write.OrganizationScope;
        PostgreSqlTrustDb.AddActor(
            command,
            "assigned_by",
            write.AssignedBy);
        command.Parameters.Add(
                "assigned_at",
                NpgsqlDbType.TimestampTz)
            .Value = write.AssignedAt;
        command.Parameters.Add(
                "event_id",
                NpgsqlDbType.Uuid)
            .Value = write.EventId;
        return Equals(
            await command.ExecuteScalarAsync(ct)
                .ConfigureAwait(false),
            true);
    }

    private static async Task<bool> RevokeRowAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        PostgreSqlTenantCapture capture,
        RoleAssignmentWrite write,
        CancellationToken ct)
    {
        _ = await PostgreSqlTrustDb.BindAsync(
                connection,
                transaction,
                capture,
                ct)
            .ConfigureAwait(false);
        await using var command = new NpgsqlCommand(
            """
            SELECT trust_store.revoke_role_assignment(
                @tenant_id,
                @assignment_id,
                @expected_version,
                @revoked_at,
                @revoked_by_kind,
                @revoked_by_person_key,
                @revoked_by_workload_id,
                @event_id);
            """,
            connection,
            transaction);
        PostgreSqlTrustDb.AddTenant(command, capture.Tenant);
        command.Parameters.Add(
                "assignment_id",
                NpgsqlDbType.Uuid)
            .Value = write.AssignmentId;
        command.Parameters.Add(
                "expected_version",
                NpgsqlDbType.Bigint)
            .Value = write.ExpectedVersion;
        command.Parameters.Add(
                "revoked_at",
                NpgsqlDbType.TimestampTz)
            .Value = write.RevokedAt!.Value;
        PostgreSqlTrustDb.AddActor(
            command,
            "revoked_by",
            write.RevokedBy!.Value);
        command.Parameters.Add(
                "event_id",
                NpgsqlDbType.Uuid)
            .Value = write.EventId;
        return Equals(
            await command.ExecuteScalarAsync(ct)
                .ConfigureAwait(false),
            true);
    }

    private static RoleAssignment ReadAssignment(
        NpgsqlDataReader reader,
        TenantId expectedTenant)
    {
        try
        {
            var tenant = new TenantId(reader.GetGuid(1));
            if (tenant != expectedTenant
                || reader.GetInt16(4)
                != (short)OrganizationScope.TenantWide)
            {
                throw Rejected();
            }

            var revokedAt = reader.IsDBNull(10)
                ? (DateTimeOffset?)null
                : PostgreSqlTrustDb.ReadTimestamp(reader, 10);
            AuditActor? revokedBy = null;
            if (!reader.IsDBNull(11))
            {
                revokedBy = PostgreSqlTrustDb.ReadActor(
                    reader,
                    11,
                    12,
                    13);
            }
            else if (!reader.IsDBNull(12)
                     || !reader.IsDBNull(13))
            {
                throw Rejected();
            }

            return RoleAssignment.Rehydrate(
                reader.GetGuid(0),
                tenant,
                new PersonKey(reader.GetGuid(2)),
                (ControlTowerRole)reader.GetInt16(3),
                PostgreSqlTrustDb.ReadActor(
                    reader,
                    5,
                    6,
                    7),
                PostgreSqlTrustDb.ReadTimestamp(reader, 8),
                reader.GetInt64(9),
                revokedAt,
                revokedBy);
        }
        catch (PostgreSqlTrustException)
        {
            throw;
        }
        catch (Exception exception)
            when (exception is ArgumentException
                or FormatException
                or InvalidCastException
                or OverflowException
                or RoleAssignmentException)
        {
            throw Rejected();
        }
    }

    private static PostgreSqlTrustException Rejected() =>
        new(PostgreSqlTrustDb.RoleAssignmentRejected);

    private sealed class RoleAssignmentWrite
    {
        private RoleAssignmentWrite(
            RoleAssignment assignment,
            Guid eventId,
            PostgreSqlEventAppendRequest eventRequest,
            long expectedVersion)
        {
            Assignment = assignment;
            AssignmentId = assignment.Id;
            SubjectPersonKey = assignment.SubjectPersonKey;
            Role = assignment.Role;
            OrganizationScope = assignment.OrganizationScope;
            AssignedBy = assignment.AssignedBy;
            AssignedAt = assignment.AssignedAt;
            RevokedAt = assignment.RevokedAt;
            RevokedBy = assignment.RevokedBy;
            EventId = eventId;
            EventRequest = eventRequest;
            ExpectedVersion = expectedVersion;
        }

        internal RoleAssignment Assignment { get; }
        internal Guid AssignmentId { get; }
        internal PersonKey SubjectPersonKey { get; }
        internal ControlTowerRole Role { get; }
        internal OrganizationScope OrganizationScope { get; }
        internal AuditActor AssignedBy { get; }
        internal DateTimeOffset AssignedAt { get; }
        internal DateTimeOffset? RevokedAt { get; }
        internal AuditActor? RevokedBy { get; }
        internal Guid EventId { get; }
        internal PostgreSqlEventAppendRequest EventRequest { get; }
        internal long ExpectedVersion { get; }

        internal static RoleAssignmentWrite Capture(
            RoleAssignment assignment,
            RoleAssignmentChanged changed,
            EventAppendMetadata metadata,
            long expectedVersion)
        {
            var payload =
                JsonSerializer.SerializeToUtf8Bytes(changed);
            var eventRequest =
                PostgreSqlEventAppendRequest.Capture(
                    changed,
                    metadata,
                    payload);
            return new RoleAssignmentWrite(
                assignment,
                changed.EventId,
                eventRequest,
                expectedVersion);
        }
    }
}
