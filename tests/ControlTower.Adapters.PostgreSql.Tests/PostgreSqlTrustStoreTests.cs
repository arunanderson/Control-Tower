using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ControlTower.Adapters.PostgreSql;
using ControlTower.Adapters.PostgreSql.Trust;
using ControlTower.Modules.Audit;
using ControlTower.Modules.Trust.Authorization;
using ControlTower.Platform.Audit;
using ControlTower.Platform.Events;
using ControlTower.Platform.Identity;
using ControlTower.Platform.Tenancy;
using Npgsql;
using NpgsqlTypes;

namespace ControlTower.Adapters.PostgreSql.Tests;

[Collection(PostgreSqlTrustStoreCollection.Name)]
public sealed class PostgreSqlTrustStoreTests(
    PostgreSqlTrustStoreFixture fixture)
{
    [Fact]
    public async Task Migration_cycle_and_runtime_identity_separation_are_exact()
    {
        if (!fixture.Enabled)
            return;

        Assert.True(fixture.BaselineRestored);
        Assert.True(fixture.MigrationCycleVerified);
        Assert.Equal("16.14", fixture.ServerVersion);
        Assert.StartsWith(
            "ct_p1_t08_",
            fixture.DatabaseName,
            StringComparison.Ordinal);

        await AssertPermissionDeniedAsync(
            fixture.NormalDataSource,
            "SELECT * FROM trust_store.person_key_map;");
        await AssertPermissionDeniedAsync(
            fixture.NormalDataSource,
            """
            SELECT *
            FROM trust_store.find_person_key(
                '11111111-1111-1111-1111-111111111111'::uuid,
                'idx-v1',
                decode(repeat('00', 32), 'hex'),
                decode(repeat('00', 32), 'hex'));
            """);
        await AssertPermissionDeniedAsync(
            fixture.PrivilegedDataSource,
            "SELECT * FROM trust_store.person_key_map;");
        await AssertPermissionDeniedAsync(
            fixture.PrivilegedDataSource,
            """
            SELECT *
            FROM trust_store.list_role_assignments(
                '11111111-1111-1111-1111-111111111111'::uuid,
                '22222222-2222-2222-2222-222222222222'::uuid);
            """);
        await AssertPermissionDeniedAsync(
            fixture.PrivilegedDataSource,
            "SELECT * FROM event_store.domain_events;");

        var tenant = NewTenant();
        await using var connection =
            await fixture.NormalDataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(
            """
            SELECT *
            FROM trust_store.read_role_assignment(
                @tenant_id,
                @assignment_id);
            """,
            connection);
        command.Parameters.Add(
                "tenant_id",
                NpgsqlDbType.Uuid)
            .Value = tenant.Value;
        command.Parameters.Add(
                "assignment_id",
                NpgsqlDbType.Uuid)
            .Value = Guid.NewGuid();
        var missingTenant = await Assert.ThrowsAsync<
            PostgresException>(
            () => command.ExecuteNonQueryAsync());
        Assert.Equal(
            PostgresErrorCodes.InsufficientPrivilege,
            missingTenant.SqlState);
    }

    [Fact]
    public async Task Person_key_lifecycle_is_protected_audited_and_severed_in_constant_time()
    {
        if (!fixture.Enabled)
            return;

        var tenant = NewTenant();
        var harness = new PostgreSqlTrustTestHarness(fixture);
        var profile = Profile();
        var protectionKeys =
            harness.Secrets.AddProfile(tenant, profile);
        var map = harness.CreatePersonKeyMap(profile);
        var directoryId = Guid.NewGuid();
        const string display = "Ada Lovelace — 研究";
        var createAccess =
            PostgreSqlTrustTestHarness.Access(
                "create protected mapping");

        PersonKeyMutationResult created;
        using (harness.Tenants.BeginScope(tenant))
        {
            created = await map.GetOrCreateAsync(
                new DirectoryIdentitySnapshot(
                    directoryId,
                    display),
                createAccess);
        }

        Assert.Equal(
            PersonKeyMutationStatus.Created,
            created.Status);
        Assert.True(created.PersonKey.IsValid);
        Assert.Equal(1, created.Version);

        var initialEvents =
            await harness.ReadEventsAsync(tenant);
        Assert.Equal(
            [
                "PrivilegedReadRecorded",
                "PersonKeyMapChanged",
            ],
            initialEvents.Select(
                stored => stored.EventType));
        Assert.All(
            initialEvents,
            stored =>
            {
                Assert.Equal(
                    EventPrivilege.Privileged,
                    stored.Privilege);
                Assert.Equal(
                    createAccess.CorrelationReference,
                    stored.CorrelationReference);
            });

        var activeRow =
            await ReadPersonRowAsync(
                tenant,
                created.PersonKey);
        Assert.NotNull(activeRow);
        Assert.Equal(1, activeRow!.Version);
        Assert.False(activeRow.IsSevered);
        Assert.Equal("aes-v1", activeRow.EncryptionReference);
        Assert.Equal("idx-v1", activeRow.IndexReference);
        Assert.Equal(32, activeRow.BlindIndex!.Length);
        Assert.NotEmpty(activeRow.Ciphertext!);
        Assert.Equal(12, activeRow.Nonce!.Length);
        Assert.Equal(16, activeRow.Tag!.Length);

        PersonKeyMutationResult existing;
        DirectoryIdentitySnapshot? roundTrip;
        PersonKey? found;
        PersonKeySeverResult conflict;
        PersonKeySeverResult severed;
        DirectoryIdentitySnapshot? afterSever;
        PersonKeySeverResult repeated;
        PersonKeyMutationResult remapped;
        using (harness.Tenants.BeginScope(tenant))
        {
            existing = await map.GetOrCreateAsync(
                new DirectoryIdentitySnapshot(
                    directoryId,
                    "Untrusted replacement"),
                PostgreSqlTrustTestHarness.Access(
                    "reuse protected mapping"));
            roundTrip = await map.GetAsync(
                created.PersonKey,
                PostgreSqlTrustTestHarness.Access(
                    "read protected mapping"));
            found = await map.FindAsync(
                directoryId,
                PostgreSqlTrustTestHarness.Access(
                    "find protected mapping"));
            conflict = await map.SeverAsync(
                created.PersonKey,
                expectedVersion: 2,
                PostgreSqlTrustTestHarness.Access(
                    "reject stale sever"));
            severed = await map.SeverAsync(
                created.PersonKey,
                expectedVersion: 1,
                PostgreSqlTrustTestHarness.Access(
                    "sever protected mapping"));
            afterSever = await map.GetAsync(
                created.PersonKey,
                PostgreSqlTrustTestHarness.Access(
                    "verify severed mapping"));
            repeated = await map.SeverAsync(
                created.PersonKey,
                expectedVersion: 1,
                PostgreSqlTrustTestHarness.Access(
                    "repeat sever"));
            remapped = await map.GetOrCreateAsync(
                new DirectoryIdentitySnapshot(
                    directoryId,
                    display),
                PostgreSqlTrustTestHarness.Access(
                    "remap severed identity"));
        }

        Assert.Equal(
            PersonKeyMutationStatus.Existing,
            existing.Status);
        Assert.Equal(created.PersonKey, existing.PersonKey);
        Assert.NotNull(roundTrip);
        Assert.Equal(directoryId, roundTrip!.DirectoryObjectId);
        Assert.Equal(display, roundTrip.DisplaySnapshot);
        Assert.Equal(created.PersonKey, found);
        Assert.Equal(PersonKeySeverStatus.Conflict, conflict.Status);
        Assert.Equal(1, conflict.Version);
        Assert.Equal(PersonKeySeverStatus.Severed, severed.Status);
        Assert.Equal(2, severed.Version);
        Assert.Null(afterSever);
        Assert.Equal(
            PersonKeySeverStatus.AlreadySevered,
            repeated.Status);
        Assert.Equal(2, repeated.Version);
        Assert.Equal(
            PersonKeyMutationStatus.Created,
            remapped.Status);
        Assert.NotEqual(created.PersonKey, remapped.PersonKey);

        var tombstone =
            await ReadPersonRowAsync(
                tenant,
                created.PersonKey);
        Assert.NotNull(tombstone);
        Assert.True(tombstone!.IsSevered);
        Assert.Equal(2, tombstone.Version);
        Assert.Null(tombstone.EncryptionReference);
        Assert.Null(tombstone.IndexReference);
        Assert.Null(tombstone.BlindIndex);
        Assert.Null(tombstone.Ciphertext);
        Assert.Null(tombstone.Nonce);
        Assert.Null(tombstone.Tag);

        var allEvents = await harness.ReadEventsAsync(tenant);
        Assert.Equal(
            3,
            allEvents.Count(stored =>
                stored.EventType == "PersonKeyMapChanged"));
        Assert.Equal(9, harness.Sink.Records.Count);
        var projectedEvidence =
            await harness.Projection.ListAsync();
        Assert.Equal(9, projectedEvidence.Count);
        var remappedRow =
            await ReadPersonRowAsync(
                tenant,
                remapped.PersonKey);
        Assert.NotNull(remappedRow);
        AssertSensitiveMaterialAbsent(
            allEvents,
            harness.Sink.Records,
            projectedEvidence,
            [activeRow!, tombstone!, remappedRow!],
            directoryId,
            display,
            protectionKeys.EncryptionKey,
            protectionKeys.IndexKey);
    }

    [Fact]
    public async Task Concurrent_person_key_create_and_sever_have_one_state_transition_each()
    {
        if (!fixture.Enabled)
            return;

        var tenant = NewTenant();
        var harness = new PostgreSqlTrustTestHarness(fixture);
        var profile = Profile();
        _ = harness.Secrets.AddProfile(tenant, profile);
        var map = harness.CreatePersonKeyMap(profile);
        var directoryId = Guid.NewGuid();

        PersonKeyMutationResult[] created;
        using (harness.Tenants.BeginScope(tenant))
        {
            created = await Task.WhenAll(
                Enumerable.Range(0, 16)
                    .Select(index =>
                        map.GetOrCreateAsync(
                            new DirectoryIdentitySnapshot(
                                directoryId,
                                "Concurrent Person"),
                            PostgreSqlTrustTestHarness.Access(
                                $"concurrent create {index}"))));
        }

        Assert.Single(
            created,
            result =>
                result.Status
                == PersonKeyMutationStatus.Created);
        Assert.Equal(
            15,
            created.Count(result =>
                result.Status
                == PersonKeyMutationStatus.Existing));
        var personKey = Assert.Single(
            created.Select(result => result.PersonKey)
                .Distinct());

        PersonKeySeverResult[] severed;
        using (harness.Tenants.BeginScope(tenant))
        {
            severed = await Task.WhenAll(
                Enumerable.Range(0, 16)
                    .Select(index =>
                        map.SeverAsync(
                            personKey,
                            expectedVersion: 1,
                            PostgreSqlTrustTestHarness.Access(
                                $"concurrent sever {index}"))));
        }

        Assert.Single(
            severed,
            result =>
                result.Status
                == PersonKeySeverStatus.Severed);
        Assert.Equal(
            15,
            severed.Count(result =>
                result.Status
                == PersonKeySeverStatus.AlreadySevered));
        var events = await harness.ReadEventsAsync(tenant);
        Assert.Equal(
            2,
            events.Count(stored =>
                stored.EventType == "PersonKeyMapChanged"));
        Assert.Equal(
            32,
            events.Count(stored =>
                stored.EventType == "PrivilegedReadRecorded"));
        Assert.Equal(32, harness.Sink.Records.Count);
    }

    [Fact]
    public async Task Role_assignment_history_is_atomic_concurrent_and_never_transfers_after_remap()
    {
        if (!fixture.Enabled)
            return;

        var tenant = NewTenant();
        var harness = new PostgreSqlTrustTestHarness(fixture);
        var profile = Profile();
        _ = harness.Secrets.AddProfile(tenant, profile);
        var personKeys = harness.CreatePersonKeyMap(profile);
        var store = harness.CreateRoleStore();
        var service = new RoleAssignmentService(
            personKeys,
            store,
            harness.Tenants,
            harness.Clock);
        var directoryId = Guid.NewGuid();
        var assigner = AuditActor.System("p1-t08-assigner");
        var revoker = AuditActor.System("p1-t08-revoker");

        Guid[] assignedIds;
        using (harness.Tenants.BeginScope(tenant))
        {
            assignedIds = await Task.WhenAll(
                Enumerable.Range(0, 16)
                    .Select(_ => service.AssignAsync(
                        directoryId,
                        ControlTowerRole.Administrator,
                        assigner)));
        }
        var firstAssignmentId =
            Assert.Single(assignedIds.Distinct());

        using (harness.Tenants.BeginScope(tenant))
        {
            await Task.WhenAll(
                Enumerable.Range(0, 16)
                    .Select(_ => service.RevokeAsync(
                        firstAssignmentId,
                        revoker)));
        }

        Guid secondAdministratorId;
        using (harness.Tenants.BeginScope(tenant))
        {
            secondAdministratorId =
                await service.AssignAsync(
                    directoryId,
                    ControlTowerRole.Administrator,
                    assigner);
            _ = await Task.WhenAll(
                new[]
                {
                    ControlTowerRole.Viewer,
                    ControlTowerRole.Operator,
                    ControlTowerRole.ExecutiveScope,
                }.Select(role => service.AssignAsync(
                    directoryId,
                    role,
                    assigner)));
        }
        Assert.NotEqual(
            firstAssignmentId,
            secondAdministratorId);

        PersonKey oldPersonKey;
        IReadOnlyList<RoleAssignment> history;
        using (harness.Tenants.BeginScope(tenant))
        {
            oldPersonKey = Assert.NotNull(
                await personKeys.FindAsync(
                    directoryId,
                    PostgreSqlTrustTestHarness.Access(
                        "resolve assigned person")));
            history =
                await store.ListForSubjectAsync(oldPersonKey);
        }

        Assert.Equal(5, history.Count);
        Assert.Single(
            history,
            assignment =>
                assignment.Id == firstAssignmentId
                && !assignment.IsActive
                && assignment.Version == 2);
        Assert.Equal(
            Enum.GetValues<ControlTowerRole>(),
            history.Where(assignment => assignment.IsActive)
                .Select(assignment => assignment.Role)
                .Order()
                .ToArray());

        PersonKeyMutationResult remapped;
        using (harness.Tenants.BeginScope(tenant))
        {
            var severed = await personKeys.SeverAsync(
                oldPersonKey,
                expectedVersion: 1,
                PostgreSqlTrustTestHarness.Access(
                    "sever assigned person"));
            Assert.Equal(
                PersonKeySeverStatus.Severed,
                severed.Status);
            remapped = await personKeys.GetOrCreateAsync(
                new DirectoryIdentitySnapshot(directoryId),
                PostgreSqlTrustTestHarness.Access(
                    "remap assigned person"));
            Assert.Empty(
                await store.ListForSubjectAsync(
                    remapped.PersonKey));
            Assert.Equal(
                5,
                (await store.ListForSubjectAsync(
                    oldPersonKey)).Count);
        }

        Assert.NotEqual(oldPersonKey, remapped.PersonKey);
        var roleEvents =
            (await harness.ReadEventsAsync(tenant))
                .Where(stored =>
                    stored.EventType
                    == "RoleAssignmentChanged")
                .ToArray();
        Assert.Equal(6, roleEvents.Length);
        Assert.Equal(
            [1L, 2L],
            roleEvents
                .Where(stored =>
                    stored.AggregateReference.Value
                    == firstAssignmentId.ToString("D"))
                .Select(stored =>
                    JsonSerializer.Deserialize<
                        RoleAssignmentChanged>(
                        stored.Payload)!.Version)
                .Order()
                .ToArray());

        var foreignTenant = NewTenant();
        using (harness.Tenants.BeginScope(foreignTenant))
        {
            Assert.Null(
                await store.GetAsync(firstAssignmentId));
            Assert.Empty(
                await store.ListForSubjectAsync(
                    oldPersonKey));
            Assert.Null(
                await personKeys.GetAsync(
                    oldPersonKey,
                    PostgreSqlTrustTestHarness.Access(
                        "foreign person lookup")));
        }
    }

    [Fact]
    public async Task Audit_and_event_failures_never_release_or_partially_commit_state()
    {
        if (!fixture.Enabled)
            return;

        var auditFailureTenant = NewTenant();
        var auditHarness =
            new PostgreSqlTrustTestHarness(fixture);
        var auditProfile = Profile();
        _ = auditHarness.Secrets.AddProfile(
            auditFailureTenant,
            auditProfile);
        var auditFailureMap =
            auditHarness.CreatePersonKeyMap(
                auditProfile,
                new FailingPrivilegedReadAuditor());

        using (auditHarness.Tenants.BeginScope(
            auditFailureTenant))
        {
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => auditFailureMap.GetOrCreateAsync(
                    new DirectoryIdentitySnapshot(
                        Guid.NewGuid()),
                    PostgreSqlTrustTestHarness.Access(
                        "prove audit failure")));
        }
        Assert.Equal(
            0,
            await CountPersonRowsAsync(
                auditFailureTenant));
        Assert.Empty(
            await auditHarness.ReadEventsAsync(
                auditFailureTenant));

        var blockedTenant = NewTenant();
        var blockedHarness =
            new PostgreSqlTrustTestHarness(fixture);
        var blockedProfile = Profile();
        _ = blockedHarness.Secrets.AddProfile(
            blockedTenant,
            blockedProfile);
        var blockingAuditor =
            new BlockingPrivilegedReadAuditor();
        var blockedMap =
            blockedHarness.CreatePersonKeyMap(
                blockedProfile,
                blockingAuditor);
        Task<PersonKeyMutationResult> pending;
        using (blockedHarness.Tenants.BeginScope(blockedTenant))
        {
            pending = blockedMap.GetOrCreateAsync(
                new DirectoryIdentitySnapshot(
                    Guid.NewGuid()),
                PostgreSqlTrustTestHarness.Access(
                    "prove audit ordering"));
            await blockingAuditor.Entered.WaitAsync(
                TimeSpan.FromSeconds(10));
            Assert.False(pending.IsCompleted);
            Assert.Equal(
                0,
                await CountPersonRowsAsync(blockedTenant));
            blockingAuditor.Release();
            Assert.Equal(
                PersonKeyMutationStatus.Created,
                (await pending).Status);
        }

        var stateFailureTenant = NewTenant();
        var stateHarness =
            new PostgreSqlTrustTestHarness(fixture);
        var stateProfile = Profile();
        _ = stateHarness.Secrets.AddProfile(
            stateFailureTenant,
            stateProfile);
        var stateFailureMap =
            stateHarness.CreatePersonKeyMap(
                stateProfile,
                stateHashChain: new ThrowingHashChain());
        using (stateHarness.Tenants.BeginScope(
            stateFailureTenant))
        {
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => stateFailureMap.GetOrCreateAsync(
                    new DirectoryIdentitySnapshot(
                        Guid.NewGuid()),
                    PostgreSqlTrustTestHarness.Access(
                        "prove state event rollback")));
        }
        Assert.Equal(
            0,
            await CountPersonRowsAsync(
                stateFailureTenant));
        var stateEvents =
            await stateHarness.ReadEventsAsync(
                stateFailureTenant);
        Assert.Single(
            stateEvents,
            stored =>
                stored.EventType
                == "PrivilegedReadRecorded");
        Assert.DoesNotContain(
            stateEvents,
            stored =>
                stored.EventType
                == "PersonKeyMapChanged");

        var roleFailureTenant = NewTenant();
        var roleHarness =
            new PostgreSqlTrustTestHarness(fixture);
        var failingRoleStore =
            roleHarness.CreateRoleStore(
                new ThrowingHashChain());
        var role = NewRoleAssignment(
            roleFailureTenant,
            PersonKey.New(),
            ControlTowerRole.Operator,
            out var changed,
            out var metadata);
        using (roleHarness.Tenants.BeginScope(
            roleFailureTenant))
        {
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => failingRoleStore.CommitAsync(
                    role,
                    changed,
                    metadata,
                    expectedVersion: 0));
        }
        using (roleHarness.Tenants.BeginScope(
            roleFailureTenant))
        {
            Assert.Null(
                await roleHarness.CreateRoleStore()
                    .GetAsync(role.Id));
        }
        Assert.Empty(
            await roleHarness.ReadEventsAsync(
                roleFailureTenant));
    }

    [Fact]
    public async Task Tenant_switch_during_hashing_rolls_back_captured_tenant_state()
    {
        if (!fixture.Enabled)
            return;

        var tenantA = NewTenant();
        var tenantB = NewTenant();
        var clock = new TrustFrozenTimeProvider(
            new DateTimeOffset(
                2026,
                7,
                24,
                12,
                0,
                0,
                TimeSpan.Zero));
        var roleTenants =
            new MutableTrustTenantContextAccessor();
        var roleSwitch =
            new TenantSwitchingHashChain(
                roleTenants,
                tenantB);
        var roleStore = new PostgreSqlRoleAssignmentStore(
            fixture.NormalDataSource,
            roleTenants,
            new PostgreSqlEventTransactionAppender(
                roleSwitch,
                clock));
        var assignment = NewRoleAssignment(
            tenantA,
            PersonKey.New(),
            ControlTowerRole.Viewer,
            out var roleChanged,
            out var roleMetadata);
        using (roleTenants.BeginScope(tenantA))
        {
            var exception = await Assert.ThrowsAsync<
                PostgreSqlTrustException>(
                () => roleStore.CommitAsync(
                    assignment,
                    roleChanged,
                    roleMetadata,
                    expectedVersion: 0));
            Assert.Equal(
                "The database tenant context is invalid.",
                exception.Message);
        }
        using (roleTenants.BeginScope(tenantA))
        {
            Assert.Null(
                await new PostgreSqlRoleAssignmentStore(
                        fixture.NormalDataSource,
                        roleTenants,
                        new PostgreSqlEventTransactionAppender(
                            new Sha256HashChain(),
                            clock))
                    .GetAsync(assignment.Id));
        }
        var eventReader =
            new PostgreSqlTrustTestHarness(fixture);
        Assert.Empty(
            await eventReader.ReadEventsAsync(tenantA));

        var personTenants =
            new MutableTrustTenantContextAccessor();
        var secrets = new MutableTrustSecretProvider();
        var profile = Profile();
        _ = secrets.AddProfile(
            tenantA,
            profile);
        var personSwitch =
            new TenantSwitchingHashChain(
                personTenants,
                tenantB);
        var sink = new RecordingPrivilegedReadSink();
        var projection =
            new RecordingPrivilegedAccessProjection();
        var auditor = new PrivilegedReadEvidenceAuditor(
            sink,
            projection,
            new PostgreSqlEventStore(
                fixture.PrivilegedAuditDataSource,
                personTenants,
                new Sha256HashChain(),
                clock),
            personTenants);
        var personMap = new PostgreSqlPersonKeyMap(
            fixture.PrivilegedDataSource,
            personTenants,
            auditor,
            secrets,
            profile,
            new PostgreSqlEventTransactionAppender(
                personSwitch,
                clock),
            clock);
        using (personTenants.BeginScope(tenantA))
        {
            var exception = await Assert.ThrowsAsync<
                PostgreSqlTrustException>(
                () => personMap.GetOrCreateAsync(
                    new DirectoryIdentitySnapshot(
                        Guid.NewGuid()),
                    PostgreSqlTrustTestHarness.Access(
                        "reject switched tenant")));
            Assert.Equal(
                "The database tenant context is invalid.",
                exception.Message);
        }
        Assert.Equal(
            0,
            await CountPersonRowsAsync(tenantA));
        var personEvents =
            await eventReader.ReadEventsAsync(tenantA);
        Assert.Single(
            personEvents,
            stored =>
                stored.EventType
                == "PrivilegedReadRecorded");
        Assert.DoesNotContain(
            personEvents,
            stored =>
                stored.EventType
                == "PersonKeyMapChanged");
        Assert.Single(sink.Records);
    }

    [Fact]
    public async Task Early_audit_database_failures_are_bounded()
    {
        if (!fixture.Enabled)
            return;

        var tenant = NewTenant();
        var harness = new PostgreSqlTrustTestHarness(fixture);
        var profile = Profile();
        _ = harness.Secrets.AddProfile(tenant, profile);
        var map = harness.CreatePersonKeyMap(
            profile,
            new NpgsqlFailingPrivilegedReadAuditor());

        using (harness.Tenants.BeginScope(tenant))
        {
            var findFailure = await Assert.ThrowsAsync<
                PostgreSqlTrustException>(
                () => map.FindAsync(
                    Guid.Empty,
                    PostgreSqlTrustTestHarness.Access(
                        "bounded empty find")));
            var getFailure = await Assert.ThrowsAsync<
                PostgreSqlTrustException>(
                () => map.GetAsync(
                    default,
                    PostgreSqlTrustTestHarness.Access(
                        "bounded empty get")));
            Assert.Equal(
                "The person-key operation was rejected.",
                findFailure.Message);
            Assert.Equal(findFailure.Message, getFailure.Message);
            Assert.DoesNotContain(
                "endpoint",
                findFailure.ToString(),
                StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(
                "endpoint",
                getFailure.ToString(),
                StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task Separate_audit_pool_prevents_state_pool_starvation()
    {
        if (!fixture.Enabled)
            return;

        var tenant = NewTenant();
        var harness = new PostgreSqlTrustTestHarness(fixture);
        var profile = Profile();
        _ = harness.Secrets.AddProfile(tenant, profile);
        var stateBuilder = new NpgsqlConnectionStringBuilder(
            fixture.PrivilegedConnectionString)
        {
            MaxPoolSize = 1,
            MinPoolSize = 1,
        };
        await using var singleConnectionStatePool =
            NpgsqlDataSource.Create(
                stateBuilder.ConnectionString);
        var map = new PostgreSqlPersonKeyMap(
            singleConnectionStatePool,
            harness.Tenants,
            harness.CreateAuditor(),
            harness.Secrets,
            profile,
            new PostgreSqlEventTransactionAppender(
                new Sha256HashChain(),
                harness.Clock),
            harness.Clock);
        var directoryId = Guid.NewGuid();

        PersonKeyMutationResult[] results;
        using (harness.Tenants.BeginScope(tenant))
        {
            results = await Task.WhenAll(
                    Enumerable.Range(0, 8)
                        .Select(index =>
                            map.GetOrCreateAsync(
                                new DirectoryIdentitySnapshot(
                                    directoryId),
                                PostgreSqlTrustTestHarness.Access(
                                    $"single-pool state {index}"))))
                .WaitAsync(TimeSpan.FromSeconds(20));
        }

        Assert.Single(
            results.Select(result => result.PersonKey)
                .Distinct());
        Assert.Single(
            results,
            result =>
                result.Status
                == PersonKeyMutationStatus.Created);
        Assert.Equal(8, harness.Sink.Records.Count);
    }

    [Fact]
    public async Task Duplicate_role_event_id_is_rejected_without_partial_state()
    {
        if (!fixture.Enabled)
            return;

        var tenant = NewTenant();
        var harness = new PostgreSqlTrustTestHarness(fixture);
        var store = harness.CreateRoleStore();
        var first = NewRoleAssignment(
            tenant,
            PersonKey.New(),
            ControlTowerRole.Viewer,
            out var firstChanged,
            out var firstMetadata);
        var second = NewRoleAssignment(
            tenant,
            PersonKey.New(),
            ControlTowerRole.Operator,
            out var secondChanged,
            out var secondMetadata);
        secondChanged = secondChanged with
        {
            EventId = firstChanged.EventId,
        };

        using (harness.Tenants.BeginScope(tenant))
        {
            var firstResult = await store.CommitAsync(
                first,
                firstChanged,
                firstMetadata,
                expectedVersion: 0);
            Assert.Equal(
                RoleAssignmentCommitStatus.Applied,
                firstResult.Status);
            var exception = await Assert.ThrowsAsync<
                PostgreSqlTrustException>(
                () => store.CommitAsync(
                    second,
                    secondChanged,
                    secondMetadata,
                    expectedVersion: 0));
            Assert.Equal(
                "The role-assignment operation was rejected.",
                exception.Message);
            Assert.Null(await store.GetAsync(second.Id));
        }

        Assert.Single(
            await harness.ReadEventsAsync(tenant),
            stored =>
                stored.EventType
                == "RoleAssignmentChanged");
    }

    [Fact]
    public void Role_assignment_commit_rejects_noncanonical_timestamps()
    {
        var tenant = NewTenant();
        var subject = PersonKey.New();
        var actor =
            AuditActor.System("p1-t08-time-test");
        var canonical =
            new DateTimeOffset(
                2026,
                7,
                24,
                12,
                0,
                0,
                TimeSpan.Zero);
        var timestamps = new[]
        {
            canonical.AddTicks(1),
            canonical.ToOffset(TimeSpan.FromHours(1)),
        };

        foreach (var timestamp in timestamps)
        {
            var assignment = new RoleAssignment(
                Guid.NewGuid(),
                tenant,
                subject,
                ControlTowerRole.Viewer,
                actor,
                timestamp);
            var changed = new RoleAssignmentChanged
            {
                AssignmentId = assignment.Id,
                SubjectPersonKey = subject,
                Role = "Viewer",
                OrganizationScope = "TenantWide",
                Change = "Assigned",
                ChangedBy = actor,
                Version = 1,
                OccurredAt = timestamp,
            };
            var metadata = new EventAppendMetadata(
                EventReference.For(
                    "role-assignment",
                    assignment.Id),
                actor,
                correlationReference:
                    EventReference.For(
                        "role-assignment-command",
                        Guid.NewGuid()));

            Assert.Throws<InvalidOperationException>(
                () => RoleAssignmentCommitSemantics.Validate(
                    assignment,
                    changed,
                    metadata,
                    expectedVersion: 0));
        }

        var canonicalAssignment = new RoleAssignment(
            Guid.NewGuid(),
            tenant,
            subject,
            ControlTowerRole.Viewer,
            actor,
            canonical);
        var noncanonicalEvent = new RoleAssignmentChanged
        {
            AssignmentId = canonicalAssignment.Id,
            SubjectPersonKey = subject,
            Role = "Viewer",
            OrganizationScope = "TenantWide",
            Change = "Assigned",
            ChangedBy = actor,
            Version = 1,
            OccurredAt =
                canonical.ToOffset(TimeSpan.FromHours(1)),
        };
        var canonicalMetadata = new EventAppendMetadata(
            EventReference.For(
                "role-assignment",
                canonicalAssignment.Id),
            actor,
            correlationReference:
                EventReference.For(
                    "role-assignment-command",
                    Guid.NewGuid()));

        Assert.Throws<InvalidOperationException>(
            () => RoleAssignmentCommitSemantics.Validate(
                canonicalAssignment,
                noncanonicalEvent,
                canonicalMetadata,
                expectedVersion: 0));

        var revokedAt = canonical.AddMinutes(1);
        var noncanonicalHistory =
            RoleAssignment.Rehydrate(
                canonicalAssignment.Id,
                tenant,
                subject,
                ControlTowerRole.Viewer,
                actor,
                canonical.ToOffset(
                    TimeSpan.FromHours(1)),
                version: 2,
                revokedAt,
                actor);
        var revocation = new RoleAssignmentChanged
        {
            AssignmentId = noncanonicalHistory.Id,
            SubjectPersonKey = subject,
            Role = "Viewer",
            OrganizationScope = "TenantWide",
            Change = "Revoked",
            ChangedBy = actor,
            Version = 2,
            OccurredAt = revokedAt,
        };

        Assert.Throws<InvalidOperationException>(
            () => RoleAssignmentCommitSemantics.Validate(
                noncanonicalHistory,
                revocation,
                canonicalMetadata,
                expectedVersion: 1));
        Assert.False(
            RoleAssignmentCommitSemantics.IsTransitionFrom(
                canonicalAssignment,
                noncanonicalHistory));
    }

    [Fact]
    public async Task Tenant_separation_rotation_and_tamper_checks_fail_closed()
    {
        if (!fixture.Enabled)
            return;

        var tenantA = NewTenant();
        var tenantB = NewTenant();
        var harness = new PostgreSqlTrustTestHarness(fixture);
        var hostileExceptions = new List<Exception>();
        var profileV1 = Profile();
        var tenantAKeys =
            harness.Secrets.AddProfile(
                tenantA,
                profileV1);
        _ = harness.Secrets.AddProfile(
            tenantB,
            profileV1,
            tenantAKeys.EncryptionKey,
            tenantAKeys.IndexKey);
        var mapV1 = harness.CreatePersonKeyMap(profileV1);
        var sharedDirectoryId = Guid.NewGuid();

        PersonKeyMutationResult mappingA;
        PersonKeyMutationResult mappingB;
        using (harness.Tenants.BeginScope(tenantA))
        {
            mappingA = await mapV1.GetOrCreateAsync(
                new DirectoryIdentitySnapshot(
                    sharedDirectoryId,
                    "Tenant A Person"),
                PostgreSqlTrustTestHarness.Access(
                    "create tenant A mapping"));
        }
        using (harness.Tenants.BeginScope(tenantB))
        {
            mappingB = await mapV1.GetOrCreateAsync(
                new DirectoryIdentitySnapshot(
                    sharedDirectoryId,
                    "Tenant B Person"),
                PostgreSqlTrustTestHarness.Access(
                    "create tenant B mapping"));
            Assert.Null(
                await mapV1.GetAsync(
                    mappingA.PersonKey,
                    PostgreSqlTrustTestHarness.Access(
                        "foreign key lookup")));
        }

        var rowA = await ReadPersonRowAsync(
            tenantA,
            mappingA.PersonKey);
        var rowB = await ReadPersonRowAsync(
            tenantB,
            mappingB.PersonKey);
        Assert.NotNull(rowA);
        Assert.NotNull(rowB);
        Assert.NotEqual(
            mappingA.PersonKey,
            mappingB.PersonKey);
        Assert.False(
            rowA!.BlindIndex!.SequenceEqual(
                rowB!.BlindIndex!));
        Assert.False(
            rowA.Ciphertext!.SequenceEqual(
                rowB.Ciphertext!));

        harness.Secrets.AddEncryptionKey(
            tenantA,
            "aes-v2",
            RandomNumberGenerator.GetBytes(32));
        var profileV2 =
            new PersonKeyProtectionProfile(
                "aes-v2",
                "idx-v1");
        var mapV2 =
            harness.CreatePersonKeyMap(profileV2);
        PersonKeyMutationResult existing;
        PersonKeyMutationResult newMapping;
        using (harness.Tenants.BeginScope(tenantA))
        {
            var oldIdentity = await mapV2.GetAsync(
                mappingA.PersonKey,
                PostgreSqlTrustTestHarness.Access(
                    "read pre-rotation mapping"));
            Assert.Equal(
                sharedDirectoryId,
                oldIdentity!.DirectoryObjectId);
            existing = await mapV2.GetOrCreateAsync(
                new DirectoryIdentitySnapshot(
                    sharedDirectoryId),
                PostgreSqlTrustTestHarness.Access(
                    "deduplicate during AES rotation"));
            newMapping = await mapV2.GetOrCreateAsync(
                new DirectoryIdentitySnapshot(
                    Guid.NewGuid()),
                PostgreSqlTrustTestHarness.Access(
                    "write with rotated AES key"));
        }
        Assert.Equal(
            PersonKeyMutationStatus.Existing,
            existing.Status);
        Assert.Equal(mappingA.PersonKey, existing.PersonKey);
        Assert.Equal(
            "aes-v2",
            (await ReadPersonRowAsync(
                tenantA,
                newMapping.PersonKey))!
                .EncryptionReference);

        var personEventsBeforeHostileReads =
            (await harness.ReadEventsAsync(tenantA))
            .Count(stored =>
                stored.EventType
                == "PersonKeyMapChanged");
        harness.Secrets.SetRaw(
            $"ct-e19-{tenantA.Value:N}-aes-aes-v1",
            $"CTE19A1:{tenantB.Value:N}:aes-v1:"
            + Convert.ToBase64String(
                tenantAKeys.EncryptionKey));
        using (harness.Tenants.BeginScope(tenantA))
        {
            var exception = await Assert.ThrowsAsync<
                PostgreSqlTrustException>(
                () => mapV1.GetAsync(
                    mappingA.PersonKey,
                    PostgreSqlTrustTestHarness.Access(
                        "reject wrong-tenant secret")));
            hostileExceptions.Add(exception);
            Assert.Equal(
                "Person-key protection is unavailable.",
                exception.Message);
        }
        harness.Secrets.AddEncryptionKey(
            tenantA,
            "aes-v1",
            tenantAKeys.EncryptionKey);

        await WritePersonEnvelopeAsync(
            tenantA,
            mappingA.PersonKey,
            rowA,
            encryptionReference: "aes-missing");
        using (harness.Tenants.BeginScope(tenantA))
        {
            var exception = await Assert.ThrowsAsync<
                PostgreSqlTrustException>(
                () => mapV1.GetAsync(
                    mappingA.PersonKey,
                    PostgreSqlTrustTestHarness.Access(
                        "reject unknown persisted AES reference")));
            hostileExceptions.Add(exception);
            Assert.Equal(
                "Person-key protection is unavailable.",
                exception.Message);
        }
        await WritePersonEnvelopeAsync(
            tenantA,
            mappingA.PersonKey,
            rowA);

        await WritePersonEnvelopeAsync(
            tenantA,
            mappingA.PersonKey,
            rowA,
            ciphertext: rowB.Ciphertext,
            nonce: rowB.Nonce,
            tag: rowB.Tag);
        using (harness.Tenants.BeginScope(tenantA))
        {
            var exception = await Assert.ThrowsAsync<
                PostgreSqlTrustException>(
                () => mapV1.GetAsync(
                    mappingA.PersonKey,
                    PostgreSqlTrustTestHarness.Access(
                        "reject foreign envelope")));
            hostileExceptions.Add(exception);
            Assert.Equal(
                "Protected identity validation failed.",
                exception.Message);
        }
        await WritePersonEnvelopeAsync(
            tenantA,
            mappingA.PersonKey,
            rowA);

        var malformed = await AssertMalformedEnvelopeRejectedAsync(
            tenantA,
            mappingA.PersonKey);
        hostileExceptions.Add(malformed);
        Assert.Equal(
            PostgresErrorCodes.CheckViolation,
            malformed.SqlState);
        Assert.Equal(
            personEventsBeforeHostileReads,
            (await harness.ReadEventsAsync(tenantA))
            .Count(stored =>
                stored.EventType
                == "PersonKeyMapChanged"));
        AssertPersonEnvelopeEqual(
            rowA,
            await ReadPersonRowAsync(
                tenantA,
                mappingA.PersonKey));
        Assert.Equal(
            2,
            await CountPersonRowsAsync(tenantA));

        harness.Secrets.AddIndexKey(
            tenantA,
            "idx-v2",
            RandomNumberGenerator.GetBytes(32));
        var changedIndexMap =
            harness.CreatePersonKeyMap(
                new PersonKeyProtectionProfile(
                    "aes-v2",
                    "idx-v2"));
        using (harness.Tenants.BeginScope(tenantA))
        {
            var exception = await Assert.ThrowsAsync<
                PostgreSqlTrustException>(
                () => changedIndexMap.GetOrCreateAsync(
                    new DirectoryIdentitySnapshot(
                        sharedDirectoryId),
                    PostgreSqlTrustTestHarness.Access(
                        "reject uncontrolled index rotation")));
            Assert.Equal(
                "The person-key operation was rejected.",
                exception.Message);
            hostileExceptions.Add(exception);
        }
        Assert.Equal(
            2,
            await CountPersonRowsAsync(tenantA));

        var missingIndexHarness =
            new PostgreSqlTrustTestHarness(fixture);
        missingIndexHarness.Secrets.AddEncryptionKey(
            tenantA,
            "aes-v1",
            tenantAKeys.EncryptionKey);
        var missingIndexMap =
            missingIndexHarness.CreatePersonKeyMap(
                profileV1);
        using (missingIndexHarness.Tenants.BeginScope(tenantA))
        {
            var exception = await Assert.ThrowsAsync<
                PostgreSqlTrustException>(
                () => missingIndexMap.GetAsync(
                    mappingA.PersonKey,
                    PostgreSqlTrustTestHarness.Access(
                        "reject unknown index key")));
            Assert.Equal(
                "Person-key protection is unavailable.",
                exception.Message);
            hostileExceptions.Add(exception);
        }

        harness.Secrets.AddEncryptionKey(
            tenantA,
            "aes-v1",
            RandomNumberGenerator.GetBytes(32));
        using (harness.Tenants.BeginScope(tenantA))
        {
            var exception = await Assert.ThrowsAsync<
                PostgreSqlTrustException>(
                () => mapV1.GetAsync(
                    mappingA.PersonKey,
                    PostgreSqlTrustTestHarness.Access(
                        "reject wrong encryption key")));
            Assert.Equal(
                "Protected identity validation failed.",
                exception.Message);
            hostileExceptions.Add(exception);
        }
        harness.Secrets.AddEncryptionKey(
            tenantA,
            "aes-v1",
            tenantAKeys.EncryptionKey);

        harness.Secrets.AddIndexKey(
            tenantA,
            "idx-v1",
            RandomNumberGenerator.GetBytes(32));
        using (harness.Tenants.BeginScope(tenantA))
        {
            var exception = await Assert.ThrowsAsync<
                PostgreSqlTrustException>(
                () => mapV2.GetOrCreateAsync(
                    new DirectoryIdentitySnapshot(
                        sharedDirectoryId),
                    PostgreSqlTrustTestHarness.Access(
                        "reject replaced index key")));
            Assert.Equal(
                "The person-key operation was rejected.",
                exception.Message);
            hostileExceptions.Add(exception);
        }
        Assert.Equal(
            2,
            await CountPersonRowsAsync(tenantA));

        harness.Secrets.AddIndexKey(
            tenantA,
            "idx-v1",
            tenantAKeys.IndexKey);
        await TamperTagAsync(
            tenantA,
            mappingA.PersonKey);
        using (harness.Tenants.BeginScope(tenantA))
        {
            var exception = await Assert.ThrowsAsync<
                PostgreSqlTrustException>(
                () => mapV1.GetAsync(
                    mappingA.PersonKey,
                    PostgreSqlTrustTestHarness.Access(
                        "reject tampered ciphertext")));
            Assert.Equal(
                "Protected identity validation failed.",
                exception.Message);
            Assert.DoesNotContain(
                sharedDirectoryId.ToString("D"),
                exception.ToString(),
                StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(
                "Tenant A Person",
                exception.ToString(),
                StringComparison.Ordinal);
            hostileExceptions.Add(exception);
        }
        AssertSensitiveExceptionTextAbsent(
            hostileExceptions,
            sharedDirectoryId,
            "Tenant A Person",
            tenantAKeys.EncryptionKey,
            tenantAKeys.IndexKey);
    }

    [Fact]
    public async Task Deferred_database_guard_rejects_state_without_its_event()
    {
        if (!fixture.Enabled)
            return;

        var tenant = NewTenant();
        var personKey = PersonKey.New();
        await using var connection =
            await fixture.PrivilegedDataSource
                .OpenConnectionAsync();
        await using var transaction =
            await connection.BeginTransactionAsync();
        await BindTenantAsync(
            connection,
            transaction,
            tenant);
        await using (var command = new NpgsqlCommand(
            """
            SELECT trust_store.insert_person_key(
                @tenant_id,
                @person_key,
                1::smallint,
                'aes-v1'::varchar(24),
                'idx-v1'::varchar(24),
                @index_key_commitment,
                @blind_index,
                @ciphertext,
                @nonce,
                @tag,
                @event_id);
            """,
            connection,
            transaction))
        {
            command.Parameters.Add(
                    "tenant_id",
                    NpgsqlDbType.Uuid)
                .Value = tenant.Value;
            command.Parameters.Add(
                    "person_key",
                    NpgsqlDbType.Uuid)
                .Value = personKey.Value;
            command.Parameters.Add(
                    "index_key_commitment",
                    NpgsqlDbType.Bytea)
                .Value = RandomNumberGenerator.GetBytes(32);
            command.Parameters.Add(
                    "blind_index",
                    NpgsqlDbType.Bytea)
                .Value = RandomNumberGenerator.GetBytes(32);
            command.Parameters.Add(
                    "ciphertext",
                    NpgsqlDbType.Bytea)
                .Value = RandomNumberGenerator.GetBytes(19);
            command.Parameters.Add(
                    "nonce",
                    NpgsqlDbType.Bytea)
                .Value = RandomNumberGenerator.GetBytes(12);
            command.Parameters.Add(
                    "tag",
                    NpgsqlDbType.Bytea)
                .Value = RandomNumberGenerator.GetBytes(16);
            command.Parameters.Add(
                    "event_id",
                    NpgsqlDbType.Uuid)
                .Value = Guid.NewGuid();
            Assert.Equal(
                true,
                await command.ExecuteScalarAsync());
        }

        var exception = await Assert.ThrowsAsync<
            PostgresException>(
            () => transaction.CommitAsync());
        Assert.Equal(
            PostgresErrorCodes.ObjectNotInPrerequisiteState,
            exception.SqlState);
        Assert.Equal(
            0,
            await CountPersonRowsAsync(tenant));
    }

    private static async Task AssertPermissionDeniedAsync(
        NpgsqlDataSource dataSource,
        string sql)
    {
        await using var connection =
            await dataSource.OpenConnectionAsync();
        await using var command =
            new NpgsqlCommand(sql, connection);
        var exception = await Assert.ThrowsAsync<
            PostgresException>(
            () => command.ExecuteNonQueryAsync());
        Assert.Equal(
            PostgresErrorCodes.InsufficientPrivilege,
            exception.SqlState);
    }

    private async Task<PersonRow?> ReadPersonRowAsync(
        TenantId tenant,
        PersonKey personKey)
    {
        await using var connection =
            await fixture.MigrationDataSource
                .OpenConnectionAsync();
        await using var transaction =
            await connection.BeginTransactionAsync();
        await BindTenantAsync(
            connection,
            transaction,
            tenant);
        await using var command = new NpgsqlCommand(
            """
            SELECT
                version,
                is_severed,
                encryption_reference,
                index_reference,
                blind_index,
                ciphertext,
                nonce,
                tag
            FROM trust_store.person_key_map
            WHERE tenant_id = @tenant_id
              AND person_key = @person_key;
            """,
            connection,
            transaction);
        command.Parameters.Add(
                "tenant_id",
                NpgsqlDbType.Uuid)
            .Value = tenant.Value;
        command.Parameters.Add(
                "person_key",
                NpgsqlDbType.Uuid)
            .Value = personKey.Value;
        await using var reader =
            await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return null;

        var row = new PersonRow(
            reader.GetInt64(0),
            reader.GetBoolean(1),
            reader.IsDBNull(2)
                ? null
                : reader.GetString(2),
            reader.IsDBNull(3)
                ? null
                : reader.GetString(3),
            BytesOrNull(reader, 4),
            BytesOrNull(reader, 5),
            BytesOrNull(reader, 6),
            BytesOrNull(reader, 7));
        Assert.False(await reader.ReadAsync());
        return row;
    }

    private async Task<long> CountPersonRowsAsync(
        TenantId tenant)
    {
        await using var connection =
            await fixture.MigrationDataSource
                .OpenConnectionAsync();
        await using var transaction =
            await connection.BeginTransactionAsync();
        await BindTenantAsync(
            connection,
            transaction,
            tenant);
        await using var command = new NpgsqlCommand(
            """
            SELECT count(*)
            FROM trust_store.person_key_map
            WHERE tenant_id = @tenant_id;
            """,
            connection,
            transaction);
        command.Parameters.Add(
                "tenant_id",
                NpgsqlDbType.Uuid)
            .Value = tenant.Value;
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private async Task WritePersonEnvelopeAsync(
        TenantId tenant,
        PersonKey personKey,
        PersonRow row,
        string? encryptionReference = null,
        byte[]? ciphertext = null,
        byte[]? nonce = null,
        byte[]? tag = null)
    {
        Assert.False(row.IsSevered);
        await using var connection =
            await fixture.MigrationDataSource
                .OpenConnectionAsync();
        await using var transaction =
            await connection.BeginTransactionAsync();
        await BindTenantAsync(
            connection,
            transaction,
            tenant);
        await using var command = new NpgsqlCommand(
            """
            UPDATE trust_store.person_key_map
            SET encryption_reference = @encryption_reference,
                index_reference = @index_reference,
                blind_index = @blind_index,
                ciphertext = @ciphertext,
                nonce = @nonce,
                tag = @tag
            WHERE tenant_id = @tenant_id
              AND person_key = @person_key;
            """,
            connection,
            transaction);
        command.Parameters.Add(
                "encryption_reference",
                NpgsqlDbType.Varchar)
            .Value =
            encryptionReference
            ?? row.EncryptionReference!;
        command.Parameters.Add(
                "index_reference",
                NpgsqlDbType.Varchar)
            .Value = row.IndexReference!;
        command.Parameters.Add(
                "blind_index",
                NpgsqlDbType.Bytea)
            .Value = row.BlindIndex!;
        command.Parameters.Add(
                "ciphertext",
                NpgsqlDbType.Bytea)
            .Value = ciphertext ?? row.Ciphertext!;
        command.Parameters.Add(
                "nonce",
                NpgsqlDbType.Bytea)
            .Value = nonce ?? row.Nonce!;
        command.Parameters.Add(
                "tag",
                NpgsqlDbType.Bytea)
            .Value = tag ?? row.Tag!;
        command.Parameters.Add(
                "tenant_id",
                NpgsqlDbType.Uuid)
            .Value = tenant.Value;
        command.Parameters.Add(
                "person_key",
                NpgsqlDbType.Uuid)
            .Value = personKey.Value;
        Assert.Equal(1, await command.ExecuteNonQueryAsync());
        await transaction.CommitAsync();
    }

    private async Task<PostgresException>
        AssertMalformedEnvelopeRejectedAsync(
            TenantId tenant,
            PersonKey personKey)
    {
        await using var connection =
            await fixture.MigrationDataSource
                .OpenConnectionAsync();
        await using var transaction =
            await connection.BeginTransactionAsync();
        await BindTenantAsync(
            connection,
            transaction,
            tenant);
        await using var command = new NpgsqlCommand(
            """
            UPDATE trust_store.person_key_map
            SET nonce = @malformed_nonce
            WHERE tenant_id = @tenant_id
              AND person_key = @person_key;
            """,
            connection,
            transaction);
        command.Parameters.Add(
                "malformed_nonce",
                NpgsqlDbType.Bytea)
            .Value = RandomNumberGenerator.GetBytes(11);
        command.Parameters.Add(
                "tenant_id",
                NpgsqlDbType.Uuid)
            .Value = tenant.Value;
        command.Parameters.Add(
                "person_key",
                NpgsqlDbType.Uuid)
            .Value = personKey.Value;
        return await Assert.ThrowsAsync<PostgresException>(
            () => command.ExecuteNonQueryAsync());
    }

    private async Task TamperTagAsync(
        TenantId tenant,
        PersonKey personKey)
    {
        await using var connection =
            await fixture.MigrationDataSource
                .OpenConnectionAsync();
        await using var transaction =
            await connection.BeginTransactionAsync();
        await BindTenantAsync(
            connection,
            transaction,
            tenant);
        await using var command = new NpgsqlCommand(
            """
            UPDATE trust_store.person_key_map
            SET tag = set_byte(
                tag,
                0,
                get_byte(tag, 0) # 255)
            WHERE tenant_id = @tenant_id
              AND person_key = @person_key;
            """,
            connection,
            transaction);
        command.Parameters.Add(
                "tenant_id",
                NpgsqlDbType.Uuid)
            .Value = tenant.Value;
        command.Parameters.Add(
                "person_key",
                NpgsqlDbType.Uuid)
            .Value = personKey.Value;
        Assert.Equal(1, await command.ExecuteNonQueryAsync());
        await transaction.CommitAsync();
    }

    private static async Task BindTenantAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        TenantId tenant)
    {
        await using var command = new NpgsqlCommand(
            """
            SELECT set_config(
                'control_tower.tenant_id',
                @tenant_id,
                true);
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue(
            "tenant_id",
            tenant.Value.ToString("D"));
        _ = await command.ExecuteScalarAsync();
    }

    private static RoleAssignment NewRoleAssignment(
        TenantId tenant,
        PersonKey subject,
        ControlTowerRole role,
        out RoleAssignmentChanged changed,
        out EventAppendMetadata metadata)
    {
        var actor =
            AuditActor.System("p1-t08-role-test");
        var occurredAt =
            new DateTimeOffset(
                2026,
                7,
                24,
                12,
                0,
                0,
                TimeSpan.Zero);
        var assignment = new RoleAssignment(
            Guid.NewGuid(),
            tenant,
            subject,
            role,
            actor,
            occurredAt);
        changed = new RoleAssignmentChanged
        {
            AssignmentId = assignment.Id,
            SubjectPersonKey = subject,
            Role = ControlTowerAccessCatalog.Name(role),
            OrganizationScope =
                OrganizationScope.TenantWide.ToString(),
            Change = "Assigned",
            ChangedBy = actor,
            Version = 1,
            OccurredAt = occurredAt,
        };
        metadata = new EventAppendMetadata(
            EventReference.For(
                "role-assignment",
                assignment.Id),
            actor,
            correlationReference:
                EventReference.For(
                    "role-assignment-command",
                    Guid.NewGuid()));
        return assignment;
    }

    private static void AssertSensitiveMaterialAbsent(
        IReadOnlyList<StoredEvent> events,
        IReadOnlyList<PrivilegedReadRecord> records,
        IReadOnlyList<PrivilegedAccessLogEntry> projections,
        IReadOnlyList<PersonRow> rows,
        Guid directoryId,
        string display,
        params byte[][] protectionKeys)
    {
        var surfaces = new List<byte[]>();
        foreach (var row in rows)
        {
            AddIfPresent(surfaces, row.BlindIndex);
            AddIfPresent(surfaces, row.Ciphertext);
            AddIfPresent(surfaces, row.Nonce);
            AddIfPresent(surfaces, row.Tag);
        }
        foreach (var stored in events)
        {
            surfaces.Add(stored.Payload);
            surfaces.Add(
                EventEnvelopeCanonicalizer.Canonicalize(
                    stored));
        }
        surfaces.Add(
            JsonSerializer.SerializeToUtf8Bytes(events));
        surfaces.Add(
            JsonSerializer.SerializeToUtf8Bytes(records));
        surfaces.Add(
            JsonSerializer.SerializeToUtf8Bytes(
                projections));
        surfaces.Add(
            JsonSerializer.SerializeToUtf8Bytes(rows));
        AssertSensitiveBytesAbsent(
            surfaces,
            directoryId,
            display,
            protectionKeys);

        var evidenceText = string.Join(
            "\n",
            events.Select(stored =>
                Encoding.UTF8.GetString(
                    stored.Payload)));
        Assert.DoesNotContain(
            directoryId.ToString("D"),
            evidenceText,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            directoryId.ToString("N"),
            evidenceText,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            display,
            evidenceText,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "aes-v1",
            evidenceText,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "idx-v1",
            evidenceText,
            StringComparison.Ordinal);
    }

    private static void AssertSensitiveExceptionTextAbsent(
        IReadOnlyList<Exception> exceptions,
        Guid directoryId,
        string display,
        params byte[][] protectionKeys)
    {
        var surfaces = exceptions
            .Select(exception =>
                Encoding.UTF8.GetBytes(
                    exception.ToString()))
            .ToArray();
        AssertSensitiveBytesAbsent(
            surfaces,
            directoryId,
            display,
            protectionKeys);
        var text = string.Join(
            "\n",
            exceptions.Select(
                exception => exception.ToString()));
        Assert.DoesNotContain(
            "aes-v1",
            text,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "aes-missing",
            text,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "idx-v1",
            text,
            StringComparison.Ordinal);
    }

    private static void AssertSensitiveBytesAbsent(
        IReadOnlyList<byte[]> surfaces,
        Guid directoryId,
        string display,
        IReadOnlyList<byte[]> protectionKeys)
    {
        var directoryBytes = directoryId.ToByteArray();
        var canonicalDirectoryBytes = new byte[16];
        Assert.True(
            directoryId.TryWriteBytes(
                canonicalDirectoryBytes,
                bigEndian: true,
                out var written));
        Assert.Equal(16, written);
        var forbidden = new List<byte[]>
        {
            directoryBytes,
            canonicalDirectoryBytes,
            Encoding.UTF8.GetBytes(
                directoryId.ToString("D")),
            Encoding.UTF8.GetBytes(
                directoryId.ToString("N")),
            Encoding.UTF8.GetBytes(display),
        };
        foreach (var key in protectionKeys)
        {
            forbidden.Add(key);
            forbidden.Add(
                Encoding.UTF8.GetBytes(
                    Convert.ToBase64String(key)));
        }

        foreach (var surface in surfaces)
        {
            foreach (var secret in forbidden)
            {
                Assert.Equal(
                    -1,
                    surface.AsSpan().IndexOf(secret));
            }
        }
    }

    private static void AssertPersonEnvelopeEqual(
        PersonRow expected,
        PersonRow? actual)
    {
        Assert.NotNull(actual);
        Assert.Equal(expected.Version, actual!.Version);
        Assert.Equal(expected.IsSevered, actual.IsSevered);
        Assert.Equal(
            expected.EncryptionReference,
            actual.EncryptionReference);
        Assert.Equal(
            expected.IndexReference,
            actual.IndexReference);
        Assert.True(
            expected.BlindIndex!.SequenceEqual(
                actual.BlindIndex!));
        Assert.True(
            expected.Ciphertext!.SequenceEqual(
                actual.Ciphertext!));
        Assert.True(
            expected.Nonce!.SequenceEqual(actual.Nonce!));
        Assert.True(
            expected.Tag!.SequenceEqual(actual.Tag!));
    }

    private static void AddIfPresent(
        ICollection<byte[]> surfaces,
        byte[]? value)
    {
        if (value is not null)
            surfaces.Add(value);
    }

    private static PersonKeyProtectionProfile Profile() =>
        new("aes-v1", "idx-v1");

    private static TenantId NewTenant() =>
        new(Guid.NewGuid());

    private static byte[]? BytesOrNull(
        NpgsqlDataReader reader,
        int ordinal) =>
        reader.IsDBNull(ordinal)
            ? null
            : reader.GetFieldValue<byte[]>(ordinal);

    private sealed record PersonRow(
        long Version,
        bool IsSevered,
        string? EncryptionReference,
        string? IndexReference,
        byte[]? BlindIndex,
        byte[]? Ciphertext,
        byte[]? Nonce,
        byte[]? Tag);
}
