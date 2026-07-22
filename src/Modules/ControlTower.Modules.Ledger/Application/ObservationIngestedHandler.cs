using System.Text.Json;
using ControlTower.Modules.Ledger.Domain;
using ControlTower.Platform.Events;
using ControlTower.Platform.Tenancy;

namespace ControlTower.Modules.Ledger.Application;

/// <summary>
/// The C1 side of the C4→C1 seam. It consumes the <c>provider.observation-ingested</c> outbox topic and
/// drives entity resolution. It does NOT reference the Providers module: it reconstructs its own contract
/// from the message's canonical JSON. The tenant travels in the payload, so the handler re-establishes the
/// tenant scope (ADR-021) before resolving. Idempotency is guaranteed by the resolution engine.
/// </summary>
public sealed class ObservationIngestedHandler(EntityResolutionService resolution, ITenantContextAccessor tenants) : IIntegrationEventHandler
{
    // Mirrors the C4 topic constant by value only — no cross-module type reference (module boundary, ADR-020).
    public string Topic => "provider.observation-ingested";

    public async Task HandleAsync(ReadOnlyMemory<byte> payload, CancellationToken ct = default)
    {
        var contract = JsonSerializer.Deserialize<ObservationIngestedContract>(payload.Span)
            ?? throw new DomainException("Unreadable ObservationIngested payload.");

        if (!Guid.TryParse(contract.Tenant, out var tenantGuid))
            throw new DomainException("ObservationIngested payload carried no valid tenant.");

        using var _ = tenants.BeginScope(new TenantId(tenantGuid));

        var descriptor = new ObservationDescriptor(
            contract.ObservationId,
            new NativeIdentifier(contract.PrimaryIdentifierSystem, contract.PrimaryIdentifierType, contract.PrimaryIdentifierValue),
            contract.DisplayName,
            contract.AssetType,
            contract.EvidenceLabel);

        await resolution.ResolveAsync(descriptor, ct);
    }

    /// <summary>C1's own reconstruction of the C4 event's JSON shape (property names must match the producer).</summary>
    private sealed record ObservationIngestedContract
    {
        public Guid ObservationId { get; init; }
        public string Tenant { get; init; } = "";
        public string SurfaceId { get; init; } = "";
        public string PrimaryIdentifierSystem { get; init; } = "";
        public string PrimaryIdentifierType { get; init; } = "";
        public string PrimaryIdentifierValue { get; init; } = "";
        public string EvidenceLabel { get; init; } = "";
        public string DisplayName { get; init; } = "";
        public string AssetType { get; init; } = "";
    }
}
