using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ControlTower.Modules.Providers.Domain;
using ControlTower.Modules.Providers.Infrastructure;
using Xunit;

namespace ControlTower.Modules.Providers.Tests;

public class CsvManualImportProviderTests
{
    private static ProviderConnectionContext Ctx(string csv) =>
        new("conn", "", new Dictionary<string, string> { ["csv"] = csv });

    [Fact]
    public async Task It_parses_rows_into_manual_import_observations()
    {
        const string csv = "key,displayName,assetType,cost,currency\nbot-1,Sales Copilot,agent,100,EUR\nbot-2,HR Flow,flow,50,EUR";
        var provider = new CsvManualImportProvider();

        var observations = new List<RawObservation>();
        await foreach (var o in provider.AcquireAsync(Ctx(csv), ProviderCapability.Cost))
            observations.Add(o);

        Assert.Equal(2, observations.Count);
        Assert.All(observations, o => Assert.Equal(CsvManualImportProvider.ManualImportLabel, o.EvidenceLabel));
        Assert.All(observations, o => Assert.Equal("manual-csv", o.SurfaceId));
        Assert.Contains(observations, o =>
            o.NativeIdentifiers.Any(n => n.IdentifierType == "csv:key" && n.Value == "bot-1") &&
            o.Attributes["displayName"] == "Sales Copilot" &&
            o.Attributes["cost"] == "100");
    }

    [Fact]
    public async Task An_undeclared_capability_yields_nothing()
    {
        var provider = new CsvManualImportProvider();
        var any = false;
        await foreach (var _ in provider.AcquireAsync(Ctx("key\nx"), ProviderCapability.Identity)) any = true;
        Assert.False(any);
    }

    [Fact]
    public async Task Health_reflects_whether_a_file_is_supplied()
    {
        var provider = new CsvManualImportProvider();
        Assert.Equal(ProviderHealthStatus.Healthy, (await provider.CheckHealthAsync(Ctx("key\nx"))).Status);
        Assert.Equal(
            ProviderHealthStatus.Disconnected,
            (await provider.CheckHealthAsync(new ProviderConnectionContext("c", "", new Dictionary<string, string>()))).Status);
    }
}
