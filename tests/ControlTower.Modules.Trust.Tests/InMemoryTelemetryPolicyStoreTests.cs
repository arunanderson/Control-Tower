using System.Text.Json;
using ControlTower.Modules.Trust.Infrastructure;
using ControlTower.Modules.Trust.Privacy;
using ControlTower.Platform.Events;
using ControlTower.Platform.Identity;
using ControlTower.Platform.Privacy;
using ControlTower.Platform.Tenancy;

namespace ControlTower.Modules.Trust.Tests;

public sealed class InMemoryTelemetryPolicyStoreTests
{
    [Fact]
    public async Task History_and_as_of_queries_preserve_both_time_axes()
    {
        var setup = Setup();
        using var scope =
            setup.Tenants.BeginScope(
                TelemetryPolicyTestData.TenantA);
        var first = TelemetryPolicyTestData.Revision(
            version: 1,
            TelemetryPolicyTestData.At(1, 10),
            validFrom: TelemetryPolicyTestData.At(1, 1),
            validTo: TelemetryPolicyTestData.At(12, 31));
        var laterKnowledgeForLaterValidity =
            TelemetryPolicyTestData.Revision(
                version: 2,
                TelemetryPolicyTestData.At(1, 20),
                validFrom:
                    TelemetryPolicyTestData.At(7, 1),
                validTo:
                    TelemetryPolicyTestData.At(12, 31));

        await TelemetryPolicyTestData.CommitAsync(
            setup.Store,
            first,
            expectedVersion: 0);
        await TelemetryPolicyTestData.CommitAsync(
            setup.Store,
            laterKnowledgeForLaterValidity,
            expectedVersion: 1);

        var beforeSecondWasRecorded =
            await setup.Store.FindAsOfAsync(
                TelemetryPolicyTestData.At(8, 1),
                TelemetryPolicyTestData.At(1, 15));
        var afterSecondWasRecorded =
            await setup.Store.FindAsOfAsync(
                TelemetryPolicyTestData.At(8, 1),
                TelemetryPolicyTestData.At(2, 1));
        var atSecondRecordBoundary =
            await setup.Store.FindAsOfAsync(
                TelemetryPolicyTestData.At(8, 1),
                TelemetryPolicyTestData.At(1, 20));
        var validBeforeSecondStarts =
            await setup.Store.FindAsOfAsync(
                TelemetryPolicyTestData.At(6, 1),
                TelemetryPolicyTestData.At(2, 1));
        var beforeFirstWasRecorded =
            await setup.Store.FindAsOfAsync(
                TelemetryPolicyTestData.At(8, 1),
                TelemetryPolicyTestData.At(1, 9));
        var atExclusiveValidTo =
            await setup.Store.FindAsOfAsync(
                TelemetryPolicyTestData.At(12, 31),
                TelemetryPolicyTestData.At(2, 1));
        var exactFirst =
            await setup.Store.GetAsync(1);
        var history =
            await setup.Store.ListHistoryAsync();

        Assert.Same(first, beforeSecondWasRecorded);
        Assert.Same(
            laterKnowledgeForLaterValidity,
            afterSecondWasRecorded);
        Assert.Same(
            laterKnowledgeForLaterValidity,
            atSecondRecordBoundary);
        Assert.Same(first, validBeforeSecondStarts);
        Assert.Null(beforeFirstWasRecorded);
        Assert.Null(atExclusiveValidTo);
        Assert.Same(first, exactFirst);
        Assert.Equal(
            [1L, 2L],
            history.Select(revision =>
                revision.Version));
        Assert.Equal(2, setup.Events.AppendCount);
    }

    [Fact]
    public async Task No_matching_rule_defaults_to_enabled_L1()
    {
        var setup = Setup();
        using var scope =
            setup.Tenants.BeginScope(
                TelemetryPolicyTestData.TenantA);
        var revision = TelemetryPolicyTestData.Revision(
            version: 1,
            TelemetryPolicyTestData.At(1, 10),
            [
                TelemetryPolicyTestData.Rule(
                    capability:
                        new TelemetryCapabilityRef(
                            "different-capability")),
            ]);
        await TelemetryPolicyTestData.CommitAsync(
            setup.Store,
            revision,
            expectedVersion: 0);

        var resolved = await setup.Store.ResolveAsync(
            Query(
                TelemetryPolicyTestData.Capability,
                TelemetryPolicyTestData.Applicability()));

        Assert.True(resolved.Enabled);
        Assert.Equal(
            PrivacyMarking.L1,
            resolved.Level);
        Assert.Equal(1, resolved.PolicyVersion);
        Assert.Equal(0, resolved.MatchedRuleCount);
        Assert.Null(resolved.Ceiling);
    }

    [Fact]
    public async Task Incomplete_multi_jurisdiction_coverage_contributes_default_L1()
    {
        var setup = Setup();
        using var scope =
            setup.Tenants.BeginScope(
                TelemetryPolicyTestData.TenantA);
        var otherJurisdiction =
            new JurisdictionRef("jurisdiction-b");
        var revision = TelemetryPolicyTestData.Revision(
            version: 1,
            TelemetryPolicyTestData.At(1, 10),
            [
                TelemetryPolicyTestData.Rule(
                    PrivacyMarking.L2,
                    jurisdiction:
                        TelemetryPolicyTestData.Jurisdiction),
            ]);
        await TelemetryPolicyTestData.CommitAsync(
            setup.Store,
            revision,
            expectedVersion: 0);

        var resolved = await setup.Store.ResolveAsync(
            Query(
                TelemetryPolicyTestData.Capability,
                TelemetryPolicyTestData.Applicability(
                    [
                        TelemetryPolicyTestData.Jurisdiction,
                        otherJurisdiction,
                    ])));

        Assert.True(resolved.Enabled);
        Assert.Equal(
            PrivacyMarking.L1,
            resolved.Level);
        Assert.Equal(1, resolved.MatchedRuleCount);
        Assert.NotNull(resolved.Ceiling);
    }

