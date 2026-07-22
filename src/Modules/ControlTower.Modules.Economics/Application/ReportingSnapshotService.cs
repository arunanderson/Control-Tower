using System.Security.Cryptography;
using System.Text.Json;
using ControlTower.Modules.Economics.Domain;
using ControlTower.Platform.Events;
using ControlTower.Platform.Tenancy;

namespace ControlTower.Modules.Economics.Application;

public sealed record ReportingPeriodView(
    Guid Id,
    DateTimeOffset Start,
    DateTimeOffset End,
    string State,
    DateTimeOffset? FrozenAt,
    string? FrozenBy);

public sealed record ReportSnapshotView(
    Guid Id,
    Guid PeriodId,
    int Version,
    DateTimeOffset FrozenAt,
    ReportInputBasis InputBasis,
    string InputBasisHash,
    string PayloadJson,
    string SignedBy,
    Guid? SupersedesSnapshotId,
    string? RestatementReason);

/// <summary>
/// C3 reporting-period lifecycle and immutable snapshot history (ADR-016). C7 invokes this service;
/// it does not calculate or persist a second economics model.
/// </summary>
public sealed class ReportingSnapshotService(IEconomicsStore store, IEventStore events, ITenantContextAccessor tenants)
{
    public async Task<Guid> CreatePeriodAsync(DateTimeOffset start, DateTimeOffset end, CancellationToken ct = default)
    {
        var period = new ReportingPeriod(Guid.NewGuid(), tenants.Current, start, end);
        await store.SavePeriodAsync(period, ct);
        return period.Id;
    }

    public async Task BeginClosingAsync(Guid periodId, CancellationToken ct = default)
    {
        var period = await RequiredPeriodAsync(periodId, ct);
        period.BeginClosing();
        await store.SavePeriodAsync(period, ct);
    }

    public async Task<ReportSnapshotView> FreezeAsync(
        Guid periodId,
        string payloadJson,
        ReportInputBasis inputBasis,
        string signedBy,
        CancellationToken ct = default)
    {
        var period = await RequiredPeriodAsync(periodId, ct);
        if ((await store.SnapshotsAsync(periodId, ct)).Count != 0)
            throw new EconomicsException("A frozen snapshot already exists; use restatement to create a new version.");

        var basis = ValidateAndCopyBasis(inputBasis);
        ValidateJson(payloadJson);
        var now = DateTimeOffset.UtcNow;
        period.Freeze(now, signedBy);
        var snapshot = NewSnapshot(period, 1, payloadJson, basis, signedBy, now, null, null);
        await store.AppendSnapshotAsync(snapshot, ct);
        await store.SavePeriodAsync(period, ct);
        await AppendAsync(new ReportingPeriodFrozen
        {
            PeriodId = period.Id,
            SnapshotId = snapshot.Id,
            Version = snapshot.Version,
            InputBasisHash = snapshot.InputBasisHash,
            SignedBy = snapshot.SignedBy,
        }, ct);
        return ToView(snapshot);
    }

    public async Task<ReportSnapshotView> RestateAsync(
        Guid periodId,
        string payloadJson,
        ReportInputBasis inputBasis,
        string signedBy,
        string reason,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new EconomicsException("A restatement reason is required.");
        var period = await RequiredPeriodAsync(periodId, ct);
        var history = await store.SnapshotsAsync(periodId, ct);
        var previous = history.LastOrDefault() ?? throw new EconomicsException("A period must be frozen before it can be restated.");
        var basis = ValidateAndCopyBasis(inputBasis);
        ValidateJson(payloadJson);
        var now = DateTimeOffset.UtcNow;
        period.Restate();
        var snapshot = NewSnapshot(period, previous.Version + 1, payloadJson, basis, signedBy, now, previous.Id, reason.Trim());
        await store.AppendSnapshotAsync(snapshot, ct);
        await store.SavePeriodAsync(period, ct);
        await AppendAsync(new ReportingPeriodRestated
        {
            PeriodId = period.Id,
            SnapshotId = snapshot.Id,
            Version = snapshot.Version,
            SupersedesSnapshotId = previous.Id,
            InputBasisHash = snapshot.InputBasisHash,
            SignedBy = snapshot.SignedBy,
            Reason = snapshot.RestatementReason!,
        }, ct);
        return ToView(snapshot);
    }

