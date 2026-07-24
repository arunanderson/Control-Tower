using System.Collections.Concurrent;
using System.Security.Cryptography;
using ControlTower.Adapters.PostgreSql.Trust;
using ControlTower.Modules.Audit;
using ControlTower.Modules.Trust.Authorization;
using ControlTower.Platform.Audit;
using ControlTower.Platform.Events;
using ControlTower.Platform.Identity;
using ControlTower.Platform.Ports;
using ControlTower.Platform.Tenancy;
using Npgsql;

namespace ControlTower.Adapters.PostgreSql.Tests;

internal sealed class PostgreSqlTrustTestHarness
{
    private readonly PostgreSqlTrustStoreFixture _fixture;

    internal PostgreSqlTrustTestHarness(
        PostgreSqlTrustStoreFixture fixture,
        DateTimeOffset? now = null)
    {
        _fixture = fixture;
        Clock = new TrustFrozenTimeProvider(
            now
            ?? new DateTimeOffset(
                2026,
                7,
                24,
                12,
                0,
                0,
                TimeSpan.Zero));
        Tenants = new TenantContextAccessor();
        Secrets = new MutableTrustSecretProvider();
        Sink = new RecordingPrivilegedReadSink();
        Projection =
            new RecordingPrivilegedAccessProjection();
    }

    internal TenantContextAccessor Tenants { get; }
    internal MutableTrustSecretProvider Secrets { get; }
    internal RecordingPrivilegedReadSink Sink { get; }
    internal RecordingPrivilegedAccessProjection Projection { get; }
    internal TimeProvider Clock { get; }

    internal PostgreSqlRoleAssignmentStore CreateRoleStore(
        IHashChain? hashChain = null) =>
        new(
            _fixture.NormalDataSource,
            Tenants,
            new PostgreSqlEventTransactionAppender(
                hashChain ?? new Sha256HashChain(),
                Clock));

    internal PostgreSqlPersonKeyMap CreatePersonKeyMap(
        PersonKeyProtectionProfile profile,
        IPrivilegedReadAuditor? auditor = null,
        IHashChain? stateHashChain = null) =>
        new(
            _fixture.PrivilegedDataSource,
            Tenants,
            auditor ?? CreateAuditor(),
            Secrets,
            profile,
            new PostgreSqlEventTransactionAppender(
                stateHashChain ?? new Sha256HashChain(),
                Clock),
            Clock);

    internal IPrivilegedReadAuditor CreateAuditor() =>
        new PrivilegedReadEvidenceAuditor(
            Sink,
            Projection,
            new PostgreSqlEventStore(
                _fixture.PrivilegedAuditDataSource,
                Tenants,
                new Sha256HashChain(),
                Clock),
            Tenants);

    internal async Task<IReadOnlyList<StoredEvent>>
        ReadEventsAsync(TenantId tenant)
    {
        var store = new PostgreSqlEventStore(
            _fixture.NormalDataSource,
            Tenants,
            new Sha256HashChain(),
            Clock);
        using (Tenants.BeginScope(tenant))
            return await store.ReadAllAsync();
    }

    internal static PersonKeyAccessContext Access(
        string purpose,
        Guid? correlationId = null) =>
        new(
            AuditActor.System("privacy-test"),
            purpose,
            EventReference.For(
                "privacy-command",
                correlationId ?? Guid.NewGuid()),
            PrivilegedReadPolicy.NotApplicable());
}