    [Fact]
    public async Task Incomplete_multi_population_coverage_contributes_default_L1()
    {
        var setup = Setup();
        using var scope =
            setup.Tenants.BeginScope(
                TelemetryPolicyTestData.TenantA);
        var otherPopulation =
            new PopulationRef("population-b");
        var revision = TelemetryPolicyTestData.Revision(
            version: 1,
            TelemetryPolicyTestData.At(1, 10),
            [
                TelemetryPolicyTestData.Rule(
                    PrivacyMarking.L2,
                    population:
                        TelemetryPolicyTestData.Population),
            ]);
        await TelemetryPolicyTestData.CommitAsync(
            setup.Store,
            revision,
            expectedVersion: 0);

        var resolved = await setup.Store.ResolveAsync(
            Query(
                TelemetryPolicyTestData.Capability,
                TelemetryPolicyTestData.Applicability(
                    populations:
                    [
                        TelemetryPolicyTestData.Population,
                        otherPopulation,
                    ])));

        Assert.True(resolved.Enabled);
        Assert.Equal(
            PrivacyMarking.L1,
            resolved.Level);
        Assert.Equal(1, resolved.MatchedRuleCount);
        Assert.NotNull(resolved.Ceiling);
    }

    [Fact]
    public async Task Joint_scope_requires_both_jurisdiction_and_population_to_match()
    {
        var setup = Setup();
        using var scope =
            setup.Tenants.BeginScope(
                TelemetryPolicyTestData.TenantA);
        var revision = TelemetryPolicyTestData.Revision(
            version: 1,
            TelemetryPolicyTestData.At(1, 10),
            [
                TelemetryPolicyTestData.Rule(
                    PrivacyMarking.L2,
                    jurisdiction:
                        TelemetryPolicyTestData.Jurisdiction,
                    population:
                        TelemetryPolicyTestData.Population),
            ]);
        await TelemetryPolicyTestData.CommitAsync(
            setup.Store,
            revision,
            expectedVersion: 0);

        var matching = await setup.Store.ResolveAsync(
            Query(
                TelemetryPolicyTestData.Capability,
                TelemetryPolicyTestData.Applicability(
                    [
                        TelemetryPolicyTestData.Jurisdiction,
                    ],
                    [
                        TelemetryPolicyTestData.Population,
                    ])));
        var nonmatching = await setup.Store.ResolveAsync(
            Query(
                TelemetryPolicyTestData.Capability,
                TelemetryPolicyTestData.Applicability(
                    [
                        TelemetryPolicyTestData.Jurisdiction,
                    ],
                    [new PopulationRef("population-b")])));

        Assert.Equal(
            PrivacyMarking.L2,
            matching.Level);
        Assert.Equal(1, matching.MatchedRuleCount);
        Assert.Equal(
            PrivacyMarking.L1,
            nonmatching.Level);
        Assert.Equal(0, nonmatching.MatchedRuleCount);
        Assert.Null(nonmatching.Ceiling);
    }

    [Fact]
    public async Task Global_rule_covers_every_applicability_cell_once()
    {
        var setup = Setup();
        using var scope =
            setup.Tenants.BeginScope(
                TelemetryPolicyTestData.TenantA);
        var revision = TelemetryPolicyTestData.Revision(
            version: 1,
            TelemetryPolicyTestData.At(1, 10),
            [
                TelemetryPolicyTestData.Rule(
                    PrivacyMarking.L3),
            ]);
        await TelemetryPolicyTestData.CommitAsync(
            setup.Store,
            revision,
            expectedVersion: 0);

        var resolved = await setup.Store.ResolveAsync(
            Query(
                TelemetryPolicyTestData.Capability,
                TelemetryPolicyTestData.Applicability(
                    [
                        TelemetryPolicyTestData.Jurisdiction,
                        new JurisdictionRef("jurisdiction-b"),
                    ],
                    [
                        TelemetryPolicyTestData.Population,
                        new PopulationRef("population-b"),
                    ])));

        Assert.True(resolved.Enabled);
        Assert.Equal(
            PrivacyMarking.L3,
            resolved.Level);
        Assert.Equal(1, resolved.MatchedRuleCount);
    }

    [Fact]
    public async Task L4_rule_at_or_after_its_time_limit_falls_back_to_L1()
    {
        var setup = Setup();
        using var scope =
            setup.Tenants.BeginScope(
                TelemetryPolicyTestData.TenantA);
        var timeLimit =
            TelemetryPolicyTestData.At(10, 1);
        var revision = TelemetryPolicyTestData.Revision(
            version: 1,
            TelemetryPolicyTestData.At(1, 10),
            [
                TelemetryPolicyTestData.Rule(
                    PrivacyMarking.L4,
                    timeLimit: timeLimit),
            ]);
        await TelemetryPolicyTestData.CommitAsync(
            setup.Store,
            revision,
            expectedVersion: 0);
        var applicability =
            TelemetryPolicyTestData.Applicability();

        var before = await setup.Store.ResolveAsync(
            Query(
                TelemetryPolicyTestData.Capability,
                applicability,
                validAt: timeLimit.AddDays(-1)));
        var at = await setup.Store.ResolveAsync(
            Query(
                TelemetryPolicyTestData.Capability,
                applicability,
                validAt: timeLimit));
        var after = await setup.Store.ResolveAsync(
            Query(
                TelemetryPolicyTestData.Capability,
                applicability,
                validAt: timeLimit.AddDays(1)));

        Assert.Equal(PrivacyMarking.L4, before.Level);
        Assert.Equal(1, before.MatchedRuleCount);
        Assert.Equal(PrivacyMarking.L1, at.Level);
        Assert.Equal(0, at.MatchedRuleCount);
        Assert.Equal(PrivacyMarking.L1, after.Level);
        Assert.Equal(0, after.MatchedRuleCount);
    }

