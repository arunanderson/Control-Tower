namespace ControlTower.Platform.Events;

/// <summary>
/// An event as persisted in the append-only stream: its position, the tenant it belongs to, the
/// canonical payload, and the hash-chain links. Immutable — there is no update or delete (ADR-015).
/// </summary>
public sealed record StoredEvent(
    long Position,
    Guid EventId,
    DateTimeOffset OccurredAt,
    Tenancy.TenantId Tenant,
    string PreviousHash,
    string Hash,
    byte[] Payload);
