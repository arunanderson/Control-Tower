using System.Buffers.Binary;
using System.Text;

namespace ControlTower.Platform.Events;

/// <summary>
/// Canonical binary integrity format for the P1-T05 storage frame. Format 1 deliberately covers the
/// fields represented by the current event skeleton. P1-T06 adds the remaining E20 audit metadata
/// before any durable event migration.
/// </summary>
public static class EventEnvelopeCanonicalizer
{
    public const int CurrentIntegrityFormatVersion = 1;
    private const int GuidLength = 16;
    private const int FixedLength =
        sizeof(int)     // integrity format
        + sizeof(long)  // position
        + GuidLength    // tenant
        + GuidLength    // event ID
        + sizeof(int)   // event-type byte length
        + sizeof(long)  // occurred-at UTC microseconds
        + sizeof(long)  // recorded-at UTC microseconds
        + sizeof(byte)  // privilege
        + sizeof(int);  // payload byte length

    public static DateTimeOffset NormalizeTimestamp(DateTimeOffset value)
    {
        var utcTicks = value.UtcTicks;
        var normalizedTicks =
            utcTicks - (utcTicks % TimeSpan.TicksPerMicrosecond);
        return new DateTimeOffset(normalizedTicks, TimeSpan.Zero);
    }

    public static byte[] Canonicalize(StoredEvent storedEvent)
    {
        ArgumentNullException.ThrowIfNull(storedEvent);
        Validate(storedEvent);

        var eventType = Encoding.UTF8.GetBytes(storedEvent.EventType);
        var payload = storedEvent.PayloadMemory.Span;
        int length;
        try
        {
            length = checked(FixedLength + eventType.Length + payload.Length);
        }
        catch (OverflowException)
        {
            throw new EventIntegrityException(
                "The event envelope is too large to canonicalize.");
        }
        var output = new byte[length];
        var span = output.AsSpan();
        var offset = 0;

        WriteInt32(span, ref offset, storedEvent.IntegrityFormatVersion);
        WriteInt64(span, ref offset, storedEvent.Position);
        WriteGuid(span, ref offset, storedEvent.Tenant.Value);
        WriteGuid(span, ref offset, storedEvent.EventId);
        WriteInt32(span, ref offset, eventType.Length);
        eventType.CopyTo(span[offset..]);
        offset += eventType.Length;
        WriteInt64(span, ref offset, ToUnixMicroseconds(storedEvent.OccurredAt));
        WriteInt64(span, ref offset, ToUnixMicroseconds(storedEvent.RecordedAt));
        span[offset++] = (byte)storedEvent.Privilege;
        WriteInt32(span, ref offset, payload.Length);
        payload.CopyTo(span[offset..]);

        return output;
    }

    public static void Validate(StoredEvent storedEvent)
    {
        ArgumentNullException.ThrowIfNull(storedEvent);
        if (storedEvent.IntegrityFormatVersion != CurrentIntegrityFormatVersion)
        {
            throw new EventIntegrityException(
                "The event integrity format is unsupported.");
        }
        if (storedEvent.Position <= 0)
            throw new EventIntegrityException("The event position is invalid.");
        if (storedEvent.Tenant.Value == Guid.Empty)
            throw new EventIntegrityException("The event tenant is invalid.");
        if (storedEvent.EventId == Guid.Empty)
            throw new EventIntegrityException("The event ID is invalid.");

        DomainEventContracts.ValidateEventType(storedEvent.EventType);
        if (!Enum.IsDefined(storedEvent.Privilege))
        {
            throw new EventIntegrityException(
                "The event privilege classification is invalid.");
        }

        ValidateNormalizedTimestamp(storedEvent.OccurredAt);
        ValidateNormalizedTimestamp(storedEvent.RecordedAt);

        if (storedEvent.Position == 1)
        {
            if (!string.Equals(
                    storedEvent.PreviousHash,
                    Sha256HashChain.Genesis,
                    StringComparison.Ordinal))
            {
                throw new EventIntegrityException(
                    "The first event has an invalid previous hash.");
            }
        }
        else if (!Sha256HashChain.IsCanonicalHash(storedEvent.PreviousHash))
        {
            throw new EventIntegrityException(
                "The event previous hash is invalid.");
        }
    }

    public static long ToUnixMicroseconds(DateTimeOffset value) =>
        (value.UtcTicks - DateTimeOffset.UnixEpoch.UtcTicks)
        / TimeSpan.TicksPerMicrosecond;

    public static DateTimeOffset FromUnixMicroseconds(long value)
    {
        try
        {
            return new DateTimeOffset(
                checked(
                    DateTimeOffset.UnixEpoch.UtcTicks
                    + value * TimeSpan.TicksPerMicrosecond),
                TimeSpan.Zero);
        }
        catch (ArgumentOutOfRangeException)
        {
            throw new EventIntegrityException(
                "The Unix microsecond value is outside the supported timestamp range.");
        }
        catch (OverflowException)
        {
            throw new EventIntegrityException(
                "The Unix microsecond value is outside the supported timestamp range.");
        }
    }

    private static void ValidateNormalizedTimestamp(DateTimeOffset value)
    {
        if (value.Offset != TimeSpan.Zero
            || value.UtcTicks % TimeSpan.TicksPerMicrosecond != 0)
        {
            throw new EventIntegrityException(
                "Event timestamps must be normalized UTC microseconds.");
        }
    }

    private static void WriteGuid(Span<byte> target, ref int offset, Guid value)
    {
        if (!value.TryWriteBytes(
                target.Slice(offset, GuidLength),
                bigEndian: true,
                out var written)
            || written != GuidLength)
        {
            throw new EventIntegrityException(
                "The event identifier could not be canonicalized.");
        }
        offset += GuidLength;
    }

    private static void WriteInt32(Span<byte> target, ref int offset, int value)
    {
        BinaryPrimitives.WriteInt32BigEndian(
            target.Slice(offset, sizeof(int)),
            value);
        offset += sizeof(int);
    }

    private static void WriteInt64(Span<byte> target, ref int offset, long value)
    {
        BinaryPrimitives.WriteInt64BigEndian(
            target.Slice(offset, sizeof(long)),
            value);
        offset += sizeof(long);
    }
}