    [Fact]
    public async Task Any_applicable_disabled_rule_wins()
    {
        var setup = Setup();
        using var scope =
            setup.Tenants.BeginScope(
                TelemetryPolicyTestData.TenantA);
        var revision = TelemetryPolicyTestData.Revision(
            version: 1,
            TelemetryPolicyTestData.At(1, 10),
            [
                TelemetryPolicyTestData.Rule(
                    PrivacyMarking.L2),
                TelemetryPolicyTestData.Rule(
                    PrivacyMarking.L1,
                    enabled: false,
                    jurisdiction:
                        TelemetryPolicyTestData.Jurisdiction),
            ]);
        await TelemetryPolicyTestData.CommitAsync(
            setup.Store,
            revision,
            expectedVersion: 0);
        var callsAfterCommit =
            setup.Ceilings.CallCount;

        var resolved = await setup.Store.ResolveAsync(
            Query(
                TelemetryPolicyTestData.Capability,
                TelemetryPolicyTestData.Applicability(
                    [
                        TelemetryPolicyTestData
                            .Jurisdiction,
                    ])));

        Assert.False(resolved.Enabled);
        Assert.Equal(
            PrivacyMarking.L1,
            resolved.Level);
        Assert.Equal(2, resolved.MatchedRuleCount);
        Assert.Equal(
            callsAfterCommit,
            setup.Ceilings.CallCount);
    }

    [Fact]
    public async Task Enabled_overlap_uses_minimum_rule_then_current_ceiling()
    {
        var setup = Setup();
        using var scope =
            setup.Tenants.BeginScope(
                TelemetryPolicyTestData.TenantA);
        var revision = TelemetryPolicyTestData.Revision(
            version: 1,
            TelemetryPolicyTestData.At(1, 10),
            [
                TelemetryPolicyTestData.Rule(
                    PrivacyMarking.L4,
                    timeLimit:
                        TelemetryPolicyTestData.At(
                            10,
                            1)),
                TelemetryPolicyTestData.Rule(
                    PrivacyMarking.L3,
                    jurisdiction:
                        TelemetryPolicyTestData.Jurisdiction),
            ]);
        await TelemetryPolicyTestData.CommitAsync(
            setup.Store,
            revision,
            expectedVersion: 0);
        var query = Query(
                TelemetryPolicyTestData.Capability,
                TelemetryPolicyTestData.Applicability(
                    [
                        TelemetryPolicyTestData
                            .Jurisdiction,
                    ]),
                validAt:
                    TelemetryPolicyTestData.At(8, 1));

        var configuredMinimum =
            await setup.Store.ResolveAsync(query);

        Assert.True(configuredMinimum.Enabled);
        Assert.Equal(
            PrivacyMarking.L3,
            configuredMinimum.Level);
        Assert.Equal(
            2,
            configuredMinimum.MatchedRuleCount);

        setup.Ceilings.Ceiling =
            PrivacyMarking.L2;
        var clamped =
            await setup.Store.ResolveAsync(query);

        Assert.True(clamped.Enabled);
        Assert.Equal(
            PrivacyMarking.L2,
            clamped.Level);
        Assert.Equal(2, clamped.MatchedRuleCount);
        Assert.NotNull(clamped.Ceiling);
        Assert.Equal(
            PrivacyMarking.L2,
            clamped.Ceiling!.Ceiling);
    }

    [Fact]
    public async Task Later_E16_tightening_clamps_existing_policy()
    {
        var setup = Setup();
        using var scope =
            setup.Tenants.BeginScope(
                TelemetryPolicyTestData.TenantA);
        var revision = TelemetryPolicyTestData.Revision(
            version: 1,
            TelemetryPolicyTestData.At(1, 10),
            [
                TelemetryPolicyTestData.Rule(
                    PrivacyMarking.L3,
                    jurisdiction:
                        TelemetryPolicyTestData.Jurisdiction),
            ]);
        await TelemetryPolicyTestData.CommitAsync(
            setup.Store,
            revision,
            expectedVersion: 0);

        setup.Ceilings.Ceiling =
            PrivacyMarking.L1;
        var resolved = await setup.Store.ResolveAsync(
            Query(
                TelemetryPolicyTestData.Capability,
                TelemetryPolicyTestData.Applicability(
                    [
                        TelemetryPolicyTestData
                            .Jurisdiction,
                    ])));

        Assert.True(resolved.Enabled);
        Assert.Equal(
            PrivacyMarking.L1,
            resolved.Level);
        Assert.Equal(1, resolved.PolicyVersion);
    }

    [Fact]
    public async Task Commit_rejects_enabled_rule_above_E16_ceiling()
    {
        await AssertRejectedByCeilingAsync(
            new MutableCeilingResolver(
                PrivacyMarking.L1),
            TelemetryPolicyTestData.Rule(
                PrivacyMarking.L2));
    }

    [Fact]
    public async Task Commit_treats_missing_E16_as_L1()
    {
        await AssertRejectedByCeilingAsync(
            new MutableCeilingResolver(
                PrivacyMarking.L1,
                authoritative: false),
            TelemetryPolicyTestData.Rule(
                PrivacyMarking.L2));
    }

