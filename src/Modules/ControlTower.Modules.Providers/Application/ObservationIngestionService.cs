using System.Text.Json;
using ControlTower.Modules.Providers.Domain;
using ControlTower.Platform.Events;
using ControlTower.Platform.Tenancy;

namespace ControlTower.Modules.Providers.Application;

/// <summary>The honest outcome of one sweep — the counts that back coverage/freshness reporting.</summary>
public sealed record IngestionResult(Guid RunId, int Observed, int New, int Changed, int Suppressed);

/// <summary>
/// The C4 invariant ingestion pipeline (Stage 7 §ingestion): the "one door in" (ADR-009/020). For a
/// configured connection it runs the same steps for every provider — contract-validate → acquire →
/// privacy-mark (Gate 1) → delta-suppress → append the immutable observation → emit
/// <see cref="ObservationIngested"/> to the hash-chained stream and stage it on the outbox. A provider
/// contributes only acquisition logic; all of the invariant work lives here, so no provider-specific
/// logic leaks out (ADR-007). This segment stops at the event boundary — the C1 resolution engine
/// consumes the event; nothing here reads or writes the ledger (I3).
/// </summary>
public sealed class ObservationIngestionService(
    IObservationStore store,
    IEventStore events,
    IOutbox outbox,
    IWatermarkStore watermarks,
    ITenantContextAccessor tenants)
{
    public const string ObservationIngestedTopic = "provider.observation-ingested";
    public const string ProviderCoverageUpdatedTopic = "provider.coverage-updated";

    public async Task<IngestionResult> IngestAsync(
        IProvider provider,
        ProviderConnectionContext context,
        ProviderCapability capability,
        CancellationToken ct = default)
    {
        // Contract validation gates admission — the same check for every provider (ADR-007).
        var validation = ProviderContractValidator.ValidateProvider(provider);
        if (!validation.IsValid)
            throw new ProviderException($"Provider '{provider.Manifest.SurfaceId}' failed contract validation: {string.Join("; ", validation.Errors)}");

        if (!provider.Supports(capability))
            throw new ProviderException($"Provider '{provider.Manifest.SurfaceId}' does not declare capability {capability}.");

        var tenant = tenants.Current; // throws outside a tenant scope (ADR-021)
        var startedAt = DateTimeOffset.UtcNow;
        var runId = Guid.NewGuid();

        // Watermark: resumable sweeps (Stage 7 §4). CSV is a full snapshot, but the abstraction is honored.
        _ = await watermarks.GetAsync(context.ConnectionId, ct);
        DateTimeOffset highWater = startedAt;

        int observed = 0, added = 0, changed = 0, suppressed = 0;

        try
        {
            await foreach (var raw in provider.AcquireAsync(context, capability, ct))
            {
                observed++;

                var deltaKey = ObservationNormalization.DeltaKeyFor(context.ConnectionId, raw);
                var hash = ObservationNormalization.ContentHash(raw);
                var last = await store.LastContentHashAsync(deltaKey, ct);

                var delta = last is null
                    ? DeltaStatus.New
                    : (last == hash ? DeltaStatus.Unchanged : DeltaStatus.Changed);

                if (delta == DeltaStatus.Unchanged)
                {
                    suppressed++; // delta-suppress: the stream records only what changed
                    continue;
                }

                var observation = new ProviderObservation
                {
                    ObservationId = Guid.NewGuid(),
                    Tenant = tenant,
                    ConnectionRef = context.ConnectionId,
                    SurfaceId = raw.SurfaceId,
                    Kind = ObservationNormalization.KindFor(raw.Capability),
                    NativeIdentifiers = raw.NativeIdentifiers,
                    Payload = raw.Attributes,
                    ObservedAt = raw.ObservedAt,
                    IngestedAt = DateTimeOffset.UtcNow,
                    PrivacyMarking = PrivacyMarking.L1, // Gate 1 default; set once (ADR-014)
                    DeltaStatus = delta,
                    EvidenceLabel = raw.EvidenceLabel,
                    ContentHash = hash,
                };
                await store.AppendAsync(observation, ct);

                var primary = raw.NativeIdentifiers.Count > 0
                    ? raw.NativeIdentifiers[0]
                    : new NativeIdentifier(raw.SurfaceId, "(none)", string.Empty);

                // Generic well-known attribute conventions — provider-agnostic, with fallbacks.
                var displayName = raw.Attributes.TryGetValue("displayName", out var dn) && !string.IsNullOrWhiteSpace(dn)
                    ? dn
                    : (!string.IsNullOrWhiteSpace(primary.Value) ? primary.Value : raw.SurfaceId);
                var assetType = raw.Attributes.TryGetValue("assetType", out var at) && !string.IsNullOrWhiteSpace(at)
                    ? at
                    : string.Empty;

                await EmitAsync(new ObservationIngested
                {
                    ObservationId = observation.ObservationId,
                    Tenant = tenant.ToString(),
                    ConnectionRef = observation.ConnectionRef,
                    SurfaceId = observation.SurfaceId,
                    Kind = observation.Kind.ToString(),
                    DeltaStatus = observation.DeltaStatus.ToString(),
                    PrivacyMarking = observation.PrivacyMarking.ToString(),
                    PrimaryIdentifierSystem = primary.System,
                    PrimaryIdentifierType = primary.IdentifierType,
                    PrimaryIdentifierValue = primary.Value,
                    EvidenceLabel = observation.EvidenceLabel,
                    ObservedAt = observation.ObservedAt,
                    DisplayName = displayName,
                    AssetType = assetType,
                }, ct);

                if (delta == DeltaStatus.New) added++; else changed++;
                if (raw.ObservedAt > highWater) highWater = raw.ObservedAt;
            }
        }
        catch
        {
            await RecordRunAndCoverageAsync("Degraded", DateTimeOffset.UtcNow, ct);
            throw;
        }

        var completedAt = DateTimeOffset.UtcNow;
        await RecordRunAndCoverageAsync("Completed", completedAt, ct);

        await watermarks.SetAsync(context.ConnectionId, highWater.ToString("O"), ct);

        return new IngestionResult(runId, observed, added, changed, suppressed);

        async Task RecordRunAndCoverageAsync(string outcome, DateTimeOffset endedAt, CancellationToken token)
        {
            await store.RecordRunAsync(new IngestionRun
            {
                RunId = runId,
                Tenant = tenant,
                ConnectionRef = context.ConnectionId,
                SurfaceId = provider.Manifest.SurfaceId,
                Capability = capability,
                StartedAt = startedAt,
                CompletedAt = endedAt,
                Observed = observed,
                New = added,
                Changed = changed,
                Suppressed = suppressed,
                Outcome = outcome,
            }, token);

            await EmitAsync(new ProviderCoverageUpdated
            {
                RunId = runId,
                Tenant = tenant.ToString(),
                ConnectionRef = context.ConnectionId,
                SurfaceId = provider.Manifest.SurfaceId,
                Capability = capability.ToString(),
                Outcome = outcome,
                CompletedAt = endedAt,
                FreshnessExpectationSeconds = provider.Manifest.FreshnessExpectation.TotalSeconds,
                Observed = observed,
                New = added,
                Changed = changed,
                Suppressed = suppressed,
            }, ProviderCoverageUpdatedTopic, token);
        }
    }

    private async Task EmitAsync(ObservationIngested @event, CancellationToken ct)
        => await EmitAsync(@event, ObservationIngestedTopic, ct);

    private async Task EmitAsync(ProviderEvent @event, string topic, CancellationToken ct)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(@event, @event.GetType());
        await events.AppendAsync(@event, payload, ct); // audit trail (ADR-015)
        await outbox.EnqueueAsync(topic, payload, ct); // reliable host-composed hand-off (Stage 9 §2.3)
    }
}
