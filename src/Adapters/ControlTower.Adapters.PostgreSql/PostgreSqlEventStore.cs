using System.Data;
using ControlTower.Platform.Events;
using ControlTower.Platform.Identity;
using ControlTower.Platform.Tenancy;
using Npgsql;
using NpgsqlTypes;

namespace ControlTower.Adapters.PostgreSql;

/// <summary>
/// Durable E20 event store for Azure Database for PostgreSQL. Tenant context, stream position,
/// recorded time, event contract, privilege and integrity links are controlled by this adapter.
/// </summary>
public sealed class PostgreSqlEventStore : IEventStore
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly ITenantContextAccessor _tenants;
    private readonly PostgreSqlEventKernel _kernel;

    public PostgreSqlEventStore(
        NpgsqlDataSource dataSource,
        ITenantContextAccessor tenants,
        IHashChain chain,
        TimeProvider? clock = null)
    {
        _dataSource = dataSource
            ?? throw new ArgumentNullException(nameof(dataSource));
        _tenants = tenants
            ?? throw new ArgumentNullException(nameof(tenants));
        ArgumentNullException.ThrowIfNull(chain);
        _kernel = new PostgreSqlEventKernel(
            chain,
            clock ?? TimeProvider.System);
    }

    public async ValueTask<StoredEvent> AppendAsync(
        IDomainEvent @event,
        EventAppendMetadata metadata,
        ReadOnlyMemory<byte> payload,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var tenant = _tenants.Current;

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

            var stored = await _kernel.AppendWithinTransactionAsync(
                    connection,
                    transaction,
                    tenant,
                    @event,
                    metadata,
                    payload,
                    ct)
                .ConfigureAwait(false);

            ct.ThrowIfCancellationRequested();
            await transaction.CommitAsync(CancellationToken.None)
                .ConfigureAwait(false);
            return stored;
        }
        catch (PostgresException)
        {
            throw PostgreSqlEventKernel.RejectedAppend();
        }
    }

    public async ValueTask<IReadOnlyList<StoredEvent>> ReadAllAsync(
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var tenant = _tenants.Current;

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
            await PostgreSqlTenantSession.BindAsync(
                    connection,
                    transaction,
                    tenant,
                    ct)
                .ConfigureAwait(false);

            await using var command = new NpgsqlCommand(
                """
                SELECT
                    integrity_format_version,
                    position,
                    event_id,
                    event_type,
                    aggregate_kind,
                    aggregate_value,
                    actor_kind,
                    actor_opaque_id,
                    occurred_at,
                    recorded_at,
                    reason,
                    correlation_kind,
                    correlation_value,
                    privilege,
                    previous_hash,
                    hash,
                    payload
                FROM event_store.domain_events
                WHERE tenant_id = @tenant_id
                ORDER BY position;
                """,
                connection,
                transaction);
            PostgreSqlTenantSession.AddTenantParameter(command, tenant);

            var stream = new List<StoredEvent>();
            await using (var reader =
                await command.ExecuteReaderAsync(ct).ConfigureAwait(false))
            {
                while (await reader.ReadAsync(ct).ConfigureAwait(false))
                {
                    stream.Add(ReadStoredEvent(reader, tenant));
                }
            }

            ct.ThrowIfCancellationRequested();
            await transaction.CommitAsync(CancellationToken.None)
                .ConfigureAwait(false);
            return stream;
        }
        catch (PostgresException)
        {
            throw new EventIntegrityException(
                "The event read was rejected.");
        }
    }

    private static StoredEvent ReadStoredEvent(
        NpgsqlDataReader reader,
        TenantId tenant)
    {
        try
        {
            var actorKind =
                (AuditActorKind)reader.GetInt16(6);
            var actorOpaqueId = reader.GetString(7);
            var actor = actorKind switch
            {
                AuditActorKind.Human => AuditActor.Person(
                    new PersonKey(Guid.ParseExact(actorOpaqueId, "D"))),
                AuditActorKind.System =>
                    AuditActor.System(actorOpaqueId),
                AuditActorKind.Provider =>
                    AuditActor.Provider(actorOpaqueId),
                _ => throw new EventIntegrityException(
                    "The stored event actor kind is invalid."),
            };

            EventReference? correlation = reader.IsDBNull(11)
                ? null
                : new EventReference(
                    reader.GetString(11),
                    reader.GetString(12));
            var stored = new StoredEvent(
                reader.GetInt32(0),
                reader.GetInt64(1),
                reader.GetGuid(2),
                reader.GetString(3),
                new EventReference(
                    reader.GetString(4),
                    reader.GetString(5)),
                actor,
                AsUtcDateTimeOffset(reader.GetDateTime(8)),
                AsUtcDateTimeOffset(reader.GetDateTime(9)),
                reader.IsDBNull(10) ? null : reader.GetString(10),
                correlation,
                tenant,
                (EventPrivilege)reader.GetInt16(13),
                reader.GetString(14),
                reader.GetString(15),
                reader.GetFieldValue<byte[]>(16));
            EventEnvelopeCanonicalizer.Validate(stored);
            return stored;
        }
        catch (EventIntegrityException)
        {
            throw;
        }
        catch (Exception exception)
            when (exception is ArgumentException
                or FormatException
                or InvalidCastException
                or OverflowException)
        {
            throw new EventIntegrityException(
                "A stored event record is malformed.");
        }
    }

    private static DateTimeOffset AsUtcDateTimeOffset(DateTime value)
    {
        var utc = value.Kind == DateTimeKind.Utc
            ? value
            : DateTime.SpecifyKind(value, DateTimeKind.Utc);
        return new DateTimeOffset(utc);
    }
}