    [Fact]
    public async Task Commit_rejects_disabled_rule_above_E16_ceiling()
    {
        await AssertRejectedByCeilingAsync(
            new MutableCeilingResolver(
                PrivacyMarking.L1),
            TelemetryPolicyTestData.Rule(
                PrivacyMarking.L2,
                enabled: false));
    }

    [Fact]
    public async Task Commit_checks_every_rule_and_forwards_each_exact_scope_to_E16()
    {
        var setup = Setup(
            new MutableCeilingResolver(
                PrivacyMarking.L2));
        using var scope =
            setup.Tenants.BeginScope(
                TelemetryPolicyTestData.TenantA);
        var revision = TelemetryPolicyTestData.Revision(
            version: 1,
            TelemetryPolicyTestData.At(1, 10),
            [
                TelemetryPolicyTestData.Rule(
                    PrivacyMarking.L1),
                TelemetryPolicyTestData.Rule(
                    PrivacyMarking.L3,
                    jurisdiction:
                        TelemetryPolicyTestData.Jurisdiction,
                    population:
                        TelemetryPolicyTestData.Population),
            ]);
        Assert.Equal(
            PrivacyMarking.L3,
            revision.Rules[1].Level);

        await Assert.ThrowsAsync<
            TelemetryPolicyException>(
            () => TelemetryPolicyTestData.CommitAsync(
                setup.Store,
                revision,
                expectedVersion: 0));

        var queries =
            setup.Ceilings.Queries.ToArray();
        Assert.Equal(2, queries.Length);
        Assert.Empty(
            queries[0].Applicability.Jurisdictions);
        Assert.Empty(
            queries[0].Applicability.Populations);
        Assert.Equal(
            [TelemetryPolicyTestData.Jurisdiction],
            queries[1].Applicability.Jurisdictions);
        Assert.Equal(
            [TelemetryPolicyTestData.Population],
            queries[1].Applicability.Populations);
        Assert.Equal(0, setup.Events.AppendCount);
        Assert.Empty(
            await setup.Store.ListHistoryAsync());
    }

    [Fact]
    public async Task Tenant_context_is_required_and_histories_are_isolated()
    {
        var setup = Setup();
        var tenantARevision =
            TelemetryPolicyTestData.Revision(
                version: 1,
                TelemetryPolicyTestData.At(1, 10));
        using (setup.Tenants.BeginScope(
                   TelemetryPolicyTestData.TenantA))
        {
            await TelemetryPolicyTestData.CommitAsync(
                setup.Store,
                tenantARevision,
                expectedVersion: 0);
            Assert.Single(
                await setup.Store.ListHistoryAsync());
        }

        using (setup.Tenants.BeginScope(
                   TelemetryPolicyTestData.TenantB))
        {
            Assert.Empty(
                await setup.Store.ListHistoryAsync());
            Assert.Null(
                await setup.Store.GetAsync(1));
            Assert.Null(
                await setup.Store.FindAsOfAsync(
                    TelemetryPolicyTestData.At(8, 1),
                    TelemetryPolicyTestData.At(8, 2)));
            var foreignResolution =
                await setup.Store.ResolveAsync(
                    Query(
                        TelemetryPolicyTestData.Capability,
                        TelemetryPolicyTestData
                            .Applicability()));
            Assert.Equal(
                PrivacyMarking.L1,
                foreignResolution.Level);
            Assert.Null(
                foreignResolution.PolicyVersion);
            Assert.Equal(
                0,
                foreignResolution.MatchedRuleCount);
            await Assert.ThrowsAsync<
                InvalidOperationException>(
                () => TelemetryPolicyTestData.CommitAsync(
                    setup.Store,
                    tenantARevision,
                    expectedVersion: 0));
            Assert.Empty(
                await setup.Store.ListHistoryAsync());
        }

        await Assert.ThrowsAsync<
            InvalidOperationException>(
            () => setup.Store.ListHistoryAsync());
        Assert.Equal(1, setup.Events.AppendCount);
    }

    [Fact]
    public async Task Every_public_operation_rejects_missing_or_default_tenant()
    {
        var tenants = new MutableTenantAccessor();
        var ceilings = new MutableCeilingResolver();
        var events = new CapturingEventStore(
            TelemetryPolicyTestData.TenantA);
        var store = new InMemoryTelemetryPolicyStore(
            tenants,
            ceilings,
            events);

        await AssertEveryOperationRejectsTenantAsync(
            store);

        tenants.SwitchTo(default);
        await AssertEveryOperationRejectsTenantAsync(
            store);

        Assert.Equal(0, ceilings.CallCount);
        Assert.Equal(0, events.AppendCount);
    }

    [Fact]
    public async Task Switched_tenant_blocks_point_history_and_as_of_reads()
    {
        var tenants = new MutableTenantAccessor();
        tenants.SwitchTo(
            TelemetryPolicyTestData.TenantA);
        var ceilings = new MutableCeilingResolver();
        var events = new CapturingEventStore(
            TelemetryPolicyTestData.TenantA);
        var store = new InMemoryTelemetryPolicyStore(
            tenants,
            ceilings,
            events);
        var revision = TelemetryPolicyTestData.Revision(
            version: 1,
            TelemetryPolicyTestData.At(1, 10));
        await TelemetryPolicyTestData.CommitAsync(
            store,
            revision,
            expectedVersion: 0);
        Func<Task>[] reads =
        [
            async () =>
            {
                _ = await store.GetAsync(1);
            },
            async () =>
            {
                _ = await store.ListHistoryAsync();
            },
            async () =>
            {
                _ = await store.FindAsOfAsync(
                    TelemetryPolicyTestData.At(8, 1),
                    TelemetryPolicyTestData.At(8, 2));
            },
        ];

        foreach (var read in reads)
        {
            tenants.SwitchTo(
                TelemetryPolicyTestData.TenantA);
            tenants.OnCurrent = () =>
            {
                tenants.OnCurrent = null;
                tenants.SwitchTo(
                    TelemetryPolicyTestData.TenantB);
            };

            await Assert.ThrowsAsync<
                InvalidOperationException>(
                read);
        }

        tenants.SwitchTo(
            TelemetryPolicyTestData.TenantA);
        Assert.Same(
            revision,
            Assert.Single(
                await store.ListHistoryAsync()));
        Assert.Equal(1, events.AppendCount);
    }

