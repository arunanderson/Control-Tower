using System.Text;
using System.Text.Json;
using ControlTower.Adapters.InMemory;
using ControlTower.Modules.Audit;
using ControlTower.Modules.Trust.Authorization;
using ControlTower.Modules.Trust.Infrastructure;
using ControlTower.Platform.Audit;
using ControlTower.Platform.Events;
using ControlTower.Platform.Identity;
using ControlTower.Platform.Tenancy;

namespace ControlTower.Host.Web.Tests;

public sealed class C8ContractTests
{
    [Fact]
    public async Task Person_key_lifecycle_is_audited_severable_and_identity_free()
    {
        var tenant = new TenantId(Guid.NewGuid());
        var directoryObjectId = Guid.NewGuid();
        const string displaySnapshot = "Raw Directory Display";
        var tenants = new TenantContextAccessor();
        var events = new InMemoryEventStore(tenants);
        var auditor = EvidenceAuditor(
            tenants,
            events,
            out var sink,
            out var projection);
        var map = new InMemoryPersonKeyMap(
            tenants,
            auditor,
            events,
            TimeProvider.System);
        var access = Access("exercise person-key lifecycle");

        using var _ = tenants.BeginScope(tenant);
        var created = await map.GetOrCreateAsync(
            new DirectoryIdentitySnapshot(
                directoryObjectId,
                displaySnapshot),
            access);
        var existing = await map.GetOrCreateAsync(
            new DirectoryIdentitySnapshot(
                directoryObjectId,
                "Untrusted Replacement"),
            access);
        Assert.Equal(PersonKeyMutationStatus.Created, created.Status);
        Assert.Equal(PersonKeyMutationStatus.Existing, existing.Status);
        Assert.Equal(created.PersonKey, existing.PersonKey);
        Assert.Equal(1, created.Version);

        Assert.Equal(
            created.PersonKey,
            await map.FindAsync(directoryObjectId, access));
        Assert.Null(await map.FindAsync(Guid.NewGuid(), access));
        var identity = await map.GetAsync(created.PersonKey, access);
        Assert.Equal(directoryObjectId, identity!.DirectoryObjectId);
        Assert.Equal(displaySnapshot, identity.DisplaySnapshot);

        var severed = await map.SeverAsync(
            created.PersonKey,
            created.Version,
            access);
        Assert.Equal(PersonKeySeverStatus.Severed, severed.Status);
        Assert.Equal(2, severed.Version);
        Assert.Null(await map.GetAsync(created.PersonKey, access));
        var repeated = await map.SeverAsync(
            created.PersonKey,
            created.Version,
            access);
        Assert.Equal(
            PersonKeySeverStatus.AlreadySevered,
            repeated.Status);
        Assert.Equal(2, repeated.Version);

        var remapped = await map.GetOrCreateAsync(
            new DirectoryIdentitySnapshot(
                directoryObjectId,
                displaySnapshot),
            access);
        Assert.Equal(PersonKeyMutationStatus.Created, remapped.Status);
        Assert.NotEqual(created.PersonKey, remapped.PersonKey);
        Assert.Equal(
            directoryObjectId,
            (await map.GetAsync(remapped.PersonKey, access))!
                .DirectoryObjectId);

        Assert.Equal(10, sink.Records.Count);
        Assert.Equal(
            sink.Records.Count,
            sink.Records.Select(record => record.AccessId)
                .Distinct()
                .Count());
        Assert.All(
            sink.Records,
            record =>
            {
                Assert.Equal(tenant, record.Tenant);
                Assert.Equal(access.Actor, record.Actor);
                Assert.Equal(access.Purpose, record.Purpose);
                Assert.Equal(
                    access.CorrelationReference,
                    record.CorrelationReference);
                Assert.Equal(
                    PrivilegedReadPolicyKind.NotApplicable,
                    record.Policy.Kind);
                Assert.Null(record.Policy.Version);
            });

        var allEvents = await events.ReadAllAsync();
        Assert.Equal(
            10,
            allEvents.Count(stored =>
                stored.EventType
                == "PrivilegedReadRecorded"));
        var projected = await projection.ListAsync();
        Assert.Equal(
            10,
            projected.Count);
        var stream = allEvents
            .Where(stored =>
                stored.EventType == "PersonKeyMapChanged")
            .ToList();
        Assert.Equal(3, stream.Count);
        Assert.All(
            stream,
            stored =>
            {
                Assert.Equal(EventPrivilege.Privileged, stored.Privilege);
                Assert.Equal(access.Actor, stored.Actor);
                Assert.Equal(access.Purpose, stored.Reason);
                Assert.Equal(
                    access.CorrelationReference,
                    stored.CorrelationReference);
                Assert.Equal(
                    "person-key",
                    stored.AggregateReference.Kind);
            });

        var evidenceText =
            JsonSerializer.Serialize(sink.Records)
            + JsonSerializer.Serialize(projected)
            + string.Join(
                "|",
                allEvents.Select(stored =>
                    Encoding.UTF8.GetString(stored.Payload)
                    + stored.AggregateReference
                    + stored.Actor
                    + stored.Reason
                    + stored.CorrelationReference));
        var rawIdentityValues = new[]
        {
            directoryObjectId.ToString("D"),
            directoryObjectId.ToString("N"),
            displaySnapshot,
            "Untrusted Replacement",
        };
        foreach (var stored in allEvents)
        {
            var canonical =
                EventEnvelopeCanonicalizer.Canonicalize(
                    stored);
            foreach (var rawIdentity in rawIdentityValues)
            {
                Assert.Equal(
                    -1,
                    canonical.AsSpan().IndexOf(
                        Encoding.UTF8.GetBytes(
                            rawIdentity)));
            }
        }
        Assert.DoesNotContain(
            directoryObjectId.ToString("D"),
            evidenceText,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            directoryObjectId.ToString("N"),
            evidenceText,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            displaySnapshot,
            evidenceText,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "Untrusted Replacement",
            evidenceText,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Person_keys_do_not_correlate_or_disclose_across_tenants()
    {
        var tenantA = new TenantId(Guid.NewGuid());
        var tenantB = new TenantId(Guid.NewGuid());
        var directoryObjectId = Guid.NewGuid();
        var tenants = new TenantContextAccessor();
        var events = new InMemoryEventStore(tenants);
        var auditor = EvidenceAuditor(
            tenants,
            events,
            out var sink,
            out _);
        var map = new InMemoryPersonKeyMap(
            tenants,
            auditor,
            events,
            TimeProvider.System);
        var access = Access("verify tenant isolation");
        PersonKey keyA;

        using (tenants.BeginScope(tenantA))
        {
            keyA = (await map.GetOrCreateAsync(
                new DirectoryIdentitySnapshot(directoryObjectId),
                access)).PersonKey;
        }

        PersonKey keyB;
        using (tenants.BeginScope(tenantB))
        {
            Assert.Null(await map.FindAsync(
                directoryObjectId,
                access));
            Assert.Null(await map.GetAsync(keyA, access));
            var foreignSever = await map.SeverAsync(
                keyA,
                expectedVersion: 1,
                access);
            Assert.Equal(
                PersonKeySeverStatus.NotFound,
                foreignSever.Status);
            Assert.Null(foreignSever.PersonKey);
            Assert.Null(foreignSever.Version);

            keyB = (await map.GetOrCreateAsync(
                new DirectoryIdentitySnapshot(directoryObjectId),
                access)).PersonKey;
            Assert.NotEqual(keyA, keyB);
        }

        using (tenants.BeginScope(tenantA))
        {
            Assert.Equal(
                keyA,
                await map.FindAsync(directoryObjectId, access));
            Assert.Equal(
                directoryObjectId,
                (await map.GetAsync(keyA, access))!
                    .DirectoryObjectId);
        }

        Assert.Contains(
            sink.Records,
            record => record.Tenant == tenantA);
        Assert.Contains(
            sink.Records,
            record => record.Tenant == tenantB);
    }

    [Fact]
    public async Task Raw_identity_cannot_enter_person_key_evidence_context()
    {
        var tenant = new TenantId(Guid.NewGuid());
        var directoryObjectId = Guid.NewGuid();
        const string displaySnapshot = "Protected Directory Display";
        var tenants = new TenantContextAccessor();
        var events = new InMemoryEventStore(tenants);
        var auditor = EvidenceAuditor(
            tenants,
            events,
            out var sink,
            out var projection);
        var map = new InMemoryPersonKeyMap(
            tenants,
            auditor,
            events,
            TimeProvider.System);

        using var _ = tenants.BeginScope(tenant);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => map.GetOrCreateAsync(
                new DirectoryIdentitySnapshot(
                    directoryObjectId,
                    displaySnapshot),
                Access(
                    $"map {directoryObjectId:D}")));

        var created = await map.GetOrCreateAsync(
            new DirectoryIdentitySnapshot(
                directoryObjectId,
                displaySnapshot),
            Access("create protected mapping"));

        Assert.Throws<ArgumentException>(
            () => AuditActor.System(
                directoryObjectId.ToString("D")));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => map.GetAsync(
                created.PersonKey,
                Access(
                    $"inspect {displaySnapshot}")));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => map.SeverAsync(
                created.PersonKey,
                created.Version,
                new PersonKeyAccessContext(
                    AuditActor.System("c8-contract-test"),
                    "sever protected mapping",
                    new EventReference(
                        "test-correlation",
                        $"request-{directoryObjectId:N}"),
                    PrivilegedReadPolicy.NotApplicable())));

