using System.Security.Cryptography;

namespace ControlTower.Platform.Events;

/// <summary>
/// SHA-256 hash chain over the prior binary digest and the canonical stored-event envelope.
/// Genesis contributes no prior digest. Hashes are persisted as canonical uppercase hexadecimal.
/// </summary>
public sealed class Sha256HashChain : IHashChain
{
    public const string Genesis = "";
    public const int HashByteLength = 32;
    public const int HashTextLength = HashByteLength * 2;

    public string ComputeNext(
        string previousHash,
        ReadOnlyMemory<byte> canonicalEnvelope)
    {
        ArgumentNullException.ThrowIfNull(previousHash);

        using var incremental = IncrementalHash.CreateHash(
            HashAlgorithmName.SHA256);
        if (!string.Equals(previousHash, Genesis, StringComparison.Ordinal))
        {
            if (!IsCanonicalHash(previousHash))
            {
                throw new EventIntegrityException(
                    "The previous event hash is invalid.");
            }
            var previous = Convert.FromHexString(previousHash);
            incremental.AppendData(previous);
        }

        incremental.AppendData(canonicalEnvelope.Span);
        return Convert.ToHexString(incremental.GetHashAndReset());
    }

    public static bool IsCanonicalHash(string? value) =>
        value is { Length: HashTextLength }
        && value.All(character =>
            character is >= '0' and <= '9'
            or >= 'A' and <= 'F');
}
