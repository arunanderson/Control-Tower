using ControlTower.Platform.Tenancy;

namespace ControlTower.Modules.Economics.Domain;

/// <summary>How cost maps to org units (Stage 4 §2.6). This train implements the DirectTag driver; other drivers are follow-ups.</summary>
public enum AllocationDriver
{
    DirectTag,
    Assignment,
    HeadcountShare,
    UsageShare,
}

public sealed class AllocationRule
{
    public AllocationRule(Guid id, int version, TenantId tenant, AllocationDriver driver, DateTimeOffset effectiveFrom)
    {
        Id = id;
        Version = version;
        Tenant = tenant;
        Driver = driver;
        EffectiveFrom = effectiveFrom;
    }

    public Guid Id { get; }
    public int Version { get; }
    public TenantId Tenant { get; }
    public AllocationDriver Driver { get; }
    public DateTimeOffset EffectiveFrom { get; }
}

public enum ReportingPeriodState
{
    Open,
    Closing,
    Frozen,
    Restated,
}

/// <summary>A financial reporting period (Stage 4 §11.4 / ADR-016). Freezing pins the numbers; restatement is a new version.</summary>
public sealed class ReportingPeriod
{
    public ReportingPeriod(Guid id, TenantId tenant, DateTimeOffset start, DateTimeOffset end)
    {
        if (end <= start) throw new EconomicsException("Reporting period end must be after start.");
        Id = id;
        Tenant = tenant;
        Start = start;
        End = end;
        State = ReportingPeriodState.Open;
    }

    public Guid Id { get; }
    public TenantId Tenant { get; }
    public DateTimeOffset Start { get; }
    public DateTimeOffset End { get; }
    public ReportingPeriodState State { get; private set; }
    public DateTimeOffset? FrozenAt { get; private set; }
    public string? FrozenBy { get; private set; }

    public void BeginClosing()
    {
        if (State != ReportingPeriodState.Open) throw new EconomicsException("Only an open period can begin closing.");
        State = ReportingPeriodState.Closing;
    }

    public void Freeze(DateTimeOffset frozenAt, string frozenBy)
    {
        if (State != ReportingPeriodState.Closing) throw new EconomicsException("Only a closing period can be frozen.");
        if (string.IsNullOrWhiteSpace(frozenBy)) throw new EconomicsException("A snapshot signer is required.");
        State = ReportingPeriodState.Frozen;
        FrozenAt = frozenAt;
        FrozenBy = frozenBy;
    }

    public void Restate()
    {
        if (State is not (ReportingPeriodState.Frozen or ReportingPeriodState.Restated))
            throw new EconomicsException("Only a frozen period can be restated.");
        State = ReportingPeriodState.Restated;
    }
}

/// <summary>The complete pinned basis needed to reproduce a frozen output (Stage 5 E14).</summary>
public sealed record ReportInputBasis
{
    public required DateTimeOffset AsOf { get; init; }
    public required IReadOnlyList<string> SourceReferences { get; init; }
    public required IReadOnlyList<string> RuleVersionReferences { get; init; }
    public required string OrganisationModelVersion { get; init; }
    public required string ObservationWatermark { get; init; }
}

/// <summary>An immutable frozen projection output + its input basis (ADR-016) — the reproducibility anchor.</summary>
public sealed record ReportSnapshot
{
    public required Guid Id { get; init; }
    public required TenantId Tenant { get; init; }
    public required Guid PeriodId { get; init; }
    public required int Version { get; init; }
    public required DateTimeOffset FrozenAt { get; init; }
    public required ReportInputBasis InputBasis { get; init; }
    public required string InputBasisHash { get; init; }
    public required string PayloadJson { get; init; }
    public required string SignedBy { get; init; }
    public Guid? SupersedesSnapshotId { get; init; }
    public string? RestatementReason { get; init; }
}
