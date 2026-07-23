using System.Globalization;
using System.Text;
using ControlTower.Adapters.InMemory;
using ControlTower.Platform.Audit;
using ControlTower.Platform.Events;
using ControlTower.Platform.Tenancy;

namespace ControlTower.Platform.Tests;

public class EventBackboneTests
{
    private static readonly TenantId Tenant = new(Guid.NewGuid());
    private static readonly DateTimeOffset FixedNow =
        new(2026, 7, 23, 9, 10, 11, 123, TimeSpan.Zero);

    [Fact]
    public void Hash_chain_is_deterministic_and_depends_on_previous_and_envelope()
    {
        var chain = new Sha256HashChain();
        var envelope = Payload("canonical");
        var first = chain.ComputeNext(Sha256HashChain.Genesis, envelope);
        var same = chain.ComputeNext(Sha256HashChain.Genesis, envelope);
        var changedEnvelope = chain.ComputeNext(
            Sha256HashChain.Genesis,
            Payload("different"));
        var chained = chain.ComputeNext(first, envelope);

        Assert.Equal(first, same);
        Assert.NotEqual(first, changedEnvelope);
        Assert.NotEqual(first, chained);
        Assert.True(Sha256HashChain.IsCanonicalHash(first));
    }

    [Fact]
    public async Task Append_owns_metadata_assigns_positions_and_normalizes_timestamps()
    {
        var occurredAt = new DateTimeOffset(
            2026, 7, 23, 14, 40, 11, 123, TimeSpan.FromHours(5.5))
            .AddTicks(7);
        var rig = BuildRig(FixedNow.AddTicks(9));
        var source = Payload("one");

        StoredEvent first;
        StoredEvent second;
        using (rig.Tenants.BeginScope(Tenant))
        {
            first = await rig.Store.AppendAsync(
                new TestEvent(Guid.NewGuid(), occurredAt),
                source);
            second = await rig.Store.AppendAsync(
                new TestEvent(Guid.NewGuid(), occurredAt.AddSeconds(1)),
                Payload("two"));
        }

        Assert.Equal(1, first.IntegrityFormatVersion);
        Assert.Equal(1, first.Position);
        Assert.Equal(2, second.Position);
        Assert.Equal(nameof(TestEvent), first.EventType);
        Assert.Equal(EventPrivilege.Standard, first.Privilege);
        Assert.Equal(TimeSpan.Zero, first.OccurredAt.Offset);
        Assert.Equal(0, first.OccurredAt.Ticks % TimeSpan.TicksPerMicrosecond);
        Assert.Equal(TimeSpan.Zero, first.RecordedAt.Offset);
        Assert.Equal(0, first.RecordedAt.Ticks % TimeSpan.TicksPerMicrosecond);
        Assert.Equal(
            EventEnvelopeCanonicalizer.NormalizeTimestamp(occurredAt),
            first.OccurredAt);
        Assert.Equal(
            EventEnvelopeCanonicalizer.NormalizeTimestamp(
                FixedNow.AddTicks(9)),
            first.RecordedAt);
        Assert.Equal(Sha256HashChain.Genesis, first.PreviousHash);
        Assert.Equal(first.Hash, second.PreviousHash);
        Assert.Equal(2, rig.Clock.GetUtcNowCalls);
    }

    [Fact]
    public async Task Append_snapshots_event_identity_and_occurrence_exactly_once()
    {
        var rig = BuildRig();
        var source = new CountingEvent(Guid.NewGuid(), FixedNow);

        using (rig.Tenants.BeginScope(Tenant))
            await rig.Store.AppendAsync(source, Payload("event"));

        Assert.Equal(1, source.EventIdReads);
        Assert.Equal(1, source.OccurredAtReads);
    }

