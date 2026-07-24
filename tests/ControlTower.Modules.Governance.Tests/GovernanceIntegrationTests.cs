using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ControlTower.Adapters.InMemory;
using ControlTower.Modules.Governance.Application;
using ControlTower.Modules.Governance.Domain;
using ControlTower.Modules.Governance.Infrastructure;
using ControlTower.Platform.Events;
using ControlTower.Platform.Identity;
using ControlTower.Platform.Tenancy;
using Xunit;

namespace ControlTower.Modules.Governance.Tests;

public class GovernanceIntegrationTests
{
    private static readonly AuditActor Gov =
        AuditActor.System("governance");

    private sealed record Rig(
        GovernanceService Service,
        IGovernanceStore Store,
        IEventStore Events,
        CountingNativeControlOrchestrator Controls,
        TenantContextAccessor Accessor);

    private sealed class CountingNativeControlOrchestrator :
        INativeControlOrchestrator
    {
        public int RequestCount { get; private set; }

        public Task<NativeControlReceipt> RequestAsync(
            NativeControlIntent intent,
            CancellationToken ct = default)
        {
            RequestCount++;
            return Task.FromResult(
                new NativeControlReceipt(
                    Recorded: true,
                    Enforced: false,
                    Note: "test"));
        }
    }

    private static Rig Build()
    {
        var accessor = new TenantContextAccessor();
        var store = new InMemoryGovernanceStore(accessor);
        var events = new InMemoryEventStore(accessor);
        var controls =
            new CountingNativeControlOrchestrator();
        return new Rig(
            new GovernanceService(
                store,
                events,
                accessor,
                controls),
            store,
            events,
            controls,
            accessor);
    }

    [Fact]
    public async Task Governance_is_tenant_isolated()
    {
        var r = Build();
        using (r.Accessor.BeginScope(new TenantId(Guid.NewGuid())))
            await r.Service.OpenCaseAsync(Guid.NewGuid(), CaseType.Approval, RiskTier.Medium, DateTimeOffset.UtcNow, Gov);

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

        var id = await r.Service.OpenCaseAsync(Guid.NewGuid(), CaseType.Approval, RiskTier.Medium, now, Gov);
        await r.Service.RecordDecisionAsync(id, ReviewerRole.Governance, Gov, true, "meets policy", "ev", now);

        var stream = await r.Events.ReadAllAsync();
        Assert.True(stream.Count >= 3); // CaseOpened + DecisionRecorded + CaseApproved
    }

    [Fact]
    public async Task C2_records_native_control_intents_but_never_enforces()
    {
        var r = Build();
        using var _ = r.Accessor.BeginScope(new TenantId(Guid.NewGuid()));
        var id = await r.Service.OpenCaseAsync(Guid.NewGuid(), CaseType.Approval, RiskTier.Low, DateTimeOffset.UtcNow, Gov);

        var receipt = await r.Service.RequestNativeControlAsync(id, "block-agent", "asset-1", "policy breach", Gov);

        Assert.True(receipt.Recorded);
        Assert.False(receipt.Enforced); // enforcement is delegated to native platforms (ADR-002)
    }

    [Fact]
    public async Task Reuse_decision_via_service_preserves_action_reason_and_outcome()
    {
        var r = Build();
        using var _ = r.Accessor.BeginScope(new TenantId(Guid.NewGuid()));
        var now = DateTimeOffset.UtcNow;

        var id = await r.Service.OpenCaseAsync(Guid.NewGuid(), CaseType.ReuseDecision, RiskTier.Low, now, Gov);
        await r.Service.RecordReuseDecisionAsync(id, ReuseAction.Reuse, "the existing Sales agent fits", Gov, now);

        var view = (await r.Service.CasesAsync(now)).Single();
        Assert.Equal("Reuse", view.ReuseAction);
        Assert.Equal("reuse:Reuse", view.Outcome);
    }

    [Fact]
    public async Task Invalid_governance_evidence_never_changes_case_or_delegates_control()
    {
        var r = Build();
        using var _ = r.Accessor.BeginScope(
            new TenantId(Guid.NewGuid()));
        var now = DateTimeOffset.UtcNow;
        var id = await r.Service.OpenCaseAsync(
            Guid.NewGuid(),
            CaseType.Approval,
            RiskTier.Medium,
            now,
            Gov);
        var baseline = JsonSerializer.Serialize(
            Assert.Single(
                await r.Service.CasesAsync(now)));
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
            var commands = new Func<Task>[]
            {
                () => r.Service.RecordDecisionAsync(
                    id,
                    ReviewerRole.Governance,
                    Gov,
                    approved: true,
                    invalidReason,
                    evidenceRef: null,
                    now),
                () => r.Service.GrantWaiverAsync(
                    id,
                    Gov,
                    invalidReason,
                    now.AddDays(1),
                    now),
                () => r.Service.RecertifyAsync(
                    id,
                    Gov,
                    invalidReason,
                    now.AddDays(1),
                    now),
                () => r.Service.RequestRetirementAsync(
                    id,
                    Gov,
                    invalidReason),
                () => r.Service.RecordReuseDecisionAsync(
                    id,
                    ReuseAction.Reuse,
                    invalidReason,
                    Gov,
                    now),
                () => r.Service.RequestNativeControlAsync(
                    id,
                    "block-agent",
                    "asset-1",
                    invalidReason,
                    Gov),
            };

            foreach (var command in commands)
            {
                await Assert.ThrowsAsync<
                    GovernanceException>(command);
                Assert.Equal(
                    baseline,
                    JsonSerializer.Serialize(
                        Assert.Single(
                            await r.Service.CasesAsync(
                                now))));
                Assert.Equal(
                    baselineEventCount,
                    (await r.Events.ReadAllAsync())
                        .Count);
                Assert.Equal(
                    0,
                    r.Controls.RequestCount);
            }
        }
    }

    [Fact]
    public async Task Empty_asset_references_leave_governance_stores_empty()
    {
        var r = Build();
        using var _ = r.Accessor.BeginScope(
            new TenantId(Guid.NewGuid()));

        await Assert.ThrowsAsync<GovernanceException>(
            () => r.Service.OpenCaseAsync(
                Guid.Empty,
                CaseType.Approval,
                RiskTier.Medium,
                DateTimeOffset.UtcNow,
                Gov));
        await Assert.ThrowsAsync<GovernanceException>(
            () => r.Service.RaiseDebtAsync(
                Guid.Empty,
                DebtType.Ownerless,
                DateTimeOffset.UtcNow));

        Assert.Empty(
            await r.Store.CasesAsync());
        Assert.Empty(
            await r.Store.DebtAsync());
        Assert.Empty(await r.Events.ReadAllAsync());
    }
}
