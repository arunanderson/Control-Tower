using ControlTower.Platform.Events;
using ControlTower.Platform.Identity;
using ControlTower.Platform.Tenancy;
using Npgsql;
using NpgsqlTypes;

namespace ControlTower.Adapters.PostgreSql.Tests;

[Collection(PostgreSqlEventKernelCollection.Name)]
public sealed class PostgreSqlEventStoreTests(
    PostgreSqlEventKernelFixture fixture)
{
    [Fact]
    public void Migration_cycle_uses_the_pinned_ephemeral_server()
    {
        if (!fixture.Enabled)
            return;

        Assert.True(fixture.MigrationCycleVerified);
        Assert.Equal("16.14", fixture.ServerVersion);
        Assert.StartsWith(
            "ct_p1_t07_",
            fixture.DatabaseName,
            StringComparison.Ordinal);
        Assert.NotEqual(
            "control_tower_runtime",
            fixture.MigratorRole);
    }

    [Fact]
    public async Task Complete_e20_record_round_trips_and_verifies_after_reopen()
    {
        if (!fixture.Enabled)
            return;

        var tenant = NewTenant();
        var tenants = new TenantContextAccessor();
        var occurredAt = new DateTimeOffset(
                2026,
                7,
                23,
                19,
                20,
                21,
                TimeSpan.FromHours(5.5))
            .AddTicks(7);
        var recordedAt = new DateTimeOffset(
                2026,
                7,
                24,
                8,
                9,
                10,
                TimeSpan.Zero)
            .AddTicks(9);
        var providerActor =
            AuditActor.Provider("provider'; SELECT harmless; --");
        var metadata = new EventAppendMetadata(
            new EventReference("ai-asset", "asset'; SELECT literal; --"),
            providerActor,
            "Approved for a literal SQL-looking payload.",
            new EventReference("request", "corr-0001"));
        var payload = new byte[]
        {
            0x00,
            0xFF,
            0x7B,
            0x27,
            0x3B,
            0x2D,
            0x2D,
        };
        var @event = new StandardTestEvent(
            Guid.NewGuid(),
            occurredAt);
        var store = CreateStore(
            tenants,
            new FrozenTimeProvider(recordedAt));

        StoredEvent appended;
        using (tenants.BeginScope(tenant))
        {
            appended = await store.AppendAsync(
                @event,
                metadata,
                payload);
        }
        payload[0] = 0x42;

        var reopened = CreateStore(
            tenants,
            new FrozenTimeProvider(recordedAt.AddMinutes(1)));
        IReadOnlyList<StoredEvent> stream;
        using (tenants.BeginScope(tenant))
        {
            stream = await reopened.ReadAllAsync();
        }

        var stored = Assert.Single(stream);
        Assert.Equal(2, stored.IntegrityFormatVersion);
        Assert.Equal(1, stored.Position);
        Assert.Equal(@event.EventId, stored.EventId);
        Assert.Equal("tests.postgresql.standard.v1", stored.EventType);
        Assert.Equal(metadata.AggregateReference, stored.AggregateReference);
        Assert.Equal(providerActor, stored.Actor);
        Assert.Equal(
            EventEnvelopeCanonicalizer.NormalizeTimestamp(occurredAt),
            stored.OccurredAt);
        Assert.Equal(
            EventEnvelopeCanonicalizer.NormalizeTimestamp(recordedAt),
            stored.RecordedAt);
        Assert.Equal(metadata.Reason, stored.Reason);
        Assert.Equal(
            metadata.CorrelationReference,
            stored.CorrelationReference);
        Assert.Equal(tenant, stored.Tenant);
        Assert.Equal(EventPrivilege.Standard, stored.Privilege);
        Assert.Equal(Sha256HashChain.Genesis, stored.PreviousHash);
        Assert.Equal(appended.Hash, stored.Hash);
        Assert.Equal(
            new byte[]
            {
                0x00,
                0xFF,
                0x7B,
                0x27,
                0x3B,
                0x2D,
                0x2D,
            },
            stored.Payload);

        var checkpoint = EventStreamCheckpoint.From(appended);
        var verification = new HashChainVerifier(
                new Sha256HashChain())
            .Verify(tenant, stream, checkpoint);
        Assert.True(verification.IsIntact);
        Assert.True(verification.IsCheckpointBound);
    }

    [Fact]
    public async Task Every_opaque_actor_shape_round_trips()
    {
        if (!fixture.Enabled)
            return;

        var actors = new[]
        {
            AuditActor.Person(PersonKey.New()),
            AuditActor.System("control-tower.worker_01"),
            AuditActor.Provider("Microsoft-Copilot-provider"),
        };

        foreach (var actor in actors)
        {
            var tenant = NewTenant();
            var tenants = new TenantContextAccessor();
            var store = CreateStore(tenants);
            using (tenants.BeginScope(tenant))
            {
                _ = await store.AppendAsync(
                    NewStandardEvent(),
                    Metadata(actor),
                    ReadOnlyMemory<byte>.Empty);
                var stored = Assert.Single(
                    await store.ReadAllAsync());
                Assert.Equal(actor, stored.Actor);
            }
        }
    }

    [Fact]
    public async Task Concurrent_same_tenant_appends_are_contiguous_and_chained()
    {
        if (!fixture.Enabled)
            return;

        const int count = 40;
        var tenant = NewTenant();
        var tenants = new TenantContextAccessor();
        var store = CreateStore(tenants);
        var release = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var tasks = Enumerable.Range(0, count)
            .Select(async index =>
            {
                using var scope = tenants.BeginScope(tenant);
                await release.Task;
                return await store.AppendAsync(
                    NewStandardEvent(),
                    Metadata(AuditActor.System($"writer-{index}")),
                    new byte[] { (byte)index });
            })
            .ToArray();

        release.SetResult();
        var appended = await Task.WhenAll(tasks);
        Assert.Equal(
            Enumerable.Range(1, count).Select(value => (long)value),
            appended.Select(item => item.Position).Order());

        IReadOnlyList<StoredEvent> stream;
        using (tenants.BeginScope(tenant))
        {
            stream = await store.ReadAllAsync();
        }
        Assert.Equal(count, stream.Count);
        Assert.Equal(
            Enumerable.Range(1, count).Select(value => (long)value),
            stream.Select(item => item.Position));
        Assert.Equal(
            Sha256HashChain.Genesis,
            stream[0].PreviousHash);
        for (var index = 1; index < stream.Count; index++)
        {
            Assert.Equal(
                stream[index - 1].Hash,
                stream[index].PreviousHash);
        }

        var verification = new HashChainVerifier(
                new Sha256HashChain())
            .Verify(tenant, stream);
        Assert.True(verification.IsIntact);
        Assert.False(verification.IsCheckpointBound);
    }

    [Fact]
    public async Task Concurrent_tenants_have_independent_non_enumerable_streams()
    {
        if (!fixture.Enabled)
            return;

        const int count = 16;
        var tenantA = NewTenant();
        var tenantB = NewTenant();
        var tenants = new TenantContextAccessor();
        var store = CreateStore(tenants);
        var release = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        async Task<StoredEvent> Append(
            TenantId tenant,
            int index)
        {
            using var scope = tenants.BeginScope(tenant);
            await release.Task;
            return await store.AppendAsync(
                NewStandardEvent(),
                Metadata(AuditActor.System($"cross-{index}")),
                new byte[] { (byte)index });
        }

        var tasksA = Enumerable.Range(0, count)
            .Select(index => Append(tenantA, index));
        var tasksB = Enumerable.Range(count, count)
            .Select(index => Append(tenantB, index));
        var all = tasksA.Concat(tasksB).ToArray();
        release.SetResult();
        _ = await Task.WhenAll(all);

        IReadOnlyList<StoredEvent> streamA;
        IReadOnlyList<StoredEvent> streamB;
        using (tenants.BeginScope(tenantA))
            streamA = await store.ReadAllAsync();
        using (tenants.BeginScope(tenantB))
            streamB = await store.ReadAllAsync();

        Assert.Equal(count, streamA.Count);
        Assert.Equal(count, streamB.Count);
        Assert.All(streamA, item => Assert.Equal(tenantA, item.Tenant));
        Assert.All(streamB, item => Assert.Equal(tenantB, item.Tenant));
        Assert.Empty(
            streamA.Select(item => item.EventId)
                .Intersect(streamB.Select(item => item.EventId)));
        Assert.Equal(1, streamA[0].Position);
        Assert.Equal(1, streamB[0].Position);
        Assert.True(
            new HashChainVerifier(new Sha256HashChain())
                .Verify(tenantA, streamA)
                .IsIntact);
        Assert.True(
            new HashChainVerifier(new Sha256HashChain())
                .Verify(tenantB, streamB)
                .IsIntact);
    }

    [Fact]
    public async Task Global_duplicate_event_id_is_generic_and_atomic()
    {
        if (!fixture.Enabled)
            return;

        var sharedEventId = Guid.NewGuid();
        var tenantA = NewTenant();
        var tenantB = NewTenant();
        var tenants = new TenantContextAccessor();
        var store = CreateStore(tenants);
        using (tenants.BeginScope(tenantA))
        {
            _ = await store.AppendAsync(
                new StandardTestEvent(
                    sharedEventId,
                    DateTimeOffset.UtcNow),
                Metadata(AuditActor.System("tenant-a")),
                new byte[] { 0xA });
        }
        using (tenants.BeginScope(tenantB))
        {
            _ = await store.AppendAsync(
                NewStandardEvent(),
                Metadata(AuditActor.System("tenant-b")),
                new byte[] { 0xB });
        }

        EventIntegrityException exception;
        using (tenants.BeginScope(tenantB))
        {
            exception = await Assert.ThrowsAsync<EventIntegrityException>(
                () => store.AppendAsync(
                        new StandardTestEvent(
                            sharedEventId,
                            DateTimeOffset.UtcNow),
                        Metadata(AuditActor.System("collision")),
                        ReadOnlyMemory<byte>.Empty)
                    .AsTask());
        }
        Assert.Equal(
            "The event append was rejected.",
            exception.Message);
        Assert.Null(exception.InnerException);
        AssertSafeFailure(exception.Message, tenantA, tenantB);

        IReadOnlyList<StoredEvent> streamA;
        IReadOnlyList<StoredEvent> streamB;
        using (tenants.BeginScope(tenantA))
            streamA = await store.ReadAllAsync();
        using (tenants.BeginScope(tenantB))
            streamB = await store.ReadAllAsync();
        Assert.Single(streamA);
        Assert.Single(streamB);

        using (tenants.BeginScope(tenantB))
        {
            var next = await store.AppendAsync(
                NewStandardEvent(),
                Metadata(AuditActor.System("after-collision")),
                ReadOnlyMemory<byte>.Empty);
            Assert.Equal(2, next.Position);
            Assert.Equal(streamB[0].Hash, next.PreviousHash);
        }
    }

    [Fact]
    public async Task Late_database_failure_rolls_back_event_and_stream_head()
    {
        if (!fixture.Enabled)
            return;

        var tenant = NewTenant();
        var tenants = new TenantContextAccessor();
        var store = CreateStore(tenants);
        await InstallFailingHeadTriggerAsync(tenant);
        try
        {
            using var scope = tenants.BeginScope(tenant);
            var exception =
                await Assert.ThrowsAsync<EventIntegrityException>(
                    () => store.AppendAsync(
                            NewStandardEvent(),
                            Metadata(AuditActor.System("late-failure")),
                            ReadOnlyMemory<byte>.Empty)
                        .AsTask());
            Assert.Equal(
                "The event append was rejected.",
                exception.Message);
            Assert.Null(exception.InnerException);
        }
        finally
        {
            await RemoveFailingHeadTriggerAsync();
        }

        Assert.Equal(
            (0L, 0L),
            await CountTenantStateAsync(tenant));
        using (tenants.BeginScope(tenant))
        {
            var first = await store.AppendAsync(
                NewStandardEvent(),
                Metadata(AuditActor.System("after-failure")),
                ReadOnlyMemory<byte>.Empty);
            Assert.Equal(1, first.Position);
            Assert.Equal(
                Sha256HashChain.Genesis,
                first.PreviousHash);
        }
    }

    [Fact]
    public async Task Validation_and_blocked_cancellation_consume_no_position()
    {
        if (!fixture.Enabled)
            return;

        var tenant = NewTenant();
        var tenants = new TenantContextAccessor();
        var store = CreateStore(tenants);

        using (tenants.BeginScope(tenant))
        {
            _ = await Assert.ThrowsAsync<EventIntegrityException>(
                () => store.AppendAsync(
                        new StandardTestEvent(
                            Guid.Empty,
                            DateTimeOffset.UtcNow),
                        Metadata(AuditActor.System("invalid")),
                        ReadOnlyMemory<byte>.Empty)
                    .AsTask());
            _ = await Assert.ThrowsAsync<EventIntegrityException>(
                () => store.AppendAsync(
                        new UndeclaredTestEvent(
                            Guid.NewGuid(),
                            DateTimeOffset.UtcNow),
                        Metadata(AuditActor.System("undeclared")),
                        ReadOnlyMemory<byte>.Empty)
                    .AsTask());
            _ = await Assert.ThrowsAsync<ArgumentNullException>(
                () => store.AppendAsync(
                        NewStandardEvent(),
                        null!,
                        ReadOnlyMemory<byte>.Empty)
                    .AsTask());
            using var cancelled = new CancellationTokenSource();
            cancelled.Cancel();
            _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => store.AppendAsync(
                        NewStandardEvent(),
                        Metadata(AuditActor.System("pre-cancelled")),
                        ReadOnlyMemory<byte>.Empty,
                        cancelled.Token)
                    .AsTask());
        }
        Assert.Equal(
            (0L, 0L),
            await CountTenantStateAsync(tenant));

        await using var lockConnection =
            await fixture.MigrationDataSource.OpenConnectionAsync();
        await using var lockTransaction =
            await lockConnection.BeginTransactionAsync();
        await BindTenantAsync(
            lockConnection,
            lockTransaction,
            tenant);
        await using (var lockCommand = new NpgsqlCommand(
            """
            SELECT *
            FROM event_store.lock_stream_head(@tenant_id);
            """,
            lockConnection,
            lockTransaction))
        {
            lockCommand.Parameters.AddWithValue(
                "tenant_id",
                tenant.Value);
            await using var reader =
                await lockCommand.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
        }

        using var blockedCancellation =
            new CancellationTokenSource(TimeSpan.FromMilliseconds(250));
        using (tenants.BeginScope(tenant))
        {
            _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => store.AppendAsync(
                        NewStandardEvent(),
                        Metadata(AuditActor.System("blocked-cancel")),
                        ReadOnlyMemory<byte>.Empty,
                        blockedCancellation.Token)
                    .AsTask());
        }
        await lockTransaction.RollbackAsync();

        Assert.Equal(
            (0L, 0L),
            await CountTenantStateAsync(tenant));
        using (tenants.BeginScope(tenant))
        {
            var first = await store.AppendAsync(
                NewStandardEvent(),
                Metadata(AuditActor.System("after-cancel")),
                ReadOnlyMemory<byte>.Empty);
            Assert.Equal(1, first.Position);
        }
    }

    [Fact]
    public async Task Rls_and_transaction_local_context_fail_closed_without_pool_leakage()
    {
        if (!fixture.Enabled)
            return;

        var tenantA = NewTenant();
        var tenantB = NewTenant();
        var tenants = new TenantContextAccessor();
        var store = CreateStore(tenants);
        using (tenants.BeginScope(tenantA))
        {
            _ = await store.AppendAsync(
                NewStandardEvent(),
                Metadata(AuditActor.System("rls-a")),
                ReadOnlyMemory<byte>.Empty);
        }

        await using (var connection =
            await fixture.RuntimeDataSource.OpenConnectionAsync())
        {
            await using var noContext = new NpgsqlCommand(
                "SELECT count(*) FROM event_store.domain_events;",
                connection);
            Assert.Equal(
                0L,
                (long)(await noContext.ExecuteScalarAsync())!);

            await using var noContextWrite = new NpgsqlCommand(
                """
                SELECT *
                FROM event_store.lock_stream_head(@tenant_id);
                """,
                connection);
            noContextWrite.Parameters.AddWithValue(
                "tenant_id",
                tenantA.Value);
            var denied = await Assert.ThrowsAsync<PostgresException>(
                () => noContextWrite.ExecuteNonQueryAsync());
            Assert.Equal("42501", denied.SqlState);
        }

        await using (var connection =
            await fixture.RuntimeDataSource.OpenConnectionAsync())
        await using (var transaction =
            await connection.BeginTransactionAsync())
        {
            await BindTenantAsync(
                connection,
                transaction,
                tenantA);
            await using var crossTenant = new NpgsqlCommand(
                """
                SELECT count(*)
                FROM event_store.domain_events
                WHERE tenant_id = @tenant_id;
                """,
                connection,
                transaction);
            crossTenant.Parameters.AddWithValue(
                "tenant_id",
                tenantB.Value);
            Assert.Equal(
                0L,
                (long)(await crossTenant.ExecuteScalarAsync())!);

            await using var forgedAppend = new NpgsqlCommand(
                """
                SELECT *
                FROM event_store.lock_stream_head(@tenant_id);
                """,
                connection,
                transaction);
            forgedAppend.Parameters.AddWithValue(
                "tenant_id",
                tenantB.Value);
            var denied = await Assert.ThrowsAsync<PostgresException>(
                () => forgedAppend.ExecuteNonQueryAsync());
            Assert.Equal("42501", denied.SqlState);
        }

        await using (var malformedConnection =
            await fixture.RuntimeDataSource.OpenConnectionAsync())
        await using (var malformedTransaction =
            await malformedConnection.BeginTransactionAsync())
        {
            await using var malformed = new NpgsqlCommand(
                """
                SELECT set_config(
                    'control_tower.tenant_id',
                    'not-a-uuid',
                    true);
                SELECT count(*) FROM event_store.domain_events;
                """,
                malformedConnection,
                malformedTransaction);
            var invalid = await Assert.ThrowsAsync<PostgresException>(
                () => malformed.ExecuteNonQueryAsync());
            Assert.Equal("22P02", invalid.SqlState);
        }

        var poolBuilder = new NpgsqlConnectionStringBuilder(
            fixture.RuntimeConnectionString)
        {
            MaxPoolSize = 1,
            MinPoolSize = 1,
            NoResetOnClose = false,
        };
        await using var singleConnectionPool =
            NpgsqlDataSource.Create(poolBuilder.ConnectionString);
        await BindAndCompleteAsync(
            singleConnectionPool,
            tenantA,
            commit: true);
        await AssertNoTenantContextAsync(singleConnectionPool);
        await BindAndCompleteAsync(
            singleConnectionPool,
            tenantB,
            commit: false);
        await AssertNoTenantContextAsync(singleConnectionPool);
        await BindCancelAndCloseAsync(
            singleConnectionPool,
            tenantA);
        await AssertNoTenantContextAsync(singleConnectionPool);
    }

    [Fact]
    public async Task Runtime_acl_and_owner_triggers_make_events_immutable()
    {
        if (!fixture.Enabled)
            return;

        var tenant = NewTenant();
        var tenants = new TenantContextAccessor();
        var store = CreateStore(tenants);
        StoredEvent appended;
        using (tenants.BeginScope(tenant))
        {
            appended = await store.AppendAsync(
                NewStandardEvent(),
                Metadata(AuditActor.System("immutable")),
                new byte[] { 1, 2, 3 });
        }

        foreach (var statement in new[]
        {
            "UPDATE event_store.domain_events SET reason = 'changed';",
            "DELETE FROM event_store.domain_events;",
            "TRUNCATE event_store.domain_events;",
            "ALTER TABLE event_store.domain_events DISABLE TRIGGER ALL;",
        })
        {
            await using var runtime =
                await fixture.RuntimeDataSource.OpenConnectionAsync();
            await using var transaction =
                await runtime.BeginTransactionAsync();
            await BindTenantAsync(runtime, transaction, tenant);
            await using var command =
                new NpgsqlCommand(statement, runtime, transaction);
            var denied = await Assert.ThrowsAsync<PostgresException>(
                () => command.ExecuteNonQueryAsync());
            Assert.Equal("42501", denied.SqlState);
        }

        foreach (var statement in new[]
        {
            "UPDATE event_store.domain_events SET reason = 'changed';",
            "DELETE FROM event_store.domain_events;",
            "TRUNCATE event_store.domain_events;",
        })
        {
            await using var owner =
                await fixture.MigrationDataSource.OpenConnectionAsync();
            await using var transaction =
                await owner.BeginTransactionAsync();
            await BindTenantAsync(owner, transaction, tenant);
            await using var command =
                new NpgsqlCommand(statement, owner, transaction);
            var blocked = await Assert.ThrowsAsync<PostgresException>(
                () => command.ExecuteNonQueryAsync());
            Assert.Equal("55000", blocked.SqlState);
        }

        using (tenants.BeginScope(tenant))
        {
            var after = Assert.Single(
                await store.ReadAllAsync());
            Assert.Equal(appended.Hash, after.Hash);
            Assert.Equal(appended.Payload, after.Payload);
            Assert.True(
                new HashChainVerifier(new Sha256HashChain())
                    .Verify(
                        tenant,
                        new[] { after },
                        EventStreamCheckpoint.From(appended))
                    .IsIntact);
        }
    }

    [Fact]
    public async Task Database_constraints_reject_unrehydratable_event_shapes()
    {
        if (!fixture.Enabled)
            return;

        var cases = new Action<NpgsqlParameterCollection>[]
        {
            parameters =>
                parameters["integrity_format_version"].Value = 1,
            parameters =>
                parameters["event_id"].Value = Guid.Empty,
            parameters =>
            {
                parameters["actor_kind"].Value =
                    (short)AuditActorKind.Human;
                parameters["actor_opaque_id"].Value =
                    "00000000-0000-0000-0000-000000000000";
            },
            parameters =>
            {
                parameters["actor_kind"].Value =
                    (short)AuditActorKind.System;
                parameters["actor_opaque_id"].Value =
                    "admin@example.com";
            },
            parameters =>
            {
                parameters["correlation_kind"].Value = "request";
                parameters["correlation_value"].Value = DBNull.Value;
            },
            parameters =>
                parameters["privilege"].Value = (short)9,
            parameters =>
                parameters["previous_hash"].Value =
                    new string('A', Sha256HashChain.HashTextLength),
            parameters =>
                parameters["hash"].Value =
                    new string('a', Sha256HashChain.HashTextLength),
        };

        foreach (var mutate in cases)
        {
            var tenant = NewTenant();
            await using var connection =
                await fixture.MigrationDataSource.OpenConnectionAsync();
            await using var transaction =
                await connection.BeginTransactionAsync();
            await BindTenantAsync(connection, transaction, tenant);
            await using var command =
                BuildDirectInsertCommand(
                    connection,
                    transaction,
                    tenant);
            mutate(command.Parameters);

            var rejected = await Assert.ThrowsAsync<PostgresException>(
                () => command.ExecuteNonQueryAsync());
            Assert.Equal("23514", rejected.SqlState);
            Assert.Equal(
                (0L, 0L),
                await CountTenantStateAsync(tenant));
        }
    }

    private PostgreSqlEventStore CreateStore(
        ITenantContextAccessor tenants,
        TimeProvider? timeProvider = null) =>
        new(
            fixture.RuntimeDataSource,
            tenants,
            new Sha256HashChain(),
            timeProvider);

    private static StandardTestEvent NewStandardEvent() =>
        new(Guid.NewGuid(), DateTimeOffset.UtcNow);

    private static TenantId NewTenant() =>
        new(Guid.NewGuid());

    private static EventAppendMetadata Metadata(AuditActor actor) =>
        new(
            new EventReference("test-aggregate", Guid.NewGuid().ToString("D")),
            actor,
            reason: null,
            correlationReference: null);

    private static async Task BindTenantAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        TenantId tenant)
    {
        await using var bind = new NpgsqlCommand(
            """
            SELECT set_config(
                'control_tower.tenant_id',
                @tenant_id,
                true);
            """,
            connection,
            transaction);
        bind.Parameters.AddWithValue(
            "tenant_id",
            tenant.Value.ToString("D"));
        _ = await bind.ExecuteScalarAsync();
    }

    private async Task<(long Events, long Heads)>
        CountTenantStateAsync(TenantId tenant)
    {
        await using var connection =
            await fixture.MigrationDataSource.OpenConnectionAsync();
        await using var transaction =
            await connection.BeginTransactionAsync();
        await BindTenantAsync(connection, transaction, tenant);
        await using var command = new NpgsqlCommand(
            """
            SELECT
                (
                    SELECT count(*)
                    FROM event_store.domain_events
                    WHERE tenant_id = @tenant_id),
                (
                    SELECT count(*)
                    FROM event_store.stream_heads
                    WHERE tenant_id = @tenant_id);
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue(
            "tenant_id",
            tenant.Value);
        await using var reader =
            await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        return (reader.GetInt64(0), reader.GetInt64(1));
    }

    private async Task InstallFailingHeadTriggerAsync(
        TenantId tenant)
    {
        await using var connection =
            await fixture.MigrationDataSource.OpenConnectionAsync();
        var tenantLiteral = tenant.Value.ToString("D");
        await using var command = new NpgsqlCommand(
            $"""
             CREATE FUNCTION event_store.p1_t07_fail_head_update()
             RETURNS trigger
             LANGUAGE plpgsql
             AS $body$
             BEGIN
                 IF NEW.tenant_id = '{tenantLiteral}'::uuid THEN
                     RAISE EXCEPTION 'injected late failure'
                         USING ERRCODE = '55000';
                 END IF;
                 RETURN NEW;
             END;
             $body$;

             CREATE TRIGGER p1_t07_fail_head_update
             BEFORE UPDATE ON event_store.stream_heads
             FOR EACH ROW
             EXECUTE FUNCTION event_store.p1_t07_fail_head_update();
             """,
            connection);
        _ = await command.ExecuteNonQueryAsync();
    }

    private static NpgsqlCommand BuildDirectInsertCommand(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        TenantId tenant)
    {
        var command = new NpgsqlCommand(
            """
            INSERT INTO event_store.domain_events (
                integrity_format_version,
                tenant_id,
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
                payload)
            VALUES (
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
            .Value = 2;
        command.Parameters.Add("tenant_id", NpgsqlDbType.Uuid).Value =
            tenant.Value;
        command.Parameters.Add("position", NpgsqlDbType.Bigint).Value =
            1L;
        command.Parameters.Add("event_id", NpgsqlDbType.Uuid).Value =
            Guid.NewGuid();
        command.Parameters.Add("event_type", NpgsqlDbType.Varchar).Value =
            "tests.postgresql.constraint.v1";
        command.Parameters.Add(
                "aggregate_kind",
                NpgsqlDbType.Varchar)
            .Value = "test-aggregate";
        command.Parameters.Add(
                "aggregate_value",
                NpgsqlDbType.Varchar)
            .Value = Guid.NewGuid().ToString("D");
        command.Parameters.Add("actor_kind", NpgsqlDbType.Smallint).Value =
            (short)AuditActorKind.System;
        command.Parameters.Add(
                "actor_opaque_id",
                NpgsqlDbType.Varchar)
            .Value = "constraint-test";
        command.Parameters.Add(
                "occurred_at",
                NpgsqlDbType.TimestampTz)
            .Value = DateTime.UtcNow;
        command.Parameters.Add(
                "recorded_at",
                NpgsqlDbType.TimestampTz)
            .Value = DateTime.UtcNow;
        command.Parameters.Add("reason", NpgsqlDbType.Varchar).Value =
            DBNull.Value;
        command.Parameters.Add(
                "correlation_kind",
                NpgsqlDbType.Varchar)
            .Value = DBNull.Value;
        command.Parameters.Add(
                "correlation_value",
                NpgsqlDbType.Varchar)
            .Value = DBNull.Value;
        command.Parameters.Add("privilege", NpgsqlDbType.Smallint).Value =
            (short)EventPrivilege.Standard;
        command.Parameters.Add(
                "previous_hash",
                NpgsqlDbType.Varchar)
            .Value = Sha256HashChain.Genesis;
        command.Parameters.Add("hash", NpgsqlDbType.Varchar).Value =
            new string('A', Sha256HashChain.HashTextLength);
        command.Parameters.Add("payload", NpgsqlDbType.Bytea).Value =
            Array.Empty<byte>();
        return command;
    }

    private async Task RemoveFailingHeadTriggerAsync()
    {
        await using var connection =
            await fixture.MigrationDataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(
            """
            DROP TRIGGER IF EXISTS p1_t07_fail_head_update
                ON event_store.stream_heads;
            DROP FUNCTION IF EXISTS
                event_store.p1_t07_fail_head_update();
            """,
            connection);
        _ = await command.ExecuteNonQueryAsync();
    }

    private static async Task BindAndCompleteAsync(
        NpgsqlDataSource dataSource,
        TenantId tenant,
        bool commit)
    {
        await using var connection =
            await dataSource.OpenConnectionAsync();
        await using var transaction =
            await connection.BeginTransactionAsync();
        await BindTenantAsync(connection, transaction, tenant);
        if (commit)
            await transaction.CommitAsync();
        else
            await transaction.RollbackAsync();
    }

    private static async Task BindCancelAndCloseAsync(
        NpgsqlDataSource dataSource,
        TenantId tenant)
    {
        await using var connection =
            await dataSource.OpenConnectionAsync();
        await using var transaction =
            await connection.BeginTransactionAsync();
        await BindTenantAsync(connection, transaction, tenant);
        using var cancellation =
            new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        await using var sleep = new NpgsqlCommand(
            "SELECT pg_sleep(10);",
            connection,
            transaction);
        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => sleep.ExecuteNonQueryAsync(cancellation.Token));
    }

    private static async Task AssertNoTenantContextAsync(
        NpgsqlDataSource dataSource)
    {
        await using var connection =
            await dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(
            """
            SELECT event_store.current_tenant_id() IS NULL;
            """,
            connection);
        Assert.Equal(
            true,
            await command.ExecuteScalarAsync());
    }

    private static void AssertSafeFailure(
        string message,
        params TenantId[] tenants)
    {
        var forbidden = new[]
        {
            "event_store",
            "domain_events",
            "stream_heads",
            "constraint",
            "duplicate",
        }.Concat(
            tenants.Select(tenant => tenant.Value.ToString("D")));
        foreach (var value in forbidden)
        {
            Assert.False(
                message.Contains(
                    value,
                    StringComparison.OrdinalIgnoreCase));
        }
    }

    [DomainEventContract(
        "tests.postgresql.standard.v1",
        EventPrivilege.Standard)]
    private sealed record StandardTestEvent(
        Guid EventId,
        DateTimeOffset OccurredAt) : IDomainEvent;

    private sealed record UndeclaredTestEvent(
        Guid EventId,
        DateTimeOffset OccurredAt) : IDomainEvent;

    private sealed class FrozenTimeProvider(
        DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}
