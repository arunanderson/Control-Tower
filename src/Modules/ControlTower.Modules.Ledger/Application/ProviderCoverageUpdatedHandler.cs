using System.Text.Json;
using ControlTower.Modules.Ledger.Domain;
using ControlTower.Platform.Events;
using ControlTower.Platform.Tenancy;

namespace ControlTower.Modules.Ledger.Application;

public sealed class ProviderCoverageUpdatedHandler(ICoverageReadModel coverage, ITenantContextAccessor tenants)
    : IIntegrationEventHandler
{
    public string Topic => "provider.coverage-updated";

    public async Task HandleAsync(ReadOnlyMemory<byte> payload, CancellationToken ct = default)
    {
        var contract = JsonSerializer.Deserialize<Contract>(payload.Span)
            ?? throw new DomainException("Unreadable ProviderCoverageUpdated payload.");
        if (!Guid.TryParse(contract.Tenant, out var tenantGuid))
            throw new DomainException("ProviderCoverageUpdated payload carried no valid tenant.");
        if (contract.FreshnessExpectationSeconds <= 0)
            throw new DomainException("ProviderCoverageUpdated payload carried no valid freshness expectation.");

        using var _ = tenants.BeginScope(new TenantId(tenantGuid));
        await coverage.ProjectAsync(new ProviderCoverageFact(
            contract.RunId, contract.ConnectionRef, contract.SurfaceId, contract.Capability,
            contract.Outcome, contract.CompletedAt, TimeSpan.FromSeconds(contract.FreshnessExpectationSeconds),
            contract.Observed, contract.New, contract.Changed, contract.Suppressed), ct);
    }

    private sealed record Contract
    {
        public Guid RunId { get; init; }
        public string Tenant { get; init; } = "";
        public string ConnectionRef { get; init; } = "";
        public string SurfaceId { get; init; } = "";
        public string Capability { get; init; } = "";
        public string Outcome { get; init; } = "";
        public DateTimeOffset CompletedAt { get; init; }
        public double FreshnessExpectationSeconds { get; init; }
        public int Observed { get; init; }
        public int New { get; init; }
        public int Changed { get; init; }
        public int Suppressed { get; init; }
    }
}
