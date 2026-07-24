using ControlTower.Modules.EnterpriseContext.Infrastructure;
using ControlTower.Modules.EnterpriseContext.Privacy;
using ControlTower.Platform.Events;
using ControlTower.Platform.Identity;
using ControlTower.Platform.Privacy;
using ControlTower.Platform.Tenancy;

namespace ControlTower.Modules.EnterpriseContext.Tests;

public sealed class JurisdictionProfileTests
{
    private static readonly TenantId TenantA = new(Guid.Parse(
        "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
    private static readonly TenantId TenantB = new(Guid.Parse(
        "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));
    private static readonly AuditActor Actor =
        AuditActor.System("privacy-admin");
    private static readonly AuditActor OtherActor =
        AuditActor.System("other-admin");
    private static readonly DateTimeOffset T0 =
        new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly JurisdictionRef JurisdictionA =
        new("jurisdiction-a");
    private static readonly JurisdictionRef JurisdictionB =
        new("jurisdiction-b");
    private static readonly RegulatoryRegimeMarker Regime =
        new("regime-a");

    [Fact]
    public void Domain_rejects_invalid_fields_and_owns_regime_markers()
    {
        Assert.Throws<ArgumentException>(
            () => new RegulatoryRegimeMarker(" regime"));
        Assert.Throws<ArgumentException>(
            () => new RegulatoryRegimeMarker("regime\n"));
        Assert.Throws<ArgumentException>(
            () => new RegulatoryRegimeMarker(
                new string('r', 129)));

        Assert.Throws<JurisdictionProfileException>(() =>
            Profile(Guid.Empty, TenantA, JurisdictionA));
        Assert.Throws<JurisdictionProfileException>(() =>
            Profile(Guid.NewGuid(), default, JurisdictionA));
        Assert.Throws<JurisdictionProfileException>(() =>
            Profile(Guid.NewGuid(), TenantA, default));
        Assert.Throws<JurisdictionProfileException>(() =>
            Profile(
                Guid.NewGuid(),
                TenantA,
                JurisdictionA,
                version: 0));
        Assert.Throws<JurisdictionProfileException>(() =>
            Profile(
                Guid.NewGuid(),
                TenantA,
                JurisdictionA,
                ceiling: (PrivacyMarking)42));
        Assert.Throws<JurisdictionProfileException>(() =>
            Profile(
                Guid.NewGuid(),
                TenantA,
                JurisdictionA,
                markers: []));
        Assert.Throws<JurisdictionProfileException>(() =>
            Profile(
                Guid.NewGuid(),
                TenantA,
                JurisdictionA,
                markers: [default]));
        Assert.Throws<JurisdictionProfileException>(() =>
            Profile(
                Guid.NewGuid(),
                TenantA,
                JurisdictionA,
                changedAt: T0.AddTicks(1)));
        Assert.Throws<JurisdictionProfileException>(() =>
            Profile(
                Guid.NewGuid(),
                TenantA,
                JurisdictionA,
                changedAt: (DateTimeOffset)default));
        Assert.Throws<JurisdictionProfileException>(() =>
            Profile(
                Guid.NewGuid(),
                TenantA,
                JurisdictionA,
                actor: default(AuditActor)));
        Assert.Throws<JurisdictionProfileException>(() =>
            Profile(
                Guid.NewGuid(),
                TenantA,
                JurisdictionA,
                reason: " "));

        var source = new[]
        {
            new RegulatoryRegimeMarker("regime-b"),
            Regime,
        };
        var profile = Profile(
            Guid.NewGuid(),
            TenantA,
            JurisdictionA,
            markers: source);
        source[0] = new("mutated");

        Assert.Equal(
            ["regime-a", "regime-b"],
            profile.RegimeMarkers.Select(marker => marker.Value));
        var exposed = Assert.IsAssignableFrom<
            IList<RegulatoryRegimeMarker>>(profile.RegimeMarkers);
        Assert.Throws<NotSupportedException>(() =>
            exposed[0] = new("forged"));

        var eventSource = new[]
        {
            Regime,
        };
        var changed = Changed(profile, regimeMarkers: eventSource);
        eventSource[0] = new("mutated");
        Assert.Equal("regime-a", changed.RegimeMarkers.Single().Value);

        Assert.Null(
            typeof(JurisdictionProfile).GetProperty("ValidFrom"));
        Assert.Null(
            typeof(JurisdictionProfile).GetProperty("ValidTo"));
        Assert.Null(
            typeof(JurisdictionProfile).GetProperty("RecordedAt"));
    }

    [Fact]
    public async Task Exact_current_and_complete_history_are_deterministic_and_immutable()
    {
        using var rig = Rig.For(TenantA);
        var id = Guid.NewGuid();
        var first = Profile(
            id,
            TenantA,
            JurisdictionA,
            ceiling: PrivacyMarking.L3);
        var second = Profile(
            id,
            TenantA,
            JurisdictionA,
            version: 2,
            ceiling: PrivacyMarking.L2,
            changedAt: T0.AddMicroseconds(1));

        AssertApplied(await Commit(rig.Store, first, expectedVersion: 0));
        AssertApplied(await Commit(rig.Store, second, expectedVersion: 1));

        Assert.Same(
            first,
            await rig.Store.GetExactAsync(id, version: 1));
        Assert.Same(
            second,
            await rig.Store.GetExactAsync(id, version: 2));
        Assert.Null(await rig.Store.GetExactAsync(id, version: 3));
        Assert.Same(second, await rig.Store.GetCurrentAsync(id));
        Assert.Null(await rig.Store.GetCurrentAsync(Guid.NewGuid()));

        var history = await rig.Store.GetHistoryAsync(id);
        Assert.Equal(
            [1L, 2L],
            history.Select(profile => profile.Version));
        var exposed =
            Assert.IsAssignableFrom<IList<JurisdictionProfile>>(history);
        Assert.Throws<NotSupportedException>(() =>
            exposed[0] = second);
    }

    [Fact]
    public async Task Resolver_uses_current_version_for_every_jurisdiction_and_fails_closed()
    {
        using var rig = Rig.For(TenantA);
        var firstId = Guid.NewGuid();
        var first = Profile(
            firstId,
            TenantA,
            JurisdictionA,
            ceiling: PrivacyMarking.L3);
        var firstCurrent = Profile(
            firstId,
            TenantA,
            JurisdictionA,
            version: 2,
            ceiling: PrivacyMarking.L1,
            changedAt: T0.AddMicroseconds(1));
        var second = Profile(
            Guid.NewGuid(),
            TenantA,
            JurisdictionB,
            ceiling: PrivacyMarking.L2);
        AssertApplied(await Commit(rig.Store, first, 0));
        AssertApplied(await Commit(rig.Store, firstCurrent, 1));
        AssertApplied(await Commit(rig.Store, second, 0));

        var resolved = await rig.Store.ResolveAsync(
            CeilingQuery(JurisdictionA, JurisdictionB));

        Assert.True(resolved.IsAuthoritative);
        Assert.Equal(PrivacyMarking.L1, resolved.Ceiling);
        Assert.Equal(
            [(JurisdictionA, 2L), (JurisdictionB, 1L)],
            resolved.MatchedVersions.Select(item =>
                (item.Jurisdiction, item.Version)));

        var missing = await rig.Store.ResolveAsync(
            CeilingQuery(
                JurisdictionA,
                JurisdictionB,
                new JurisdictionRef("jurisdiction-missing")));
        Assert.False(missing.IsAuthoritative);
        Assert.Equal(PrivacyMarking.L1, missing.Ceiling);
        Assert.Equal(2, missing.MatchedVersions.Count);

        var empty = await rig.Store.ResolveAsync(
            new JurisdictionCeilingQuery(
                new PrivacyApplicability([], [])));
        Assert.False(empty.IsAuthoritative);
        Assert.Equal(PrivacyMarking.L1, empty.Ceiling);
        Assert.Empty(empty.MatchedVersions);

        var populationOnly = await rig.Store.ResolveAsync(
            new JurisdictionCeilingQuery(
                new PrivacyApplicability(
                    [],
                    [new PopulationRef("population-a")])));
        Assert.False(populationOnly.IsAuthoritative);
        Assert.Equal(PrivacyMarking.L1, populationOnly.Ceiling);
        Assert.Empty(populationOnly.MatchedVersions);
    }

    [Fact]
    public async Task Second_profile_id_for_a_jurisdiction_conflicts_without_append()
    {
        using var rig = Rig.For(TenantA);
        var authoritative = Profile(
            Guid.NewGuid(),
            TenantA,
            JurisdictionA);
        var duplicate = Profile(
            Guid.NewGuid(),
            TenantA,
            JurisdictionA);
        AssertApplied(await Commit(rig.Store, authoritative, 0));

        var result = await Commit(rig.Store, duplicate, 0);

        Assert.Equal(
            JurisdictionProfileCommitStatus.Conflict,
            result.Status);
        Assert.Same(authoritative, result.Authoritative);
        Assert.Equal(1, rig.Events.AppendCount);
        Assert.Empty(
            await rig.Store.GetHistoryAsync(duplicate.Id));
    }

    [Fact]
    public async Task Every_public_operation_rejects_missing_or_malformed_tenant()
    {
        await AssertEveryPublicOperationRejectsTenant(
            new FixedTenantAccessor(
                hasTenant: false,
                TenantA));
        await AssertEveryPublicOperationRejectsTenant(
            new FixedTenantAccessor(
                hasTenant: true,
                default));
    }

    [Fact]
    public async Task Foreign_tenant_data_is_not_disclosed_or_mutated()
    {
        var tenants = new TenantContextAccessor();
        var events = new RecordingEventStore();
        var store = new InMemoryJurisdictionProfileStore(
            tenants,
            events);
        var profile = Profile(
            Guid.NewGuid(),
            TenantA,
            JurisdictionA);

        using (tenants.BeginScope(TenantA))
            AssertApplied(await Commit(store, profile, 0));
        using (tenants.BeginScope(TenantB))
        {
            Assert.Null(
                await store.GetExactAsync(profile.Id, profile.Version));
            Assert.Null(await store.GetCurrentAsync(profile.Id));
            Assert.Empty(await store.GetHistoryAsync(profile.Id));
            var foreign = await store.ResolveAsync(
                CeilingQuery(JurisdictionA));
            Assert.False(foreign.IsAuthoritative);
            Assert.Equal(PrivacyMarking.L1, foreign.Ceiling);
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => Commit(store, profile, 0));
        }

        Assert.Equal(1, events.AppendCount);
    }

    [Fact]
    public async Task Tenant_switch_before_append_leaves_event_and_state_empty()
    {
        var tenants = new MutableTenantAccessor(TenantA)
        {
            SwitchOnCurrentRead = 2,
            SwitchToTenant = TenantB,
        };
        var events = new RecordingEventStore();
        var store = new InMemoryJurisdictionProfileStore(
            tenants,
            events);
        var profile = Profile(
            Guid.NewGuid(),
            TenantA,
            JurisdictionA);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Commit(store, profile, 0));
        Assert.Equal(TenantB, tenants.CurrentTenant);
        Assert.Equal(0, events.AppendCount);

        tenants.CurrentTenant = TenantA;
        tenants.SwitchOnCurrentRead = null;
        Assert.Empty(await store.GetHistoryAsync(profile.Id));
    }

    [Fact]
    public async Task Successful_append_commits_captured_tenant_state_even_if_dependency_switches_tenant()
    {
        var tenants = new MutableTenantAccessor(TenantA);
        var events = new RecordingEventStore
        {
            OnAppend = () => tenants.CurrentTenant = TenantB,
        };
        var store = new InMemoryJurisdictionProfileStore(
            tenants,
            events);
        var profile = Profile(
            Guid.NewGuid(),
            TenantA,
            JurisdictionA);

        var result = await Commit(store, profile, 0);

        AssertApplied(result);
        Assert.Equal(TenantB, tenants.CurrentTenant);
        Assert.Equal(1, events.AppendCount);
        Assert.Null(await store.GetCurrentAsync(profile.Id));

        tenants.CurrentTenant = TenantA;
        Assert.Same(profile, await store.GetCurrentAsync(profile.Id));
        Assert.Same(
            profile,
            Assert.Single(await store.GetHistoryAsync(profile.Id)));
    }

    [Fact]
    public async Task Tenant_switch_during_every_read_prevents_result_disclosure()
    {
        var tenants = new MutableTenantAccessor(TenantA);
        var events = new RecordingEventStore();
        var store = new InMemoryJurisdictionProfileStore(
            tenants,
            events);
        var profile = Profile(
            Guid.NewGuid(),
            TenantA,
            JurisdictionA);
        AssertApplied(await Commit(store, profile, 0));

        Func<Task>[] reads =
        [
            async () =>
            {
                _ = await store.GetExactAsync(
                    profile.Id,
                    profile.Version);
            },
            async () =>
            {
                _ = await store.GetCurrentAsync(profile.Id);
            },
            async () =>
            {
                _ = await store.GetHistoryAsync(profile.Id);
            },
            async () =>
            {
                _ = await store.ResolveAsync(
                    CeilingQuery(JurisdictionA));
            },
        ];

        foreach (var read in reads)
        {
            tenants.CurrentTenant = TenantA;
            tenants.SwitchToTenant = TenantB;
            tenants.SwitchOnCurrentRead =
                tenants.CurrentReadCount + 3;

            await Assert.ThrowsAsync<InvalidOperationException>(read);
            Assert.Equal(TenantB, tenants.CurrentTenant);
            tenants.SwitchOnCurrentRead = null;
        }

        tenants.CurrentTenant = TenantA;
        Assert.Same(profile, await store.GetCurrentAsync(profile.Id));
        Assert.Equal(1, events.AppendCount);
    }

    [Fact]
    public async Task Forged_event_state_and_metadata_fields_fail_before_append()
    {
        var correlationA = new EventReference(
            "command",
            Guid.NewGuid().ToString("D"));
        var correlationB = new EventReference(
            "command",
            Guid.NewGuid().ToString("D"));

        await AssertForgeryRejected((profile, _, metadata) =>
            (Changed(profile, eventId: Guid.Empty), metadata));
        await AssertForgeryRejected((profile, _, metadata) =>
            (
                Changed(
                    profile,
                    occurredAt: profile.ChangedAt.AddMicroseconds(1)),
                metadata));
        await AssertForgeryRejected((profile, _, metadata) =>
            (
                Changed(profile, profileId: Guid.NewGuid()),
                metadata));
        await AssertForgeryRejected((profile, _, metadata) =>
            (
                Changed(profile, jurisdiction: JurisdictionB),
                metadata));
        await AssertForgeryRejected((profile, _, metadata) =>
            (
                Changed(profile, version: profile.Version + 1),
                metadata));
        await AssertForgeryRejected((profile, _, metadata) =>
            (
                Changed(profile, ceiling: PrivacyMarking.L2),
                metadata));
        await AssertForgeryRejected((profile, _, metadata) =>
            (
                Changed(
                    profile,
                    regimeMarkers:
                    [
                        new RegulatoryRegimeMarker("regime-forged"),
                    ]),
                metadata));
        await AssertForgeryRejected((profile, _, metadata) =>
            (
                Changed(profile, actor: OtherActor),
                metadata));
        await AssertForgeryRejected((profile, _, metadata) =>
            (
                Changed(profile, reason: "forged reason"),
                metadata));
        await AssertForgeryRejected((profile, changed, _) =>
            (
                changed,
                new EventAppendMetadata(
                    EventReference.For(
                        "jurisdiction-profile",
                        Guid.NewGuid()),
                    profile.ChangedBy,
                    profile.ChangeReason)));
        await AssertForgeryRejected((profile, changed, _) =>
            (
                changed,
                new EventAppendMetadata(
                    JurisdictionProfileCommitSemantics
                        .AggregateReference(profile.Id),
                    OtherActor,
                    profile.ChangeReason)));
        await AssertForgeryRejected((profile, changed, _) =>
            (
                changed,
                new EventAppendMetadata(
                    JurisdictionProfileCommitSemantics
                        .AggregateReference(profile.Id),
                    profile.ChangedBy)));
        await AssertForgeryRejected((profile, _, _) =>
            (
                Changed(profile, correlation: correlationA),
                JurisdictionProfileCommitSemantics.Metadata(
                    profile,
                    correlationB)));
        await AssertForgeryRejected((profile, changed, _) =>
            (
                changed,
                JurisdictionProfileCommitSemantics.Metadata(
                    profile,
                    correlationA)));
        await AssertForgeryRejected((profile, _, _) =>
            (
                Changed(
                    profile,
                    correlation: default(EventReference)),
                JurisdictionProfileCommitSemantics.Metadata(profile)));

        using var rig = Rig.For(TenantA);
        var foreignState = Profile(
            Guid.NewGuid(),
            TenantB,
            JurisdictionA);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Commit(rig.Store, foreignState, 0));
        Assert.Equal(0, rig.Events.AppendCount);
    }

    [Fact]
    public async Task Successful_commit_appends_exact_event_metadata_and_payload()
    {
        using var rig = Rig.For(TenantA);
        var correlation = new EventReference(
            "command",
            Guid.NewGuid().ToString("D"));
        var profile = Profile(
            Guid.NewGuid(),
            TenantA,
            JurisdictionA,
            ceiling: PrivacyMarking.L2,
            markers:
            [
                Regime,
                new RegulatoryRegimeMarker("regime-b"),
            ]);

        AssertApplied(
            await Commit(
                rig.Store,
                profile,
                expectedVersion: 0,
                correlation));

        var changed =
            Assert.IsType<JurisdictionProfileChanged>(
                rig.Events.LastEvent);
        Assert.NotEqual(Guid.Empty, changed.EventId);
        Assert.True(
            changed.OccurredAt.EqualsExact(profile.ChangedAt));
        Assert.Equal(profile.Id, changed.ProfileId);
        Assert.Equal(profile.Jurisdiction, changed.Jurisdiction);
        Assert.Equal(profile.Version, changed.Version);
        Assert.Equal(
            profile.TelemetryCeiling,
            changed.TelemetryCeiling);
        Assert.Equal(profile.RegimeMarkers, changed.RegimeMarkers);
        Assert.Equal(profile.ChangedBy, changed.ChangedBy);
        Assert.Equal(profile.ChangeReason, changed.ChangeReason);
        Assert.Equal(correlation, changed.CorrelationReference);

        var metadata = Assert.IsType<EventAppendMetadata>(
            rig.Events.LastMetadata);
        Assert.Equal(
            JurisdictionProfileCommitSemantics.AggregateReference(
                profile.Id),
            metadata.AggregateReference);
        Assert.Equal(profile.ChangedBy, metadata.Actor);
        Assert.Equal(profile.ChangeReason, metadata.Reason);
        Assert.Equal(correlation, metadata.CorrelationReference);
        Assert.NotEmpty(rig.Events.LastPayload.ToArray());
    }

    [Fact]
    public async Task Append_failure_leaves_create_and_revision_history_unchanged()
    {
        using var rig = Rig.For(TenantA);
        var id = Guid.NewGuid();
        var first = Profile(
            id,
            TenantA,
            JurisdictionA);
        rig.Events.FailOnAppend = 1;

        await Assert.ThrowsAsync<TestAppendException>(
            () => Commit(rig.Store, first, 0));
        Assert.Empty(await rig.Store.GetHistoryAsync(id));

        rig.Events.FailOnAppend = null;
        AssertApplied(await Commit(rig.Store, first, 0));
        var second = Profile(
            id,
            TenantA,
            JurisdictionA,
            version: 2,
            ceiling: PrivacyMarking.L2,
            changedAt: T0.AddMicroseconds(1));
        rig.Events.FailOnAppend = 3;

        await Assert.ThrowsAsync<TestAppendException>(
            () => Commit(rig.Store, second, 1));
        var history = await rig.Store.GetHistoryAsync(id);
        Assert.Same(first, Assert.Single(history));
        Assert.Same(first, await rig.Store.GetCurrentAsync(id));
    }

    [Fact]
    public async Task Revision_version_and_changed_time_must_advance_monotonically()
    {
        using var rig = Rig.For(TenantA);
        var id = Guid.NewGuid();
        var first = Profile(
            id,
            TenantA,
            JurisdictionA);

        var invalidCreate = Profile(
            Guid.NewGuid(),
            TenantA,
            JurisdictionB,
            version: 2);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Commit(rig.Store, invalidCreate, 0));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => Commit(
                rig.Store,
                Profile(
                    Guid.NewGuid(),
                    TenantA,
                    JurisdictionB),
                expectedVersion: -1));
        AssertApplied(await Commit(rig.Store, first, 0));

