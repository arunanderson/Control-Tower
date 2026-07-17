using System.Runtime.CompilerServices;
using ControlTower.Modules.Providers.Domain;

namespace ControlTower.Modules.Providers.Infrastructure;

/// <summary>
/// The manual CSV import provider (ADR-013) — an ordinary provider implementing the same C4.5 contract
/// as any other. It requires no credentials; observations are labelled "Self-reported / Manual Import".
/// Expected CSV columns: key,displayName,assetType[,cost,currency]. Simple comma parsing (no quoted
/// fields yet — a documented limitation).
/// </summary>
public sealed class CsvManualImportProvider : IProvider
{
    public const string ManualImportLabel = "Self-reported / Manual Import";

    public ProviderManifest Manifest { get; } = new()
    {
        SurfaceId = "manual-csv",
        DisplayName = "Manual CSV Import",
        Version = "1.0.0",
        Capabilities = new HashSet<ProviderCapability> { ProviderCapability.Inventory, ProviderCapability.Cost },
        NativeIdentifierTypes = ["csv:key"],
        PayloadSchemaVersion = 1,
        Auth = new ProviderAuthRequirement(ProviderAuthKind.None, [], "Operator-provided file; no credentials."),
        FreshnessExpectation = TimeSpan.FromDays(30),
        HealthNote = "Healthy when a CSV payload is supplied via settings['csv'].",
    };

    public Task<ProviderHealth> CheckHealthAsync(ProviderConnectionContext context, CancellationToken ct = default)
    {
        var hasCsv = context.Settings.ContainsKey("csv");
        return Task.FromResult(new ProviderHealth(
            hasCsv ? ProviderHealthStatus.Healthy : ProviderHealthStatus.Disconnected,
            DateTimeOffset.UtcNow,
            hasCsv ? "CSV payload supplied" : "No CSV payload"));
    }

    public async IAsyncEnumerable<RawObservation> AcquireAsync(
        ProviderConnectionContext context, ProviderCapability capability, [EnumeratorCancellation] CancellationToken ct = default)
    {
        await Task.CompletedTask;
        if (!Manifest.Capabilities.Contains(capability)) yield break;
        if (!context.Settings.TryGetValue("csv", out var csv) || string.IsNullOrWhiteSpace(csv)) yield break;

        var lines = csv.Replace("\r\n", "\n").Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length < 2) yield break;

        var header = lines[0].Split(',').Select(h => h.Trim()).ToArray();
        int Column(string name) => Array.FindIndex(header, h => string.Equals(h, name, StringComparison.OrdinalIgnoreCase));
        var keyIdx = Column("key");
        var nameIdx = Column("displayName");
        var typeIdx = Column("assetType");
        var costIdx = Column("cost");
        var currencyIdx = Column("currency");

        for (var i = 1; i < lines.Length; i++)
        {
            ct.ThrowIfCancellationRequested();
            var cols = lines[i].Split(',');
            if (keyIdx < 0 || keyIdx >= cols.Length) continue;
            var key = cols[keyIdx].Trim();
            if (string.IsNullOrWhiteSpace(key)) continue;

            var attributes = new Dictionary<string, string>();
            if (nameIdx >= 0 && nameIdx < cols.Length) attributes["displayName"] = cols[nameIdx].Trim();
            if (typeIdx >= 0 && typeIdx < cols.Length) attributes["assetType"] = cols[typeIdx].Trim();
            if (capability == ProviderCapability.Cost)
            {
                if (costIdx >= 0 && costIdx < cols.Length) attributes["cost"] = cols[costIdx].Trim();
                if (currencyIdx >= 0 && currencyIdx < cols.Length) attributes["currency"] = cols[currencyIdx].Trim();
            }

            yield return new RawObservation
            {
                SurfaceId = Manifest.SurfaceId,
                Capability = capability,
                NativeIdentifiers = [new NativeIdentifier("manual-csv", "csv:key", key)],
                Attributes = attributes,
                ObservedAt = DateTimeOffset.UtcNow,
                EvidenceLabel = ManualImportLabel,
            };
        }
    }
}