    [Fact]
    public void Canonical_envelope_is_deterministic_across_equivalent_offsets()
    {
        var instant = new DateTimeOffset(
            2026, 7, 23, 14, 40, 11, 123, TimeSpan.FromHours(5.5));
        var first = Stored(
            occurredAt: EventEnvelopeCanonicalizer.NormalizeTimestamp(instant));
        var sameInstant = first with
        {
            OccurredAt = EventEnvelopeCanonicalizer.NormalizeTimestamp(
                instant.ToOffset(TimeSpan.FromHours(-7))),
        };
        var roundTripped = first with
        {
            OccurredAt = EventEnvelopeCanonicalizer.FromUnixMicroseconds(
                EventEnvelopeCanonicalizer.ToUnixMicroseconds(
                    first.OccurredAt)),
            RecordedAt = EventEnvelopeCanonicalizer.FromUnixMicroseconds(
                EventEnvelopeCanonicalizer.ToUnixMicroseconds(
                    first.RecordedAt)),
        };
        var canonical = EventEnvelopeCanonicalizer.Canonicalize(first);
        var chain = new Sha256HashChain();

        Assert.Equal(
            canonical,
            EventEnvelopeCanonicalizer.Canonicalize(sameInstant));
        Assert.Equal(canonical, EventEnvelopeCanonicalizer.Canonicalize(roundTripped));
        Assert.Equal(
            chain.ComputeNext(Sha256HashChain.Genesis, canonical),
            chain.ComputeNext(
                Sha256HashChain.Genesis,
                EventEnvelopeCanonicalizer.Canonicalize(roundTripped)));
    }

    [Fact]
    public void Canonical_envelope_matches_the_cross_platform_format_vector()
    {
        var stored = new StoredEvent(
            EventEnvelopeCanonicalizer.CurrentIntegrityFormatVersion,
            1,
            Guid.Parse("ffeeddcc-bbaa-9988-7766-554433221100"),
            nameof(TestEvent),
            DateTimeOffset.UnixEpoch.AddTicks(12_345_670),
            DateTimeOffset.UnixEpoch.AddTicks(-9_999_990),
            new TenantId(
                Guid.Parse("00112233-4455-6677-8899-aabbccddeeff")),
            EventPrivilege.Standard,
            Sha256HashChain.Genesis,
            string.Empty,
            new byte[] { 0x00, 0xFF, 0x41 });

        var canonical = EventEnvelopeCanonicalizer.Canonicalize(stored);
        var hash = new Sha256HashChain().ComputeNext(
            Sha256HashChain.Genesis,
            canonical);

        Assert.Equal(
            "00000001000000000000000100112233445566778899AABBCCDDEEFF"
            + "FFEEDDCCBBAA9988776655443322110000000009546573744576656E74"
            + "000000000012D687FFFFFFFFFFF0BDC1000000000300FF41",
            Convert.ToHexString(canonical));
        Assert.Equal(
            "02CDD29DAEDCD9FF308F08908EC45762FF1FC0105725F8E935A7883AF890FA5F",
            hash);
        Assert.Equal(
            "A142C4BF8F975191F476817B20B9186081B54F489EE907D0AA4FCF2D64EE5E80",
            new Sha256HashChain().ComputeNext(hash, canonical));
    }