    [Fact]
    public async Task Resolver_tenant_switch_fails_before_append_or_mutation()
    {
        var tenants = new MutableTenantAccessor();
        tenants.SwitchTo(
            TelemetryPolicyTestData.TenantA);
        var ceilings = new MutableCeilingResolver();
        var events = new CapturingEventStore(
            TelemetryPolicyTestData.TenantA);
        var store = new InMemoryTelemetryPolicyStore(
            tenants,
            ceilings,
            events);
        ceilings.OnResolve = () => tenants.SwitchTo(
            TelemetryPolicyTestData.TenantB);
        var revision = TelemetryPolicyTestData.Revision(
            version: 1,
            TelemetryPolicyTestData.At(1, 10),
            [TelemetryPolicyTestData.Rule()]);

        await Assert.ThrowsAsync<
            InvalidOperationException>(
            () => TelemetryPolicyTestData.CommitAsync(
                store,
                revision,
                expectedVersion: 0));

        Assert.Equal(0, events.AppendCount);
        tenants.SwitchTo(
            TelemetryPolicyTestData.TenantA);
        Assert.Empty(await store.ListHistoryAsync());
    }

    [Fact]
    public async Task Resolver_tenant_switch_fails_policy_resolution()
    {
        var tenants = new MutableTenantAccessor();
        tenants.SwitchTo(
            TelemetryPolicyTestData.TenantA);
        var ceilings = new MutableCeilingResolver();
        var events = new CapturingEventStore(
            TelemetryPolicyTestData.TenantA);
        var store = new InMemoryTelemetryPolicyStore(
            tenants,
            ceilings,
            events);
        var revision = TelemetryPolicyTestData.Revision(
            version: 1,
            TelemetryPolicyTestData.At(1, 10),
            [TelemetryPolicyTestData.Rule()]);
        await TelemetryPolicyTestData.CommitAsync(
            store,
            revision,
            expectedVersion: 0);
        ceilings.OnResolve = () => tenants.SwitchTo(
            TelemetryPolicyTestData.TenantB);

        await Assert.ThrowsAsync<
            InvalidOperationException>(
            () => store.ResolveAsync(
                Query(
                    TelemetryPolicyTestData.Capability,
                    TelemetryPolicyTestData
                        .Applicability())));

        tenants.SwitchTo(
            TelemetryPolicyTestData.TenantA);
        Assert.Single(await store.ListHistoryAsync());
        Assert.Equal(1, events.AppendCount);
    }

    [Fact]
    public async Task Successful_append_commits_captured_tenant_state_even_when_dependency_switches_tenant()
    {
        var tenants = new MutableTenantAccessor();
        tenants.SwitchTo(
            TelemetryPolicyTestData.TenantA);
        var ceilings = new MutableCeilingResolver();
        var events = new CapturingEventStore(
            TelemetryPolicyTestData.TenantA)
        {
            OnAppend = () => tenants.SwitchTo(
                TelemetryPolicyTestData.TenantB),
        };
        var store = new InMemoryTelemetryPolicyStore(
            tenants,
            ceilings,
            events);
        var revision = TelemetryPolicyTestData.Revision(
            version: 1,
            TelemetryPolicyTestData.At(1, 10));

        var result =
            await TelemetryPolicyTestData.CommitAsync(
                store,
                revision,
                expectedVersion: 0);

        Assert.Equal(
            TelemetryPolicyCommitStatus.Applied,
            result.Status);
        Assert.Same(revision, result.Authoritative);
        Assert.Equal(1, events.AppendCount);
        tenants.SwitchTo(
            TelemetryPolicyTestData.TenantA);
        Assert.Same(
            revision,
            Assert.Single(
                await store.ListHistoryAsync()));
    }

