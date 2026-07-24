using System.Data;
using System.Text.Json;
using ControlTower.Adapters.PostgreSql;
using ControlTower.Modules.Trust.Authorization;
using ControlTower.Platform.Audit;
using ControlTower.Platform.Events;
using ControlTower.Platform.Identity;
using ControlTower.Platform.Ports;
using ControlTower.Platform.Tenancy;
using Npgsql;
using NpgsqlTypes;

namespace ControlTower.Adapters.PostgreSql.Trust;

/// <summary>
/// Durable privileged-zone E19 map. The privileged runtime identity has only bounded point
/// functions; raw directory identity is protected before SQL and no bulk-read surface exists.
/// The supplied auditor must own an independent privileged connection pool so evidence append
/// cannot wait behind state transactions that are themselves awaiting that evidence.
/// </summary>
public sealed class PostgreSqlPersonKeyMap : IPersonKeyMap
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly ITenantContextAccessor _tenants;
    private readonly IPrivilegedReadAuditor _auditor;
    private readonly PersonKeyProtectionProfile _profile;
    private readonly PersonKeyFieldProtector _protector;
    private readonly PostgreSqlEventTransactionAppender _events;
    private readonly TimeProvider _clock;

    public PostgreSqlPersonKeyMap(
        NpgsqlDataSource privilegedDataSource,
        ITenantContextAccessor tenants,
        IPrivilegedReadAuditor auditor,
        ISecretProvider secrets,
        PersonKeyProtectionProfile profile,
        PostgreSqlEventTransactionAppender events,
        TimeProvider clock)
    {
        _dataSource = privilegedDataSource
            ?? throw new ArgumentNullException(
                nameof(privilegedDataSource));
        _tenants = tenants
            ?? throw new ArgumentNullException(nameof(tenants));
        _auditor = auditor
            ?? throw new ArgumentNullException(nameof(auditor));
        _profile = profile
            ?? throw new ArgumentNullException(nameof(profile));
        _protector = new PersonKeyFieldProtector(
            secrets
            ?? throw new ArgumentNullException(nameof(secrets)));
        _events = events
            ?? throw new ArgumentNullException(nameof(events));
        _clock = clock
            ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<PersonKey?> FindAsync(
        Guid directoryObjectId,
        PersonKeyAccessContext access,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var tenantCapture =
            PostgreSqlTenantCapture.Capture(_tenants);
        ArgumentNullException.ThrowIfNull(access);
        if (directoryObjectId != Guid.Empty)
        {
            PersonKeyMapSemantics.RejectRawIdentityContext(
                access,
                directoryObjectId,
                displaySnapshot: null);
        }
        else
        {
            await AuditAsync(
                    tenantCapture,
                    access,
                    new EventReference(
                        "person-key-map",
                        "directory-lookup"),
                    ct)
                .ConfigureAwait(false);
            return null;
        }

        var lookup = await _protector.CreateLookupAsync(
                tenantCapture,
                directoryObjectId,
                _profile,
                ct)
            .ConfigureAwait(false);
        PostgreSqlTrustDb.EnsureAmbientTenant(
            _tenants,
            tenantCapture);

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
            var entry = await FindEntryAsync(
                    connection,
                    transaction,
                    tenantCapture,
                    lookup,
                    ct)
                .ConfigureAwait(false);
            if (entry is null)
            {
                await AuditAsync(
                        tenantCapture,
                        access,
                        new EventReference(
                            "person-key-map",
                            "directory-lookup"),
                        ct)
                    .ConfigureAwait(false);
                await RebindAndCommitAsync(
                        connection,
                        transaction,
                        tenantCapture,
                        ct)
                    .ConfigureAwait(false);
                return null;
            }

            var identity = await UnprotectAndValidateAsync(
                    connection,
                    transaction,
                    tenantCapture,
                    entry,
                    access,
                    ct)
                .ConfigureAwait(false);
            if (!PersonKeyFieldProtector.SameDirectoryObject(
                    directoryObjectId,
                    identity.DirectoryObjectId))
            {
                throw InvalidProtectedIdentity();
            }

            await AuditAsync(
                    tenantCapture,
                    access,
                    EventReference.For(
                        "person-key",
                        entry.PersonKey.Value),
                    ct)
                .ConfigureAwait(false);
            await RebindAndCommitAsync(
                    connection,
                    transaction,
                    tenantCapture,
                    ct)
                .ConfigureAwait(false);
            return entry.PersonKey;
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

    public async Task<DirectoryIdentitySnapshot?> GetAsync(
        PersonKey personKey,
        PersonKeyAccessContext access,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var tenantCapture =
            PostgreSqlTenantCapture.Capture(_tenants);
        ArgumentNullException.ThrowIfNull(access);
        if (!personKey.IsValid)
        {
            await AuditAsync(
                    tenantCapture,
                    access,
                    new EventReference(
                        "person-key-map",
                        "person-lookup"),
                    ct)
                .ConfigureAwait(false);
            return null;
        }

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
            var entry = await GetEntryAsync(
                    connection,
                    transaction,
                    tenantCapture,
                    personKey,
                    ct)
                .ConfigureAwait(false);
            if (entry is not null
                && entry.PersonKey != personKey)
            {
                throw InvalidProtectedIdentity();
            }
            if (entry is null || entry.IsSevered)
            {
                await AuditAsync(
                        tenantCapture,
                        access,
                        EventReference.For(
                            "person-key",
                            personKey.Value),
                        ct)
                    .ConfigureAwait(false);
                await RebindAndCommitAsync(
                        connection,
                        transaction,
                        tenantCapture,
                        ct)
                    .ConfigureAwait(false);
                return null;
            }

            var identity = await UnprotectAndValidateAsync(
                    connection,
                    transaction,
                    tenantCapture,
                    entry,
                    access,
                    ct)
                .ConfigureAwait(false);
            await AuditAsync(
                    tenantCapture,
                    access,
                    EventReference.For(
                        "person-key",
                        personKey.Value),
                    ct)
                .ConfigureAwait(false);
            await RebindAndCommitAsync(
                    connection,
                    transaction,
                    tenantCapture,
                    ct)
                .ConfigureAwait(false);
            return identity;
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

    public async Task<PersonKeyMutationResult> GetOrCreateAsync(
        DirectoryIdentitySnapshot identity,
        PersonKeyAccessContext access,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var tenantCapture =
            PostgreSqlTenantCapture.Capture(_tenants);
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(access);
        PersonKeyMapSemantics.RejectRawIdentityContext(
            access,
            identity.DirectoryObjectId,
            identity.DisplaySnapshot);

        var lookup = await _protector.CreateLookupAsync(
                tenantCapture,
                identity.DirectoryObjectId,
                _profile,
                ct)
            .ConfigureAwait(false);
        PostgreSqlTrustDb.EnsureAmbientTenant(
            _tenants,
            tenantCapture);

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
            await LockCreationAsync(
                    connection,
                    transaction,
                    tenantCapture,
                    lookup,
                    ct)
                .ConfigureAwait(false);
            var existing = await FindEntryAsync(
                    connection,
                    transaction,
                    tenantCapture,
                    lookup,
                    ct)
                .ConfigureAwait(false);
            if (existing is not null)
            {
                var authoritative =
                    await UnprotectAndValidateAsync(
                            connection,
                            transaction,
                            tenantCapture,
                            existing,
                            access,
                            ct)
                        .ConfigureAwait(false);
                if (!PersonKeyFieldProtector
                    .SameDirectoryObject(
                        identity.DirectoryObjectId,
                        authoritative.DirectoryObjectId))
                {
                    throw InvalidProtectedIdentity();
                }

                await AuditAsync(
                        tenantCapture,
                        access,
                        EventReference.For(
                            "person-key",
                            existing.PersonKey.Value),
                        ct)
                    .ConfigureAwait(false);
                await RebindAndCommitAsync(
                        connection,
                        transaction,
                        tenantCapture,
                        ct)
                    .ConfigureAwait(false);
                return new(
                    PersonKeyMutationStatus.Existing,
                    existing.PersonKey,
                    existing.Version);
            }

            await AuditAsync(
                    tenantCapture,
                    access,
                    new EventReference(
                        "person-key-map",
                        "directory-lookup"),
                    ct)
                .ConfigureAwait(false);
            var personKey = PersonKey.New();
            var changed = PersonKeyMapSemantics.Created(
                personKey,
                Now());
            var eventRequest = CaptureEvent(
                changed,
                PersonKeyMapSemantics.Metadata(
                    personKey,
                    access));
            var protectedIdentity =
                await _protector.ProtectAsync(
                        tenantCapture,
                        personKey,
                        identity,
                        _profile,
                        lookup,
                        ct)
                    .ConfigureAwait(false);
            PostgreSqlTrustDb.EnsureAmbientTenant(
                _tenants,
                tenantCapture);
            _ = await PostgreSqlTrustDb.BindAsync(
                    connection,
                    transaction,
                    tenantCapture,
                    ct)
                .ConfigureAwait(false);
            var inserted = await InsertAsync(
                    connection,
                    transaction,
                    tenantCapture,
                    personKey,
                    protectedIdentity,
                    lookup.IndexKeyCommitment,
                    changed.EventId,
                    ct)
                .ConfigureAwait(false);
            if (!inserted)
                throw Rejected();

            var bound = await PostgreSqlTrustDb.BindAsync(
                    connection,
                    transaction,
                    tenantCapture,
                    ct)
                .ConfigureAwait(false);
            _ = await _events.AppendWithinTransactionAsync(
                    bound,
                    eventRequest,
                    ct)
                .ConfigureAwait(false);
            await PostgreSqlTrustDb.CommitAsync(
                    transaction,
                    _tenants,
                    tenantCapture,
                    ct)
                .ConfigureAwait(false);
                return new(
                    PersonKeyMutationStatus.Created,
                    personKey,
                    Version: 1);
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

    public async Task<PersonKeySeverResult> SeverAsync(
        PersonKey personKey,
        long expectedVersion,
        PersonKeyAccessContext access,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var tenantCapture =
            PostgreSqlTenantCapture.Capture(_tenants);
        if (!personKey.IsValid)
            throw new ArgumentException(
                "A non-empty PersonKey is required.",
                nameof(personKey));
        if (expectedVersion <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(expectedVersion),
                "A positive expected version is required.");
        ArgumentNullException.ThrowIfNull(access);

        var changed = PersonKeyMapSemantics.Severed(
            personKey,
            Now());
        var eventRequest = CaptureEvent(
            changed,
            PersonKeyMapSemantics.Metadata(
                personKey,
                access));
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
            var entry = await GetEntryAsync(
                    connection,
                    transaction,
                    tenantCapture,
                    personKey,
                    ct)
                .ConfigureAwait(false);
            if (entry is not null
                && entry.PersonKey != personKey)
            {
                throw InvalidProtectedIdentity();
            }
            if (entry is null)
            {
                await AuditAsync(
                        tenantCapture,
                        access,
                        new EventReference(
                            "person-key-map",
                            "sever"),
                        ct)
                    .ConfigureAwait(false);
                await RebindAndCommitAsync(
                        connection,
                        transaction,
                        tenantCapture,
                        ct)
                    .ConfigureAwait(false);
                return new(
                    PersonKeySeverStatus.NotFound,
                    null,
                    null);
            }
            if (entry.IsSevered)
            {
                await AuditAsync(
                        tenantCapture,
                        access,
                        EventReference.For(
                            "person-key",
                            personKey.Value),
                        ct)
                    .ConfigureAwait(false);
                await RebindAndCommitAsync(
                        connection,
                        transaction,
                        tenantCapture,
                        ct)
                    .ConfigureAwait(false);
                return new(
                    PersonKeySeverStatus.AlreadySevered,
                    personKey,
                    entry.Version);
            }

            _ = await UnprotectAndValidateAsync(
                    connection,
                    transaction,
                    tenantCapture,
                    entry,
                    access,
                    ct)
                .ConfigureAwait(false);
            await AuditAsync(
                    tenantCapture,
                    access,
                    EventReference.For(
                        "person-key",
                        personKey.Value),
                    ct)
                .ConfigureAwait(false);
            if (entry.Version != expectedVersion)
            {
                await RebindAndCommitAsync(
                        connection,
                        transaction,
                        tenantCapture,
                        ct)
                    .ConfigureAwait(false);
                return new(
                    PersonKeySeverStatus.Conflict,
                    personKey,
                    entry.Version);
            }

            _ = await PostgreSqlTrustDb.BindAsync(
                    connection,
                    transaction,
                    tenantCapture,
                    ct)
                .ConfigureAwait(false);
            var severed = await SeverRowAsync(
                    connection,
                    transaction,
                    tenantCapture,
                    personKey,
                    expectedVersion,
                    changed.EventId,
                    ct)
                .ConfigureAwait(false);
            if (!severed)
                throw Rejected();

            var bound = await PostgreSqlTrustDb.BindAsync(
                    connection,
                    transaction,
                    tenantCapture,
                    ct)
                .ConfigureAwait(false);
            _ = await _events.AppendWithinTransactionAsync(
                    bound,
                    eventRequest,
                    ct)
                .ConfigureAwait(false);
            await PostgreSqlTrustDb.CommitAsync(
                    transaction,
                    _tenants,
                    tenantCapture,
                    ct)
                .ConfigureAwait(false);
            return new(
                PersonKeySeverStatus.Severed,
                personKey,
                Version: 2);
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

    private async Task<DirectoryIdentitySnapshot>
        UnprotectAndValidateAsync(
            NpgsqlConnection connection,
            NpgsqlTransaction transaction,
            PostgreSqlTenantCapture tenantCapture,
            PersonKeyEntry entry,
            PersonKeyAccessContext access,
            CancellationToken ct)
    {
        var protectedIdentity = entry.ProtectedIdentity
            ?? throw InvalidProtectedIdentity();
        var identity = await _protector.UnprotectAsync(
                tenantCapture,
                entry.PersonKey,
                protectedIdentity,
                ct)
            .ConfigureAwait(false);
        _ = await PostgreSqlTrustDb.BindAsync(
                connection,
                transaction,
                tenantCapture,
                ct)
            .ConfigureAwait(false);
        PersonKeyMapSemantics.RejectRawIdentityContext(
            access,
            identity.DirectoryObjectId,
            identity.DisplaySnapshot);
        return identity;
    }

    private async ValueTask AuditAsync(
        PostgreSqlTenantCapture tenantCapture,
        PersonKeyAccessContext access,
        EventReference resource,
        CancellationToken ct)
    {
        PostgreSqlTrustDb.EnsureAmbientTenant(
            _tenants,
            tenantCapture);
        var record = PersonKeyMapSemantics.ReadRecord(
            tenantCapture.Tenant,
            access,
            resource,
            Now());
        try
        {
            await _auditor.RecordAsync(record, ct)
                .ConfigureAwait(false);
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
        PostgreSqlTrustDb.EnsureAmbientTenant(
            _tenants,
            tenantCapture);
    }

    private async Task RebindAndCommitAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        PostgreSqlTenantCapture capture,
        CancellationToken ct)
    {
        _ = await PostgreSqlTrustDb.BindAsync(
                connection,
                transaction,
                capture,
                ct)
            .ConfigureAwait(false);
        await PostgreSqlTrustDb.CommitAsync(
                transaction,
                _tenants,
                capture,
                ct)
            .ConfigureAwait(false);
    }

    private static async Task LockCreationAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        PostgreSqlTenantCapture capture,
        PersonKeyLookup lookup,
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
            SELECT trust_store.lock_person_key_creation(
                @tenant_id,
                @index_reference,
                @index_key_commitment);
            """,
            connection,
            transaction);
        PostgreSqlTrustDb.AddTenant(command, capture.Tenant);
        command.Parameters.Add(
                "index_reference",
                NpgsqlDbType.Varchar)
            .Value = lookup.IndexReference;
        command.Parameters.Add(
                "index_key_commitment",
                NpgsqlDbType.Bytea)
            .Value = lookup.IndexKeyCommitment;
        _ = await command.ExecuteScalarAsync(ct)
            .ConfigureAwait(false);
    }

    private static async Task<PersonKeyEntry?> FindEntryAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        PostgreSqlTenantCapture capture,
        PersonKeyLookup lookup,
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
            FROM trust_store.find_person_key(
                @tenant_id,
                @index_reference,
                @index_key_commitment,
                @blind_index);
            """,
            connection,
            transaction);
        PostgreSqlTrustDb.AddTenant(command, capture.Tenant);
        command.Parameters.Add(
                "index_reference",
                NpgsqlDbType.Varchar)
            .Value = lookup.IndexReference;
        command.Parameters.Add(
                "index_key_commitment",
                NpgsqlDbType.Bytea)
            .Value = lookup.IndexKeyCommitment;
        command.Parameters.Add(
                "blind_index",
                NpgsqlDbType.Bytea)
            .Value = lookup.BlindIndex;
        return await ReadSingleEntryAsync(
                command,
                capture.Tenant,
                ct)
            .ConfigureAwait(false);
    }

    private static async Task<PersonKeyEntry?> GetEntryAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        PostgreSqlTenantCapture capture,
        PersonKey personKey,
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
            FROM trust_store.read_person_key(
                @tenant_id,
                @person_key);
            """,
            connection,
            transaction);
        PostgreSqlTrustDb.AddTenant(command, capture.Tenant);
        command.Parameters.Add(
                "person_key",
                NpgsqlDbType.Uuid)
            .Value = personKey.Value;
        return await ReadSingleEntryAsync(
                command,
                capture.Tenant,
                ct)
            .ConfigureAwait(false);
    }

    private static async Task<PersonKeyEntry?>
        ReadSingleEntryAsync(
            NpgsqlCommand command,
            TenantId tenant,
            CancellationToken ct)
    {
        await using var reader =
            await command.ExecuteReaderAsync(ct)
                .ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false))
            return null;

        var entry = ReadEntry(reader, tenant);
        if (await reader.ReadAsync(ct).ConfigureAwait(false))
            throw InvalidProtectedIdentity();
        return entry;
    }

    private static PersonKeyEntry ReadEntry(
        NpgsqlDataReader reader,
        TenantId expectedTenant)
    {
        try
        {
            var personKey =
                new PersonKey(reader.GetGuid(0));
            var tenant =
                new TenantId(reader.GetGuid(1));
            var version = reader.GetInt64(2);
            var isSevered = reader.GetBoolean(3);
            if (tenant != expectedTenant)
                throw InvalidProtectedIdentity();

            if (isSevered)
            {
                if (version != 2
                    || Enumerable.Range(4, 7)
                        .Any(ordinal =>
                            !reader.IsDBNull(ordinal)))
                {
                    throw InvalidProtectedIdentity();
                }

                return new PersonKeyEntry(
                    personKey,
                    version,
                    isSevered: true,
                    protectedIdentity: null);
            }

            if (version != 1
                || Enumerable.Range(4, 7)
                    .Any(reader.IsDBNull))
            {
                throw InvalidProtectedIdentity();
            }

            return new PersonKeyEntry(
                personKey,
                version,
                isSevered: false,
                new ProtectedPersonIdentity(
                    reader.GetInt16(4),
                    reader.GetString(5),
                    reader.GetString(6),
                    reader.GetFieldValue<byte[]>(7),
                    reader.GetFieldValue<byte[]>(8),
                    reader.GetFieldValue<byte[]>(9),
                    reader.GetFieldValue<byte[]>(10)));
        }
        catch (PostgreSqlTrustException)
        {
            throw;
        }
        catch (Exception exception)
            when (exception is ArgumentException
                or FormatException
                or InvalidCastException
                or OverflowException)
        {
            throw InvalidProtectedIdentity();
        }
    }

    private static async Task<bool> InsertAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        PostgreSqlTenantCapture capture,
        PersonKey personKey,
        ProtectedPersonIdentity protectedIdentity,
        byte[] indexKeyCommitment,
        Guid eventId,
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
            SELECT trust_store.insert_person_key(
                @tenant_id,
                @person_key,
                @protection_format,
                @encryption_reference,
                @index_reference,
                @index_key_commitment,
                @blind_index,
                @ciphertext,
                @nonce,
                @tag,
                @event_id);
            """,
            connection,
            transaction);
        PostgreSqlTrustDb.AddTenant(command, capture.Tenant);
        command.Parameters.Add(
                "person_key",
                NpgsqlDbType.Uuid)
            .Value = personKey.Value;
        command.Parameters.Add(
                "protection_format",
                NpgsqlDbType.Smallint)
            .Value = protectedIdentity.Format;
        command.Parameters.Add(
                "encryption_reference",
                NpgsqlDbType.Varchar)
            .Value = protectedIdentity.EncryptionReference;
        command.Parameters.Add(
                "index_reference",
                NpgsqlDbType.Varchar)
            .Value = protectedIdentity.IndexReference;
        command.Parameters.Add(
                "index_key_commitment",
                NpgsqlDbType.Bytea)
            .Value = indexKeyCommitment;
        command.Parameters.Add(
                "blind_index",
                NpgsqlDbType.Bytea)
            .Value = protectedIdentity.BlindIndex;
        command.Parameters.Add(
                "ciphertext",
                NpgsqlDbType.Bytea)
            .Value = protectedIdentity.Ciphertext;
        command.Parameters.Add(
                "nonce",
                NpgsqlDbType.Bytea)
            .Value = protectedIdentity.Nonce;
        command.Parameters.Add(
                "tag",
                NpgsqlDbType.Bytea)
            .Value = protectedIdentity.Tag;
        command.Parameters.Add(
                "event_id",
                NpgsqlDbType.Uuid)
            .Value = eventId;
        return Equals(
            await command.ExecuteScalarAsync(ct)
                .ConfigureAwait(false),
            true);
    }

    private static async Task<bool> SeverRowAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        PostgreSqlTenantCapture capture,
        PersonKey personKey,
        long expectedVersion,
        Guid eventId,
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
            SELECT trust_store.sever_person_key(
                @tenant_id,
                @person_key,
                @expected_version,
                @event_id);
            """,
            connection,
            transaction);
        PostgreSqlTrustDb.AddTenant(command, capture.Tenant);
        command.Parameters.Add(
                "person_key",
                NpgsqlDbType.Uuid)
            .Value = personKey.Value;
        command.Parameters.Add(
                "expected_version",
                NpgsqlDbType.Bigint)
            .Value = expectedVersion;
        command.Parameters.Add(
                "event_id",
                NpgsqlDbType.Uuid)
            .Value = eventId;
        return Equals(
            await command.ExecuteScalarAsync(ct)
                .ConfigureAwait(false),
            true);
    }

    private PostgreSqlEventAppendRequest CaptureEvent(
        PersonKeyMapChanged changed,
        EventAppendMetadata metadata) =>
        PostgreSqlEventAppendRequest.Capture(
            changed,
            metadata,
            JsonSerializer.SerializeToUtf8Bytes(changed));

    private DateTimeOffset Now() =>
        EventEnvelopeCanonicalizer.NormalizeTimestamp(
            _clock.GetUtcNow());

    private static PostgreSqlTrustException Rejected() =>
        new(PostgreSqlTrustDb.PersonKeyRejected);

    private static PostgreSqlTrustException
        InvalidProtectedIdentity() =>
        new(PostgreSqlTrustDb.ProtectedIdentityInvalid);

    private sealed class PersonKeyEntry(
        PersonKey personKey,
        long version,
        bool isSevered,
        ProtectedPersonIdentity? protectedIdentity)
    {
        internal PersonKey PersonKey { get; } = personKey;
        internal long Version { get; } = version;
        internal bool IsSevered { get; } = isSevered;
        internal ProtectedPersonIdentity? ProtectedIdentity { get; } =
            protectedIdentity;
    }
}
