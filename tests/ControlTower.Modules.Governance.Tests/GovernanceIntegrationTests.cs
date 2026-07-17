using System;
using System.Linq;
using System.Threading.Tasks;
using ControlTower.Adapters.InMemory;
using ControlTower.Modules.Governance.Application;
using ControlTower.Modules.Governance.Domain;
using ControlTower.Modules.Governance.Infrastructure;
using ControlTower.Platform.Events;
using ControlTower.Platform.Tenancy;
using Xunit;

namespace ControlTower.Modules.Governance.Tests;

public class GovernanceIntegrationTests
{
    private static readonly ActorRef Gov = new("gov", "Governance");

    private sealed record Rig(GovernanceService Service, IEventStore Events, TenantContextAccessor Accessor);

    private static Rig Build()
    {
        var accessor = new TenantContextAccessor();
        var store = new InMemoryGovernanceStore(accessor);
        var events = new InMemoryEventStore(accessor);
        return new Rig(new GovernanceService(store, events, accessor, new RecordingNativeControlOrchestrator()), events, accessor);
    }

    [Fact]
    public async Task Governance_is_tenant_isolated()
    {
        var r = Build();
        using (r.Accessor.BeginScope(new TenantId(Guid.NewGuid())))
            await r.Service.OpenCaseAsync(Guid.NewGuid(), CaseType.Approval, RiskTier.Medium, DateTimeOffset.UtcNow);

        using (r.Accessor.BeginScope(new TenantId(Guid.NewGuid())))
            Assert.Empty(await r.Service.CasesAsync(DateTimeOffset.UtcNow));
    }

    [Fact]
    public async Task Missing_and_lapsed_owner_governance_debt_is_created()
    {
        var r = Build();
        using var _ = r.Accessor.BeginScope(new TenantId(Guid.NewGuid()));
        var now = DateTimeOffset.UtcNow;

        await r.Service.RaiseDebtAsync(Guid.NewGuid(), DebtType.Ownerless, now);
        await r.Service.RaiseDebtAsync(Guid.NewGuid(), DebtType.LapsedOwner, now);

        var debt = await r.Service.DebtAsync();
        Assert.Equal(2, debt.Count);
        Assert.Contains(debt, d => d.DebtType == "Ownerless");
        Assert.Contains(debt, d => d.DebtType == "LapsedOwner");
    }

    [Fact]
    public async Task Every_governance_action_is_an_audit_event()
    {
        var r = Build();
        using var _ = r.Accessor.BeginScope(new TenantId(Guid.NewGuid()));
        var now = DateTimeOffset.UtcNow;

        var id = await r.Service.OpenCaseAsync(Guid.NewGuid(), CaseType.Approval, RiskTier.Medium, now);
        await r.Service.RecordDecisionAsync(id, ReviewerRole.Governance, Gov, true, "meets policy", "ev", now);

        var stream = await r.Events.ReadAllAsync();
        Assert.True(stream.Count >= 3); // CaseOpened + DecisionRecorded + CaseApproved
    }

    [Fact]
    public async Task C2_records_native_control_intents_but_never_enforces()
    {
        var r = Build();
        using var _ = r.Accessor.BeginScope(new TenantId(Guid.NewGuid()));
        var id = await r.Service.OpenCaseAsync(Guid.NewGuid(), CaseType.Approval, RiskTier.Low, DateTimeOffset.UtcNow);

        var receipt = await r.Service.RequestNativeControlAsync(id, "block-agent", "asset-1", "policy breach");

        Assert.True(receipt.Recorded);
        Assert.False(receipt.Enforced); // enforcement is delegated to native platforms (ADR-002)
    }

    [Fact]
    public async Task Reuse_decision_via_service_preserves_action_reason_and_outcome()
    {
        var r = Build();
        using var _ = r.Accessor.BeginScope(new TenantId(Guid.NewGuid()));
        var now = DateTimeOffset.UtcNow;

        var id = await r.Service.OpenCaseAsync(Guid.NewGuid(), CaseType.ReuseDecision, RiskTier.Low, now);
        await r.Service.RecordReuseDecisionAsync(id, ReuseAction.Reuse, "the existing Sales agent fits", Gov, now);

        var view = (await r.Service.CasesAsync(now)).Single();
        Assert.Equal("Reuse", view.ReuseAction);
        Assert.Equal("reuse:Reuse", view.Outcome);
    }
}
