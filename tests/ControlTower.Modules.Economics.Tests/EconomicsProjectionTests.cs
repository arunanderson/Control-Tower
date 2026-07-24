using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ControlTower.Adapters.InMemory;
using ControlTower.Modules.Economics.Application;
using ControlTower.Modules.Economics.Domain;
using ControlTower.Modules.Economics.Infrastructure;
using ControlTower.Platform.Events;
using ControlTower.Platform.Identity;
using ControlTower.Platform.Tenancy;
using Xunit;

namespace ControlTower.Modules.Economics.Tests;

public class EconomicsProjectionTests
{
    private static AuditActor Actor(string id) =>
        AuditActor.System(id);

    private static EconomicFigure Cost(decimal amount) =>
        new(new Money(amount, "EUR"), new Evidence("Azure Cost Management", EvidenceClass.Measured, "billing meter", DateTimeOffset.UtcNow, ValidationState.SystemObserved));

    private static EconomicFigure Value(decimal amount, EvidenceClass evidenceClass, ValidationState state) =>
        new(new Money(amount, "EUR"), new Evidence("declarer", evidenceClass, "value methodology", DateTimeOffset.UtcNow, state));

    private sealed record Rig(
        EconomicsIngestionService Ingest,
        EconomicsProjectionService Project,
        IEconomicsStore Store,
        IEventStore Events,
        TenantContextAccessor Accessor);

    private static Rig Build()
    {
        var accessor = new TenantContextAccessor();
        var store = new InMemoryEconomicsStore(accessor);
        var events = new InMemoryEventStore(accessor);
        return new Rig(
            new EconomicsIngestionService(
                store,
                events,
                accessor),
            new EconomicsProjectionService(store),
            store,
            events,
            accessor);
    }

    private static async Task SeedAsync(Rig r)
    {
        var start = DateTimeOffset.UtcNow.AddDays(-30);
        var end = DateTimeOffset.UtcNow.AddDays(-1);
        var agent1 = Guid.NewGuid();
        var agent2 = Guid.NewGuid();
        var flow = Guid.NewGuid();

        await r.Ingest.IngestCostAsync(agent1, "agent", Cost(100m), start, end, Actor("cost-ingestion"), "Sales", "Commercial");
        await r.Ingest.DeclareValueAsync(agent1, "agent", BenefitType.CostAvoided, Value(300m, EvidenceClass.FinanciallyValidated, ValidationState.FinanceVerified), Actor("finance"));
        await r.Ingest.IngestCostAsync(agent2, "agent", Cost(200m), start, end, Actor("cost-ingestion"), "Engineering", "Tech");
        await r.Ingest.DeclareValueAsync(agent2, "agent", BenefitType.TimeSaved, Value(100m, EvidenceClass.SelfReported, ValidationState.Estimated), Actor("owner"));
        await r.Ingest.IngestCostAsync(flow, "flow", Cost(50m), start, end, Actor("cost-ingestion")); // untagged → Unattributed
    }

    [Fact]
    public async Task One_model_produces_asset_agent_department_portfolio_and_executive_views()
    {
        var r = Build();
        using var _ = r.Accessor.BeginScope(new TenantId(Guid.NewGuid()));
        await SeedAsync(r);
        var asOf = DateTimeOffset.UtcNow;

        var assets = await r.Project.AssetEconomicsAsync(asOf);
        Assert.Equal(3, assets.Count);

        var agentRoi = await r.Project.AgentRoiAsync(asOf);
        Assert.Equal(300m, agentRoi.Cost.Amount); // agents only: 100 + 200
        Assert.Equal(400m, agentRoi.Value.Amount); // 300 + 100

        var portfolio = await r.Project.PortfolioRoiAsync(asOf);
        Assert.Equal(350m, portfolio.Cost.Amount); // includes the flow's 50

        var departments = await r.Project.DepartmentRoiAsync(asOf);
        Assert.Contains(departments, d => d.Scope == "department:Sales");
        Assert.Contains(departments, d => d.Scope == "department:Unattributed");
    }

    [Fact]
    public async Task Unattributed_cost_is_never_spread_into_departments()
    {
        var r = Build();
        using var _ = r.Accessor.BeginScope(new TenantId(Guid.NewGuid()));
        await SeedAsync(r);
        var asOf = DateTimeOffset.UtcNow;

        var departments = await r.Project.DepartmentRoiAsync(asOf);
        var sales = departments.Single(d => d.Scope == "department:Sales");
        var unattributed = departments.Single(d => d.Scope == "department:Unattributed");

        Assert.Equal(100m, sales.Cost.Amount);        // the flow's 50 is NOT added here
        Assert.Equal(50m, unattributed.Cost.Amount);  // it stays visible in Unattributed

        var exec = await r.Project.ExecutiveAsync(asOf);
        Assert.Equal(50m, exec.UnattributedCost.Amount);
        Assert.Equal(300m / 400m, exec.ValidatedToDeclaredRatio); // only agent1's 300 is validated
    }

