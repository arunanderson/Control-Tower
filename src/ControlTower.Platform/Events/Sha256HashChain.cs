using System.Security.Cryptography;
using System.Text;

namespace ControlTower.Platform.Events;

/// <summary>
/// SHA-256 hash chain. Pure and deterministic (no infrastructure) — safe in the production domain.
/// Each hash covers the previous hash plus the record's canonical payload, so any tampering with an
/// earlier record breaks every subsequent link (ADR-021 verifiable evidence integrity).
/// </summary>
public sealed class Sha256HashChain : IHashChain
{
    public const string Genesis = "";

    public string ComputeNext(string previousHash, ReadOnlyMemory<byte> payload)
    {
        var prevBytes = Encoding.UTF8.GetBytes(previousHash ?? string.Empty);
        var buffer = new byte[prevBytes.Length + payload.Length];
        prevBytes.CopyTo(buffer, 0);
        payload.Span.CopyTo(buffer.AsSpan(prevBytes.Length));
        return Convert.ToHexString(SHA256.HashData(buffer));
    }
}
