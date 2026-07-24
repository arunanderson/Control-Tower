using System.Text.Json;
using ControlTower.Modules.Trust.Authorization;
using ControlTower.Platform.Audit;
using ControlTower.Platform.Events;
using ControlTower.Platform.Identity;
using ControlTower.Platform.Tenancy;

namespace ControlTower.Modules.Trust.Infrastructure;

/// <summary>
/// DEV-ONLY tenant-partitioned E19 substitute. It models the production contract's direct forward
/// and reverse indexes, audit-before-release reads, event-before-mutation writes and O(1) severance.
/// Production replaces raw values with field-protected ciphertext and blind indexes in P1-T08.
/// </summary>
public sealed class InMemoryPersonKeyMap(
    ITenantContextAccessor tenants,
    IPrivilegedReadAuditor auditor,
    IEventStore events,
    TimeProvider clock)
    : IPersonKeyMap
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<TenantId, TenantBucket> _byTenant = [];

    public async Task<PersonKey?> FindAsync(
        Guid directoryObjectId,
        PersonKeyAccessContext access,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(access);

        var tenant = tenants.Current;
        await _gate.WaitAsync(ct);
        try
        {
            if (directoryObjectId != Guid.Empty)
                RejectRawIdentityContext(
                    access,
                    directoryObjectId,
                    displaySnapshot: null);

            if (directoryObjectId == Guid.Empty)
            {
                await AuditReadAsync(
                    tenant,
                    access,
                    new EventReference(
                        "person-key-map",
                        "directory-lookup"),
                    ct);
                return null;
            }

            var personKey =
                BucketFor(tenant).ByDirectory.TryGetValue(
                    directoryObjectId,
                    out var entry)
                && !entry.IsSevered
                    ? entry.PersonKey
                    : (PersonKey?)null;

            await AuditReadAsync(
                tenant,
                access,
                personKey is { } found
                    ? EventReference.For(
                        "person-key",
                        found.Value)
                    : new EventReference(
                        "person-key-map",
                        "directory-lookup"),
                ct);
            return personKey;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<DirectoryIdentitySnapshot?> GetAsync(
        PersonKey personKey,
        PersonKeyAccessContext access,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(access);

        var tenant = tenants.Current;
        await _gate.WaitAsync(ct);
        try
        {
            if (!personKey.IsValid)
            {
                await AuditReadAsync(
                    tenant,
                    access,
                    new EventReference(
                        "person-key-map",
                        "person-lookup"),
                    ct);
                return null;
            }

            var identity =
                BucketFor(tenant).ByPersonKey.TryGetValue(
                    personKey,
                    out var entry)
                && !entry.IsSevered
                    ? entry.Identity
                    : null;
            if (identity is not null)
            {
                RejectRawIdentityContext(
                    access,
                    identity.DirectoryObjectId,
                    identity.DisplaySnapshot);
            }
            await AuditReadAsync(
                tenant,
                access,
                EventReference.For(
                    "person-key",
                    personKey.Value),
                ct);
            return identity;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<PersonKeyMutationResult> GetOrCreateAsync(
        DirectoryIdentitySnapshot identity,
        PersonKeyAccessContext access,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(access);

        var tenant = tenants.Current;
        await _gate.WaitAsync(ct);
        try
        {
            RejectRawIdentityContext(
                access,
                identity.DirectoryObjectId,
                identity.DisplaySnapshot);

            var bucket = BucketFor(tenant);
            if (bucket.ByDirectory.TryGetValue(
                    identity.DirectoryObjectId,
                    out var existing)
                && !existing.IsSevered)
            {
                await AuditReadAsync(
                    tenant,
                    access,
                    EventReference.For(
                        "person-key",
                        existing.PersonKey.Value),
                    ct);
                return new(
                    PersonKeyMutationStatus.Existing,
                    existing.PersonKey,
                    existing.Version);
            }

            await AuditReadAsync(
                tenant,
                access,
                new EventReference(
                    "person-key-map",
                    "directory-lookup"),
                ct);

            var personKey = PersonKey.New();
            const long version = 1;
            var changed = new PersonKeyMapChanged
            {
                PersonKey = personKey,
                Version = version,
                Change = "Created",
                OccurredAt =
                    EventEnvelopeCanonicalizer.NormalizeTimestamp(
                        clock.GetUtcNow()),
            };
            await events.AppendAsync(
                changed,
                new EventAppendMetadata(
                    EventReference.For(
                        "person-key",
                        personKey.Value),
                    access.Actor,
                    access.Purpose,
                    access.CorrelationReference),
                JsonSerializer.SerializeToUtf8Bytes(changed),
                ct);

            var entry = new Entry(
                personKey,
                identity,
                version,
                isSevered: false);
            bucket.ByDirectory.Add(
                identity.DirectoryObjectId,
                entry);
            bucket.ByPersonKey.Add(personKey, entry);
            return new(
                PersonKeyMutationStatus.Created,
                personKey,
                version);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<PersonKeySeverResult> SeverAsync(
        PersonKey personKey,
        long expectedVersion,
        PersonKeyAccessContext access,
        CancellationToken ct = default)
    {
        if (!personKey.IsValid)
            throw new ArgumentException(
                "A non-empty PersonKey is required.",
                nameof(personKey));
        if (expectedVersion <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(expectedVersion),
                "A positive expected version is required.");
        ArgumentNullException.ThrowIfNull(access);

        var tenant = tenants.Current;
        await _gate.WaitAsync(ct);
        try
        {
            var bucket = BucketFor(tenant);
            if (!bucket.ByPersonKey.TryGetValue(
                    personKey,
                    out var entry))
            {
                await AuditReadAsync(
                    tenant,
                    access,
                    new EventReference(
                        "person-key-map",
                        "sever"),
                    ct);
                return new(
                    PersonKeySeverStatus.NotFound,
                    null,
                    null);
            }

            if (entry.Identity is { } identity)
            {
                RejectRawIdentityContext(
                    access,
                    identity.DirectoryObjectId,
                    identity.DisplaySnapshot);
            }
            await AuditReadAsync(
                tenant,
                access,
                EventReference.For(
                    "person-key",
                    personKey.Value),
                ct);

            if (entry.IsSevered)
            {
                return new(
                    PersonKeySeverStatus.AlreadySevered,
                    personKey,
                    entry.Version);
            }

            if (entry.Version != expectedVersion)
            {
                return new(
                    PersonKeySeverStatus.Conflict,
                    personKey,
                    entry.Version);
            }

            var nextVersion = checked(entry.Version + 1);
            var changed = new PersonKeyMapChanged
            {
                PersonKey = personKey,
                Version = nextVersion,
                Change = "Severed",
                OccurredAt =
                    EventEnvelopeCanonicalizer.NormalizeTimestamp(
                        clock.GetUtcNow()),
            };
            await events.AppendAsync(
                changed,
                new EventAppendMetadata(
                    EventReference.For(
                        "person-key",
                        personKey.Value),
                    access.Actor,
                    access.Purpose,
                    access.CorrelationReference),
                JsonSerializer.SerializeToUtf8Bytes(changed),
                ct);

            var directoryObjectId =
                entry.Identity!.DirectoryObjectId;
            bucket.ByDirectory.Remove(directoryObjectId);
            entry.Sever(nextVersion);
            return new(
                PersonKeySeverStatus.Severed,
                personKey,
                nextVersion);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async ValueTask AuditReadAsync(
        TenantId tenant,
        PersonKeyAccessContext access,
        EventReference resource,
        CancellationToken ct)
    {
        var record = new PrivilegedReadRecord(
            Guid.NewGuid(),
            tenant,
            access.Actor,
            resource,
            access.Purpose,
            access.Policy,
            access.CorrelationReference,
            EventEnvelopeCanonicalizer.NormalizeTimestamp(
                clock.GetUtcNow()));
        await auditor.RecordAsync(record, ct);
    }

    private static void RejectRawIdentityContext(
        PersonKeyAccessContext access,
        Guid directoryObjectId,
        string? displaySnapshot)
    {
        var directoryId = directoryObjectId.ToString("D");
        var compactDirectoryId =
            directoryObjectId.ToString("N");
        foreach (var value in ContextValues(access))
        {
            if (value.Contains(
                    directoryId,
                    StringComparison.OrdinalIgnoreCase)
                || value.Contains(
                    compactDirectoryId,
                    StringComparison.OrdinalIgnoreCase)
                || (displaySnapshot is not null
                    && value.Contains(
                        displaySnapshot,
                        StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException(
                    "Raw directory identity is forbidden in person-key access evidence.");
            }
        }
    }

    private static IEnumerable<string> ContextValues(
        PersonKeyAccessContext access)
    {
        yield return access.Actor.OpaqueId;
        yield return access.Purpose;
        yield return access.CorrelationReference.Kind;
        yield return access.CorrelationReference.Value;
        if (access.Policy.Version is { } version)
        {
            yield return version.Kind;
            yield return version.Value;
        }
    }

    private TenantBucket BucketFor(TenantId tenant) =>
        _byTenant.TryGetValue(tenant, out var bucket)
            ? bucket
            : _byTenant[tenant] = new TenantBucket();

    private sealed class TenantBucket
    {
        public Dictionary<Guid, Entry> ByDirectory { get; } = [];

        public Dictionary<PersonKey, Entry> ByPersonKey { get; } = [];
    }

    private sealed class Entry(
        PersonKey personKey,
        DirectoryIdentitySnapshot identity,
        long version,
        bool isSevered)
    {
        public PersonKey PersonKey { get; } = personKey;

        public DirectoryIdentitySnapshot? Identity { get; private set; } =
            identity;

        public long Version { get; private set; } = version;

        public bool IsSevered { get; private set; } = isSevered;

        public void Sever(long nextVersion)
        {
            Identity = null;
            Version = nextVersion;
            IsSevered = true;
        }
    }
}
