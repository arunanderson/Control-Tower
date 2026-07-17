using ControlTower.Modules.Providers.Application;

namespace ControlTower.Modules.Providers.Infrastructure;

/// <summary>DEV-ONLY (DEV-001) in-memory watermark store. Production: Azure PostgreSQL (DEC-001).</summary>
public sealed class InMemoryWatermarkStore : IWatermarkStore
{
    private readonly Dictionary<string, string> _watermarks = [];
    private readonly object _gate = new();

    public Task<string?> GetAsync(string connectionId, CancellationToken ct = default)
    {
        lock (_gate)
        {
            _watermarks.TryGetValue(connectionId, out var watermark);
            return Task.FromResult(watermark);
        }
    }

    public Task SetAsync(string connectionId, string watermark, CancellationToken ct = default)
    {
        lock (_gate) _watermarks[connectionId] = watermark;
        return Task.CompletedTask;
    }
}
