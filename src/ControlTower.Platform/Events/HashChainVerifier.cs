namespace ControlTower.Platform.Events;

/// <summary>Result of verifying the integrity of an event stream.</summary>
public sealed record ChainVerificationResult(bool IsIntact, long? FirstBrokenPosition, string? Detail)
{
    public static ChainVerificationResult Intact { get; } = new(true, null, null);
}

/// <summary>
/// Recomputes the hash chain over a stored stream and reports the first position where the chain is
/// broken (tampering, reordering, or a missing link). Production logic — used by the integrity
/// verification job and included in evidence exports (ADR-021).
/// </summary>
public sealed class HashChainVerifier(IHashChain hashChain)
{
    public ChainVerificationResult Verify(IReadOnlyList<StoredEvent> stream)
    {
        var previousHash = Sha256HashChain.Genesis;
        foreach (var e in stream)
        {
            if (e.PreviousHash != previousHash)
                return new ChainVerificationResult(false, e.Position, "previous-hash link mismatch");

            var expected = hashChain.ComputeNext(previousHash, e.Payload);
            if (e.Hash != expected)
                return new ChainVerificationResult(false, e.Position, "recomputed hash mismatch (payload tampered)");

            previousHash = e.Hash;
        }

        return ChainVerificationResult.Intact;
    }
}