    [Fact]
    public async Task Forged_state_event_metadata_tuples_fail_before_IO()
    {
        var setup = Setup();
        using var scope =
            setup.Tenants.BeginScope(
                TelemetryPolicyTestData.TenantA);
        var revision = TelemetryPolicyTestData.Revision(
            version: 1,
            TelemetryPolicyTestData.At(1, 10),
            [TelemetryPolicyTestData.Rule()]);
        var validChanged =
            TelemetryPolicyCommitSemantics.Changed(
                revision);
        var validMetadata =
            TelemetryPolicyCommitSemantics.Metadata(
                revision);
        var forgedCorrelation = new EventReference(
            "request",
            "forged");
        var wrongActor = AuditActor.System(
            "different-policy-actor");
        Assert.Throws<ArgumentException>(
            () => new EventAppendMetadata(
                validMetadata.AggregateReference,
                default,
                validMetadata.Reason));
        var cases = new[]
        {
            (
                validChanged with
                {
                    EventId = Guid.Empty,
                },
                validMetadata),
            (
                validChanged with
                {
                    Tenant =
                        TelemetryPolicyTestData.TenantB,
                },
                validMetadata),
            (
                validChanged with
                {
                    Version = 2,
                },
                validMetadata),
            (
                validChanged with
                {
                    ValidFrom = default,
                },
                validMetadata),
            (
                validChanged with
                {
                    ValidTo = null,
                },
                validMetadata),
            (
                validChanged with
                {
                    RecordedAt = default,
                },
                validMetadata),
            (
                validChanged with
                {
                    OccurredAt = default,
                },
                validMetadata),
            (
                validChanged with
                {
                    ChangedBy = wrongActor,
                },
                validMetadata),
            (
                validChanged with
                {
                    Justification = null!,
                },
                validMetadata),
            (
                validChanged with
                {
                    PolicyFingerprint =
                        new string('0', 64),
                },
                validMetadata),
            (
                validChanged with
                {
                    RuleCount = 0,
                },
                validMetadata),
            (
                validChanged with
                {
                    Rules =
                        Array.Empty<
                            TelemetryPolicyRule>(),
                },
                validMetadata),
            (
                validChanged,
                new EventAppendMetadata(
                    validMetadata.AggregateReference,
                    validMetadata.Actor,
                    "forged reason")),
            (
                validChanged,
                new EventAppendMetadata(
                    validMetadata.AggregateReference,
                    wrongActor,
                    validMetadata.Reason)),
            (
                validChanged with
                {
                    CorrelationReference =
                        forgedCorrelation,
                },
                validMetadata),
            (
                validChanged,
                new EventAppendMetadata(
                    new EventReference(
                        "telemetry-policy",
                        TelemetryPolicyTestData
                            .TenantB.Value.ToString("D")),
                    validMetadata.Actor,
                    validMetadata.Reason)),
        };

        foreach (var (changed, metadata) in cases)
        {
            await Assert.ThrowsAsync<
                InvalidOperationException>(
                () => setup.Store.CommitAsync(
                    revision,
                    changed,
                    metadata,
                    expectedVersion: 0));
        }

        Assert.Equal(0, setup.Ceilings.CallCount);
        Assert.Equal(0, setup.Events.AppendCount);
        Assert.Empty(
            await setup.Store.ListHistoryAsync());
    }

    [Fact]
    public async Task Successful_commit_appends_the_exact_canonical_event_and_metadata()
    {
        var setup = Setup();
        using var scope =
            setup.Tenants.BeginScope(
                TelemetryPolicyTestData.TenantA);
        var revision = TelemetryPolicyTestData.Revision(
            version: 1,
            TelemetryPolicyTestData.At(1, 10),
            [
                TelemetryPolicyTestData.Rule(
                    PrivacyMarking.L4,
                    jurisdiction:
                        TelemetryPolicyTestData.Jurisdiction,
                    population:
                        TelemetryPolicyTestData.Population,
                    timeLimit:
                        TelemetryPolicyTestData.At(10, 1)),
            ]);
        var correlation = new EventReference(
            "request",
            "request-1");
        var expectedChanged =
            TelemetryPolicyCommitSemantics.Changed(
                revision,
                correlation);
        var expectedMetadata =
            TelemetryPolicyCommitSemantics.Metadata(
                revision,
                correlation);

        var result = await setup.Store.CommitAsync(
            revision,
            expectedChanged,
            expectedMetadata,
            expectedVersion: 0);

        Assert.Equal(
            TelemetryPolicyCommitStatus.Applied,
            result.Status);
        Assert.Same(revision, result.Authoritative);
        var appended = Assert.IsType<
            TelemetryPolicyChanged>(
            Assert.Single(setup.Events.Appended));
        Assert.Equal(expectedChanged, appended);
        Assert.Equal(
            expectedMetadata,
            Assert.Single(setup.Events.Metadata));
        var eventRule =
            Assert.Single(appended.Rules);
        var revisionRule =
            Assert.Single(revision.Rules);
        Assert.Equal(revisionRule, eventRule);
        using var payload = JsonDocument.Parse(
            Assert.Single(
                setup.Events.Payloads));
        var payloadRule = Assert.Single(
            payload.RootElement
                .GetProperty("Rules")
                .EnumerateArray());
        Assert.Equal(
            revisionRule.Capability.Value,
            payloadRule
                .GetProperty("Capability")
                .GetProperty("Value")
                .GetString());
        Assert.Equal(
            revisionRule.Jurisdiction!.Value.Value,
            payloadRule
                .GetProperty("Jurisdiction")
                .GetProperty("Value")
                .GetString());
        Assert.Equal(
            revisionRule.Population!.Value.Value,
            payloadRule
                .GetProperty("Population")
                .GetProperty("Value")
                .GetString());
        Assert.Equal(
            (int)revisionRule.Level,
            payloadRule
                .GetProperty("Level")
                .GetInt32());
        Assert.Equal(
            revisionRule.ActivationPurpose,
            payloadRule
                .GetProperty("ActivationPurpose")
                .GetString());
        Assert.Equal(
            revisionRule.ApprovalReference!.Value.Value,
            payloadRule
                .GetProperty("ApprovalReference")
                .GetProperty("Value")
                .GetString());
        Assert.Equal(
            revisionRule.RetentionPolicy!.Value.Value,
            payloadRule
                .GetProperty("RetentionPolicy")
                .GetProperty("Value")
                .GetString());
        Assert.Equal(
            revisionRule.TimeLimit,
            payloadRule
                .GetProperty("TimeLimit")
                .GetDateTimeOffset());
        Assert.Same(
            revision,
            Assert.Single(
                await setup.Store.ListHistoryAsync()));
    }

