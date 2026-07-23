using ControlTower.Platform.Tenancy;

namespace ControlTower.Platform.Events;

public enum ChainVerificationAssurance
{
    Broken = 0,
    InternallyIntactUnanchored = 1,
    TrustedCheckpointBound = 2,
}

/// <summary>Result of verifying the integrity of an event stream.</summary>
public sealed record ChainVerificationResult(
    bool IsIntact,
    long? FirstBrokenPosition,
    string? Detail,
    ChainVerificationAssurance Assurance)
{
    public bool IsCheckpointBound =>
        Assurance == ChainVerificationAssurance.TrustedCheckpointBound;

    internal static ChainVerificationResult Broken(
        long? position,
        string detail) =>
        new(false, position, detail, ChainVerificationAssurance.Broken);

    internal static ChainVerificationResult Intact(bool checkpointBound) =>
        new(
            true,
            null,
            null,
            checkpointBound
                ? ChainVerificationAssurance.TrustedCheckpointBound
                : ChainVerificationAssurance.InternallyIntactUnanchored);
}

/// <summary>
/// A checkpoint obtained through a separately trusted channel. P1-T05 does not persist or anchor
/// checkpoints; the value lets the verifier detect suffix loss once WORM anchoring is composed.
/// </summary>
public sealed record EventStreamCheckpoint(
    int IntegrityFormatVersion,
    TenantId Tenant,
    long Position,
    string Hash)
{
    public static EventStreamCheckpoint From(StoredEvent storedEvent)
    {
        ArgumentNullException.ThrowIfNull(storedEvent);
        return new(
            storedEvent.IntegrityFormatVersion,
            storedEvent.Tenant,
            storedEvent.Position,
            storedEvent.Hash);
    }
}

/// <summary>
/// Recomputes the hash chain and reports the first structural or cryptographic break. Without a
/// separately trusted checkpoint, an intact result proves only the supplied prefix; it never claims
/// that the stream tail is complete. The expected tenant is supplied independently so even an empty
/// stream or genesis checkpoint remains tenant-bound.
/// </summary>
public sealed class HashChainVerifier(IHashChain hashChain)
{
    public ChainVerificationResult Verify(
        TenantId expectedTenant,
        IReadOnlyList<StoredEvent> stream,
        EventStreamCheckpoint? trustedCheckpoint = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (expectedTenant.Value == Guid.Empty)
        {
            return ChainVerificationResult.Broken(
                1,
                "expected tenant invalid");
        }

        var previousHash = Sha256HashChain.Genesis;
        var eventIds = new HashSet<Guid>();

        for (var index = 0; index < stream.Count; index++)
        {
            var storedEvent = stream[index];
            var expectedPosition = index + 1L;
            if (storedEvent is null)
            {
                return ChainVerificationResult.Broken(
                    expectedPosition,
                    "event record missing");
            }
            if (storedEvent.Position != expectedPosition)
            {
                return ChainVerificationResult.Broken(
                    expectedPosition,
                    "event position discontinuity");
            }
            if (storedEvent.Tenant != expectedTenant)
            {
                return ChainVerificationResult.Broken(
                    storedEvent.Position,
                    "tenant stream mismatch");
            }
            if (!eventIds.Add(storedEvent.EventId))
            {
                return ChainVerificationResult.Broken(
                    storedEvent.Position,
                    "duplicate event ID");
            }
            if (!string.Equals(
                    storedEvent.PreviousHash,
                    previousHash,
                    StringComparison.Ordinal))
            {
                return ChainVerificationResult.Broken(
                    storedEvent.Position,
                    "previous-hash link mismatch");
            }

            byte[] canonicalEnvelope;
            string expectedHash;
            try
            {
                canonicalEnvelope =
                    EventEnvelopeCanonicalizer.Canonicalize(storedEvent);
                expectedHash = hashChain.ComputeNext(
                    previousHash,
                    canonicalEnvelope);
            }
            catch (EventIntegrityException)
            {
                return ChainVerificationResult.Broken(
                    storedEvent.Position,
                    "event envelope invalid");
            }

            if (!Sha256HashChain.IsCanonicalHash(storedEvent.Hash)
                || !string.Equals(
                    storedEvent.Hash,
                    expectedHash,
                    StringComparison.Ordinal))
            {
                return ChainVerificationResult.Broken(
                    storedEvent.Position,
                    "event hash mismatch");
            }

            previousHash = storedEvent.Hash;
        }

        if (trustedCheckpoint is null)
            return ChainVerificationResult.Intact(checkpointBound: false);

        return VerifyCheckpoint(
            stream,
            expectedTenant,
            previousHash,
            trustedCheckpoint);
    }

    private static ChainVerificationResult VerifyCheckpoint(
        IReadOnlyList<StoredEvent> stream,
        TenantId expectedTenant,
        string finalHash,
        EventStreamCheckpoint checkpoint)
    {
        if (checkpoint.IntegrityFormatVersion
            != EventEnvelopeCanonicalizer.CurrentIntegrityFormatVersion
            || checkpoint.Tenant.Value == Guid.Empty
            || checkpoint.Position < 0
            || (checkpoint.Position == 0
                ? !string.Equals(
                    checkpoint.Hash,
                    Sha256HashChain.Genesis,
                    StringComparison.Ordinal)
                : !Sha256HashChain.IsCanonicalHash(checkpoint.Hash)))
        {
            return ChainVerificationResult.Broken(
                stream.Count == 0 ? 1 : stream[^1].Position,
                "trusted checkpoint invalid");
        }

        if (expectedTenant != checkpoint.Tenant)
        {
            return ChainVerificationResult.Broken(
                1,
                "trusted checkpoint tenant mismatch");
        }

        var actualPosition = stream.Count == 0 ? 0 : stream[^1].Position;
        if (actualPosition != checkpoint.Position)
        {
            var firstMissingOrUnexpected =
                Math.Min(actualPosition, checkpoint.Position) + 1;
            return ChainVerificationResult.Broken(
                firstMissingOrUnexpected,
                "trusted checkpoint position mismatch");
        }

        if (!string.Equals(
                finalHash,
                checkpoint.Hash,
                StringComparison.Ordinal))
        {
            return ChainVerificationResult.Broken(
                actualPosition == 0 ? 1 : actualPosition,
                "trusted checkpoint hash mismatch");
        }

        return ChainVerificationResult.Intact(checkpointBound: true);
    }
}
