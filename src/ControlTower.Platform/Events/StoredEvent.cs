using ControlTower.Platform.Tenancy;
using ControlTower.Platform.Identity;

namespace ControlTower.Platform.Events;

/// <summary>
/// A deeply immutable event as persisted in the append-only stream. The public payload accessor
/// always returns a defensive copy; canonicalization uses the privately owned bytes.
/// </summary>
public sealed record StoredEvent
{
    private byte[] _payload;

    public StoredEvent(
        int integrityFormatVersion,
        long position,
        Guid eventId,
        string eventType,
        EventReference aggregateReference,
        AuditActor actor,
        DateTimeOffset occurredAt,
        DateTimeOffset recordedAt,
        string? reason,
        EventReference? correlationReference,
        TenantId tenant,
        EventPrivilege privilege,
        string previousHash,
        string hash,
        ReadOnlyMemory<byte> payload)
    {
        IntegrityFormatVersion = integrityFormatVersion;
        Position = position;
        EventId = eventId;
        EventType = eventType;
        AggregateReference = aggregateReference;
        Actor = actor;
        OccurredAt = occurredAt;
        RecordedAt = recordedAt;
        Reason = reason;
        CorrelationReference = correlationReference;
        Tenant = tenant;
        Privilege = privilege;
        PreviousHash = previousHash;
        Hash = hash;
        _payload = payload.ToArray();
    }

    public int IntegrityFormatVersion { get; init; }
    public long Position { get; init; }
    public Guid EventId { get; init; }
    public string EventType { get; init; }
    public EventReference AggregateReference { get; init; }
    public AuditActor Actor { get; init; }
    public DateTimeOffset OccurredAt { get; init; }
    public DateTimeOffset RecordedAt { get; init; }
    public string? Reason { get; init; }
    public EventReference? CorrelationReference { get; init; }
    public TenantId Tenant { get; init; }
    public EventPrivilege Privilege { get; init; }
    public string PreviousHash { get; init; }
    public string Hash { get; init; }

    public byte[] Payload
    {
        get => _payload.ToArray();
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            _payload = value.ToArray();
        }
    }

    internal ReadOnlyMemory<byte> PayloadMemory => _payload;
}