    [Fact]
    public async Task Failed_initial_append_leaves_event_queue_and_history_empty()
    {
        var setup = Setup();
        setup.Events.Failure =
            new InvalidOperationException(
                "event append failed");
        using var scope =
            setup.Tenants.BeginScope(
                TelemetryPolicyTestData.TenantA);
        var revision = TelemetryPolicyTestData.Revision(
            version: 1,
            TelemetryPolicyTestData.At(1, 10));

        await Assert.ThrowsAsync<
            InvalidOperationException>(
            () => TelemetryPolicyTestData.CommitAsync(
                setup.Store,
                revision,
                expectedVersion: 0));

        Assert.Equal(1, setup.Events.AppendCount);
        Assert.Empty(setup.Events.Appended);
        Assert.Empty(setup.Events.Metadata);
        Assert.Empty(setup.Events.Payloads);
        Assert.Empty(
            await setup.Store.ListHistoryAsync());
    }

    [Fact]
    public async Task Append_failure_preserves_the_preexisting_policy_snapshot()
    {
        var setup = Setup();
        using var scope =
            setup.Tenants.BeginScope(
                TelemetryPolicyTestData.TenantA);
        var first = TelemetryPolicyTestData.Revision(
            version: 1,
            TelemetryPolicyTestData.At(1, 10));
        await TelemetryPolicyTestData.CommitAsync(
            setup.Store,
            first,
            expectedVersion: 0);
        setup.Events.Failure =
            new InvalidOperationException(
                "event append failed");
        var rejected = TelemetryPolicyTestData.Revision(
            version: 2,
            TelemetryPolicyTestData.At(1, 20));

        await Assert.ThrowsAsync<
            InvalidOperationException>(
            () => TelemetryPolicyTestData.CommitAsync(
                setup.Store,
                rejected,
                expectedVersion: 1));

        Assert.Equal(2, setup.Events.AppendCount);
        Assert.Single(setup.Events.Appended);
        Assert.Single(setup.Events.Metadata);
        Assert.Single(setup.Events.Payloads);
        Assert.Same(
            first,
            Assert.Single(
                await setup.Store.ListHistoryAsync()));
        Assert.Null(
            await setup.Store.GetAsync(2));
    }

    [Fact]
    public async Task Stale_commit_returns_authoritative_conflict_without_append()
    {
        var setup = Setup();
        using var scope =
            setup.Tenants.BeginScope(
                TelemetryPolicyTestData.TenantA);
        var first = TelemetryPolicyTestData.Revision(
            version: 1,
            TelemetryPolicyTestData.At(1, 10),
            [TelemetryPolicyTestData.Rule()]);
        var authoritativeSecond =
            TelemetryPolicyTestData.Revision(
                version: 2,
                TelemetryPolicyTestData.At(1, 20),
                [TelemetryPolicyTestData.Rule()],
                justification: "authoritative second");
        var staleSecond =
            TelemetryPolicyTestData.Revision(
                version: 2,
                TelemetryPolicyTestData.At(1, 21),
                [
                    TelemetryPolicyTestData.Rule(
                        PrivacyMarking.L2),
                ],
                justification: "stale second");
        await TelemetryPolicyTestData.CommitAsync(
            setup.Store,
            first,
            expectedVersion: 0);
        await TelemetryPolicyTestData.CommitAsync(
            setup.Store,
            authoritativeSecond,
            expectedVersion: 1);
        var callsBeforeStale =
            setup.Ceilings.CallCount;
        setup.Ceilings.Ceiling =
            PrivacyMarking.L1;
        setup.Ceilings.OnResolve = () =>
            throw new InvalidOperationException(
                "A stale write must not resolve a ceiling.");

        var conflict =
            await TelemetryPolicyTestData.CommitAsync(
                setup.Store,
                staleSecond,
                expectedVersion: 1);

        Assert.Equal(
            TelemetryPolicyCommitStatus.Conflict,
            conflict.Status);
        Assert.Same(
            authoritativeSecond,
            conflict.Authoritative);
        Assert.Equal(2, setup.Events.AppendCount);
        Assert.Equal(
            [1L, 2L],
            (await setup.Store.ListHistoryAsync())
                .Select(revision =>
                    revision.Version));
        Assert.Equal(
            callsBeforeStale,
            setup.Ceilings.CallCount);
    }

    [Fact]
    public async Task Commit_rejects_create_and_skip_versions_before_IO()
    {
        var setup = Setup();
        using var scope =
            setup.Tenants.BeginScope(
                TelemetryPolicyTestData.TenantA);
        var invalidCreate =
            TelemetryPolicyTestData.Revision(
                version: 2,
                TelemetryPolicyTestData.At(1, 10));

        await Assert.ThrowsAsync<
            InvalidOperationException>(
            () => TelemetryPolicyTestData.CommitAsync(
                setup.Store,
                invalidCreate,
                expectedVersion: 0));
        Assert.Equal(0, setup.Ceilings.CallCount);
        Assert.Equal(0, setup.Events.AppendCount);

        var first = TelemetryPolicyTestData.Revision(
            version: 1,
            TelemetryPolicyTestData.At(1, 10));
        await TelemetryPolicyTestData.CommitAsync(
            setup.Store,
            first,
            expectedVersion: 0);
        var skipped = TelemetryPolicyTestData.Revision(
            version: 3,
            TelemetryPolicyTestData.At(1, 20));

        await Assert.ThrowsAsync<
            InvalidOperationException>(
            () => TelemetryPolicyTestData.CommitAsync(
                setup.Store,
                skipped,
                expectedVersion: 1));
        Assert.Equal(0, setup.Ceilings.CallCount);
        Assert.Equal(1, setup.Events.AppendCount);
        Assert.Same(
            first,
            Assert.Single(
                await setup.Store.ListHistoryAsync()));
    }

