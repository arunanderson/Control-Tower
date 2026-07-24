using ControlTower.Modules.Trust.Privacy;
using ControlTower.Platform.Events;
using ControlTower.Platform.Identity;
using ControlTower.Platform.Privacy;
using ControlTower.Platform.Tenancy;

namespace ControlTower.Modules.Trust.Tests;

public sealed class TelemetryPolicyDomainTests
{
    [Fact]
    public void Revision_defensively_owns_and_orders_rules()
    {
        var later = TelemetryPolicyTestData.Rule(
            capability: new("z-capability"));
        var earlier = TelemetryPolicyTestData.Rule(
            capability: new("a-capability"));
        var source = new List<TelemetryPolicyRule>
        {
            later,
            earlier,
        };

        var revision = TelemetryPolicyTestData.Revision(
            version: 1,
            TelemetryPolicyTestData.At(1, 10),
            source);
        source.Clear();

        Assert.Equal(2, revision.Rules.Count);
        Assert.Equal(
            "a-capability",
            revision.Rules[0].Capability.Value);
        var mutableView =
            Assert.IsAssignableFrom<
                IList<TelemetryPolicyRule>>(
                revision.Rules);
        Assert.Throws<NotSupportedException>(
            () => mutableView.Add(
                TelemetryPolicyTestData.Rule()));
    }

    [Fact]
    public void Revision_rejects_invalid_identity_time_actor_and_history_shape()
    {
        var validFrom = TelemetryPolicyTestData.At(1, 1);
        var recordedAt = TelemetryPolicyTestData.At(1, 10);
        var validRule = TelemetryPolicyTestData.Rule();

        Assert.Throws<TelemetryPolicyException>(
            () => new TelemetryPolicyRevision(
                default,
                1,
                validFrom,
                TelemetryPolicyTestData.At(12, 31),
                recordedAt,
                TelemetryPolicyTestData.Actor,
                "reason",
                [validRule]));
        Assert.Throws<TelemetryPolicyException>(
            () => new TelemetryPolicyRevision(
                TelemetryPolicyTestData.TenantA,
                0,
                validFrom,
                TelemetryPolicyTestData.At(12, 31),
                recordedAt,
                TelemetryPolicyTestData.Actor,
                "reason",
                [validRule]));
        Assert.Throws<ArgumentException>(
            () => new TelemetryPolicyRevision(
                TelemetryPolicyTestData.TenantA,
                1,
                validFrom.AddTicks(1),
                TelemetryPolicyTestData.At(12, 31),
                recordedAt,
                TelemetryPolicyTestData.Actor,
                "reason",
                [validRule]));
        Assert.Throws<ArgumentException>(
            () => new TelemetryPolicyRevision(
                TelemetryPolicyTestData.TenantA,
                1,
                validFrom,
                TelemetryPolicyTestData.At(12, 31),
                recordedAt.AddTicks(1),
                TelemetryPolicyTestData.Actor,
                "reason",
                [validRule]));
        Assert.Throws<ArgumentException>(
            () => new TelemetryPolicyRevision(
                TelemetryPolicyTestData.TenantA,
                1,
                validFrom,
                TelemetryPolicyTestData.At(12, 31),
                default,
                TelemetryPolicyTestData.Actor,
                "reason",
                [validRule]));
        Assert.Throws<TelemetryPolicyException>(
            () => new TelemetryPolicyRevision(
                TelemetryPolicyTestData.TenantA,
                1,
                validFrom,
                validFrom,
                recordedAt,
                TelemetryPolicyTestData.Actor,
                "reason",
                [validRule]));
        Assert.Throws<TelemetryPolicyException>(
            () => new TelemetryPolicyRevision(
                TelemetryPolicyTestData.TenantA,
                1,
                validFrom,
                TelemetryPolicyTestData.At(12, 31),
                recordedAt,
                default,
                "reason",
                [validRule]));
        Assert.Throws<TelemetryPolicyException>(
            () => new TelemetryPolicyRevision(
                TelemetryPolicyTestData.TenantA,
                1,
                validFrom,
                TelemetryPolicyTestData.At(12, 31),
                recordedAt,
                TelemetryPolicyTestData.Actor,
                " reason ",
                [validRule]));
        Assert.Throws<TelemetryPolicyException>(
            () => new TelemetryPolicyRevision(
                TelemetryPolicyTestData.TenantA,
                1,
                validFrom,
                TelemetryPolicyTestData.At(12, 31),
                recordedAt,
                TelemetryPolicyTestData.Actor,
                new string('x', 2049),
                [validRule]));
        Assert.Throws<TelemetryPolicyException>(
            () => new TelemetryPolicyRevision(
                TelemetryPolicyTestData.TenantA,
                1,
                validFrom,
                TelemetryPolicyTestData.At(12, 31),
                recordedAt,
                TelemetryPolicyTestData.Actor,
                "reason",
                [validRule, validRule]));
    }