    public async Task<IReadOnlyList<ReportingPeriodView>> PeriodsAsync(CancellationToken ct = default) =>
        (await store.PeriodsAsync(ct)).Select(ToView).ToList();

    public async Task<IReadOnlyList<ReportSnapshotView>> SnapshotsAsync(Guid periodId, CancellationToken ct = default)
    {
        _ = await RequiredPeriodAsync(periodId, ct);
        return (await store.SnapshotsAsync(periodId, ct)).Select(ToView).ToList();
    }

    private async Task<ReportingPeriod> RequiredPeriodAsync(Guid id, CancellationToken ct) =>
        await store.GetPeriodAsync(id, ct) ?? throw new EconomicsException("Reporting period not found in this tenant.");

    private static ReportSnapshot NewSnapshot(
        ReportingPeriod period,
        int version,
        string payloadJson,
        ReportInputBasis basis,
        string signedBy,
        DateTimeOffset frozenAt,
        Guid? supersedes,
        string? reason)
    {
        if (string.IsNullOrWhiteSpace(signedBy)) throw new EconomicsException("A snapshot signer is required.");
        return new ReportSnapshot
        {
            Id = Guid.NewGuid(),
            Tenant = period.Tenant,
            PeriodId = period.Id,
            Version = version,
            FrozenAt = frozenAt,
            InputBasis = basis,
            InputBasisHash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(basis))).ToLowerInvariant(),
            PayloadJson = payloadJson,
            SignedBy = signedBy.Trim(),
            SupersedesSnapshotId = supersedes,
            RestatementReason = reason,
        };
    }

    private static ReportInputBasis ValidateAndCopyBasis(ReportInputBasis basis)
    {
        if (basis is null) throw new EconomicsException("A complete report input basis is required.");
        var sources = CopyRequired(basis.SourceReferences, "source references");
        var rules = CopyRequired(basis.RuleVersionReferences, "rule version references");
        if (basis.AsOf == default || string.IsNullOrWhiteSpace(basis.OrganisationModelVersion) || string.IsNullOrWhiteSpace(basis.ObservationWatermark))
            throw new EconomicsException("Input basis requires as-of time, organisation model version, and observation watermark.");
        return new ReportInputBasis
        {
            AsOf = basis.AsOf,
            SourceReferences = Array.AsReadOnly(sources),
            RuleVersionReferences = Array.AsReadOnly(rules),
            OrganisationModelVersion = basis.OrganisationModelVersion.Trim(),
            ObservationWatermark = basis.ObservationWatermark.Trim(),
        };
    }

    private static string[] CopyRequired(IReadOnlyList<string>? values, string name)
    {
        if (values is null || values.Count == 0 || values.Any(string.IsNullOrWhiteSpace))
            throw new EconomicsException($"Input basis {name} are required.");
        return values.Select(x => x.Trim()).ToArray();
    }

    private static void ValidateJson(string payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson)) throw new EconomicsException("Snapshot output payload is required.");
        try { using var _ = JsonDocument.Parse(payloadJson); }
        catch (JsonException) { throw new EconomicsException("Snapshot output payload must be valid JSON."); }
    }

    private async Task AppendAsync(EconomicsEvent domainEvent, CancellationToken ct)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(domainEvent, domainEvent.GetType());
        await events.AppendAsync(domainEvent, payload, ct);
    }

    private static ReportingPeriodView ToView(ReportingPeriod p) =>
        new(p.Id, p.Start, p.End, p.State.ToString(), p.FrozenAt, p.FrozenBy);

    private static ReportSnapshotView ToView(ReportSnapshot s) =>
        new(s.Id, s.PeriodId, s.Version, s.FrozenAt, s.InputBasis, s.InputBasisHash, s.PayloadJson,
            s.SignedBy, s.SupersedesSnapshotId, s.RestatementReason);
}
