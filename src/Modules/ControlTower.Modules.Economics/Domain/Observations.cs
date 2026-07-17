using ControlTower.Platform.Tenancy;

namespace ControlTower.Modules.Economics.Domain;

/// <summary>
/// An immutable, append-only cost fact (a CostObservation sub-kind, Stage 4 §2.1). Attribution
/// dimensions (asset type, department, business unit) are denormalised for the read models; a null
/// department is Unattributed and is never spread (ADR-025). The cost carries its own evidence.
/// </summary>
public sealed record CostObservation
{
    public required Guid Id { get; init; }
    public required TenantId Tenant { get; init; }
    public required Guid AssetId { get; init; }
    public required string AssetType { get; init; }
    public string? Department { get; init; }
    public string? BusinessUnit { get; init; }
    public required EconomicFigure Cost { get; init; }
    public required DateTimeOffset PeriodStart { get; init; }
    public required DateTimeOffset PeriodEnd { get; init; }
}

/// <summary>An immutable, append-only usage/adoption fact (a UsageObservation sub-kind, Stage 4 §2.1).</summary>
public sealed record UsageObservation
{
    public required Guid Id { get; init; }
    public required TenantId Tenant { get; init; }
    public required Guid AssetId { get; init; }
    public required string Metric { get; init; }
    public required decimal Quantity { get; init; }
    public required Evidence Evidence { get; init; }
    public required DateTimeOffset PeriodStart { get; init; }
    public required DateTimeOffset PeriodEnd { get; init; }
}
