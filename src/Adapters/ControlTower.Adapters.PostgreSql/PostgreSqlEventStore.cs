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
    private readonly PostgreSqlEventTransactionAppender _appender;

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
        _appender = new PostgreSqlEventTransactionAppender(
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
        var tenantCapture =
            PostgreSqlTenantCapture.Capture(_tenants);
        var request = PostgreSqlEventAppendRequest.Capture(
            @event,
            metadata,
            payload);

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
            var tenantTransaction =
                await PostgreSqlTenantTransaction.BindAsync(
                        connection,
                        transaction,
                        tenantCapture,
                        ct)
                    .ConfigureAwait(false);

            var stored = await _appender.AppendWithinTransactionAsync(
                    tenantTransaction,
                    request,
                    ct)
                .ConfigureAwait(false);

            ct.ThrowIfCancellationRequested();
            await transaction.CommitAsync(CancellationToken.None)
                .ConfigureAwait(false);
            return stored;
        }
        catch (PostgresException)
        {
            throw PostgreSqlEventTransactionAppender.RejectedAppend();
        }
    }

    public async ValueTask<IReadOnlyList<StoredEvent>> ReadAllAsync(
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var tenantCapture =
            PostgreSqlTenantCapture.Capture(_tenants);

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
            var tenantTransaction =
                await PostgreSqlTenantTransaction.BindAsync(
                        connection,
                        transaction,
                        tenantCapture,
                        ct)
                    .ConfigureAwait(false);
            var tenant = tenantTransaction.Tenant;

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
            PostgreSqlTenantBinding.AddTenantParameter(command, tenant);

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
/// Opaque tenant authority captured from the ambient accessor before caller-controlled work or I/O.
/// The captured authority cannot be constructed from an arbitrary tenant identifier.
/// </summary>
public sealed class PostgreSqlTenantCapture
{
    private readonly ITenantContextAccessor _tenants;

    private PostgreSqlTenantCapture(
        ITenantContextAccessor tenants,
        TenantId tenant)
    {
        _tenants = tenants;
        Tenant = tenant;
    }

    public TenantId Tenant { get; }

    public static PostgreSqlTenantCapture Capture(
        ITenantContextAccessor tenants)
    {
        ArgumentNullException.ThrowIfNull(tenants);
        var tenant = tenants.Current;
        if (tenant.Value == Guid.Empty)
        {
            throw new EventIntegrityException(
                "The database tenant context is invalid.");
        }

        return new PostgreSqlTenantCapture(
            tenants,
            tenant);
    }

    internal void EnsureAmbientTenant()
    {
        try
        {
            if (_tenants.Current == Tenant)
                return;
        }
        catch (InvalidOperationException)
        {
            // Fall through to the bounded rejection below.
        }

        throw new EventIntegrityException(
            "The database tenant context is invalid.");
    }
}

/// <summary>
/// Non-owning, tenant-bound PostgreSQL transaction scope. The scope captures the ambient tenant
/// before binding, cannot be retargeted, and never commits, rolls back or disposes its handles.
/// Callers must not use a connection or transaction concurrently.
/// </summary>
public sealed class PostgreSqlTenantTransaction
{
    private readonly PostgreSqlTenantCapture _tenantCapture;

    private PostgreSqlTenantTransaction(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        PostgreSqlTenantCapture tenantCapture)
    {
        Connection = connection;
        Transaction = transaction;
        _tenantCapture = tenantCapture;
    }

    internal NpgsqlConnection Connection { get; }

    internal NpgsqlTransaction Transaction { get; }

    public TenantId Tenant => _tenantCapture.Tenant;

    /// <summary>
    /// Binds an already-captured ambient tenant transaction-locally. Rebinding the same transaction
    /// to the same tenant is idempotent; a different existing binding is rejected.
    /// </summary>
    public static ValueTask<PostgreSqlTenantTransaction> BindAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        PostgreSqlTenantCapture tenantCapture,
        CancellationToken ct = default)
    {
        ValidateBindingArguments(
            connection,
            transaction,
            tenantCapture,
            ct);
        tenantCapture.EnsureAmbientTenant();
        return BindCoreAsync(
            connection,
            transaction,
            tenantCapture,
            ct);
    }

    internal void EnsureAmbientTenant()
    {
        _tenantCapture.EnsureAmbientTenant();
    }

    private static void ValidateBindingArguments(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        PostgreSqlTenantCapture tenantCapture,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(tenantCapture);
        ct.ThrowIfCancellationRequested();
        if (!ReferenceEquals(transaction.Connection, connection))
        {
            throw new ArgumentException(
                "The transaction does not belong to the supplied connection.",
                nameof(transaction));
        }
    }

    private static async ValueTask<PostgreSqlTenantTransaction>
        BindCoreAsync(
            NpgsqlConnection connection,
            NpgsqlTransaction transaction,
            PostgreSqlTenantCapture tenantCapture,
            CancellationToken ct)
    {
        await PostgreSqlTenantBinding.BindAsync(
                connection,
                transaction,
                tenantCapture.Tenant,
                ct)
            .ConfigureAwait(false);
        return new PostgreSqlTenantTransaction(
            connection,
            transaction,
            tenantCapture);
    }
}

/// <summary>
/// Immutable, caller-independent E20 input captured before database work begins.
/// </summary>
public sealed class PostgreSqlEventAppendRequest
{
    private readonly byte[] _payload;

