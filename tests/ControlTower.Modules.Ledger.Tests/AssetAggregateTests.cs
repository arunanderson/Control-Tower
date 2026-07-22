using System;
using System.Linq;
using ControlTower.Modules.Ledger.Domain;
using ControlTower.Platform.Tenancy;
using Xunit;

namespace ControlTower.Modules.Ledger.Tests;

public class AssetAggregateTests
{
    private static readonly TenantId Tenant = new(Guid.NewGuid());
    private static AIAsset Discover(string type = "agent") =>
        AIAsset.Discover(Tenant, "Sales Copilot", new AssetType(type), TaxonomyScheme.Default);

    [Fact]
    public void Discover_creates_a_discovered_draft_and_raises_the_event()
    {
        var asset = Discover();
        Assert.Equal(RegistrationStatus.Discovered, asset.RegistrationStatus);
        Assert.Equal(OperationalLifecycleState.Draft, asset.OperationalLifecycleState);
        Assert.True(asset.IsOwnerless);
        Assert.Contains(asset.DequeueEvents(), e => e is AssetDiscovered);
    }

    [Fact]
    public void Discover_rejects_a_type_outside_the_taxonomy()
    {
        Assert.Throws<DomainException>(() => Discover("not-a-real-type"));
    }

    [Fact]
    public void Register_before_triage_is_illegal()
    {
        var asset = Discover();
        Assert.Throws<DomainException>(() => asset.Register("purpose", new PersonRef("o", "Alice")));
    }

    [Fact]
    public void Happy_path_discover_triage_register_assigns_owner_and_purpose()
    {
        var asset = Discover();
        asset.Triage();
        asset.Register("Drafts sales emails", new PersonRef("obj-1", "Alice"));

        Assert.Equal(RegistrationStatus.Registered, asset.RegistrationStatus);
        Assert.Equal("Drafts sales emails", asset.BusinessPurpose);
        Assert.False(asset.IsOwnerless);
    }

    [Fact]
    public void Register_requires_a_business_purpose()
    {
        var asset = Discover();
        asset.Triage();
        Assert.Throws<DomainException>(() => asset.Register(" ", new PersonRef("o", "Alice")));
    }

    [Fact]
    public void Retire_requires_registered()
    {
        var asset = Discover();
        Assert.Throws<DomainException>(asset.Retire);
    }

    [Fact]
    public void Lifecycle_allows_valid_and_blocks_invalid_transitions()
    {
        var asset = Discover();
        asset.TransitionLifecycle(OperationalLifecycleState.Pilot);
        asset.TransitionLifecycle(OperationalLifecycleState.Production);
        Assert.Equal(OperationalLifecycleState.Production, asset.OperationalLifecycleState);

        var other = Discover();
        Assert.Throws<DomainException>(() => other.TransitionLifecycle(OperationalLifecycleState.Production));
    }

    [Fact]
    public void Ownership_lapse_makes_the_asset_ownerless_then_reassignment_restores_an_owner()
    {
        var asset = Discover();
        asset.Triage();
        var alice = new PersonRef("a", "Alice");
        asset.Register("purpose", alice);
        Assert.False(asset.IsOwnerless);

        asset.LapseOwnership(alice);
        Assert.True(asset.IsOwnerless);

        asset.AssignOwnership(new PersonRef("b", "Bob"), OwnershipRole.Owner);
        Assert.False(asset.IsOwnerless);
        Assert.Equal(2, asset.Ownerships.Count); // history is never overwritten
    }

    [Fact]
    public void Resolution_link_confidence_rolls_up_to_the_weakest_material_link()
    {
        var asset = Discover();
        Assert.Equal(MatchConfidence.Manual, asset.MatchConfidence);

        var low = asset.AddResolutionLink(NativeIdentifierSet.Of(new NativeIdentifier("csv", "csv:key", "b1")),
            MatchMethod.Heuristic, MatchConfidence.Low, "system");
        Assert.Equal(MatchConfidence.Low, asset.MatchConfidence);

        // A stronger link does NOT mask the weak one — lowest-confidence-wins (ADR-024/025).
        asset.AddResolutionLink(NativeIdentifierSet.Of(new NativeIdentifier("csv", "csv:key", "a1")),
            MatchMethod.DocumentedJoin, MatchConfidence.High, "system");
        Assert.Equal(MatchConfidence.Low, asset.MatchConfidence);

        // Sever (not delete) the weak link → the link is retained, and roll-up rises to High.
        asset.SeverResolutionLink(low.Id, "system", "superseded by documented join");
        Assert.Equal(MatchConfidence.High, asset.MatchConfidence);
        Assert.Equal(2, asset.ResolutionLinks.Count); // severed link retained
        Assert.Single(asset.ActiveResolutionLinks);
        Assert.Contains(asset.ResolutionLinks, l => l.Id == low.Id && l.Status == LinkStatus.Severed);
    }

    [Fact]
    public void Match_confidence_change_raises_an_event()
    {
        var asset = Discover();
        asset.DequeueEvents();
        asset.AddResolutionLink(NativeIdentifierSet.Of(new NativeIdentifier("graph", "appId", "a1")),
            MatchMethod.DocumentedJoin, MatchConfidence.High, "system");
        Assert.Contains(asset.DequeueEvents(), e => e is MatchConfidenceChanged { To: MatchConfidence.High });
    }
}
