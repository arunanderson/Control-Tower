using System.Text.Json;
using ControlTower.Modules.Trust.Privacy;
using ControlTower.Platform.Events;
using ControlTower.Platform.Privacy;
using ControlTower.Platform.Tenancy;

namespace ControlTower.Modules.Trust.Infrastructure;

/// <summary>
/// DEV-ONLY tenant-partitioned E17 substitute. Immutable revisions preserve valid and record time;
/// the serialized commit gate models optimistic concurrency and event-before-state atomicity.
/// </summary>
public sealed class InMemoryTelemetryPolicyStore(
    ITenantContextAccessor tenants,
    IJurisdictionCeilingResolver ceilings,
    IEventStore events)
    : ITelemetryPolicyStore
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private Dictionary<
        TenantId,
        List<TelemetryPolicyRevision>> _byTenant = [];

    public async Task<TelemetryPolicyRevision?> GetAsync(
        long version,
        CancellationToken ct = default)
    {
        var tenant = CaptureTenant();
        if (version <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(version),
                "A positive telemetry-policy version is required.");
        }

        await _gate.WaitAsync(ct);
        try
        {
            EnsureTenant(tenant);
            var revision = HistoryForRead(tenant)
                .SingleOrDefault(revision =>
                    revision.Version == version);
            EnsureTenant(tenant);
            return revision;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<TelemetryPolicyRevision>>
        ListHistoryAsync(
            CancellationToken ct = default)
    {
        var tenant = CaptureTenant();
        await _gate.WaitAsync(ct);
        try
        {
            EnsureTenant(tenant);
            var history = HistoryForRead(tenant)
                .OrderBy(revision => revision.Version)
                .ToList();
            EnsureTenant(tenant);
            return history;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<TelemetryPolicyRevision?> FindAsOfAsync(
        DateTimeOffset validAt,
        DateTimeOffset recordedAt,
        CancellationToken ct = default)
    {
        var tenant = CaptureTenant();
        PrivacyContractTime.RequireNormalized(
            validAt,
            nameof(validAt));
        PrivacyContractTime.RequireNormalized(
            recordedAt,
            nameof(recordedAt));

        await _gate.WaitAsync(ct);
        try
        {
            EnsureTenant(tenant);
            var revision = FindAsOf(
                HistoryForRead(tenant),
                validAt,
                recordedAt);
            EnsureTenant(tenant);
            return revision;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<TelemetryPolicyResolution> ResolveAsync(
        TelemetryPolicyResolutionQuery query,
        CancellationToken ct = default)
    {
        var tenant = CaptureTenant();
        ArgumentNullException.ThrowIfNull(query);

        TelemetryPolicyRevision? revision;
        await _gate.WaitAsync(ct);
        try
        {
            EnsureTenant(tenant);
            revision = FindAsOf(
                HistoryForRead(tenant),
                query.ValidAt,
                query.RecordedAt);
            EnsureTenant(tenant);
        }
        finally
        {
            _gate.Release();
        }

        if (revision is null)
        {
            return new(
                Enabled: true,
                PrivacyMarking.L1,
                PolicyVersion: null,
                MatchedRuleCount: 0,
                Ceiling: null);
        }

        var jurisdictions =
            query.Applicability.Jurisdictions.Count == 0
                ? new JurisdictionRef?[] { null }
                : query.Applicability.Jurisdictions
                    .Select(value =>
                        (JurisdictionRef?)value)
                    .ToArray();
        var populations =
            query.Applicability.Populations.Count == 0
                ? new PopulationRef?[] { null }
                : query.Applicability.Populations
                    .Select(value =>
                        (PopulationRef?)value)
                    .ToArray();
        var matched = new HashSet<TelemetryPolicyRule>();
        var disabled = false;
        var configured = PrivacyMarking.L4;
        foreach (var jurisdiction in jurisdictions)
        {
            foreach (var population in populations)
            {
                var cellMatched = false;
                foreach (var rule in revision.Rules)
                {
                    if (!rule.AppliesToCell(
                            query.Capability,
                            jurisdiction,
                            population,
                            query.ValidAt))
                    {
                        continue;
                    }

                    cellMatched = true;
                    matched.Add(rule);
                    if (!rule.Enabled)
                    {
                        disabled = true;
                    }
                    else
                    {
                        configured =
                            (PrivacyMarking)Math.Min(
                                (int)configured,
                                (int)rule.Level);
                    }
                }

                if (!cellMatched)
                    configured = PrivacyMarking.L1;
            }
        }

        if (matched.Count == 0)
        {
            return new(
                Enabled: true,
                PrivacyMarking.L1,
                revision.Version,
                MatchedRuleCount: 0,
                Ceiling: null);
        }
        if (disabled)
        {
            return new(
                Enabled: false,
                PrivacyMarking.L1,
                revision.Version,
                matched.Count,
                Ceiling: null);
        }

        EnsureTenant(tenant);
        var ceiling = await ceilings.ResolveAsync(
            new JurisdictionCeilingQuery(
                query.Applicability),
            ct);
        EnsureTenant(tenant);
        var effective = (PrivacyMarking)Math.Min(
            (int)configured,
            (int)ceiling.Ceiling);
        return new(
            Enabled: true,
            effective,
            revision.Version,
            matched.Count,
            ceiling);
    }

    public async Task<TelemetryPolicyCommitResult> CommitAsync(
        TelemetryPolicyRevision revision,
        TelemetryPolicyChanged changed,
        EventAppendMetadata metadata,
        long expectedVersion,
        CancellationToken ct = default)
    {
        var tenant = CaptureTenant();
        ArgumentNullException.ThrowIfNull(revision);
        if (revision.Tenant != tenant)
        {
            throw new InvalidOperationException(
                "Cross-tenant telemetry-policy write denied.");
        }
        TelemetryPolicyCommitSemantics.Validate(
            revision,
            changed,
            metadata,
            expectedVersion);

        await _gate.WaitAsync(ct);
        try
        {
            EnsureTenant(tenant);
            var authoritative = HistoryForRead(tenant)
                .OrderByDescending(item => item.Version)
                .FirstOrDefault();
            var authoritativeVersion =
                authoritative?.Version ?? 0;
            if (authoritativeVersion != expectedVersion)
            {
                return new(
                    TelemetryPolicyCommitStatus.Conflict,
                    authoritative);
            }
            if (authoritative is not null
                && revision.RecordedAt
                    <= authoritative.RecordedAt)
            {
                throw new InvalidOperationException(
                    "A telemetry-policy revision must advance record time.");
            }

            foreach (var rule in revision.Rules)
            {
                EnsureTenant(tenant);
                var ceiling = await ceilings.ResolveAsync(
                    new JurisdictionCeilingQuery(
                        rule.CeilingApplicability()),
                    ct);
                EnsureTenant(tenant);
                if (rule.Level > ceiling.Ceiling)
                {
                    throw new TelemetryPolicyException(
                        "The telemetry-policy rule exceeds the effective jurisdiction ceiling.");
                }
            }

            var prepared = _byTenant.ToDictionary(
                pair => pair.Key,
                pair => pair.Value.ToList());
            if (prepared.TryGetValue(
                    tenant,
                    out var preparedHistory))
            {
                preparedHistory.Add(revision);
            }
            else
            {
                prepared.Add(tenant, [revision]);
            }
            var applied = new TelemetryPolicyCommitResult(
                TelemetryPolicyCommitStatus.Applied,
                revision);
            var payload =
                JsonSerializer.SerializeToUtf8Bytes(
                    changed);

            EnsureTenant(tenant);
            await events.AppendAsync(
                changed,
                metadata,
                payload,
                ct);
            _byTenant = prepared;
            return applied;
        }
        finally
        {
            _gate.Release();
        }
    }

    private IReadOnlyList<TelemetryPolicyRevision> HistoryForRead(
        TenantId tenant) =>
        _byTenant.TryGetValue(
            tenant,
            out var history)
            ? history
            : [];

    private TenantId CaptureTenant()
    {
        if (!tenants.HasTenant)
        {
            throw new InvalidOperationException(
                "A valid ambient tenant is required for the telemetry-policy operation.");
        }

        var tenant = tenants.Current;
        if (tenant.Value == Guid.Empty)
        {
            throw new InvalidOperationException(
                "A valid ambient tenant is required for the telemetry-policy operation.");
        }

        return tenant;
    }

    private void EnsureTenant(TenantId captured)
    {
        if (!tenants.HasTenant)
        {
            throw new InvalidOperationException(
                "The ambient tenant changed during the telemetry-policy operation.");
        }

        var current = tenants.Current;
        if (captured.Value == Guid.Empty
            || current.Value == Guid.Empty
            || current != captured)
        {
            throw new InvalidOperationException(
                "The ambient tenant changed during the telemetry-policy operation.");
        }
    }

    private static TelemetryPolicyRevision? FindAsOf(
        IEnumerable<TelemetryPolicyRevision> history,
        DateTimeOffset validAt,
        DateTimeOffset recordedAt) =>
        history
            .Where(revision =>
                revision.RecordedAt <= recordedAt
                && revision.IsValidAt(validAt))
            .OrderByDescending(revision =>
                revision.RecordedAt)
            .ThenByDescending(revision =>
                revision.Version)
            .FirstOrDefault();
}