internal sealed class MutableTrustSecretProvider
    : ISecretProvider
{
    private readonly ConcurrentDictionary<string, string>
        _secrets = new(StringComparer.Ordinal);

    internal (byte[] EncryptionKey, byte[] IndexKey)
        AddProfile(
            TenantId tenant,
            PersonKeyProtectionProfile profile,
            byte[]? encryptionKey = null,
            byte[]? indexKey = null)
    {
        var encryption =
            encryptionKey?.ToArray()
            ?? RandomNumberGenerator.GetBytes(32);
        var index =
            indexKey?.ToArray()
            ?? RandomNumberGenerator.GetBytes(32);
        AddEncryptionKey(
            tenant,
            profile.EncryptionReference,
            encryption);
        AddIndexKey(
            tenant,
            profile.IndexReference,
            index);
        return (encryption, index);
    }

    internal void AddEncryptionKey(
        TenantId tenant,
        string reference,
        byte[] key)
    {
        _secrets[
            $"ct-e19-{tenant.Value:N}-aes-{reference}"] =
            $"CTE19A1:{tenant.Value:N}:{reference}:{Convert.ToBase64String(key)}";
    }

    internal void AddIndexKey(
        TenantId tenant,
        string reference,
        byte[] key)
    {
        _secrets[
            $"ct-e19-{tenant.Value:N}-idx-{reference}"] =
            $"CTE19I1:{tenant.Value:N}:{reference}:{Convert.ToBase64String(key)}";
    }

    internal void SetRaw(string name, string value) =>
        _secrets[name] = value;

    public ValueTask<string> GetSecretAsync(
        string name,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return _secrets.TryGetValue(name, out var value)
            ? ValueTask.FromResult(value)
            : throw new KeyNotFoundException(
                "The requested test secret is absent.");
    }
}

internal sealed class RecordingPrivilegedReadSink
    : IPrivilegedReadRecordSink
{
    private readonly ConcurrentQueue<PrivilegedReadRecord>
        _records = new();

    internal IReadOnlyList<PrivilegedReadRecord> Records =>
        _records.ToArray();

    public ValueTask StoreAsync(
        PrivilegedReadRecord record,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        _records.Enqueue(record);
        return ValueTask.CompletedTask;
    }
}

internal sealed class RecordingPrivilegedAccessProjection
    : IPrivilegedAccessProjection
{
    private readonly ConcurrentQueue<PrivilegedAccessLogEntry>
        _entries = new();

    public Task ProjectAsync(
        PrivilegedAccessLogEntry entry,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        _entries.Enqueue(entry);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<PrivilegedAccessLogEntry>>
        ListAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<
            PrivilegedAccessLogEntry>>(
            _entries.ToArray());
    }
}

internal sealed class TrustFrozenTimeProvider(
    DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}

internal sealed class FailingPrivilegedReadAuditor
    : IPrivilegedReadAuditor
{
    public ValueTask RecordAsync(
        PrivilegedReadRecord record,
        CancellationToken ct = default) =>
        throw new InvalidOperationException(
            "Injected privileged-read audit failure.");
}

internal sealed class NpgsqlFailingPrivilegedReadAuditor
    : IPrivilegedReadAuditor
{
    public ValueTask RecordAsync(
        PrivilegedReadRecord record,
        CancellationToken ct = default) =>
        throw new NpgsqlException(
            "Injected endpoint detail must be bounded.");
}

internal sealed class BlockingPrivilegedReadAuditor
    : IPrivilegedReadAuditor
{
    private readonly TaskCompletionSource _entered =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _release =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal Task Entered => _entered.Task;

    internal void Release() => _release.TrySetResult();

    public async ValueTask RecordAsync(
        PrivilegedReadRecord record,
        CancellationToken ct = default)
    {
        _entered.TrySetResult();
        await _release.Task.WaitAsync(ct);
    }
}

internal sealed class ThrowingHashChain : IHashChain
{
    public string ComputeNext(
        string previousHash,
        ReadOnlyMemory<byte> canonicalEnvelope) =>
        throw new InvalidOperationException(
            "Injected state-event hash failure.");
}

internal sealed class MutableTrustTenantContextAccessor
    : ITenantContextAccessor
{
    private TenantId? _current;

    public bool HasTenant => _current is not null;

    public TenantId Current =>
        _current
        ?? throw new InvalidOperationException(
            "No mutable test tenant is set.");

    public IDisposable BeginScope(TenantId tenant)
    {
        var previous = _current;
        _current = tenant;
        return new MutableTrustTenantScope(
            () => _current = previous);
    }

    internal void SwitchTo(TenantId tenant) =>
        _current = tenant;

    private sealed class MutableTrustTenantScope(
        Action restore) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            restore();
        }
    }
}

internal sealed class TenantSwitchingHashChain(
    MutableTrustTenantContextAccessor tenants,
    TenantId target) : IHashChain
{
    private readonly Sha256HashChain _inner = new();

    public string ComputeNext(
        string previousHash,
        ReadOnlyMemory<byte> canonicalEnvelope)
    {
        tenants.SwitchTo(target);
        return _inner.ComputeNext(
            previousHash,
            canonicalEnvelope);
    }
}
