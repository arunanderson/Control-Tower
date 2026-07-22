using System.Text;
using System.Text.Json;
using ControlTower.Adapters.InMemory;
using ControlTower.Modules.Economics.Application;
using ControlTower.Modules.Economics.Domain;
using ControlTower.Modules.Economics.Infrastructure;
using ControlTower.Platform.Events;
using ControlTower.Platform.Tenancy;

namespace ControlTower.Modules.Economics.Tests;

public class ReportingSnapshotTests
{
    private sealed record Rig(
        ReportingSnapshotService Service,
        IEconomicsStore Store,
        IEventStore Events,
        TenantContextAccessor Tenants);

    private static Rig Build()
    {
        var tenants = new TenantContextAccessor();
        var store = new InMemoryEconomicsStore(tenants);
        var events = new InMemoryEventStore(tenants);
        return new Rig(new ReportingSnapshotService(store, events, tenants), store, events, tenants);
    }

    private static ReportInputBasis Basis(string watermark = "observation:100") => new()
    {
        AsOf = new DateTimeOffset(2026, 6, 30, 23, 59, 59, TimeSpan.Zero),
        SourceReferences = ["cost-observations@2026-06-30", "value-declarations@2026-06-30"],
        RuleVersionReferences = ["allocation-rule:7", "fx-table:2026-06"],
        OrganisationModelVersion = "org-model:12",
        ObservationWatermark = watermark,
    };

    private static async Task<Guid> ClosingPeriodAsync(Rig rig)
    {
        var id = await rig.Service.CreatePeriodAsync(
            new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero));
        await rig.Service.BeginClosingAsync(id);
        return id;
    }

    [Fact]
    public async Task Freeze_persists_signed_immutable_version_one_and_rejects_repeat()
    {
        var rig = Build();
        using var _ = rig.Tenants.BeginScope(new TenantId(Guid.NewGuid()));
        var periodId = await ClosingPeriodAsync(rig);

        var snapshot = await rig.Service.FreezeAsync(periodId, "{\"totalSpend\":125.50}", Basis(), "finance@example.com");

        Assert.Equal(1, snapshot.Version);
        Assert.Equal("finance@example.com", snapshot.SignedBy);
        Assert.NotEmpty(snapshot.InputBasisHash);
        Assert.Null(snapshot.SupersedesSnapshotId);
        var period = Assert.Single(await rig.Service.PeriodsAsync());
        Assert.Equal("Frozen", period.State);
        await Assert.ThrowsAsync<EconomicsException>(() =>
            rig.Service.FreezeAsync(periodId, "{}", Basis(), "finance@example.com"));
    }

    [Fact]
    public async Task Restatement_appends_a_superseding_version_without_changing_version_one()
    {
        var rig = Build();
        using var _ = rig.Tenants.BeginScope(new TenantId(Guid.NewGuid()));
        var periodId = await ClosingPeriodAsync(rig);
        var first = await rig.Service.FreezeAsync(periodId, "{\"totalSpend\":125.50}", Basis(), "finance@example.com");
        var firstBytes = JsonSerializer.SerializeToUtf8Bytes(first);

        var second = await rig.Service.RestateAsync(
            periodId, "{\"totalSpend\":127.00}", Basis("observation:104"), "controller@example.com", "Late invoice");
        var history = await rig.Service.SnapshotsAsync(periodId);

        Assert.Equal(2, second.Version);
        Assert.Equal(first.Id, second.SupersedesSnapshotId);
        Assert.Equal("Late invoice", second.RestatementReason);
        Assert.Equal(firstBytes, JsonSerializer.SerializeToUtf8Bytes(history[0]));
        Assert.Equal("Restated", Assert.Single(await rig.Service.PeriodsAsync()).State);
    }

    [Fact]
    public async Task Freeze_and_restatement_events_capture_identity_signer_reason_and_hash_chain()
    {
        var rig = Build();
        using var _ = rig.Tenants.BeginScope(new TenantId(Guid.NewGuid()));
        var periodId = await ClosingPeriodAsync(rig);
        var first = await rig.Service.FreezeAsync(periodId, "{}", Basis(), "finance@example.com");
        var second = await rig.Service.RestateAsync(periodId, "{\"corrected\":true}", Basis("observation:101"), "controller@example.com", "Correction");

        var stream = await rig.Events.ReadAllAsync();
        Assert.Equal(2, stream.Count);
        var frozen = Encoding.UTF8.GetString(stream[0].Payload);
        var restated = Encoding.UTF8.GetString(stream[1].Payload);
        Assert.Contains(first.Id.ToString(), frozen, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("finance@example.com", frozen);
        Assert.Contains(second.Id.ToString(), restated, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("controller@example.com", restated);
        Assert.Contains("Correction", restated);
        Assert.Equal(stream[0].Hash, stream[1].PreviousHash);
    }

    [Fact]
    public async Task Periods_and_snapshots_are_tenant_isolated()
    {
        var rig = Build();
        var tenantA = new TenantId(Guid.NewGuid());
        Guid periodId;
        using (rig.Tenants.BeginScope(tenantA))
        {
            periodId = await ClosingPeriodAsync(rig);
            await rig.Service.FreezeAsync(periodId, "{}", Basis(), "finance@example.com");
        }

        using (rig.Tenants.BeginScope(new TenantId(Guid.NewGuid())))
        {
            Assert.Empty(await rig.Service.PeriodsAsync());
            await Assert.ThrowsAsync<EconomicsException>(() => rig.Service.SnapshotsAsync(periodId));
        }
    }
}
