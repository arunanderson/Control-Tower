using ControlTower.Modules.Providers.Application;
using ControlTower.Modules.Providers.Domain;
using ControlTower.Platform.Tenancy;

namespace ControlTower.Modules.Providers.Infrastructure;

public sealed class InMemoryProviderConnectionStore(ITenantContextAccessor tenants) : IProviderConnectionStore
{
    private readonly object _gate = new();
    private readonly Dictionary<TenantId, Dictionary<string, ProviderConnection>> _connections = [];

    public Task SaveAsync(ProviderConnection connection, CancellationToken ct = default)
    {
        var tenant = tenants.Current;
        if (connection.Tenant != tenant) throw new InvalidOperationException("Cross-tenant provider connection write denied.");
        lock (_gate)
        {
            if (!_connections.TryGetValue(tenant, out var bucket)) _connections[tenant] = bucket = [];
            bucket[connection.ConnectionId] = connection;
        }
        return Task.CompletedTask;
    }

    public Task<ProviderConnection?> GetAsync(string connectionId, CancellationToken ct = default)
    {
        var tenant = tenants.Current;
        lock (_gate)
            return Task.FromResult(_connections.TryGetValue(tenant, out var bucket) && bucket.TryGetValue(connectionId, out var value) ? value : null);
    }
}

public sealed class InMemoryProviderJobReceiptStore(ITenantContextAccessor tenants) : IProviderJobReceiptStore
{
    private readonly object _gate = new();
    private readonly Dictionary<TenantId, Dictionary<Guid, string>> _states = [];

    public Task<bool> TryStartAsync(Guid jobId, CancellationToken ct = default)
    {
        var tenant = tenants.Current;
        lock (_gate)
        {
            if (!_states.TryGetValue(tenant, out var states)) _states[tenant] = states = [];
            if (states.ContainsKey(jobId)) return Task.FromResult(false);
            states[jobId] = "Started";
            return Task.FromResult(true);
        }
    }

    public Task CompleteAsync(Guid jobId, CancellationToken ct = default)
    {
        var tenant = tenants.Current;
        lock (_gate) _states[tenant][jobId] = "Completed";
        return Task.CompletedTask;
    }

    public Task ReleaseAsync(Guid jobId, CancellationToken ct = default)
    {
        var tenant = tenants.Current;
        lock (_gate)
            if (_states.TryGetValue(tenant, out var states)) states.Remove(jobId);
        return Task.CompletedTask;
    }
}