    [Fact]
    public async Task Commit_rejects_equal_or_backward_record_time_before_IO()
    {
        var setup = Setup();
        using var scope =
            setup.Tenants.BeginScope(
                TelemetryPolicyTestData.TenantA);
        var first = TelemetryPolicyTestData.Revision(
            version: 1,
            TelemetryPolicyTestData.At(1, 10));
        await TelemetryPolicyTestData.CommitAsync(
            setup.Store,
            first,
            expectedVersion: 0);
        var invalidCandidates = new[]
        {
            TelemetryPolicyTestData.Revision(
                version: 2,
                TelemetryPolicyTestData.At(1, 10)),
            TelemetryPolicyTestData.Revision(
                version: 2,
                TelemetryPolicyTestData.At(1, 9)),
        };

        foreach (var candidate in invalidCandidates)
        {
            await Assert.ThrowsAsync<
                InvalidOperationException>(
                () => TelemetryPolicyTestData.CommitAsync(
                    setup.Store,
                    candidate,
                    expectedVersion: 1));
        }

        Assert.Equal(0, setup.Ceilings.CallCount);
        Assert.Equal(1, setup.Events.AppendCount);
        Assert.Same(
            first,
            Assert.Single(
                await setup.Store.ListHistoryAsync()));
    }

    [Fact]
    public async Task Concurrent_writers_produce_one_revision_and_one_conflict()
    {
        var setup = Setup();
        using var scope =
            setup.Tenants.BeginScope(
                TelemetryPolicyTestData.TenantA);
        var first = TelemetryPolicyTestData.Revision(
            version: 1,
            TelemetryPolicyTestData.At(1, 10));
        await TelemetryPolicyTestData.CommitAsync(
            setup.Store,
            first,
            expectedVersion: 0);
        var candidateA =
            TelemetryPolicyTestData.Revision(
                version: 2,
                TelemetryPolicyTestData.At(1, 20),
                justification: "candidate a");
        var candidateB =
            TelemetryPolicyTestData.Revision(
                version: 2,
                TelemetryPolicyTestData.At(1, 21),
                justification: "candidate b");

        var results = await Task.WhenAll(
            TelemetryPolicyTestData.CommitAsync(
                setup.Store,
                candidateA,
                expectedVersion: 1),
            TelemetryPolicyTestData.CommitAsync(
                setup.Store,
                candidateB,
                expectedVersion: 1));

        Assert.Single(
            results,
            result => result.Status
                == TelemetryPolicyCommitStatus.Applied);
        Assert.Single(
            results,
            result => result.Status
                == TelemetryPolicyCommitStatus.Conflict);
        Assert.Equal(2, setup.Events.AppendCount);
        Assert.Equal(
            2,
            (await setup.Store.ListHistoryAsync())
                .Count);
    }

    private static async Task AssertRejectedByCeilingAsync(
        MutableCeilingResolver ceilings,
        TelemetryPolicyRule rule)
    {
        var setup = Setup(ceilings);
        using var scope =
            setup.Tenants.BeginScope(
                TelemetryPolicyTestData.TenantA);
        var revision = TelemetryPolicyTestData.Revision(
            version: 1,
            TelemetryPolicyTestData.At(1, 10),
            [rule]);

        await Assert.ThrowsAsync<
            TelemetryPolicyException>(
            () => TelemetryPolicyTestData.CommitAsync(
                setup.Store,
                revision,
                expectedVersion: 0));

        Assert.Equal(1, ceilings.CallCount);
        Assert.Equal(0, setup.Events.AppendCount);
        Assert.Empty(
            await setup.Store.ListHistoryAsync());
    }

    private static async Task AssertEveryOperationRejectsTenantAsync(
        InMemoryTelemetryPolicyStore store)
    {
        var revision = TelemetryPolicyTestData.Revision(
            version: 1,
            TelemetryPolicyTestData.At(1, 10),
            [TelemetryPolicyTestData.Rule()]);
        Func<Task>[] operations =
        [
            async () =>
            {
                _ = await store.GetAsync(1);
            },
            async () =>
            {
                _ = await store.ListHistoryAsync();
            },
            async () =>
            {
                _ = await store.FindAsOfAsync(
                    TelemetryPolicyTestData.At(8, 1),
                    TelemetryPolicyTestData.At(8, 2));
            },
            async () =>
            {
                _ = await store.ResolveAsync(
                    Query(
                        TelemetryPolicyTestData.Capability,
                        TelemetryPolicyTestData
                            .Applicability()));
            },
            async () =>
            {
                _ = await TelemetryPolicyTestData.CommitAsync(
                    store,
                    revision,
                    expectedVersion: 0);
            },
        ];

        foreach (var operation in operations)
        {
            await Assert.ThrowsAsync<
                InvalidOperationException>(
                operation);
        }
    }

    private static TelemetryPolicyResolutionQuery Query(
        TelemetryCapabilityRef capability,
        PrivacyApplicability applicability,
        DateTimeOffset? validAt = null,
        DateTimeOffset? recordedAt = null) =>
        new(
            capability,
            applicability,
            validAt ?? TelemetryPolicyTestData.At(8, 1),
            recordedAt ?? TelemetryPolicyTestData.At(8, 2));

    private static StoreSetup Setup(
        MutableCeilingResolver? ceilings = null)
    {
        var tenants = new TenantContextAccessor();
        var eventStore = new CapturingEventStore(
            TelemetryPolicyTestData.TenantA);
        var resolver =
            ceilings ?? new MutableCeilingResolver();
        return new(
            tenants,
            resolver,
            eventStore,
            new InMemoryTelemetryPolicyStore(
                tenants,
                resolver,
                eventStore));
    }

    private sealed record StoreSetup(
        TenantContextAccessor Tenants,
        MutableCeilingResolver Ceilings,
        CapturingEventStore Events,
        InMemoryTelemetryPolicyStore Store);
}
