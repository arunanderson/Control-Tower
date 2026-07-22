using ControlTower.Modules.Providers.Domain;

namespace ControlTower.Modules.Providers.Application;

/// <summary>
/// Tenant-scoped, append-only store for immutable provider observations and their ingestion-run log
/// (Stage 5 E2/E3, ADR-015). Deliberately exposes no update or delete — immutability is a property of
/// the contract. Implementations resolve the tenant from the ambient context (ADR-021). Production:
/// Azure Database for PostgreSQL (DEC-001); dev: in-memory.
/// </summary>
public interface IObservationStore
{
    /// <summary>Appends an immutable observation.</summary>
    Task AppendAsync(ProviderObservation observation, CancellationToken ct = default);

    /// <summary>
    /// The content hash last seen for a delta key (connection + primary identifier), or null if the
    /// entity has never been observed. Used to classify New / Changed / Unchanged before appending.
    /// </summary>
    Task<string?> LastContentHashAsync(string deltaKey, CancellationToken ct = default);

    /// <summary>Records an immutable ingestion-run log entry.</summary>
    Task RecordRunAsync(IngestionRun run, CancellationToken ct = default);

    /// <summary>All observations for the current tenant (for the resolution pipeline and rebuilds).</summary>
    Task<IReadOnlyList<ProviderObservation>> ObservationsAsync(CancellationToken ct = default);

    /// <summary>All ingestion runs for the current tenant.</summary>
    Task<IReadOnlyList<IngestionRun>> RunsAsync(CancellationToken ct = default);
}
