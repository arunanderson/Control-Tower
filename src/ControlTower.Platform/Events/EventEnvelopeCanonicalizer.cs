using System.Buffers.Binary;
using System.Text;

namespace ControlTower.Platform.Events;

/// <summary>
/// Canonical binary integrity format for the complete E20 event record. Every variable field is
/// length-delimited; optional fields carry an explicit presence byte; scalar values use network
/// byte order and timestamps use signed UTC microseconds.
/// </summary>
public static class EventEnvelopeCanonicalizer
{
    public const int CurrentIntegrityFormatVersion = 2;
    private const int GuidLength = 16;

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

        var eventType = Utf8(storedEvent.EventType);
        var aggregateKind = Utf8(storedEvent.AggregateReference.Kind);
        var aggregateValue = Utf8(storedEvent.AggregateReference.Value);
        var actorId = Utf8(storedEvent.Actor.OpaqueId);
        var reason = storedEvent.Reason is null
            ? null
            : Utf8(storedEvent.Reason);
        var correlationKind =
            storedEvent.CorrelationReference is { } correlation
                ? Utf8(correlation.Kind)
                : null;
        var correlationValue =
            storedEvent.CorrelationReference is { } correlationValueReference
                ? Utf8(correlationValueReference.Value)
                : null;
        var payload = storedEvent.PayloadMemory.Span;

        int length;
        try
        {
            length = checked(
                sizeof(int) // integrity format
                + sizeof(long) // position
                + GuidLength // tenant
                + GuidLength // event ID
                + EncodedLength(eventType)
                + EncodedLength(aggregateKind)
                + EncodedLength(aggregateValue)
                + sizeof(byte) // actor kind
                + EncodedLength(actorId)
                + sizeof(long) // occurred-at UTC microseconds
                + sizeof(long) // recorded-at UTC microseconds
                + sizeof(byte) // reason presence
                + (reason is null ? 0 : EncodedLength(reason))
                + sizeof(byte) // correlation presence
                + (correlationKind is null
                    ? 0
                    : EncodedLength(correlationKind)
                      + EncodedLength(correlationValue!))
                + sizeof(byte) // privilege
                + sizeof(int) // payload length
                + payload.Length);
        }
        catch (OverflowException)
        {
            throw new EventIntegrityException(
                "The event envelope is too large to canonicalize.");
        }

        var output = new byte[length];
        var span = output.AsSpan();
        var offset = 0;

        WriteInt32(
            span,
            ref offset,
            storedEvent.IntegrityFormatVersion);
        WriteInt64(span, ref offset, storedEvent.Position);
        WriteGuid(span, ref offset, storedEvent.Tenant.Value);
        WriteGuid(span, ref offset, storedEvent.EventId);
        WriteBytes(span, ref offset, eventType);
        WriteBytes(span, ref offset, aggregateKind);
        WriteBytes(span, ref offset, aggregateValue);
        span[offset++] = (byte)storedEvent.Actor.Kind;
        WriteBytes(span, ref offset, actorId);
        WriteInt64(
            span,
            ref offset,
            ToUnixMicroseconds(storedEvent.OccurredAt));
        WriteInt64(
            span,
            ref offset,
            ToUnixMicroseconds(storedEvent.RecordedAt));
        WriteOptionalBytes(span, ref offset, reason);
        if (correlationKind is null)
        {
            span[offset++] = 0;
        }
        else
        {
            span[offset++] = 1;
            WriteBytes(span, ref offset, correlationKind);
            WriteBytes(span, ref offset, correlationValue!);
        }
        span[offset++] = (byte)storedEvent.Privilege;
        WriteInt32(span, ref offset, payload.Length);
        payload.CopyTo(span[offset..]);
        offset += payload.Length;

        if (offset != output.Length)
        {
            throw new EventIntegrityException(
                "The event envelope length is inconsistent.");
        }

        return output;
    }

    public static void Validate(StoredEvent storedEvent)
    {
        ArgumentNullException.ThrowIfNull(storedEvent);
        if (storedEvent.IntegrityFormatVersion
            != CurrentIntegrityFormatVersion)
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
        if (!storedEvent.AggregateReference.IsValid)
        {
            throw new EventIntegrityException(
                "The event aggregate reference is invalid.");
        }
        if (!storedEvent.Actor.IsValid)
        {
            throw new EventIntegrityException(
                "The event actor is invalid.");
        }
        try
        {
            _ = new EventAppendMetadata(
                storedEvent.AggregateReference,
                storedEvent.Actor,
                storedEvent.Reason,
                storedEvent.CorrelationReference);
        }
        catch (ArgumentException exception)
        {
            throw new EventIntegrityException(
                $"The event append metadata is invalid: {exception.Message}");
        }
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
        else if (!Sha256HashChain.IsCanonicalHash(
                     storedEvent.PreviousHash))
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

    private static byte[] Utf8(string value)
    {
        try
        {
            return EventText.EncodeUtf8(value);
        }
        catch (EncoderFallbackException)
        {
            throw new EventIntegrityException(
                "The event envelope contains invalid Unicode.");
        }
    }

    private static int EncodedLength(byte[] value) =>
        checked(sizeof(int) + value.Length);

    private static void ValidateNormalizedTimestamp(
        DateTimeOffset value)
    {
        if (value.Offset != TimeSpan.Zero
            || value.UtcTicks % TimeSpan.TicksPerMicrosecond != 0)
        {
            throw new EventIntegrityException(
                "Event timestamps must be normalized UTC microseconds.");
        }
    }

    private static void WriteOptionalBytes(
        Span<byte> target,
        ref int offset,
        byte[]? value)
    {
        if (value is null)
        {
            target[offset++] = 0;
            return;
        }

        target[offset++] = 1;
        WriteBytes(target, ref offset, value);
    }

    private static void WriteBytes(
        Span<byte> target,
        ref int offset,
        byte[] value)
    {
        WriteInt32(target, ref offset, value.Length);
        value.CopyTo(target[offset..]);
        offset += value.Length;
    }

    private static void WriteGuid(
        Span<byte> target,
        ref int offset,
        Guid value)
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

    private static void WriteInt32(
        Span<byte> target,
        ref int offset,
        int value)
    {
        BinaryPrimitives.WriteInt32BigEndian(
            target.Slice(offset, sizeof(int)),
            value);
        offset += sizeof(int);
    }

    private static void WriteInt64(
        Span<byte> target,
        ref int offset,
        long value)
    {
        BinaryPrimitives.WriteInt64BigEndian(
            target.Slice(offset, sizeof(long)),
            value);
        offset += sizeof(long);
    }
}
