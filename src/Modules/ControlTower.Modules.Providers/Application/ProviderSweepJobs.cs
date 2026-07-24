using System.Text.Json;
using ControlTower.Modules.Providers.Domain;
using ControlTower.Platform.Events;
using ControlTower.Platform.Identity;
using ControlTower.Platform.Tenancy;

namespace ControlTower.Modules.Providers.Application;

public sealed class ProviderSweepRequestService(
    IProviderConnectionStore connections,
    IEventStore events,
    IOutbox outbox,
    ITenantContextAccessor tenants)
{
    public const string Topic = "provider.sweep-requested";

    public async Task<Guid> RequestAsync(
        string connectionId,
        ProviderCapability capability,
        AuditActor requestedBy,
        CancellationToken ct = default)
    {
        if (!requestedBy.IsValid)
            throw new ProviderException(
                "A provider-sweep request actor is required.");
        var connection = await connections.GetAsync(connectionId, ct)
            ?? throw new ProviderException($"Provider connection '{connectionId}' was not found in this tenant.");
        if (!connection.Enabled) throw new ProviderException($"Provider connection '{connectionId}' is disabled.");
        if (!connection.Capabilities.Contains(capability))
            throw new ProviderException($"Provider connection '{connectionId}' does not enable {capability}.");

        var jobId = Guid.NewGuid();
        var @event = new ProviderSweepRequested
        {
            EventId = jobId,
            JobId = jobId,
            Tenant = tenants.Current.ToString(),
            ConnectionId = connection.ConnectionId,
            SurfaceId = connection.SurfaceId,
            Capability = capability.ToString(),
        };
        var payload = JsonSerializer.SerializeToUtf8Bytes(@event);
        await events.AppendAsync(
            @event,
            new EventAppendMetadata(
                EventReference.For(
                    "provider-sweep",
                    jobId),
                requestedBy,
                reason: null,
                new EventReference(
                    "provider-connection",
                    connection.ConnectionId)),
            payload,
            ct);
        await outbox.EnqueueAsync(Topic, payload, ct);
        return @event.JobId;
    }
}

public sealed class ProviderSweepRequestedHandler(
    IProviderConnectionStore connections,
    IProviderRegistry providers,
    IProviderJobReceiptStore receipts,
    ObservationIngestionService ingestion,
    ITenantContextAccessor tenants) : IIntegrationEventHandler
{
    public string Topic => ProviderSweepRequestService.Topic;

    public async Task HandleAsync(ReadOnlyMemory<byte> payload, CancellationToken ct = default)
    {
        var contract = JsonSerializer.Deserialize<Contract>(payload.Span)
            ?? throw new ProviderException("Unreadable ProviderSweepRequested payload.");
        if (!Guid.TryParse(contract.Tenant, out var tenantGuid) || !Enum.TryParse<ProviderCapability>(contract.Capability, out var capability))
            throw new ProviderException("ProviderSweepRequested carried an invalid tenant or capability.");

        using var _ = tenants.BeginScope(new TenantId(tenantGuid));
        if (!await receipts.TryStartAsync(contract.JobId, ct)) return;

        try
        {
            var connection = await connections.GetAsync(contract.ConnectionId, ct)
                ?? throw new ProviderException($"Provider connection '{contract.ConnectionId}' was not found.");
            if (!connection.Enabled || !string.Equals(connection.SurfaceId, contract.SurfaceId, StringComparison.OrdinalIgnoreCase))
                throw new ProviderException("Provider sweep job does not match an enabled connection.");
            var provider = providers.Resolve(contract.SurfaceId)
                ?? throw new ProviderException($"Provider '{contract.SurfaceId}' is not registered.");

            var context = new ProviderConnectionContext(
                connection.ConnectionId, connection.CredentialReference, connection.Configuration);
            await ingestion.IngestAsync(provider, context, capability, ct);
            await receipts.CompleteAsync(contract.JobId, ct);
        }
        catch
        {
            await receipts.ReleaseAsync(contract.JobId, ct);
            throw;
        }
    }

    private sealed record Contract
    {
        public Guid JobId { get; init; }
        public string Tenant { get; init; } = "";
        public string ConnectionId { get; init; } = "";
        public string SurfaceId { get; init; } = "";
        public string Capability { get; init; } = "";
    }
}
