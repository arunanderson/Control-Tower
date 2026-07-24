using System.Globalization;
using System.Text;
using System.Text.Json;
using ControlTower.Adapters.InMemory;
using ControlTower.Platform.Audit;
using ControlTower.Platform.Events;
using ControlTower.Platform.Identity;
using ControlTower.Platform.Tenancy;
using Microsoft.Extensions.DependencyInjection;

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
                Metadata(),
                source);
            second = await rig.Store.AppendAsync(
                new TestEvent(Guid.NewGuid(), occurredAt.AddSeconds(1)),
                Metadata("aggregate-2"),
                Payload("two"));
        }

        Assert.Equal(2, first.IntegrityFormatVersion);
        Assert.Equal(1, first.Position);
        Assert.Equal(2, second.Position);
        Assert.Equal(nameof(TestEvent), first.EventType);
        Assert.Equal(
            new EventReference("test-aggregate", "aggregate-1"),
            first.AggregateReference);
        Assert.Equal(AuditActor.System("platform-test"), first.Actor);
        Assert.Equal("event-backbone-test", first.Reason);
        Assert.Equal(
            new EventReference("request", "request-1"),
            first.CorrelationReference);
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
        {
            await rig.Store.AppendAsync(
                source,
                Metadata(),
                Payload("event"));
        }

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
    public void Optional_metadata_presence_has_distinct_canonical_bytes_and_hashes()
    {
        var absent = Stored() with
        {
            Reason = null,
            CorrelationReference = null,
        };
        var reasonPresent = absent with
        {
            Reason = "r",
        };
        var correlationPresent = absent with
        {
            CorrelationReference =
                new EventReference("request", "r"),
        };
        var bothPresent = reasonPresent with
        {
            CorrelationReference =
                new EventReference("request", "r"),
        };
        var values = new[]
        {
            absent,
            reasonPresent,
            correlationPresent,
            bothPresent,
        };
        var canonical = values
            .Select(EventEnvelopeCanonicalizer.Canonicalize)
            .Select(bytes => Convert.ToHexString(bytes))
            .ToArray();
        var chain = new Sha256HashChain();
        var hashes = values
            .Select(value => chain.ComputeNext(
                Sha256HashChain.Genesis,
                EventEnvelopeCanonicalizer.Canonicalize(value)))
            .ToArray();

        Assert.Equal(values.Length, canonical.Distinct().Count());
        Assert.Equal(values.Length, hashes.Distinct().Count());
    }

    [Fact]
    public void Canonical_envelope_matches_the_cross_platform_format_vector()
    {
        var stored = new StoredEvent(
            EventEnvelopeCanonicalizer.CurrentIntegrityFormatVersion,
            1,
            Guid.Parse("ffeeddcc-bbaa-9988-7766-554433221100"),
            nameof(TestEvent),
            new EventReference("test-aggregate", "aggregate-001"),
            AuditActor.Provider("provider-01"),
            DateTimeOffset.UnixEpoch.AddTicks(12_345_670),
            DateTimeOffset.UnixEpoch.AddTicks(-9_999_990),
            "why",
            new EventReference("request", "abc-123"),
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
        var chained = new Sha256HashChain().ComputeNext(hash, canonical);

        Assert.Equal(
            "00000002000000000000000100112233445566778899AABBCCDDEEFF"
            + "FFEEDDCCBBAA9988776655443322110000000009546573744576656E74"
            + "0000000E746573742D616767726567617465"
            + "0000000D6167677265676174652D303031"
            + "030000000B70726F76696465722D3031"
            + "000000000012D687FFFFFFFFFFF0BDC1"
            + "0100000003776879"
            + "010000000772657175657374000000076162632D313233"
            + "000000000300FF41",
            Convert.ToHexString(canonical));
        Assert.Equal(
            "A3490E9C641477F7869E10F8A01710F4E1F51E11F0A9BA86EA27C79F0F8FAA5B",
            hash);
        Assert.Equal(
            "A2B40DB499E3559CEC0BE68F161D29ED524BCE0F5C82510D869D1D5C7D48087C",
            chained);
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
            ("legacy format", value => value with { IntegrityFormatVersion = 1 }),
            ("format", value => value with { IntegrityFormatVersion = 99 }),
            ("position", value => value with { Position = 9 }),
            ("tenant", value => value with { Tenant = new TenantId(Guid.NewGuid()) }),
            ("event ID", value => value with { EventId = Guid.NewGuid() }),
            ("event type", value => value with { EventType = "TamperedEvent" }),
            ("aggregate", value => value with
            {
                AggregateReference =
                    new EventReference("test-aggregate", "tampered"),
            }),
            ("actor", value => value with
            {
                Actor = AuditActor.System("tampered"),
            }),
            ("occurred", value => value with { OccurredAt = value.OccurredAt.AddTicks(10) }),
            ("recorded", value => value with { RecordedAt = value.RecordedAt.AddTicks(10) }),
            ("reason", value => value with { Reason = "tampered" }),
            ("correlation", value => value with
            {
                CorrelationReference =
                    new EventReference("request", "tampered"),
            }),
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
                Metadata(),
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
            await rig.Store.AppendAsync(
                duplicate,
                Metadata(),
                Payload("first"));
            await Assert.ThrowsAsync<EventIntegrityException>(
                async () => await rig.Store.AppendAsync(
                    duplicate,
                    Metadata(),
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
                    Metadata(),
                    Payload("missing")));
            await Assert.ThrowsAsync<EventIntegrityException>(
                async () => await rig.Store.AppendAsync(
                    new InvalidContractEvent(Guid.NewGuid(), FixedNow),
                    Metadata(),
                    Payload("invalid")));
            await Assert.ThrowsAsync<EventIntegrityException>(
                async () => await rig.Store.AppendAsync(
                    new UnboundedContractEvent(Guid.NewGuid(), FixedNow),
                    Metadata(),
                    Payload("unbounded")));
            await Assert.ThrowsAsync<EventIntegrityException>(
                async () => await rig.Store.AppendAsync(
                    new InvalidPrivilegeEvent(Guid.NewGuid(), FixedNow),
                    Metadata(),
                    Payload("privilege")));
            await Assert.ThrowsAsync<EventIntegrityException>(
                async () => await rig.Store.AppendAsync(
                    new TestEvent(Guid.Empty, FixedNow),
                    Metadata(),
                    Payload("empty-id")));
            Assert.Empty(await rig.Store.ReadAllAsync());
        }
    }

    [Fact]
    public async Task Append_requires_valid_v2_metadata_before_stream_mutation()
    {
        var rig = BuildRig();

        Assert.Throws<ArgumentException>(
            () => new EventAppendMetadata(
                default,
                AuditActor.System("platform-test")));
        Assert.Throws<ArgumentException>(
            () => new EventAppendMetadata(
                new EventReference("test-aggregate", "aggregate-1"),
                default));
        Assert.Throws<ArgumentException>(
            () => new EventAppendMetadata(
                new EventReference("test-aggregate", "aggregate-1"),
                AuditActor.System("platform-test"),
                "\r\n"));
        Assert.Throws<ArgumentException>(
            () => new EventReference(
                "test-aggregate",
                "\uD800"));
        Assert.Throws<ArgumentException>(
            () => new EventReference(
                "test-aggregate",
                " aggregate-1 "));
        Assert.Throws<ArgumentException>(
            () => AuditActor.System(
                " platform-test "));
        Assert.DoesNotContain(
            typeof(AuditActor).GetConstructors(),
            constructor => constructor.GetParameters()
                .Select(parameter => parameter.ParameterType)
                .SequenceEqual(
                [
                    typeof(AuditActorKind),
                    typeof(string),
                ]));
        Assert.Throws<ArgumentException>(
            () => new AuditActor(
                AuditActorKind.Human,
                personKey: null,
                workloadId: Guid.NewGuid().ToString("D")));
        Assert.Throws<ArgumentException>(
            () => new AuditActor(
                AuditActorKind.Human,
                new PersonKey(Guid.NewGuid()),
                "raw-identity"));
        var rawDirectoryId = Guid.NewGuid();
        foreach (var invalidWorkloadActor in new[]
                 {
                     rawDirectoryId.ToString("D"),
                     rawDirectoryId.ToString("N"),
                     $"entra:tenant:{rawDirectoryId:D}",
                     "person@example.com",
                 })
        {
            Assert.Throws<ArgumentException>(
                () => AuditActor.System(
                    invalidWorkloadActor));
            Assert.Throws<ArgumentException>(
                () => AuditActor.Provider(
                    invalidWorkloadActor));
        }
        Assert.Throws<ArgumentException>(
            () => new EventAppendMetadata(
                new EventReference("test-aggregate", "aggregate-1"),
                AuditActor.System("platform-test"),
                "\uD800"));
        Assert.Throws<ArgumentException>(
            () => new EventAppendMetadata(
                new EventReference("test-aggregate", "aggregate-1"),
                AuditActor.System("platform-test"),
                " padded reason "));
        Assert.Throws<ArgumentException>(
            () => new EventAppendMetadata(
                new EventReference("test-aggregate", "aggregate-1"),
                AuditActor.System("platform-test"),
                correlationReference: default(EventReference)));
        Assert.True(
            new EventReference(
                "test-aggregate",
                "valid-\U0001F680")
                .IsValid);
        Assert.NotNull(
            new EventAppendMetadata(
                new EventReference("test-aggregate", "aggregate-1"),
                AuditActor.System("platform-test"),
                "valid \U0001F680"));

        using (rig.Tenants.BeginScope(Tenant))
        {
            await Assert.ThrowsAsync<ArgumentNullException>(
                async () => await rig.Store.AppendAsync(
                    NewEvent(),
                    null!,
                    Payload("missing-metadata")));
            Assert.Empty(await rig.Store.ReadAllAsync());
        }
    }

    [Fact]
    public void Event_metadata_boundaries_are_exact()
    {
        Assert.True(
            AuditActor.System(
                    new string('s', 128))
                .IsValid);
        Assert.True(
            AuditActor.Provider(
                    new string('p', 128))
                .IsValid);
        Assert.Throws<ArgumentException>(
            () => AuditActor.System(
                new string('s', 129)));
        Assert.Throws<ArgumentException>(
            () => AuditActor.Provider(
                new string('p', 129)));
        Assert.True(
            new EventReference(
                new string('a', 64),
                new string('v', 256))
                .IsValid);
        Assert.Throws<ArgumentException>(
            () => new EventReference(
                new string('a', 65),
                "value"));
        Assert.Throws<ArgumentException>(
            () => new EventReference(
                "reference",
                new string('v', 257)));

        Assert.Equal(
            2048,
            new EventAppendMetadata(
                new EventReference("aggregate", "value"),
                AuditActor.System("platform-test"),
                new string('r', 2048))
                .Reason!
                .Length);
        Assert.Throws<ArgumentException>(
            () => new EventAppendMetadata(
                new EventReference("aggregate", "value"),
                AuditActor.System("platform-test"),
                new string('r', 2049)));
    }

    [Fact]
    public void Audit_actor_json_round_trips_the_typed_identity_shape()
    {
        var actors = new[]
        {
            AuditActor.Person(
                new PersonKey(Guid.NewGuid())),
            AuditActor.System("platform-test"),
            AuditActor.Provider(
                "provider:custom/surface"),
        };

        foreach (var actor in actors)
        {
            var json = JsonSerializer.Serialize(actor);
            var roundTripped =
                JsonSerializer.Deserialize<AuditActor>(
                    json);

            Assert.Equal(actor, roundTripped);
            Assert.DoesNotContain(
                nameof(AuditActor.OpaqueId),
                json,
                StringComparison.Ordinal);
            Assert.DoesNotContain(
                nameof(AuditActor.IsValid),
                json,
                StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Canonical_envelope_rejects_invalid_v2_metadata()
    {
        var stored = Stored();
        var malformed = new[]
        {
            stored with { AggregateReference = default },
            stored with { Actor = default },
            stored with { Reason = "\n" },
            stored with { Reason = "\uD800" },
            stored with
            {
                CorrelationReference = default(EventReference),
            },
        };

        foreach (var value in malformed)
        {
            Assert.Throws<EventIntegrityException>(
                () => EventEnvelopeCanonicalizer.Canonicalize(value));
        }

        Assert.NotEmpty(
            EventEnvelopeCanonicalizer.Canonicalize(
                stored with { Reason = "\uFFFD" }));
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
        {
            tenantA = await rig.Store.AppendAsync(
                firstEvent,
                Metadata(),
                Payload("A"));
        }
        using (rig.Tenants.BeginScope(otherTenant))
        {
            tenantB = await rig.Store.AppendAsync(
                secondEvent,
                Metadata(),
                Payload("B"));
            await Assert.ThrowsAsync<EventIntegrityException>(
                async () => await rig.Store.AppendAsync(
                    firstEvent,
                    Metadata(),
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
        var auditor = new CapturingPrivilegedReadAuditor();
        var accessId =
            Guid.Parse("11111111-2222-3333-4444-555555555555");
        var actor = AuditActor.System("platform-test");
        var resource =
            new EventReference("asset-usage", "asset-123");
        var policy = PrivilegedReadPolicy.Applied(
            new EventReference("policy-version", "policy-7"));
        var correlation =
            new EventReference("request", "request-1");
        var expected = new PrivilegedReadRecord(
            accessId,
            Tenant,
            actor,
            resource,
            "quarterly-review",
            policy,
            correlation,
            FixedNow);

        await auditor.RecordAsync(expected);

        var actual = Assert.Single(auditor.Records);
        Assert.Same(expected, actual);
        Assert.Equal(accessId, actual.AccessId);
        Assert.Equal(Tenant, actual.Tenant);
        Assert.Equal(actor, actual.Actor);
        Assert.Equal(resource, actual.Resource);
        Assert.Equal("quarterly-review", actual.Purpose);
        Assert.Equal(policy, actual.Policy);
        Assert.Equal(
            new EventReference("policy-version", "policy-7"),
            actual.Policy.Version);
        Assert.Equal(correlation, actual.CorrelationReference);
        Assert.Equal(FixedNow, actual.OccurredAt);
    }

    [Fact]
    public async Task Raw_in_memory_privileged_auditor_fails_closed()
    {
        var services = new ServiceCollection();
        services.AddInMemoryAdapters();
        var registration = Assert.Single(
            services,
            descriptor =>
                descriptor.ServiceType
                == typeof(IPrivilegedReadAuditor));
        Assert.Equal(
            typeof(ControlTower.Adapters.InMemory
                .InMemoryPrivilegedReadAuditor),
            registration.ImplementationType);
        var adapter =
            new ControlTower.Adapters.InMemory
                .InMemoryPrivilegedReadAuditor();
        var record = new PrivilegedReadRecord(
            Guid.NewGuid(),
            Tenant,
            AuditActor.System("platform-test"),
            new EventReference("asset-usage", "asset-123"),
            "quarterly-review",
            PrivilegedReadPolicy.NotApplicable(),
            new EventReference("request", "request-1"),
            FixedNow);

        var exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await adapter.RecordAsync(record));

        Assert.Equal(
            "Privileged reads require the complete C9 evidence auditor.",
            exception.Message);
        Assert.Empty(adapter.Records);

        await adapter.StoreAsync(record);
        Assert.Same(record, Assert.Single(adapter.Records));
    }

    [Fact]
    public void Event_store_contract_remains_append_only_and_requires_v2_metadata()
    {
        var methods = typeof(IEventStore)
            .GetMethods()
            .Select(method => method.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            new[] { nameof(IEventStore.AppendAsync), nameof(IEventStore.ReadAllAsync) },
            methods);

        var append = Assert.Single(
            typeof(IEventStore).GetMethods(),
            method => method.Name == nameof(IEventStore.AppendAsync));
        Assert.Equal(
            new[]
            {
                typeof(IDomainEvent),
                typeof(EventAppendMetadata),
                typeof(ReadOnlyMemory<byte>),
                typeof(CancellationToken),
            },
            append.GetParameters()
                .Select(parameter => parameter.ParameterType)
                .ToArray());
        Assert.Equal(typeof(ValueTask<StoredEvent>), append.ReturnType);
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
                    Metadata($"aggregate-{index + 1}"),
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
            new EventReference("test-aggregate", "aggregate-1"),
            AuditActor.System("platform-test"),
            occurredAt
                ?? EventEnvelopeCanonicalizer.NormalizeTimestamp(FixedNow),
            EventEnvelopeCanonicalizer.NormalizeTimestamp(FixedNow),
            "event-backbone-test",
            new EventReference("request", "request-1"),
            Tenant,
            EventPrivilege.Standard,
            Sha256HashChain.Genesis,
            string.Empty,
            Payload("payload"));

    private static EventAppendMetadata Metadata(
        string aggregateValue = "aggregate-1") =>
        new(
            new EventReference("test-aggregate", aggregateValue),
            AuditActor.System("platform-test"),
            "event-backbone-test",
            new EventReference("request", "request-1"));

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

    private sealed class CapturingPrivilegedReadAuditor : IPrivilegedReadAuditor
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
