using System.Collections.Concurrent;
using ControlTower.Modules.Trust.Privacy;
using ControlTower.Platform.Events;
using ControlTower.Platform.Identity;
using ControlTower.Platform.Privacy;
using ControlTower.Platform.Tenancy;

namespace ControlTower.Modules.Trust.Tests;

internal static class TelemetryPolicyTestData
{
    public static readonly TenantId TenantA =
        new(Guid.Parse("11111111-1111-1111-1111-111111111111"));

    public static readonly TenantId TenantB =
        new(Guid.Parse("22222222-2222-2222-2222-222222222222"));

    public static readonly AuditActor Actor =
        AuditActor.System("telemetry-policy-tests");

    public static readonly TelemetryCapabilityRef Capability =
        new("usage");

    public static readonly JurisdictionRef Jurisdiction =
        new("jurisdiction-a");

    public static readonly PopulationRef Population =
        new("population-a");

    public static DateTimeOffset At(
        int month,
        int day) =>
        new(
            2026,
            month,
            day,
            0,
            0,
            0,
            TimeSpan.Zero);

    public static TelemetryPolicyRule Rule(
        PrivacyMarking level = PrivacyMarking.L1,
        bool enabled = true,
        TelemetryCapabilityRef? capability = null,
        JurisdictionRef? jurisdiction = null,
        PopulationRef? population = null,
        DateTimeOffset? timeLimit = null)
    {
        var activationRequired =
            enabled && level >= PrivacyMarking.L2;
        return new(
            capability ?? Capability,
            jurisdiction,
            population,
            enabled,
            level,
            activationRequired ? "approved telemetry purpose" : null,
            activationRequired
                ? new EventReference(
                    "policy-approval",
                    "approval-1")
                : null,
            activationRequired
                ? new RetentionPolicyRef("retention-1")
                : null,
            enabled && level == PrivacyMarking.L4
                ? timeLimit ?? At(10, 1)
                : null);
    }

    public static TelemetryPolicyRevision Revision(
        long version,
        DateTimeOffset recordedAt,
        IEnumerable<TelemetryPolicyRule>? rules = null,
        TenantId? tenant = null,
        DateTimeOffset? validFrom = null,
        DateTimeOffset? validTo = null,
        string justification = "approved policy change") =>
        new(
            tenant ?? TenantA,
            version,
            validFrom ?? At(1, 1),
            validTo ?? At(12, 31),
            recordedAt,
            Actor,
            justification,
            rules ?? []);

    public static async Task<TelemetryPolicyCommitResult>
        CommitAsync(
            ITelemetryPolicyStore store,
            TelemetryPolicyRevision revision,
            long expectedVersion,
            EventReference? correlation = null) =>
        await store.CommitAsync(
            revision,
            TelemetryPolicyCommitSemantics.Changed(
                revision,
                correlation),
            TelemetryPolicyCommitSemantics.Metadata(
                revision,
                correlation),
            expectedVersion);

    public static PrivacyApplicability Applicability(
        IEnumerable<JurisdictionRef>? jurisdictions = null,
        IEnumerable<PopulationRef>? populations = null) =>
        new(
            jurisdictions ?? [],
            populations ?? []);
}

internal sealed class MutableCeilingResolver(
    PrivacyMarking ceiling = PrivacyMarking.L4,
    bool authoritative = true)
    : IJurisdictionCeilingResolver
{
    private int _callCount;

    public PrivacyMarking Ceiling { get; set; } = ceiling;

    public bool Authoritative { get; set; } = authoritative;

    public Action? OnResolve { get; set; }

    public int CallCount => Volatile.Read(ref _callCount);

    public ConcurrentQueue<JurisdictionCeilingQuery> Queries { get; } =
        new();

    public Task<JurisdictionCeilingResolution> ResolveAsync(
        JurisdictionCeilingQuery query,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        Interlocked.Increment(ref _callCount);
        Queries.Enqueue(query);
        OnResolve?.Invoke();
        return Task.FromResult(
            Authoritative
                ? new JurisdictionCeilingResolution(
                    Ceiling,
                    isAuthoritative: true,
                    [
                        new JurisdictionProfileVersionRef(
                            new JurisdictionRef(
                                "ceiling-evidence"),
                            version: 1),
                    ])
                : new JurisdictionCeilingResolution(
                    PrivacyMarking.L1,
                    isAuthoritative: false,
                    []));
    }
}

internal sealed class CapturingEventStore(
    TenantId tenant)
    : IEventStore
{
    private int _appendCount;

    public Exception? Failure { get; set; }

    public Action? OnAppend { get; set; }

    public int AppendCount => Volatile.Read(ref _appendCount);

    public ConcurrentQueue<IDomainEvent> Appended { get; } =
        new();

    public ConcurrentQueue<EventAppendMetadata> Metadata { get; } =
        new();

    public ConcurrentQueue<byte[]> Payloads { get; } =
        new();

    public ValueTask<StoredEvent> AppendAsync(
        IDomainEvent @event,
        EventAppendMetadata metadata,
        ReadOnlyMemory<byte> payload,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var position = Interlocked.Increment(
            ref _appendCount);
        OnAppend?.Invoke();
        if (Failure is { } failure)
            return ValueTask.FromException<StoredEvent>(failure);

        Appended.Enqueue(@event);
        Metadata.Enqueue(metadata);
        Payloads.Enqueue(payload.ToArray());
        var contract = DomainEventContracts.Resolve(@event);
        return ValueTask.FromResult(
            new StoredEvent(
                EventEnvelopeCanonicalizer
                    .CurrentIntegrityFormatVersion,
                position,
                @event.EventId,
                contract.EventType,
                metadata.AggregateReference,
                metadata.Actor,
                EventEnvelopeCanonicalizer.NormalizeTimestamp(
                    @event.OccurredAt),
                EventEnvelopeCanonicalizer.NormalizeTimestamp(
                    @event.OccurredAt),
                metadata.Reason,
                metadata.CorrelationReference,
                tenant,
                contract.Privilege,
                string.Empty,
                string.Empty,
                payload));
    }

    public ValueTask<IReadOnlyList<StoredEvent>> ReadAllAsync(
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return ValueTask.FromResult<IReadOnlyList<StoredEvent>>(
            []);
    }
}

internal sealed class MutableTenantAccessor : ITenantContextAccessor
{
    private TenantId? _current;

    public Action? OnCurrent { get; set; }

    public bool HasTenant => _current is not null;

    public TenantId Current
    {
        get
        {
            var current = _current
                ?? throw new InvalidOperationException(
                    "No tenant context is set.");
            OnCurrent?.Invoke();
            return current;
        }
    }

    public IDisposable BeginScope(TenantId tenant)
    {
        var previous = _current;
        _current = tenant;
        return new Scope(() => _current = previous);
    }

    public void SwitchTo(TenantId tenant) =>
        _current = tenant;

    public void Clear() =>
        _current = null;

    private sealed class Scope(Action dispose)
        : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            dispose();
        }
    }
}
