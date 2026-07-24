using System.Text.Json;
using ControlTower.Modules.EnterpriseContext.Privacy;
using ControlTower.Platform.Events;
using ControlTower.Platform.Privacy;
using ControlTower.Platform.Tenancy;

namespace ControlTower.Modules.EnterpriseContext.Infrastructure;

/// <summary>
/// DEV-ONLY tenant-partitioned E16 substitute. Immutable revisions preserve simple versioned
/// history; the serialized commit gate models optimistic concurrency and event-before-state
/// atomicity.
/// </summary>
public sealed class InMemoryJurisdictionProfileStore(
    ITenantContextAccessor tenants,
    IEventStore events)
    : IJurisdictionProfileStore, IJurisdictionCeilingResolver
{
    private sealed class TenantBucket
    {
        public Dictionary<Guid, List<JurisdictionProfile>> ById { get; } = [];

        public Dictionary<JurisdictionRef, Guid> IdByJurisdiction { get; } = [];
    }

    private readonly SemaphoreSlim _gate = new(1, 1);
    private Dictionary<TenantId, TenantBucket> _byTenant = [];

    public async Task<JurisdictionProfile?> GetExactAsync(
        Guid profileId,
        long version,
        CancellationToken ct = default)
    {
        var tenant = CaptureTenant();
        ValidateIdentity(profileId, version);

        await _gate.WaitAsync(ct);
        try
        {
            EnsureTenant(tenant);
            var profile = TryHistory(tenant, profileId)
                ?.SingleOrDefault(profile =>
                    profile.Version == version);
            EnsureTenant(tenant);
            return profile;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<JurisdictionProfile?> GetCurrentAsync(
        Guid profileId,
        CancellationToken ct = default)
    {
        var tenant = CaptureTenant();
        ValidateProfileId(profileId);

        await _gate.WaitAsync(ct);
        try
        {
            EnsureTenant(tenant);
            var profile = LatestOrDefault(
                TryHistory(tenant, profileId));
            EnsureTenant(tenant);
            return profile;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<JurisdictionProfile>> GetHistoryAsync(
        Guid profileId,
        CancellationToken ct = default)
    {
        var tenant = CaptureTenant();
        ValidateProfileId(profileId);

        await _gate.WaitAsync(ct);
        try
        {
            EnsureTenant(tenant);
            var history = TryHistory(tenant, profileId)
                    ?.OrderBy(profile => profile.Version)
                    .ToArray()
                ?? [];
            var immutable = Array.AsReadOnly(history);
            EnsureTenant(tenant);
            return immutable;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<JurisdictionCeilingResolution> ResolveAsync(
        JurisdictionCeilingQuery query,
        CancellationToken ct = default)
    {
        var tenant = CaptureTenant();
        ArgumentNullException.ThrowIfNull(query);

        await _gate.WaitAsync(ct);
        try
        {
            EnsureTenant(tenant);
            if (query.Applicability.Jurisdictions.Count == 0)
            {
                EnsureTenant(tenant);
                return FailClosed([]);
            }

            var matched = new List<JurisdictionProfile>(
                query.Applicability.Jurisdictions.Count);
            foreach (var jurisdiction
                     in query.Applicability.Jurisdictions)
            {
                var profile = LatestOrDefault(
                    TryHistory(tenant, jurisdiction));
                if (profile is null
                    || profile.Tenant != tenant
                    || profile.Jurisdiction != jurisdiction)
                {
                    EnsureTenant(tenant);
                    return FailClosed(matched);
                }

                matched.Add(profile);
            }

            var resolution = new JurisdictionCeilingResolution(
                (PrivacyMarking)matched.Min(profile =>
                    (int)profile.TelemetryCeiling),
                isAuthoritative: true,
                matched.Select(profile =>
                    new JurisdictionProfileVersionRef(
                        profile.Jurisdiction,
                        profile.Version)));
            EnsureTenant(tenant);
            return resolution;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<JurisdictionProfileCommitResult> CommitAsync(
        JurisdictionProfile profile,
        JurisdictionProfileChanged changed,
        EventAppendMetadata metadata,
        long expectedVersion,
        CancellationToken ct = default)
    {
        var tenant = CaptureTenant();
        ArgumentNullException.ThrowIfNull(profile);
        if (profile.Tenant != tenant)
        {
            throw new InvalidOperationException(
                "Cross-tenant jurisdiction-profile write denied.");
        }
        JurisdictionProfileCommitSemantics.Validate(
            profile,
            changed,
            metadata,
            expectedVersion);

        await _gate.WaitAsync(ct);
        try
        {
            EnsureTenant(tenant);
            _byTenant.TryGetValue(tenant, out var bucket);
            var history = bucket is not null
                && bucket.ById.TryGetValue(profile.Id, out var existingHistory)
                    ? existingHistory
                    : null;
            var current = history?
                .OrderByDescending(item => item.Version)
                .FirstOrDefault();

            if (expectedVersion == 0)
            {
                if (current is not null)
                {
                    EnsureTenant(tenant);
                    return Conflict(current);
                }
                if (bucket is not null
                    && bucket.IdByJurisdiction.TryGetValue(
                        profile.Jurisdiction,
                        out var authoritativeId))
                {
                    EnsureTenant(tenant);
                    return Conflict(
                        Latest(bucket.ById[authoritativeId]));
                }
            }
            else
            {
                if (current is null
                    || current.Version != expectedVersion)
                {
                    EnsureTenant(tenant);
                    return Conflict(
                        current
                        ?? LatestForJurisdiction(
                            bucket,
                            profile.Jurisdiction));
                }
                if (!JurisdictionProfileCommitSemantics.IsRevisionOf(
                        current,
                        profile))
                {
                    throw new InvalidOperationException(
                        "The jurisdiction-profile revision does not match the current aggregate.");
                }
            }

            var prepared = new Dictionary<TenantId, TenantBucket>(
                _byTenant);
            var preparedBucket = bucket is null
                ? new TenantBucket()
                : CloneBucket(bucket);
            if (preparedBucket.ById.TryGetValue(
                    profile.Id,
                    out var preparedHistory))
            {
                preparedHistory.Add(profile);
            }
            else
            {
                preparedBucket.ById.Add(
                    profile.Id,
                    [profile]);
                preparedBucket.IdByJurisdiction.Add(
                    profile.Jurisdiction,
                    profile.Id);
            }
            prepared[tenant] = preparedBucket;
            var applied = new JurisdictionProfileCommitResult(
                JurisdictionProfileCommitStatus.Applied,
                profile);
            var payload =
                JsonSerializer.SerializeToUtf8Bytes(changed);

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

    private IReadOnlyList<JurisdictionProfile>? TryHistory(
        TenantId tenant,
        Guid profileId) =>
        _byTenant.TryGetValue(tenant, out var bucket)
        && bucket.ById.TryGetValue(profileId, out var history)
            ? history
            : null;

    private IReadOnlyList<JurisdictionProfile>? TryHistory(
        TenantId tenant,
        JurisdictionRef jurisdiction) =>
        _byTenant.TryGetValue(tenant, out var bucket)
        && bucket.IdByJurisdiction.TryGetValue(
            jurisdiction,
            out var profileId)
        && bucket.ById.TryGetValue(profileId, out var history)
            ? history
            : null;

    private static TenantBucket CloneBucket(TenantBucket source)
    {
        var clone = new TenantBucket();
        foreach (var (profileId, history) in source.ById)
            clone.ById.Add(profileId, [.. history]);
        foreach (var (jurisdiction, profileId)
                 in source.IdByJurisdiction)
        {
            clone.IdByJurisdiction.Add(
                jurisdiction,
                profileId);
        }

        return clone;
    }

    private TenantId CaptureTenant()
    {
        if (!tenants.HasTenant)
        {
            throw new InvalidOperationException(
                "A valid ambient tenant is required for the jurisdiction-profile operation.");
        }

        var tenant = tenants.Current;
        if (tenant.Value == Guid.Empty)
        {
            throw new InvalidOperationException(
                "A valid ambient tenant is required for the jurisdiction-profile operation.");
        }

        return tenant;
    }

    private void EnsureTenant(TenantId captured)
    {
        if (!tenants.HasTenant
            || tenants.Current != captured)
        {
            throw new InvalidOperationException(
                "The ambient tenant changed during the jurisdiction-profile operation.");
        }
    }

    private static JurisdictionCeilingResolution FailClosed(
        IEnumerable<JurisdictionProfile> matched) =>
        new(
            PrivacyMarking.L1,
            isAuthoritative: false,
            matched.Select(profile =>
                new JurisdictionProfileVersionRef(
                    profile.Jurisdiction,
                    profile.Version)));

    private static JurisdictionProfileCommitResult Conflict(
        JurisdictionProfile? authoritative) =>
        new(
            JurisdictionProfileCommitStatus.Conflict,
            authoritative);

    private static JurisdictionProfile? LatestForJurisdiction(
        TenantBucket? bucket,
        JurisdictionRef jurisdiction) =>
        bucket is not null
        && bucket.IdByJurisdiction.TryGetValue(
            jurisdiction,
            out var profileId)
            ? Latest(bucket.ById[profileId])
            : null;

    private static JurisdictionProfile Latest(
        IEnumerable<JurisdictionProfile> history) =>
        history.OrderByDescending(profile =>
                profile.Version)
            .First();

    private static JurisdictionProfile? LatestOrDefault(
        IEnumerable<JurisdictionProfile>? history) =>
        history?
            .OrderByDescending(profile =>
                profile.Version)
            .FirstOrDefault();

    private static void ValidateIdentity(
        Guid profileId,
        long version)
    {
        ValidateProfileId(profileId);
        if (version <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(version),
                "A positive jurisdiction-profile version is required.");
        }
    }

    private static void ValidateProfileId(Guid profileId)
    {
        if (profileId == Guid.Empty)
        {
            throw new ArgumentException(
                "A jurisdiction-profile ID is required.",
                nameof(profileId));
        }
    }
}