        var skippedVersion = Profile(
            id,
            TenantA,
            JurisdictionA,
            version: 3,
            changedAt: T0.AddMicroseconds(1));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Commit(rig.Store, skippedVersion, 1));

        foreach (var changedAt in new[]
                 {
                     T0,
                     T0.AddMicroseconds(-1),
                 })
        {
            var invalid = Profile(
                id,
                TenantA,
                JurisdictionA,
                version: 2,
                changedAt: changedAt);
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => Commit(rig.Store, invalid, 1));
        }

        var valid = Profile(
            id,
            TenantA,
            JurisdictionA,
            version: 2,
            changedAt: T0.AddMicroseconds(1));
        AssertApplied(await Commit(rig.Store, valid, 1));
        Assert.Equal(2, rig.Events.AppendCount);
    }

    [Fact]
    public async Task Stale_and_concurrent_writes_have_one_authoritative_history()
    {
        using var rig = Rig.For(TenantA);
        var id = Guid.NewGuid();
        var first = Profile(
            id,
            TenantA,
            JurisdictionA);
        AssertApplied(await Commit(rig.Store, first, 0));

        var contenderA = Profile(
            id,
            TenantA,
            JurisdictionA,
            version: 2,
            ceiling: PrivacyMarking.L2,
            changedAt: T0.AddMicroseconds(1),
            reason: "contender a");
        var contenderB = Profile(
            id,
            TenantA,
            JurisdictionA,
            version: 2,
            ceiling: PrivacyMarking.L1,
            changedAt: T0.AddMicroseconds(2),
            reason: "contender b");

        var results = await Task.WhenAll(
            Commit(rig.Store, contenderA, 1),
            Commit(rig.Store, contenderB, 1));

        var applied = Assert.Single(
            results,
            result =>
                result.Status
                == JurisdictionProfileCommitStatus.Applied);
        var conflict = Assert.Single(
            results,
            result =>
                result.Status
                == JurisdictionProfileCommitStatus.Conflict);
        Assert.Same(applied.Authoritative, conflict.Authoritative);
        Assert.Equal(
            [1L, 2L],
            (await rig.Store.GetHistoryAsync(id))
                .Select(profile => profile.Version));
        Assert.Equal(2, rig.Events.AppendCount);

        var stale = Profile(
            id,
            TenantA,
            JurisdictionA,
            version: 2,
            ceiling: PrivacyMarking.L4,
            changedAt: T0.AddMicroseconds(3),
            reason: "stale");
        var staleResult = await Commit(rig.Store, stale, 1);
        Assert.Equal(
            JurisdictionProfileCommitStatus.Conflict,
            staleResult.Status);
        Assert.Same(applied.Authoritative, staleResult.Authoritative);
        Assert.Equal(2, rig.Events.AppendCount);
    }

    private static JurisdictionProfile Profile(
        Guid id,
        TenantId tenant,
        JurisdictionRef jurisdiction,
        long version = 1,
        PrivacyMarking ceiling = PrivacyMarking.L3,
        IEnumerable<RegulatoryRegimeMarker>? markers = null,
        DateTimeOffset? changedAt = null,
        AuditActor? actor = null,
        string reason = "jurisdiction review") =>
        new(
            id,
            tenant,
            jurisdiction,
            version,
            ceiling,
            markers ?? [Regime],
            changedAt ?? T0,
            actor ?? Actor,
            reason);

    private static JurisdictionProfileChanged Changed(
        JurisdictionProfile profile,
        Guid? eventId = null,
        DateTimeOffset? occurredAt = null,
        Guid? profileId = null,
        JurisdictionRef? jurisdiction = null,
        long? version = null,
        PrivacyMarking? ceiling = null,
        IEnumerable<RegulatoryRegimeMarker>? regimeMarkers = null,
        AuditActor? actor = null,
        string? reason = null,
        EventReference? correlation = null) =>
        new(
            eventId ?? Guid.NewGuid(),
            occurredAt ?? profile.ChangedAt,
            profileId ?? profile.Id,
            jurisdiction ?? profile.Jurisdiction,
            version ?? profile.Version,
            ceiling ?? profile.TelemetryCeiling,
            regimeMarkers ?? profile.RegimeMarkers,
            actor ?? profile.ChangedBy,
            reason ?? profile.ChangeReason,
            correlation);

    private static Task<JurisdictionProfileCommitResult> Commit(
        InMemoryJurisdictionProfileStore store,
        JurisdictionProfile profile,
        long expectedVersion,
        EventReference? correlation = null) =>
        store.CommitAsync(
            profile,
            JurisdictionProfileCommitSemantics.Changed(
                profile,
                correlation),
            JurisdictionProfileCommitSemantics.Metadata(
                profile,
                correlation),
            expectedVersion);

    private static JurisdictionCeilingQuery CeilingQuery(
        params JurisdictionRef[] jurisdictions) =>
        new(new PrivacyApplicability(jurisdictions, []));

    private static void AssertApplied(
        JurisdictionProfileCommitResult result)
    {
        Assert.Equal(
            JurisdictionProfileCommitStatus.Applied,
            result.Status);
        Assert.NotNull(result.Authoritative);
    }

    private static async Task AssertEveryPublicOperationRejectsTenant(
        ITenantContextAccessor tenants)
    {
        var events = new RecordingEventStore();
        var store = new InMemoryJurisdictionProfileStore(
            tenants,
            events);
        var profile = Profile(
            Guid.NewGuid(),
            TenantA,
            JurisdictionA);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.GetExactAsync(profile.Id, profile.Version));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.GetCurrentAsync(profile.Id));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.GetHistoryAsync(profile.Id));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.ResolveAsync(CeilingQuery(JurisdictionA)));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Commit(store, profile, 0));
        Assert.Equal(0, events.AppendCount);
    }

    private static async Task AssertForgeryRejected(
        Func<
            JurisdictionProfile,
            JurisdictionProfileChanged,
            EventAppendMetadata,
            (JurisdictionProfileChanged Changed,
                EventAppendMetadata Metadata)> forge)
    {
        using var rig = Rig.For(TenantA);
        var profile = Profile(
            Guid.NewGuid(),
            TenantA,
            JurisdictionA);
        var changed =
            JurisdictionProfileCommitSemantics.Changed(profile);
        var metadata =
            JurisdictionProfileCommitSemantics.Metadata(profile);
        var forged = forge(profile, changed, metadata);

        await Assert.ThrowsAnyAsync<Exception>(() =>
            rig.Store.CommitAsync(
                profile,
                forged.Changed,
                forged.Metadata,
                expectedVersion: 0));
        Assert.Equal(0, rig.Events.AppendCount);
        Assert.Empty(
            await rig.Store.GetHistoryAsync(profile.Id));
    }

    private sealed record Rig(
        TenantContextAccessor Tenants,
        InMemoryJurisdictionProfileStore Store,
        RecordingEventStore Events,
        IDisposable Scope) : IDisposable
    {
        public static Rig For(TenantId tenant)
        {
            var tenants = new TenantContextAccessor();
            var events = new RecordingEventStore();
            return new(
                tenants,
                new InMemoryJurisdictionProfileStore(
                    tenants,
                    events),
                events,
                tenants.BeginScope(tenant));
        }

        public void Dispose() => Scope.Dispose();
    }

    private sealed class RecordingEventStore : IEventStore
    {
        public int AppendCount { get; private set; }

        public int? FailOnAppend { get; set; }

        public Action? OnAppend { get; set; }

        public IDomainEvent? LastEvent { get; private set; }

        public EventAppendMetadata? LastMetadata { get; private set; }

        public ReadOnlyMemory<byte> LastPayload { get; private set; }

        public ValueTask<StoredEvent> AppendAsync(
            IDomainEvent @event,
            EventAppendMetadata metadata,
            ReadOnlyMemory<byte> payload,
            CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            AppendCount++;
            LastEvent = @event;
            LastMetadata = metadata;
            LastPayload = payload;
            OnAppend?.Invoke();
            if (AppendCount == FailOnAppend)
                throw new TestAppendException();
            return ValueTask.FromResult<StoredEvent>(null!);
        }

        public ValueTask<IReadOnlyList<StoredEvent>> ReadAllAsync(
            CancellationToken ct = default) =>
            ValueTask.FromResult<IReadOnlyList<StoredEvent>>([]);
    }

    private sealed class MutableTenantAccessor(TenantId tenant)
        : ITenantContextAccessor
    {
        public TenantId CurrentTenant { get; set; } = tenant;

        public int CurrentReadCount { get; private set; }

        public int? SwitchOnCurrentRead { get; set; }

        public TenantId SwitchToTenant { get; set; } = tenant;

        public bool HasTenant => true;

        public TenantId Current
        {
            get
            {
                CurrentReadCount++;
                if (SwitchOnCurrentRead == CurrentReadCount)
                    CurrentTenant = SwitchToTenant;
                return CurrentTenant;
            }
        }

        public IDisposable BeginScope(TenantId scopedTenant)
        {
            var previous = CurrentTenant;
            CurrentTenant = scopedTenant;
            return new DelegateScope(() =>
                CurrentTenant = previous);
        }
    }

    private sealed class FixedTenantAccessor(
        bool hasTenant,
        TenantId tenant) : ITenantContextAccessor
    {
        public bool HasTenant { get; } = hasTenant;

        public TenantId Current { get; } = tenant;

        public IDisposable BeginScope(TenantId scopedTenant) =>
            throw new NotSupportedException();
    }

    private sealed class DelegateScope(Action dispose) : IDisposable
    {
        public void Dispose() => dispose();
    }

    private sealed class TestAppendException : Exception;
}