        Assert.Single(sink.Records);
        Assert.Single(await projection.ListAsync());
        Assert.Single(
            await events.ReadAllAsync(),
            stored =>
                stored.EventType == "PersonKeyMapChanged");
    }

    [Fact]
    public async Task Privileged_evidence_event_failure_releases_and_projects_nothing()
    {
        var tenant = new TenantId(Guid.NewGuid());
        var directoryObjectId = Guid.NewGuid();
        var tenants = new TenantContextAccessor();
        var events = new ToggleEventStore(
            new InMemoryEventStore(tenants))
        {
            FailedEventType =
                "PrivilegedReadRecorded",
        };
        var auditor = EvidenceAuditor(
            tenants,
            events,
            out var sink,
            out var projection);
        var map = new InMemoryPersonKeyMap(
            tenants,
            auditor,
            events,
            TimeProvider.System);

        using var _ = tenants.BeginScope(tenant);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => map.GetOrCreateAsync(
                new DirectoryIdentitySnapshot(
                    directoryObjectId),
                Access("prove evidence event failure")));

        Assert.Empty(sink.Records);
        Assert.Empty(await projection.ListAsync());
        Assert.Empty(await events.ReadAllAsync());

        events.FailedEventType = null;
        Assert.Null(
            await map.FindAsync(
                directoryObjectId,
                Access("verify unchanged state")));
        Assert.Single(sink.Records);
        Assert.Single(await projection.ListAsync());
        Assert.Single(
            await events.ReadAllAsync(),
            stored =>
                stored.EventType
                == "PrivilegedReadRecorded");
    }

    [Fact]
    public async Task Audit_failure_releases_nothing_and_leaves_person_key_state_unchanged()
    {
        var tenant = new TenantId(Guid.NewGuid());
        var directoryObjectId = Guid.NewGuid();
        var tenants = new TenantContextAccessor();
        var auditor = new ToggleAuditor { Fail = true };
        var events = new InMemoryEventStore(tenants);
        var map = new InMemoryPersonKeyMap(
            tenants,
            auditor,
            events,
            TimeProvider.System);
        var access = Access("prove audit failure");

        using var _ = tenants.BeginScope(tenant);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => map.GetOrCreateAsync(
                new DirectoryIdentitySnapshot(directoryObjectId),
                access));

        auditor.Fail = false;
        Assert.Null(await map.FindAsync(directoryObjectId, access));
        Assert.Empty(await events.ReadAllAsync());

        var created = await map.GetOrCreateAsync(
            new DirectoryIdentitySnapshot(
                directoryObjectId,
                "Protected Display"),
            access);
        auditor.Fail = true;
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => map.GetAsync(created.PersonKey, access));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => map.SeverAsync(
                created.PersonKey,
                created.Version,
                access));

        auditor.Fail = false;
        Assert.Equal(
            created.PersonKey,
            await map.FindAsync(directoryObjectId, access));
        Assert.Equal(
            directoryObjectId,
            (await map.GetAsync(created.PersonKey, access))!
                .DirectoryObjectId);
        Assert.Single(
            await events.ReadAllAsync(),
            stored =>
                stored.EventType == "PersonKeyMapChanged");
    }

    [Fact]
    public async Task Event_failure_leaves_create_and_sever_state_unchanged()
    {
        var tenant = new TenantId(Guid.NewGuid());
        var directoryObjectId = Guid.NewGuid();
        var tenants = new TenantContextAccessor();
        var events = new ToggleEventStore(
            new InMemoryEventStore(tenants))
        {
            FailedEventType = "PersonKeyMapChanged",
        };
        var auditor = EvidenceAuditor(
            tenants,
            events,
            out var eventFailureSink,
            out var eventFailureProjection);
        var map = new InMemoryPersonKeyMap(
            tenants,
            auditor,
            events,
            TimeProvider.System);
        var access = Access("prove event failure");

        using var _ = tenants.BeginScope(tenant);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => map.GetOrCreateAsync(
                new DirectoryIdentitySnapshot(directoryObjectId),
                access));

        events.FailedEventType = null;
        Assert.Null(await map.FindAsync(directoryObjectId, access));
        var created = await map.GetOrCreateAsync(
            new DirectoryIdentitySnapshot(directoryObjectId),
            access);

        events.FailedEventType = "PersonKeyMapChanged";
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => map.SeverAsync(
                created.PersonKey,
                created.Version,
                access));

        events.FailedEventType = null;
        Assert.Equal(
            created.PersonKey,
            await map.FindAsync(directoryObjectId, access));
        Assert.Equal(
            directoryObjectId,
            (await map.GetAsync(created.PersonKey, access))!
                .DirectoryObjectId);
        Assert.Single(
            await events.ReadAllAsync(),
            stored =>
                stored.EventType == "PersonKeyMapChanged");
        Assert.Equal(
            6,
            eventFailureSink.Records.Count);
        Assert.Equal(
            6,
            (await eventFailureProjection.ListAsync())
            .Count);
        Assert.Equal(
            6,
            (await events.ReadAllAsync())
            .Count(stored =>
                stored.EventType
                == "PrivilegedReadRecorded"));
    }

    [Fact]
    public async Task Concurrent_person_key_creation_has_one_key_and_one_event()
    {
        var tenant = new TenantId(Guid.NewGuid());
        var directoryObjectId = Guid.NewGuid();
        var tenants = new TenantContextAccessor();
        var events = new InMemoryEventStore(tenants);
        var auditor = EvidenceAuditor(
            tenants,
            events,
            out var sink,
            out var concurrencyProjection);
        var map = new InMemoryPersonKeyMap(
            tenants,
            auditor,
            events,
            TimeProvider.System);
        var access = Access("race person-key creation");

        using var _ = tenants.BeginScope(tenant);
        var results = await Task.WhenAll(
            Enumerable.Range(0, 64)
                .Select(_ => map.GetOrCreateAsync(
                    new DirectoryIdentitySnapshot(
                        directoryObjectId),
                    access)));

        Assert.Single(
            results.Select(result => result.PersonKey)
                .Distinct());
        Assert.Single(
            results,
            result =>
                result.Status
                == PersonKeyMutationStatus.Created);
        Assert.Equal(
            63,
            results.Count(result =>
                result.Status
                == PersonKeyMutationStatus.Existing));
        Assert.Equal(64, sink.Records.Count);
        Assert.Equal(
            64,
            (await events.ReadAllAsync()).Count(
                stored =>
                    stored.EventType
                    == "PrivilegedReadRecorded"));
        Assert.Single(
            await events.ReadAllAsync(),
            stored =>
                stored.EventType == "PersonKeyMapChanged");
    }

    [Fact]
    public void Role_assignment_rehydration_rejects_impossible_state()
    {
        var tenant = new TenantId(Guid.NewGuid());
        var subject = PersonKey.New();
        var actor = AuditActor.System("rehydration-test");
        var assignedAt =
            EventEnvelopeCanonicalizer.NormalizeTimestamp(
                DateTimeOffset.UtcNow);

        Assert.Throws<RoleAssignmentException>(
            () => RoleAssignment.Rehydrate(
                Guid.Empty,
                tenant,
                subject,
                ControlTowerRole.Viewer,
                actor,
                assignedAt,
                version: 1,
                revokedAt: null,
                revokedBy: null));
        Assert.Throws<RoleAssignmentException>(
            () => RoleAssignment.Rehydrate(
                Guid.NewGuid(),
                default,
                subject,
                ControlTowerRole.Viewer,
                actor,
                assignedAt,
                version: 1,
                revokedAt: null,
                revokedBy: null));
        Assert.Throws<RoleAssignmentException>(
            () => RoleAssignment.Rehydrate(
                Guid.NewGuid(),
                tenant,
                default,
                ControlTowerRole.Viewer,
                actor,
                assignedAt,
                version: 1,
                revokedAt: null,
                revokedBy: null));
        Assert.Throws<RoleAssignmentException>(
            () => RoleAssignment.Rehydrate(
                Guid.NewGuid(),
                tenant,
                subject,
                (ControlTowerRole)999,
                actor,
                assignedAt,
                version: 1,
                revokedAt: null,
                revokedBy: null));
        Assert.Throws<RoleAssignmentException>(
            () => RoleAssignment.Rehydrate(
                Guid.NewGuid(),
                tenant,
                subject,
                ControlTowerRole.Viewer,
                default,
                assignedAt,
                version: 1,
                revokedAt: null,
                revokedBy: null));
        Assert.Throws<RoleAssignmentException>(
            () => RoleAssignment.Rehydrate(
                Guid.NewGuid(),
                tenant,
                subject,
                ControlTowerRole.Viewer,
                actor,
                assignedAt,
                version: 0,
                revokedAt: null,
                revokedBy: null));
        Assert.Throws<RoleAssignmentException>(
            () => RoleAssignment.Rehydrate(
                Guid.NewGuid(),
                tenant,
                subject,
                ControlTowerRole.Viewer,
                actor,
                assignedAt,
                version: 2,
                revokedAt: null,
                revokedBy: null));
        Assert.Throws<RoleAssignmentException>(
            () => RoleAssignment.Rehydrate(
                Guid.NewGuid(),
                tenant,
                subject,
                ControlTowerRole.Viewer,
                actor,
                assignedAt,
                version: 1,
                revokedAt: assignedAt.AddMinutes(1),
                revokedBy: actor));
        Assert.Throws<RoleAssignmentException>(
            () => RoleAssignment.Rehydrate(
                Guid.NewGuid(),
                tenant,
                subject,
                ControlTowerRole.Viewer,
                actor,
                assignedAt,
                version: 2,
                revokedAt: assignedAt.AddMinutes(-1),
                revokedBy: actor));
    }

    [Fact]
    public async Task Concurrent_role_changes_produce_one_event_per_version()
    {
        var tenant = new TenantId(Guid.NewGuid());
        var directoryObjectId = Guid.NewGuid();
        var tenants = new TenantContextAccessor();
        var events = new InMemoryEventStore(tenants);
        var auditor = EvidenceAuditor(
            tenants,
            events,
            out var roleSink,
            out var roleProjection);
        var personKeys = new InMemoryPersonKeyMap(
            tenants,
            auditor,
            events,
            TimeProvider.System);
        var store = new InMemoryRoleAssignmentStore(
            tenants,
            events);
        var service = new RoleAssignmentService(
            personKeys,
            store,
            tenants,
            TimeProvider.System);
        var assigner = AuditActor.System("concurrent-assigner");
        var revoker = AuditActor.System("concurrent-revoker");

        using var _ = tenants.BeginScope(tenant);
        var assignmentIds = await Task.WhenAll(
            Enumerable.Range(0, 32)
                .Select(_ => service.AssignAsync(
                    directoryObjectId,
                    ControlTowerRole.Administrator,
                    assigner)));
        var assignmentId = Assert.Single(
            assignmentIds.Distinct());
        var assigned = await store.GetAsync(assignmentId);
        Assert.NotNull(assigned);
        Assert.True(assigned!.IsActive);
        Assert.Equal(1, assigned.Version);

        var assignedEvents = (await events.ReadAllAsync())
            .Where(stored =>
                stored.EventType == "RoleAssignmentChanged")
            .ToList();
        Assert.Single(assignedEvents);
        var assignedPayload =
            JsonSerializer.Deserialize<RoleAssignmentChanged>(
                assignedEvents[0].Payload);
        Assert.Equal(assigner, assignedPayload!.ChangedBy);
        Assert.Equal(1, assignedPayload.Version);

        await Task.WhenAll(
            Enumerable.Range(0, 32)
                .Select(_ => service.RevokeAsync(
                    assignmentId,
                    revoker)));
        var revoked = await store.GetAsync(assignmentId);
        Assert.NotNull(revoked);
        Assert.False(revoked!.IsActive);
        Assert.Equal(2, revoked.Version);

        var roleEvents = (await events.ReadAllAsync())
            .Where(stored =>
                stored.EventType == "RoleAssignmentChanged")
            .ToList();
        Assert.Equal(2, roleEvents.Count);
        Assert.Equal(
            [1L, 2L],
            roleEvents
                .Select(stored =>
                    JsonSerializer
                        .Deserialize<RoleAssignmentChanged>(
                            stored.Payload)!.Version)
                .Order()
                .ToArray());

        var staleEvent = new RoleAssignmentChanged
        {
            AssignmentId = revoked.Id,
            SubjectPersonKey = revoked.SubjectPersonKey,
            Role = ControlTowerAccessCatalog.Name(
                revoked.Role),
            OrganizationScope =
                revoked.OrganizationScope.ToString(),
            Change = "Revoked",
            ChangedBy = revoker,
            Version = revoked.Version,
            OccurredAt = revoked.RevokedAt!.Value,
        };
        var stale = await store.CommitAsync(
            revoked,
            staleEvent,
            new EventAppendMetadata(
                EventReference.For(
                    "role-assignment",
                    revoked.Id),
                revoker,
                correlationReference:
                    EventReference.For(
                        "role-assignment-command",
                        Guid.NewGuid())),
            expectedVersion: 1);
        Assert.Equal(
            RoleAssignmentCommitStatus.Conflict,
            stale.Status);
        Assert.Equal(revoked, stale.Authoritative);
        Assert.Equal(
            2,
            (await events.ReadAllAsync())
                .Count(stored =>
                    stored.EventType
                    == "RoleAssignmentChanged"));
    }

    private static PersonKeyAccessContext Access(
        string purpose) =>
        new(
            AuditActor.System("c8-contract-test"),
            purpose,
            EventReference.For(
                "test-correlation",
                Guid.NewGuid()),
            PrivilegedReadPolicy.NotApplicable());

    private static IPrivilegedReadAuditor EvidenceAuditor(
        TenantContextAccessor tenants,
        IEventStore events,
        out InMemoryPrivilegedReadAuditor sink,
        out InMemoryPrivilegedAccessProjection projection)
    {
        sink = new InMemoryPrivilegedReadAuditor();
        projection =
            new InMemoryPrivilegedAccessProjection(tenants);
        return new PrivilegedReadEvidenceAuditor(
            sink,
            projection,
            events,
            tenants);
    }

    private sealed class ToggleAuditor : IPrivilegedReadAuditor
    {
        public bool Fail { get; set; }

        public List<PrivilegedReadRecord> Records { get; } = [];

        public ValueTask RecordAsync(
            PrivilegedReadRecord record,
            CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (Fail)
            {
                return ValueTask.FromException(
                    new InvalidOperationException(
                        "Synthetic privileged-audit failure."));
            }

            Records.Add(record);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ToggleEventStore(IEventStore inner)
        : IEventStore
    {
        public string? FailedEventType { get; set; }

        public ValueTask<StoredEvent> AppendAsync(
            IDomainEvent @event,
            EventAppendMetadata metadata,
            ReadOnlyMemory<byte> payload,
            CancellationToken ct = default) =>
            string.Equals(
                FailedEventType,
                @event.GetType().Name,
                StringComparison.Ordinal)
                ? ValueTask.FromException<StoredEvent>(
                    new InvalidOperationException(
                        "Synthetic event-store failure."))
                : inner.AppendAsync(
                    @event,
                    metadata,
                    payload,
                    ct);

        public ValueTask<IReadOnlyList<StoredEvent>>
            ReadAllAsync(
                CancellationToken ct = default) =>
            inner.ReadAllAsync(ct);
    }
}