    [Fact]
    public void Enabled_L2_or_higher_requires_complete_activation_evidence()
    {
        var capability =
            TelemetryPolicyTestData.Capability;
        var approval = new EventReference(
            "policy-approval",
            "approval-1");
        var retention =
            new RetentionPolicyRef("retention-1");

        Assert.Throws<TelemetryPolicyException>(
            () => new TelemetryPolicyRule(
                capability,
                null,
                null,
                enabled: true,
                PrivacyMarking.L2));
        Assert.Throws<TelemetryPolicyException>(
            () => new TelemetryPolicyRule(
                capability,
                null,
                null,
                enabled: true,
                PrivacyMarking.L2,
                "purpose",
                approvalReference: null,
                retention));
        Assert.Throws<TelemetryPolicyException>(
            () => new TelemetryPolicyRule(
                capability,
                null,
                null,
                enabled: true,
                PrivacyMarking.L2,
                "purpose",
                approval,
                retentionPolicy: null));

        var valid = new TelemetryPolicyRule(
            capability,
            null,
            null,
            enabled: true,
            PrivacyMarking.L2,
            "purpose",
            approval,
            retention);
        Assert.Equal("purpose", valid.ActivationPurpose);

        Assert.Throws<TelemetryPolicyException>(
            () => new TelemetryPolicyRule(
                capability,
                null,
                null,
                enabled: true,
                PrivacyMarking.L1,
                "unnecessary evidence",
                approval,
                retention));
    }

    [Fact]
    public void Enabled_L4_is_bounded_by_an_explicit_policy_time_limit()
    {
        Assert.Throws<TelemetryPolicyException>(
            () => new TelemetryPolicyRule(
                TelemetryPolicyTestData.Capability,
                null,
                null,
                enabled: true,
                PrivacyMarking.L4,
                "purpose",
                new EventReference(
                    "policy-approval",
                    "approval-1"),
                new RetentionPolicyRef("retention-1")));

        var nonCanonical =
            TelemetryPolicyTestData.At(6, 1)
                .AddTicks(1);
        Assert.Throws<ArgumentException>(
            () => TelemetryPolicyTestData.Rule(
                PrivacyMarking.L4,
                timeLimit: nonCanonical));

        var outsideRevision =
            TelemetryPolicyTestData.Rule(
                PrivacyMarking.L4,
                timeLimit:
                    TelemetryPolicyTestData.At(12, 31));
        Assert.Throws<TelemetryPolicyException>(
            () => TelemetryPolicyTestData.Revision(
                version: 1,
                TelemetryPolicyTestData.At(1, 10),
                [outsideRevision],
                validTo:
                    TelemetryPolicyTestData.At(6, 1)));
    }

    [Fact]
    public void Fingerprint_distinguishes_absent_and_literal_dash_scopes()
    {
        var noScope = TelemetryPolicyTestData.Revision(
            version: 1,
            TelemetryPolicyTestData.At(1, 10),
            [
                TelemetryPolicyTestData.Rule(),
            ]);
        var dashJurisdiction =
            TelemetryPolicyTestData.Revision(
                version: 1,
                TelemetryPolicyTestData.At(1, 10),
                [
                    TelemetryPolicyTestData.Rule(
                        jurisdiction:
                            new JurisdictionRef("-")),
                ]);
        var dashPopulation =
            TelemetryPolicyTestData.Revision(
                version: 1,
                TelemetryPolicyTestData.At(1, 10),
                [
                    TelemetryPolicyTestData.Rule(
                        population:
                            new PopulationRef("-")),
                ]);

        Assert.NotEqual(
            noScope.Fingerprint,
            dashJurisdiction.Fingerprint);
        Assert.NotEqual(
            noScope.Fingerprint,
            dashPopulation.Fingerprint);
        Assert.NotEqual(
            dashJurisdiction.Fingerprint,
            dashPopulation.Fingerprint);
    }

    [Fact]
    public void Canonical_change_is_privileged_and_binds_optional_correlation()
    {
        var source = new List<TelemetryPolicyRule>
        {
            TelemetryPolicyTestData.Rule(
                capability:
                    new TelemetryCapabilityRef(
                        "z-capability")),
            TelemetryPolicyTestData.Rule(
                capability:
                    new TelemetryCapabilityRef(
                        "a-capability")),
        };
        var revision = TelemetryPolicyTestData.Revision(
            version: 1,
            TelemetryPolicyTestData.At(1, 10),
            source);
        var correlation = new EventReference(
            "request",
            "request-1");
        var changed =
            TelemetryPolicyCommitSemantics.Changed(
                revision,
                correlation) with
            {
                Rules = source,
            };
        source.Clear();
        var metadata =
            TelemetryPolicyCommitSemantics.Metadata(
                revision,
                correlation);

        TelemetryPolicyCommitSemantics.Validate(
            revision,
            changed,
            metadata,
            expectedVersion: 0);
        var contract = DomainEventContracts.Resolve(
            changed);
        Assert.Equal(
            "TelemetryPolicyChanged",
            contract.EventType);
        Assert.Equal(
            EventPrivilege.Privileged,
            contract.Privilege);
        Assert.Equal(
            correlation,
            changed.CorrelationReference);
        Assert.Equal(
            correlation,
            metadata.CorrelationReference);
        Assert.Equal(2, changed.Rules.Count);
        Assert.Equal(
            "a-capability",
            changed.Rules[0].Capability.Value);
        var mutableView =
            Assert.IsAssignableFrom<
                IList<TelemetryPolicyRule>>(
                changed.Rules);
        Assert.Throws<NotSupportedException>(
            () => mutableView.Add(
                TelemetryPolicyTestData.Rule()));
    }
}