/// <summary>
/// Transaction-aware E20 kernel. Later PostgreSQL adapters can compose authoritative state and its
/// audit event in one transaction without duplicating event serialization or integrity logic.
/// </summary>
internal sealed class PostgreSqlEventKernel(
    IHashChain chain,
    TimeProvider clock)
{
    internal async ValueTask<StoredEvent> AppendWithinTransactionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        TenantId tenant,
        IDomainEvent @event,
        EventAppendMetadata metadata,
        ReadOnlyMemory<byte> payload,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(metadata);
        ct.ThrowIfCancellationRequested();

        if (@event.EventId == Guid.Empty)
            throw new EventIntegrityException("The event ID is invalid.");

        var contract = DomainEventContracts.Resolve(@event);
        var occurredAt = EventEnvelopeCanonicalizer.NormalizeTimestamp(
            @event.OccurredAt);
        var ownedPayload = payload.ToArray();

        try
        {
            await PostgreSqlTenantSession.BindAsync(
                    connection,
                    transaction,
                    tenant,
                    ct)
                .ConfigureAwait(false);
            var (position, previousHash) =
                await LockStreamHeadAsync(
                        connection,
                        transaction,
                        tenant,
                        ct)
                    .ConfigureAwait(false);

            var recordedAt = EventEnvelopeCanonicalizer.NormalizeTimestamp(
                clock.GetUtcNow());
            var prospective = new StoredEvent(
                EventEnvelopeCanonicalizer.CurrentIntegrityFormatVersion,
                position,
                @event.EventId,
                contract.EventType,
                metadata.AggregateReference,
                metadata.Actor,
                occurredAt,
                recordedAt,
                metadata.Reason,
                metadata.CorrelationReference,
                tenant,
                contract.Privilege,
                previousHash,
                string.Empty,
                ownedPayload);
            var hash = chain.ComputeNext(
                previousHash,
                EventEnvelopeCanonicalizer.Canonicalize(prospective));
            var stored = prospective with { Hash = hash };

            await AppendEventAsync(
                    connection,
                    transaction,
                    stored,
                    ct)
                .ConfigureAwait(false);
            return stored;
        }
        catch (PostgresException)
        {
            throw RejectedAppend();
        }
    }

    internal static EventIntegrityException RejectedAppend() =>
        new("The event append was rejected.");

    private static async Task<(long Position, string PreviousHash)>
        LockStreamHeadAsync(
            NpgsqlConnection connection,
            NpgsqlTransaction transaction,
            TenantId tenant,
            CancellationToken ct)
    {
        await using var command = new NpgsqlCommand(
            """
            SELECT stream_position, previous_hash
            FROM event_store.lock_stream_head(@tenant_id);
            """,
            connection,
            transaction);
        PostgreSqlTenantSession.AddTenantParameter(command, tenant);
        await using var reader =
            await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false))
            throw RejectedAppend();

        var position = reader.GetInt64(0);
        var previousHash = reader.GetString(1);
        if (await reader.ReadAsync(ct).ConfigureAwait(false))
            throw RejectedAppend();

        return (position, previousHash);
    }

    private static async Task AppendEventAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        StoredEvent stored,
        CancellationToken ct)
    {
        await using var command = new NpgsqlCommand(
            """
            SELECT event_store.append_event(
                @integrity_format_version,
                @tenant_id,
                @position,
                @event_id,
                @event_type,
                @aggregate_kind,
                @aggregate_value,
                @actor_kind,
                @actor_opaque_id,
                @occurred_at,
                @recorded_at,
                @reason,
                @correlation_kind,
                @correlation_value,
                @privilege,
                @previous_hash,
                @hash,
                @payload);
            """,
            connection,
            transaction);
        command.Parameters.Add(
                "integrity_format_version",
                NpgsqlDbType.Integer)
            .Value = stored.IntegrityFormatVersion;
        PostgreSqlTenantSession.AddTenantParameter(
            command,
            stored.Tenant);
        command.Parameters.Add("position", NpgsqlDbType.Bigint).Value =
            stored.Position;
        command.Parameters.Add("event_id", NpgsqlDbType.Uuid).Value =
            stored.EventId;
        AddVarchar(command, "event_type", stored.EventType);
        AddVarchar(
            command,
            "aggregate_kind",
            stored.AggregateReference.Kind);
        AddVarchar(
            command,
            "aggregate_value",
            stored.AggregateReference.Value);
        command.Parameters.Add("actor_kind", NpgsqlDbType.Smallint).Value =
            (short)stored.Actor.Kind;
        AddVarchar(
            command,
            "actor_opaque_id",
            stored.Actor.OpaqueId);
        command.Parameters.Add(
                "occurred_at",
                NpgsqlDbType.TimestampTz)
            .Value = stored.OccurredAt.UtcDateTime;
        command.Parameters.Add(
                "recorded_at",
                NpgsqlDbType.TimestampTz)
            .Value = stored.RecordedAt.UtcDateTime;
        AddNullableVarchar(command, "reason", stored.Reason);
        AddNullableVarchar(
            command,
            "correlation_kind",
            stored.CorrelationReference?.Kind);
        AddNullableVarchar(
            command,
            "correlation_value",
            stored.CorrelationReference?.Value);
        command.Parameters.Add("privilege", NpgsqlDbType.Smallint).Value =
            (short)stored.Privilege;
        AddVarchar(command, "previous_hash", stored.PreviousHash);
        AddVarchar(command, "hash", stored.Hash);
        command.Parameters.Add("payload", NpgsqlDbType.Bytea).Value =
            stored.Payload;
        _ = await command.ExecuteScalarAsync(ct).ConfigureAwait(false);
    }

    private static void AddVarchar(
        NpgsqlCommand command,
        string name,
        string value)
    {
        command.Parameters.Add(name, NpgsqlDbType.Varchar).Value = value;
    }

    private static void AddNullableVarchar(
        NpgsqlCommand command,
        string name,
        string? value)
    {
        command.Parameters.Add(name, NpgsqlDbType.Varchar).Value =
            (object?)value ?? DBNull.Value;
    }
}

internal static class PostgreSqlTenantSession
{
    internal static async Task BindAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        TenantId tenant,
        CancellationToken ct)
    {
        await using var command = new NpgsqlCommand(
            """
            SELECT set_config(
                'control_tower.tenant_id',
                @tenant_context,
                true);
            """,
            connection,
            transaction);
        command.Parameters.Add(
                "tenant_context",
                NpgsqlDbType.Text)
            .Value = tenant.Value.ToString("D");
        _ = await command.ExecuteScalarAsync(ct).ConfigureAwait(false);
    }

    internal static void AddTenantParameter(
        NpgsqlCommand command,
        TenantId tenant)
    {
        command.Parameters.Add("tenant_id", NpgsqlDbType.Uuid).Value =
            tenant.Value;
    }
}