    private PostgreSqlEventAppendRequest(
        Guid eventId,
        DateTimeOffset occurredAt,
        DomainEventContract contract,
        EventReference aggregateReference,
        AuditActor actor,
        string? reason,
        EventReference? correlationReference,
        byte[] payload)
    {
        EventId = eventId;
        OccurredAt = occurredAt;
        EventType = contract.EventType;
        Privilege = contract.Privilege;
        AggregateReference = aggregateReference;
        Actor = actor;
        Reason = reason;
        CorrelationReference = correlationReference;
        _payload = payload;
    }

    internal Guid EventId { get; }

    internal DateTimeOffset OccurredAt { get; }

    internal string EventType { get; }

    internal EventPrivilege Privilege { get; }

    internal EventReference AggregateReference { get; }

    internal AuditActor Actor { get; }

    internal string? Reason { get; }

    internal EventReference? CorrelationReference { get; }

    internal ReadOnlyMemory<byte> Payload => _payload;

    /// <summary>
    /// Synchronously snapshots all caller-owned event input. The returned request owns its payload
    /// bytes and can safely be used after the caller mutates its source objects or buffer.
    /// </summary>
    public static PostgreSqlEventAppendRequest Capture(
        IDomainEvent @event,
        EventAppendMetadata metadata,
        ReadOnlyMemory<byte> payload)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(metadata);

        var contract = DomainEventContracts.Resolve(@event);
        var eventId = @event.EventId;
        var occurredAt = @event.OccurredAt;
        var aggregateReference = metadata.AggregateReference;
        var actor = metadata.Actor;
        var reason = metadata.Reason;
        var correlationReference = metadata.CorrelationReference;
        var ownedPayload = payload.ToArray();

        if (eventId == Guid.Empty)
            throw new EventIntegrityException("The event ID is invalid.");

        return new PostgreSqlEventAppendRequest(
            eventId,
            EventEnvelopeCanonicalizer.NormalizeTimestamp(occurredAt),
            contract,
            aggregateReference,
            actor,
            reason,
            correlationReference,
            ownedPayload);
    }
}

/// <summary>
/// Transaction-aware E20 appender. PostgreSQL state adapters can compose authoritative state and
/// its audit event in one transaction without duplicating event serialization or integrity logic.
/// This type never commits or rolls back the caller-owned transaction.
/// </summary>
public sealed class PostgreSqlEventTransactionAppender
{
    private readonly IHashChain _chain;
    private readonly TimeProvider _clock;

    public PostgreSqlEventTransactionAppender(
        IHashChain chain,
        TimeProvider clock)
    {
        _chain = chain ?? throw new ArgumentNullException(nameof(chain));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async ValueTask<StoredEvent> AppendWithinTransactionAsync(
        PostgreSqlTenantTransaction tenantTransaction,
        PostgreSqlEventAppendRequest request,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(tenantTransaction);
        ArgumentNullException.ThrowIfNull(request);
        ct.ThrowIfCancellationRequested();
        tenantTransaction.EnsureAmbientTenant();
        var connection = tenantTransaction.Connection;
        var transaction = tenantTransaction.Transaction;
        var tenant = tenantTransaction.Tenant;

        try
        {
            await PostgreSqlTenantBinding.BindAsync(
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
                _clock.GetUtcNow());
            var prospective = new StoredEvent(
                EventEnvelopeCanonicalizer.CurrentIntegrityFormatVersion,
                position,
                request.EventId,
                request.EventType,
                request.AggregateReference,
                request.Actor,
                request.OccurredAt,
                recordedAt,
                request.Reason,
                request.CorrelationReference,
                tenant,
                request.Privilege,
                previousHash,
                string.Empty,
                request.Payload);
            var hash = _chain.ComputeNext(
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
        PostgreSqlTenantBinding.AddTenantParameter(command, tenant);
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
        PostgreSqlTenantBinding.AddTenantParameter(
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

internal static class PostgreSqlTenantBinding
{
    internal static async Task BindAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        TenantId tenant,
        CancellationToken ct)
    {
        var requestedTenant = tenant.Value.ToString("D");
        await using (var currentCommand = new NpgsqlCommand(
            """
            SELECT NULLIF(
                current_setting(
                    'control_tower.tenant_id',
                    true),
                '');
            """,
            connection,
            transaction))
        {
            var currentValue =
                await currentCommand.ExecuteScalarAsync(ct)
                    .ConfigureAwait(false);
            var currentTenant = currentValue is null or DBNull
                ? null
                : (string)currentValue;
            if (currentTenant is not null)
            {
                if (string.Equals(
                        currentTenant,
                        requestedTenant,
                        StringComparison.Ordinal))
                {
                    return;
                }

                throw new EventIntegrityException(
                    "The database tenant context is invalid.");
            }
        }

        await using var bindCommand = new NpgsqlCommand(
            """
            SELECT set_config(
                'control_tower.tenant_id',
                @tenant_context,
                true);
            """,
            connection,
            transaction);
        bindCommand.Parameters.Add(
                "tenant_context",
                NpgsqlDbType.Text)
            .Value = requestedTenant;
        _ = await bindCommand.ExecuteScalarAsync(ct).ConfigureAwait(false);
    }

    internal static void AddTenantParameter(
        NpgsqlCommand command,
        TenantId tenant)
    {
        command.Parameters.Add("tenant_id", NpgsqlDbType.Uuid).Value =
            tenant.Value;
    }
}