    [Fact]
    public void Canonical_envelope_is_culture_independent()
    {
        var stored = Stored();
        var expected = EventEnvelopeCanonicalizer.Canonicalize(stored);
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ar-SA");
            CultureInfo.CurrentUICulture =
                CultureInfo.GetCultureInfo("tr-TR");

            Assert.Equal(
                expected,
                EventEnvelopeCanonicalizer.Canonicalize(stored));
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    [Fact]
    public void Canonical_envelope_rejects_sub_microsecond_timestamps()
    {
        var stored = Stored() with
        {
            OccurredAt = Stored().OccurredAt.AddTicks(1),
        };

        Assert.Throws<EventIntegrityException>(
            () => EventEnvelopeCanonicalizer.Canonicalize(stored));
    }

    [Fact]
    public async Task Verifier_distinguishes_unanchored_and_checkpoint_bound_integrity()
    {
        var stream = await BuildStreamAsync(5);
        var verifier = new HashChainVerifier(new Sha256HashChain());

        var unanchored = verifier.Verify(Tenant, stream);
        var checkpointBound = verifier.Verify(
            Tenant,
            stream,
            EventStreamCheckpoint.From(stream[^1]));

        Assert.True(unanchored.IsIntact);
        Assert.False(unanchored.IsCheckpointBound);
        Assert.Equal(
            ChainVerificationAssurance.InternallyIntactUnanchored,
            unanchored.Assurance);
        Assert.True(checkpointBound.IsIntact);
        Assert.True(checkpointBound.IsCheckpointBound);
        Assert.Equal(
            ChainVerificationAssurance.TrustedCheckpointBound,
            checkpointBound.Assurance);
    }

    [Fact]
    public async Task Trusted_checkpoint_detects_suffix_loss_that_an_unanchored_prefix_cannot()
    {
        var stream = await BuildStreamAsync(5);
        var checkpoint = EventStreamCheckpoint.From(stream[^1]);
        var prefix = stream.Take(3).ToList();
        var verifier = new HashChainVerifier(new Sha256HashChain());

        var unanchored = verifier.Verify(Tenant, prefix);
        var checkpointBound = verifier.Verify(Tenant, prefix, checkpoint);

        Assert.True(unanchored.IsIntact);
        Assert.False(unanchored.IsCheckpointBound);
        Assert.False(checkpointBound.IsIntact);
        Assert.Equal(4, checkpointBound.FirstBrokenPosition);
        Assert.Equal(
            ChainVerificationAssurance.Broken,
            checkpointBound.Assurance);
    }

    [Fact]
    public async Task Trusted_checkpoint_detects_an_internally_valid_substituted_tail()
    {
        var original = await BuildStreamAsync(3);
        var checkpoint = EventStreamCheckpoint.From(original[^1]);
        var substituted = original.ToList();
        var prospective = substituted[^1] with
        {
            Payload = Payload("substituted"),
            Hash = string.Empty,
        };
        substituted[^1] = prospective with
        {
            Hash = new Sha256HashChain().ComputeNext(
                prospective.PreviousHash,
                EventEnvelopeCanonicalizer.Canonicalize(prospective)),
        };
        var verifier = new HashChainVerifier(new Sha256HashChain());

        Assert.True(verifier.Verify(Tenant, substituted).IsIntact);
        var bound = verifier.Verify(Tenant, substituted, checkpoint);
        Assert.False(bound.IsIntact);
        Assert.Equal(3, bound.FirstBrokenPosition);
    }

    [Fact]
    public async Task Every_persisted_integrity_field_is_tamper_evident()
    {
        var original = await BuildStreamAsync(3);
        var mutations = new (string Field, Func<StoredEvent, StoredEvent> Apply)[]
        {
            ("zero format", value => value with { IntegrityFormatVersion = 0 }),
            ("format", value => value with { IntegrityFormatVersion = 99 }),
            ("position", value => value with { Position = 9 }),
            ("tenant", value => value with { Tenant = new TenantId(Guid.NewGuid()) }),
            ("event ID", value => value with { EventId = Guid.NewGuid() }),
            ("event type", value => value with { EventType = "TamperedEvent" }),
            ("occurred", value => value with { OccurredAt = value.OccurredAt.AddTicks(10) }),
            ("recorded", value => value with { RecordedAt = value.RecordedAt.AddTicks(10) }),
            ("privilege", value => value with { Privilege = EventPrivilege.Privileged }),
            ("previous hash", value => value with { PreviousHash = new string('A', 64) }),
            ("hash", value => value with { Hash = new string('A', 64) }),
            ("payload", value => value with { Payload = Payload("tampered") }),
        };
        var verifier = new HashChainVerifier(new Sha256HashChain());

        foreach (var mutation in mutations)
        {
            var stream = original.ToList();
            stream[1] = mutation.Apply(stream[1]);
            var result = verifier.Verify(Tenant, stream);
            Assert.False(result.IsIntact);
            Assert.Equal(2, result.FirstBrokenPosition);
            Assert.Equal(
                ChainVerificationAssurance.Broken,
                result.Assurance);
        }
    }

    [Fact]
    public async Task Structural_reorder_duplicate_and_interior_removal_fail_at_first_gap()
    {
        var original = await BuildStreamAsync(3);
        var verifier = new HashChainVerifier(new Sha256HashChain());
        var malformed = new (StoredEvent[] Stream, long FirstBrokenPosition)[]
        {
            (new[] { original[0], original[2], original[1] }, 2),
            (new[] { original[0], original[1], original[1] }, 3),
            (new[] { original[0], original[2] }, 2),
        };

        foreach (var malformedStream in malformed)
        {
            var result = verifier.Verify(Tenant, malformedStream.Stream);
            Assert.False(result.IsIntact);
            Assert.Equal(
                malformedStream.FirstBrokenPosition,
                result.FirstBrokenPosition);
        }
    }

    [Fact]
    public async Task Append_and_read_never_expose_mutable_payload_ownership()
    {
        var rig = BuildRig();
        var source = Payload("original");
        StoredEvent appended;
        using (rig.Tenants.BeginScope(Tenant))
        {
            appended = await rig.Store.AppendAsync(
                NewEvent(),
                source);
        }

        source[0] = (byte)'X';
        var appendResult = appended.Payload;
        appendResult[1] = (byte)'X';

        StoredEvent retained;
        using (rig.Tenants.BeginScope(Tenant))
            retained = Assert.Single(await rig.Store.ReadAllAsync());
        var readResult = retained.Payload;
        readResult[2] = (byte)'X';

        Assert.Equal("original", Encoding.UTF8.GetString(appended.Payload));
        Assert.Equal("original", Encoding.UTF8.GetString(retained.Payload));
        using (rig.Tenants.BeginScope(Tenant))
        {
            Assert.Equal(
                "original",
                Encoding.UTF8.GetString(
                    Assert.Single(await rig.Store.ReadAllAsync()).Payload));
        }
    }

    [Fact]
    public async Task Duplicate_event_ID_is_rejected_before_stream_mutation()
    {
        var rig = BuildRig();
        var duplicate = NewEvent();
        using (rig.Tenants.BeginScope(Tenant))
        {
            await rig.Store.AppendAsync(duplicate, Payload("first"));
            await Assert.ThrowsAsync<EventIntegrityException>(
                async () => await rig.Store.AppendAsync(
                    duplicate,
                    Payload("second")));
            Assert.Single(await rig.Store.ReadAllAsync());
        }
    }

    [Fact]
    public async Task Missing_or_invalid_event_contract_fails_before_stream_mutation()
    {
        var rig = BuildRig();
        using (rig.Tenants.BeginScope(Tenant))
        {
            await Assert.ThrowsAsync<EventIntegrityException>(
                async () => await rig.Store.AppendAsync(
                    new UndeclaredEvent(Guid.NewGuid(), FixedNow),
                    Payload("missing")));
            await Assert.ThrowsAsync<EventIntegrityException>(
                async () => await rig.Store.AppendAsync(
                    new InvalidContractEvent(Guid.NewGuid(), FixedNow),
                    Payload("invalid")));
            await Assert.ThrowsAsync<EventIntegrityException>(
                async () => await rig.Store.AppendAsync(
                    new UnboundedContractEvent(Guid.NewGuid(), FixedNow),
                    Payload("unbounded")));
            await Assert.ThrowsAsync<EventIntegrityException>(
                async () => await rig.Store.AppendAsync(
                    new InvalidPrivilegeEvent(Guid.NewGuid(), FixedNow),
                    Payload("privilege")));
            await Assert.ThrowsAsync<EventIntegrityException>(
                async () => await rig.Store.AppendAsync(
                    new TestEvent(Guid.Empty, FixedNow),
                    Payload("empty-id")));
            Assert.Empty(await rig.Store.ReadAllAsync());
        }
    }

    [Fact]
    public async Task Event_ID_uniqueness_is_global_while_hash_chains_are_tenant_scoped()
    {
        var rig = BuildRig();
        var firstEvent = NewEvent();
        var secondEvent = NewEvent();
        StoredEvent tenantA;
        StoredEvent tenantB;
        var otherTenant = new TenantId(Guid.NewGuid());

        using (rig.Tenants.BeginScope(Tenant))
            tenantA = await rig.Store.AppendAsync(firstEvent, Payload("A"));
        using (rig.Tenants.BeginScope(otherTenant))
        {
            tenantB = await rig.Store.AppendAsync(secondEvent, Payload("B"));
            await Assert.ThrowsAsync<EventIntegrityException>(
                async () => await rig.Store.AppendAsync(
                    firstEvent,
                    Payload("duplicate")));
            Assert.Single(await rig.Store.ReadAllAsync());
        }

        Assert.Equal(1, tenantA.Position);
        Assert.Equal(1, tenantB.Position);
        Assert.Equal(Sha256HashChain.Genesis, tenantA.PreviousHash);
        Assert.Equal(Sha256HashChain.Genesis, tenantB.PreviousHash);
        Assert.NotEqual(tenantA.Hash, tenantB.Hash);
        var verifier = new HashChainVerifier(new Sha256HashChain());
        Assert.True(verifier.Verify(Tenant, [tenantA]).IsIntact);
        Assert.True(verifier.Verify(otherTenant, [tenantB]).IsIntact);
    }

    [Fact]
    public void Empty_stream_is_unanchored_unless_bound_to_an_empty_checkpoint()
    {
        var verifier = new HashChainVerifier(new Sha256HashChain());
        var unanchored = verifier.Verify(Tenant, []);
        var bound = verifier.Verify(
            Tenant,
            [],
            new EventStreamCheckpoint(
                EventEnvelopeCanonicalizer.CurrentIntegrityFormatVersion,
                Tenant,
                0,
                Sha256HashChain.Genesis));
        var foreignExpectedTenant = verifier.Verify(
            new TenantId(Guid.NewGuid()),
            [],
            new EventStreamCheckpoint(
                EventEnvelopeCanonicalizer.CurrentIntegrityFormatVersion,
                Tenant,
                0,
                Sha256HashChain.Genesis));

        Assert.True(unanchored.IsIntact);
        Assert.False(unanchored.IsCheckpointBound);
        Assert.True(bound.IsIntact);
        Assert.True(bound.IsCheckpointBound);
        Assert.False(foreignExpectedTenant.IsIntact);
    }

    [Fact]
    public async Task Invalid_or_foreign_checkpoint_fails_closed()
    {
        var stream = await BuildStreamAsync(2);
        var verifier = new HashChainVerifier(new Sha256HashChain());
        var valid = EventStreamCheckpoint.From(stream[^1]);

        var invalidVersion = verifier.Verify(
            Tenant,
            stream,
            valid with { IntegrityFormatVersion = 99 });
        var foreignTenant = verifier.Verify(
            Tenant,
            stream,
            valid with { Tenant = new TenantId(Guid.NewGuid()) });
        var substitutedTail = verifier.Verify(
            Tenant,
            stream,
            valid with { Hash = new string('A', 64) });

        Assert.False(invalidVersion.IsIntact);
        Assert.False(foreignTenant.IsIntact);
        Assert.False(substitutedTail.IsIntact);
    }

    [Fact]
    public async Task Outbox_drains_in_order_and_acknowledged_messages_do_not_reappear()
    {
        var outbox = new InMemoryOutbox();
        await outbox.EnqueueAsync("assets", Payload("a"));
        await outbox.EnqueueAsync("assets", Payload("b"));

        var batch = await outbox.DequeueBatchAsync(10);
        Assert.Equal(
            new long[] { 1, 2 },
            batch.Select(message => message.Position).ToArray());

        await outbox.AcknowledgeAsync(1);
        var remaining = await outbox.DequeueBatchAsync(10);
        Assert.Equal(
            new long[] { 2 },
            remaining.Select(message => message.Position).ToArray());
    }

    [Fact]
    public async Task Privileged_read_is_recorded()
    {
        var auditor = new InMemoryPrivilegedReadAuditor();
        await auditor.RecordAsync(new PrivilegedReadRecord(
            Tenant,
            "person:opaque",
            "AssetUsage",
            "asset-123",
            "quarterly-review",
            FixedNow));

        Assert.Single(auditor.Records);
        Assert.Equal("asset-123", auditor.Records[0].ResourceId);
    }

    [Fact]
    public void Event_store_contract_remains_append_only()
    {
        var methods = typeof(IEventStore)
            .GetMethods()
            .Select(method => method.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            new[] { nameof(IEventStore.AppendAsync), nameof(IEventStore.ReadAllAsync) },
            methods);
    }

    private static Rig BuildRig(DateTimeOffset? now = null)
    {
        var tenants = new TenantContextAccessor();
        var clock = new FixedTimeProvider(now ?? FixedNow);
        return new(
            tenants,
            clock,
            new InMemoryEventStore(
                tenants,
                clock,
                new Sha256HashChain()));
    }

    private static async Task<List<StoredEvent>> BuildStreamAsync(int count)
    {
        var rig = BuildRig();
        using (rig.Tenants.BeginScope(Tenant))
        {
            for (var index = 0; index < count; index++)
            {
                await rig.Store.AppendAsync(
                    NewEvent(FixedNow.AddSeconds(index)),
                    Payload($"event-{index}"));
            }
            return (await rig.Store.ReadAllAsync()).ToList();
        }
    }

    private static StoredEvent Stored(
        DateTimeOffset? occurredAt = null) =>
        new(
            EventEnvelopeCanonicalizer.CurrentIntegrityFormatVersion,
            1,
            Guid.Parse("11111111-2222-3333-4444-555555555555"),
            nameof(TestEvent),
            occurredAt
                ?? EventEnvelopeCanonicalizer.NormalizeTimestamp(FixedNow),
            EventEnvelopeCanonicalizer.NormalizeTimestamp(FixedNow),
            Tenant,
            EventPrivilege.Standard,
            Sha256HashChain.Genesis,
            string.Empty,
            Payload("payload"));

    private static TestEvent NewEvent(DateTimeOffset? occurredAt = null) =>
        new(
            Guid.NewGuid(),
            occurredAt ?? FixedNow);

    private static byte[] Payload(string value) =>
        Encoding.UTF8.GetBytes(value);

    private sealed record Rig(
        TenantContextAccessor Tenants,
        FixedTimeProvider Clock,
        InMemoryEventStore Store);

    private sealed class FixedTimeProvider(
        DateTimeOffset utcNow) : TimeProvider
    {
        public int GetUtcNowCalls { get; private set; }

        public override DateTimeOffset GetUtcNow()
        {
            GetUtcNowCalls++;
            return utcNow;
        }
    }

    [DomainEventContract(nameof(TestEvent), EventPrivilege.Standard)]
    private sealed record TestEvent(
        Guid EventId,
        DateTimeOffset OccurredAt) : IDomainEvent;

    [DomainEventContract(nameof(CountingEvent), EventPrivilege.Standard)]
    private sealed class CountingEvent(
        Guid eventId,
        DateTimeOffset occurredAt) : IDomainEvent
    {
        public int EventIdReads { get; private set; }
        public int OccurredAtReads { get; private set; }

        public Guid EventId
        {
            get
            {
                EventIdReads++;
                return eventId;
            }
        }

        public DateTimeOffset OccurredAt
        {
            get
            {
                OccurredAtReads++;
                return occurredAt;
            }
        }
    }

    private sealed record UndeclaredEvent(
        Guid EventId,
        DateTimeOffset OccurredAt) : IDomainEvent;

    [DomainEventContract("invalid event type", EventPrivilege.Standard)]
    private sealed record InvalidContractEvent(
        Guid EventId,
        DateTimeOffset OccurredAt) : IDomainEvent;

    [DomainEventContract(
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA",
        EventPrivilege.Standard)]
    private sealed record UnboundedContractEvent(
        Guid EventId,
        DateTimeOffset OccurredAt) : IDomainEvent;

    [DomainEventContract("InvalidPrivilegeEvent", (EventPrivilege)99)]
    private sealed record InvalidPrivilegeEvent(
        Guid EventId,
        DateTimeOffset OccurredAt) : IDomainEvent;

    private sealed class InMemoryPrivilegedReadAuditor : IPrivilegedReadAuditor
    {
        public List<PrivilegedReadRecord> Records { get; } = [];

        public ValueTask RecordAsync(
            PrivilegedReadRecord record,
            CancellationToken ct = default)
        {
            Records.Add(record);
            return ValueTask.CompletedTask;
        }
    }
}
