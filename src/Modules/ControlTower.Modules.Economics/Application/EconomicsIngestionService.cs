using System.Text.Json;
using ControlTower.Modules.Economics.Domain;
using ControlTower.Platform.Events;
using ControlTower.Platform.Tenancy;

namespace ControlTower.Modules.Economics.Application;

/// <summary>
/// Write side of the economics model: ingest immutable cost/usage facts and declare/validate value.
/// Every write appends a domain event to the immutable stream (ADR-015) — nothing is overwritten.
/// The Finance validation workflow is expressed as forward-only value revisions.
/// </summary>
public sealed class EconomicsIngestionService(IEconomicsStore store, IEventStore events, ITenantContextAccessor tenants)
{
    public async Task IngestCostAsync(Guid assetId, string assetType, EconomicFigure cost, DateTimeOffset periodStart, DateTimeOffset periodEnd, string? department = null, string? businessUnit = null, CancellationToken ct = default)
    {
        var observation = new CostObservation
        {
            Id = Guid.NewGuid(),
            Tenant = tenants.Current,
            AssetId = assetId,
            AssetType = assetType,
            Department = department,
            BusinessUnit = businessUnit,
            Cost = cost,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
        };
        await store.AddCostAsync(observation, ct);
        await AppendAsync(new CostObserved
        {
            AssetId = assetId,
            Amount = cost.Amount.Amount,
            Currency = cost.Amount.Currency,
            EvidenceClass = cost.Confidence.ToString(),
        }, ct);
    }

    public async Task<Guid> DeclareValueAsync(Guid assetId, string assetType, BenefitType benefit, EconomicFigure figure, string declaredBy, CancellationToken ct = default)
    {
        var declaration = new ValueDeclaration(Guid.NewGuid(), tenants.Current, assetId, assetType, benefit, figure, declaredBy);
        await store.SaveDeclarationAsync(declaration, ct);
        await AppendAsync(new ValueDeclared
        {
            AssetId = assetId,
            Amount = figure.Amount.Amount,
            Currency = figure.Amount.Currency,
            EvidenceClass = figure.Confidence.ToString(),
            ValidationState = figure.Evidence.ValidationState.ToString(),
        }, ct);
        return declaration.Id;
    }

    public async Task ValidateValueAsync(Guid declarationId, EconomicFigure updated, string reason, string by, CancellationToken ct = default)
    {
        var declaration = await store.GetDeclarationAsync(declarationId, ct)
            ?? throw new EconomicsException("Value declaration not found in this tenant.");
        declaration.Revise(updated, reason, by);
        await store.SaveDeclarationAsync(declaration, ct);
        await AppendAsync(new ValueRevisedEvent
        {
            AssetId = declaration.AssetId,
            ToValidationState = updated.Evidence.ValidationState.ToString(),
            Reason = reason,
        }, ct);
    }

    private async Task AppendAsync(EconomicsEvent domainEvent, CancellationToken ct)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(domainEvent, domainEvent.GetType());
        await events.AppendAsync(domainEvent, payload, ct);
    }
}
