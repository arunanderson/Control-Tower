using ControlTower.Modules.Providers.Application;
using ControlTower.Modules.Providers.Domain;
using ControlTower.Platform.Tenancy;

namespace ControlTower.Modules.Providers.Infrastructure;

/// <summary>
/// DEV-ONLY (DEV-001) tenant-partitioned in-memory observation store. Append-only by construction: it
/// exposes no mutation of stored observations. Tracks the last content hash per delta key to drive
/// suppression. Production: Azure Database for PostgreSQL (DEC-001), append-only partitions.
/// </summary>
public sealed class InMemoryObservationStore(ITenantContextAccessor tenants) : IObservationStore
{
    private sealed class Bucket
    {
        public List<ProviderObservation> Observations { get; } = [];
        public List<IngestionRun> Runs { get; } = [];
        public Dictionary<string, string> LastHashByDeltaKey { get; } = [];
    }

    private readonly Dictionary<TenantId, Bucket> _byTenant = [];
    private readonly object _gate = new();

    public Task AppendAsync(ProviderObservation observation, CancellationToken ct = default)
    {
        var tenant = tenants.Current;
        if (observation.Tenant != tenant) throw new InvalidOperationException("Cross-tenant observation write rejected.");
        var deltaKey = observation.NativeIdentifiers.Count > 0
            ? $"{observation.ConnectionRef}|{observation.SurfaceId}|{observation.NativeIdentifiers[0].System}:{observation.NativeIdentifiers[0].IdentifierType}:{observation.NativeIdentifiers[0].Value}"
            : $"{observation.ConnectionRef}|{observation.SurfaceId}|(none)";
        lock (_gate)
        {
            var bucket = BucketFor(tenant);
            bucket.Observations.Add(observation);
            bucket.LastHashByDeltaKey[deltaKey] = observation.ContentHash;
        }
        return Task.CompletedTask;
    }

    public Task<string?> LastContentHashAsync(string deltaKey, CancellationToken ct = default)
    {
        var tenant = tenants.Current;
        lock (_gate)
        {
            BucketFor(tenant).LastHashByDeltaKey.TryGetValue(deltaKey, out var hash);
            return Task.FromResult(hash);
        }
    }

    public Task RecordRunAsync(IngestionRun run, CancellationToken ct = default)
    {
        var tenant = tenants.Current;
        if (run.Tenant != tenant) throw new InvalidOperationException("Cross-tenant ingestion-run write rejected.");
        lock (_gate) BucketFor(tenant).Runs.Add(run);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ProviderObservation>> ObservationsAsync(CancellationToken ct = default)
    {
        var tenant = tenants.Current;
        lock (_gate) return Task.FromResult<IReadOnlyList<ProviderObservation>>(BucketFor(tenant).Observations.ToList());
    }

    public Task<IReadOnlyList<IngestionRun>> RunsAsync(CancellationToken ct = default)
    {
        var tenant = tenants.Current;
        lock (_gate) return Task.FromResult<IReadOnlyList<IngestionRun>>(BucketFor(tenant).Runs.ToList());
    }

    private Bucket BucketFor(TenantId tenant) =>
        _byTenant.TryGetValue(tenant, out var bucket) ? bucket : _byTenant[tenant] = new Bucket();
}