    [Fact]
    public async Task No_economic_amount_is_exposed_without_evidence_fields()
    {
        var r = Build();
        using var _ = r.Accessor.BeginScope(new TenantId(Guid.NewGuid()));
        await SeedAsync(r);
        var asOf = DateTimeOffset.UtcNow;

        var amounts = new List<EconomicAmount>();
        foreach (var a in await r.Project.AssetEconomicsAsync(asOf))
        {
            amounts.Add(a.Cost);
            amounts.Add(a.Value);
            amounts.Add(a.NetBenefit);
        }
        var exec = await r.Project.ExecutiveAsync(asOf);
        amounts.Add(exec.TotalSpend);
        amounts.Add(exec.DeclaredValue);
        amounts.Add(exec.UnattributedCost);

        Assert.NotEmpty(amounts);
        foreach (var amount in amounts)
        {
            Assert.False(string.IsNullOrWhiteSpace(amount.EvidenceClass));
            Assert.False(string.IsNullOrWhiteSpace(amount.Source));
            Assert.False(string.IsNullOrWhiteSpace(amount.Methodology));
            Assert.False(string.IsNullOrWhiteSpace(amount.ValidationState));
            Assert.NotEqual(default, amount.AsOf);
        }
    }

    [Fact]
    public async Task Projection_is_reproducible_for_a_given_as_of()
    {
        var r = Build();
        using var _ = r.Accessor.BeginScope(new TenantId(Guid.NewGuid()));
        await SeedAsync(r);
        var asOf = DateTimeOffset.UtcNow;

        var first = await r.Project.PortfolioRoiAsync(asOf);
        var second = await r.Project.PortfolioRoiAsync(asOf);

        Assert.Equal(first.Cost.Amount, second.Cost.Amount);
        Assert.Equal(first.Value.Amount, second.Value.Amount);
        Assert.Equal(first.SinglePointRoi, second.SinglePointRoi);
        Assert.Equal(first.ValidatedOnlyRoi, second.ValidatedOnlyRoi);
    }

    [Fact]
    public async Task Economics_is_tenant_isolated()
    {
        var r = Build();
        using (r.Accessor.BeginScope(new TenantId(Guid.NewGuid())))
        {
            await SeedAsync(r);
        }

        using (r.Accessor.BeginScope(new TenantId(Guid.NewGuid())))
        {
            Assert.Empty(await r.Project.AssetEconomicsAsync(DateTimeOffset.UtcNow));
        }
    }

    [Fact]
    public async Task Value_validation_is_forward_only_and_never_overwrites_history()
    {
        var r = Build();
        using var _ = r.Accessor.BeginScope(new TenantId(Guid.NewGuid()));
        var assetId = Guid.NewGuid();

        var declId = await r.Ingest.DeclareValueAsync(assetId, "agent", BenefitType.CostAvoided,
            Value(300m, EvidenceClass.SelfReported, ValidationState.Estimated), Actor("owner"));

        await r.Ingest.ValidateValueAsync(declId,
            Value(300m, EvidenceClass.Measured, ValidationState.BusinessValidated), "reviewed with owner", Actor("coe"));
        await r.Ingest.ValidateValueAsync(declId,
            Value(280m, EvidenceClass.FinanciallyValidated, ValidationState.FinanceVerified), "GL reconciled", Actor("finance"));

        var declaration = await r.Store.GetDeclarationAsync(declId);
        Assert.NotNull(declaration);
        Assert.Equal(ValidationState.FinanceVerified, declaration!.State);
        Assert.Equal(3, declaration.Revisions.Count); // initial + 2 validations; nothing overwritten

        // Moving backward is rejected.
        await Assert.ThrowsAsync<EconomicsException>(() =>
            r.Ingest.ValidateValueAsync(declId, Value(280m, EvidenceClass.SelfReported, ValidationState.Estimated), "oops", Actor("someone")));
    }

    [Fact]
    public async Task Invalid_economics_evidence_fails_before_store_mutation()
    {
        var r = Build();
        using var _ = r.Accessor.BeginScope(
            new TenantId(Guid.NewGuid()));
        var now = DateTimeOffset.UtcNow;

        await Assert.ThrowsAsync<EconomicsException>(
            () => r.Ingest.IngestCostAsync(
                Guid.Empty,
                "agent",
                Cost(100m),
                now.AddDays(-1),
                now,
                Actor("cost-ingestion")));
        await Assert.ThrowsAsync<EconomicsException>(
            () => r.Ingest.DeclareValueAsync(
                Guid.Empty,
                "agent",
                BenefitType.CostAvoided,
                Value(
                    300m,
                    EvidenceClass.SelfReported,
                    ValidationState.Estimated),
                Actor("owner")));

        Assert.Empty(await r.Store.CostsAsync());
        Assert.Empty(await r.Store.DeclarationsAsync());
        Assert.Empty(await r.Events.ReadAllAsync());

        var assetId = Guid.NewGuid();
        var declarationId =
            await r.Ingest.DeclareValueAsync(
                assetId,
                "agent",
                BenefitType.CostAvoided,
                Value(
                    300m,
                    EvidenceClass.SelfReported,
                    ValidationState.Estimated),
                Actor("owner"));
        var baselineEventCount =
            (await r.Events.ReadAllAsync()).Count;

        foreach (var invalidReason in new[]
                 {
                     " padded reason ",
                     new string('x', 2049),
                     "invalid\u0001reason",
                     "invalid\uD800reason",
                 })
        {
            await Assert.ThrowsAsync<EconomicsException>(
                () => r.Ingest.ValidateValueAsync(
                    declarationId,
                    Value(
                        300m,
                        EvidenceClass.Measured,
                        ValidationState.BusinessValidated),
                    invalidReason,
                    Actor("finance")));

            var declaration =
                await r.Store.GetDeclarationAsync(
                    declarationId);
            Assert.NotNull(declaration);
            Assert.Equal(
                ValidationState.Estimated,
                declaration!.State);
            Assert.Single(declaration.Revisions);
            Assert.Equal(
                baselineEventCount,
                (await r.Events.ReadAllAsync()).Count);
        }
    }
}
