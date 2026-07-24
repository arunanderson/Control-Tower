using System.Text.Json;
using ControlTower.Modules.Economics.Domain;
using ControlTower.Platform.Events;
using ControlTower.Platform.Identity;
using ControlTower.Platform.Tenancy;

namespace ControlTower.Modules.Economics.Application;

/// <summary>
/// Write side of the economics model: ingest immutable cost/usage facts and declare/validate value.
/// Every write appends a domain event to the immutable stream (ADR-015) — nothing is overwritten.
/// The Finance validation workflow is expressed as forward-only value revisions.
/// </summary>
public sealed class EconomicsIngestionService(IEconomicsStore store, IEventStore events, ITenantContextAccessor tenants)
{
    public async Task IngestCostAsync(Guid assetId, string assetType, EconomicFigure cost, DateTimeOffset periodStart, DateTimeOffset periodEnd, AuditActor actor, string? department = null, string? businessUnit = null, CancellationToken ct = default)
    {
        RequireActor(actor);
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
        var @event = new CostObserved
        {
            AssetId = assetId,
            Amount = cost.Amount.Amount,
            Currency = cost.Amount.Currency,
            EvidenceClass = cost.Confidence.ToString(),
        };
        var prepared = PrepareEvent(
            @event,
            "cost-observation",
            observation.Id,
            actor,
            reason: null,
            "ai-asset",
            assetId);

        await store.AddCostAsync(observation, ct);
        await AppendAsync(prepared, ct);
    }

    public async Task<Guid> DeclareValueAsync(Guid assetId, string assetType, BenefitType benefit, EconomicFigure figure, AuditActor declaredBy, CancellationToken ct = default)
    {
        var declaration = new ValueDeclaration(Guid.NewGuid(), tenants.Current, assetId, assetType, benefit, figure, declaredBy);
        var @event = new ValueDeclared
        {
            AssetId = assetId,
            Amount = figure.Amount.Amount,
            Currency = figure.Amount.Currency,
            EvidenceClass = figure.Confidence.ToString(),
            ValidationState = figure.Evidence.ValidationState.ToString(),
        };
        var prepared = PrepareEvent(
            @event,
            "value-declaration",
            declaration.Id,
            declaredBy,
            reason: null,
            "ai-asset",
            assetId);

        await store.SaveDeclarationAsync(declaration, ct);
        await AppendAsync(prepared, ct);
        return declaration.Id;
    }

    public async Task ValidateValueAsync(Guid declarationId, EconomicFigure updated, string reason, AuditActor by, CancellationToken ct = default)
    {
        var declaration = await store.GetDeclarationAsync(declarationId, ct)
            ?? throw new EconomicsException("Value declaration not found in this tenant.");
        var @event = new ValueRevisedEvent
        {
            AssetId = declaration.AssetId,
            ToValidationState = updated.Evidence.ValidationState.ToString(),
            Reason = reason,
        };
        var prepared = PrepareEvent(
            @event,
            "value-declaration",
            declaration.Id,
            by,
            reason,
            "ai-asset",
            declaration.AssetId);

        declaration.Revise(updated, reason, by);
        await store.SaveDeclarationAsync(declaration, ct);
        await AppendAsync(prepared, ct);
    }

    private static PreparedEvent PrepareEvent(
        EconomicsEvent domainEvent,
        string aggregateKind,
        Guid aggregateId,
        AuditActor actor,
        string? reason,
        string? correlationKind,
        Guid? correlationId)
    {
        try
        {
            return new PreparedEvent(
                domainEvent,
                new EventAppendMetadata(
                    EventReference.For(
                        aggregateKind,
                        aggregateId),
                    actor,
                    reason,
                    correlationId is { } value
                        ? EventReference.For(
                            correlationKind!,
                            value)
                        : null),
                JsonSerializer.SerializeToUtf8Bytes(
                    domainEvent,
                    domainEvent.GetType()));
        }
        catch (ArgumentException)
        {
            throw new EconomicsException(
                "The economics evidence context is invalid.");
        }
    }

    private async Task AppendAsync(
        PreparedEvent prepared,
        CancellationToken ct) =>
        await events.AppendAsync(
            prepared.Event,
            prepared.Metadata,
            prepared.Payload,
            ct);

    private sealed record PreparedEvent(
        EconomicsEvent Event,
        EventAppendMetadata Metadata,
        byte[] Payload);

    private static void RequireActor(AuditActor actor)
    {
        if (!actor.IsValid)
            throw new EconomicsException("An audit actor is required.");
    }
}
