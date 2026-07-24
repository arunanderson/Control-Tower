using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using ControlTower.Adapters.PostgreSql;
using ControlTower.Modules.Trust.Authorization;
using ControlTower.Platform.Identity;
using ControlTower.Platform.Ports;

namespace ControlTower.Adapters.PostgreSql.Trust;

/// <summary>
/// Non-secret E19 key references. Rotating the AES reference affects new ciphertext only; the
/// tenant-specific deterministic lookup key remains immutable until a controlled reindex migration.
/// </summary>
public sealed class PersonKeyProtectionProfile
{
    public PersonKeyProtectionProfile(
        string encryptionReference,
        string indexReference)
    {
        if (!IsReference(encryptionReference))
            throw new ArgumentException(
                "A bounded encryption-key reference is required.",
                nameof(encryptionReference));
        if (!IsReference(indexReference))
            throw new ArgumentException(
                "A bounded index-key reference is required.",
                nameof(indexReference));

        EncryptionReference = encryptionReference;
        IndexReference = indexReference;
    }

    public string EncryptionReference { get; }

    public string IndexReference { get; }

    internal static bool IsReference(string? value) =>
        !string.IsNullOrEmpty(value)
        && value.Length <= 24
        && char.IsAsciiLetterOrDigit(value[0])
        && value[0] is not (>= 'A' and <= 'Z')
        && value.All(character =>
            char.IsAsciiDigit(character)
            || character is >= 'a' and <= 'z'
            || character == '-');
}

/// <summary>
/// E19 application-layer field protection. PostgreSQL receives only authenticated ciphertext,
/// tenant-keyed lookup material and non-secret references; key material remains behind
/// <see cref="ISecretProvider"/>.
/// </summary>
internal sealed class PersonKeyFieldProtector
{
    internal const short ProtectionFormat = 1;
    private const int KeyLength = 32;
    private const int BlindIndexLength = 32;
    private const int NonceLength = 12;
    private const int TagLength = 16;
    private const int MaximumDisplayUtf8Length = 1024;
    private const int PlaintextHeaderLength = 19;
    private const string EncryptionSecretPrefix = "CTE19A1";
    private const string IndexSecretPrefix = "CTE19I1";
    private static readonly byte[] LookupDomain =
        Encoding.ASCII.GetBytes("CT/E19/LOOKUP/v1");
    private static readonly byte[] IndexKeyDomain =
        Encoding.ASCII.GetBytes("CT/E19/INDEX-KEY/v1");
    private static readonly byte[] IdentityDomain =
        Encoding.ASCII.GetBytes("CT/E19/IDENTITY/v1");
    private static readonly UTF8Encoding StrictUtf8 =
        new(
            encoderShouldEmitUTF8Identifier: false,
            throwOnInvalidBytes: true);
    private readonly ISecretProvider _secrets;

    internal PersonKeyFieldProtector(ISecretProvider secrets)
    {
        _secrets = secrets
            ?? throw new ArgumentNullException(nameof(secrets));
    }

    internal async ValueTask<PersonKeyLookup> CreateLookupAsync(
        PostgreSqlTenantCapture tenantCapture,
        Guid directoryObjectId,
        PersonKeyProtectionProfile profile,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(tenantCapture);
        ArgumentNullException.ThrowIfNull(profile);
        if (directoryObjectId == Guid.Empty)
            throw new ArgumentException(
                "A non-empty directory object ID is required.",
                nameof(directoryObjectId));

        var secretName = SecretName(
            tenantCapture,
            "idx",
            profile.IndexReference);
        var key = await ReadKeyAsync(
                secretName,
                IndexSecretPrefix,
                tenantCapture,
                profile.IndexReference,
                ct)
            .ConfigureAwait(false);
        try
        {
            var blindIndex = ComputeBlindIndex(
                key,
                tenantCapture,
                profile.IndexReference,
                directoryObjectId);
            var keyCommitment =
                ComputeIndexKeyCommitment(
                    key,
                    tenantCapture,
                    profile.IndexReference);
            return new PersonKeyLookup(
                profile.IndexReference,
                blindIndex,
                keyCommitment);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    internal async ValueTask<ProtectedPersonIdentity> ProtectAsync(
        PostgreSqlTenantCapture tenantCapture,
        PersonKey personKey,
        DirectoryIdentitySnapshot identity,
        PersonKeyProtectionProfile profile,
        PersonKeyLookup lookup,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(tenantCapture);
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(lookup);
        if (!personKey.IsValid)
            throw new ArgumentException(
                "A non-empty PersonKey is required.",
                nameof(personKey));

        var encryptionSecretName = SecretName(
            tenantCapture,
            "aes",
            profile.EncryptionReference);
        var encryptionKey = await ReadKeyAsync(
                encryptionSecretName,
                EncryptionSecretPrefix,
                tenantCapture,
                profile.EncryptionReference,
                ct)
            .ConfigureAwait(false);
        byte[]? plaintext = null;
        byte[]? aad = null;
        try
        {
            if (!string.Equals(
                    lookup.IndexReference,
                    profile.IndexReference,
                    StringComparison.Ordinal)
                || lookup.BlindIndex.Length
                    != BlindIndexLength
                || lookup.IndexKeyCommitment.Length
                    != BlindIndexLength)
            {
                throw Unavailable();
            }

            var encryptionCommitment =
                ComputeIndexKeyCommitment(
                    encryptionKey,
                    tenantCapture,
                    profile.IndexReference);
            try
            {
                if (CryptographicOperations.FixedTimeEquals(
                        encryptionCommitment,
                        lookup.IndexKeyCommitment))
                {
                    throw Unavailable();
                }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(
                    encryptionCommitment);
            }

            plaintext = EncodeIdentity(identity);
            aad = BuildAssociatedData(
                tenantCapture,
                personKey,
                profile.EncryptionReference,
                profile.IndexReference,
                lookup.BlindIndex);
            var nonce = RandomNumberGenerator.GetBytes(
                NonceLength);
            var tag = new byte[TagLength];
            var ciphertext = new byte[plaintext.Length];
            using (var aes = new AesGcm(
                encryptionKey,
                TagLength))
            {
                aes.Encrypt(
                    nonce,
                    plaintext,
                    ciphertext,
                    tag,
                    aad);
            }

            return new ProtectedPersonIdentity(
                ProtectionFormat,
                profile.EncryptionReference,
                profile.IndexReference,
                lookup.BlindIndex,
                ciphertext,
                nonce,
                tag);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (PostgreSqlTrustException)
        {
            throw;
        }
        catch (Exception)
        {
            throw Unavailable();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(encryptionKey);
            if (plaintext is not null)
                CryptographicOperations.ZeroMemory(plaintext);
            if (aad is not null)
                CryptographicOperations.ZeroMemory(aad);
        }
    }

    internal async ValueTask<DirectoryIdentitySnapshot>
        UnprotectAsync(
            PostgreSqlTenantCapture tenantCapture,
            PersonKey personKey,
            ProtectedPersonIdentity protectedIdentity,
            CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(tenantCapture);
        ArgumentNullException.ThrowIfNull(protectedIdentity);
        var snapshot = protectedIdentity.Clone();
        if (!personKey.IsValid)
            throw Invalid();
        if (snapshot.Format != ProtectionFormat
            || !PersonKeyProtectionProfile.IsReference(
                snapshot.EncryptionReference)
            || !PersonKeyProtectionProfile.IsReference(
                snapshot.IndexReference)
            || snapshot.BlindIndex.Length
                != BlindIndexLength
            || snapshot.Nonce.Length != NonceLength
            || snapshot.Tag.Length != TagLength
            || snapshot.Ciphertext.Length
                < PlaintextHeaderLength
            || snapshot.Ciphertext.Length
                > PlaintextHeaderLength
                    + MaximumDisplayUtf8Length)
        {
            snapshot.Clear();
            throw Invalid();
        }

        var encryptionSecretName = SecretName(
            tenantCapture,
            "aes",
            snapshot.EncryptionReference);
        var indexSecretName = SecretName(
            tenantCapture,
            "idx",
            snapshot.IndexReference);
        byte[]? encryptionKey = null;
        byte[]? indexKey = null;
        byte[]? plaintext = null;
        byte[]? aad = null;
        byte[]? expectedBlindIndex = null;
        try
        {
            encryptionKey = await ReadKeyAsync(
                    encryptionSecretName,
                    EncryptionSecretPrefix,
                    tenantCapture,
                    snapshot.EncryptionReference,
                    ct)
                .ConfigureAwait(false);
            indexKey = await ReadKeyAsync(
                    indexSecretName,
                    IndexSecretPrefix,
                    tenantCapture,
                    snapshot.IndexReference,
                    ct)
                .ConfigureAwait(false);
            if (CryptographicOperations.FixedTimeEquals(
                    encryptionKey,
                    indexKey))
            {
                throw Unavailable();
            }

            plaintext = new byte[snapshot.Ciphertext.Length];
            aad = BuildAssociatedData(
                tenantCapture,
                personKey,
                snapshot.EncryptionReference,
                snapshot.IndexReference,
                snapshot.BlindIndex);
            using (var aes = new AesGcm(
                encryptionKey,
                TagLength))
            {
                aes.Decrypt(
                    snapshot.Nonce,
                    snapshot.Ciphertext,
                    snapshot.Tag,
                    plaintext,
                    aad);
            }

            var identity = DecodeIdentity(plaintext);
            expectedBlindIndex = ComputeBlindIndex(
                indexKey,
                tenantCapture,
                snapshot.IndexReference,
                identity.DirectoryObjectId);
            if (!CryptographicOperations.FixedTimeEquals(
                    expectedBlindIndex,
                    snapshot.BlindIndex))
            {
                throw Invalid();
            }

            return identity;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (PostgreSqlTrustException)
        {
            throw;
        }
        catch (Exception exception)
            when (exception is CryptographicException
                or ArgumentException
                or DecoderFallbackException
                or OverflowException)
        {
            throw Invalid();
        }
        finally
        {
            if (encryptionKey is not null)
                CryptographicOperations.ZeroMemory(
                    encryptionKey);
            if (indexKey is not null)
                CryptographicOperations.ZeroMemory(indexKey);
            if (plaintext is not null)
                CryptographicOperations.ZeroMemory(plaintext);
            if (aad is not null)
                CryptographicOperations.ZeroMemory(aad);
            if (expectedBlindIndex is not null)
            {
                CryptographicOperations.ZeroMemory(
                    expectedBlindIndex);
            }
            snapshot.Clear();
        }
    }

    internal static bool SameDirectoryObject(
        Guid left,
        Guid right)
    {
        Span<byte> leftBytes = stackalloc byte[16];
        Span<byte> rightBytes = stackalloc byte[16];
        try
        {
            WriteGuid(left, leftBytes);
            WriteGuid(right, rightBytes);
            return CryptographicOperations.FixedTimeEquals(
                leftBytes,
                rightBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(leftBytes);
            CryptographicOperations.ZeroMemory(rightBytes);
        }
    }

    private async ValueTask<byte[]> ReadKeyAsync(
        string secretName,
        string expectedPrefix,
        PostgreSqlTenantCapture tenantCapture,
        string expectedReference,
        CancellationToken ct)
    {
        string secret;
        try
        {
            secret = await _secrets.GetSecretAsync(
                    secretName,
                    ct)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            throw Unavailable();
        }

        try
        {
            return ParseKey(
                secret,
                expectedPrefix,
                tenantCapture.Tenant.Value,
                expectedReference);
        }
        catch (PostgreSqlTrustException)
        {
            throw;
        }
        catch (Exception)
        {
            throw Unavailable();
        }
    }

    private static byte[] ParseKey(
        string secret,
        string expectedPrefix,
        Guid expectedTenant,
        string expectedReference)
    {
        try
        {
            var span = secret.AsSpan();
            var first = span.IndexOf(':');
            var second = first < 0
                ? -1
                : span[(first + 1)..].IndexOf(':');
            if (second >= 0)
                second += first + 1;
            var third = second < 0
                ? -1
                : span[(second + 1)..].IndexOf(':');
            if (third >= 0)
                third += second + 1;
            if (first <= 0
                || second <= first + 1
                || third <= second + 1
                || span[(third + 1)..].Contains(':')
                || !span[..first].SequenceEqual(
                    expectedPrefix))
            {
                throw Unavailable();
            }

            Span<char> tenantChars = stackalloc char[32];
            if (!expectedTenant.TryFormat(
                    tenantChars,
                    out var tenantWritten,
                    "N")
                || tenantWritten != tenantChars.Length
                || !span[(first + 1)..second]
                    .SequenceEqual(tenantChars)
                || !span[(second + 1)..third]
                    .SequenceEqual(expectedReference))
            {
                throw Unavailable();
            }

            var encoded = span[(third + 1)..];
            if (encoded.Length != 44)
                throw Unavailable();
            var key = new byte[KeyLength];
            if (!Convert.TryFromBase64Chars(
                    encoded,
                    key,
                    out var bytesWritten)
                || bytesWritten != KeyLength)
            {
                CryptographicOperations.ZeroMemory(key);
                throw Unavailable();
            }

            Span<char> canonical = stackalloc char[44];
            try
            {
                if (!Convert.TryToBase64Chars(
                        key,
                        canonical,
                        out var charsWritten)
                    || charsWritten != canonical.Length
                    || !encoded.SequenceEqual(canonical)
                    || key.All(value => value == 0))
                {
                    CryptographicOperations.ZeroMemory(key);
                    throw Unavailable();
                }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(
                    MemoryMarshal.AsBytes(canonical));
            }

            return key;
        }
        catch (PostgreSqlTrustException)
        {
            throw;
        }
        catch (Exception)
        {
            throw Unavailable();
        }
    }

    private static byte[] ComputeBlindIndex(
        ReadOnlySpan<byte> key,
        PostgreSqlTenantCapture tenantCapture,
        string indexReference,
        Guid directoryObjectId)
    {
        var referenceLength =
            Encoding.ASCII.GetByteCount(indexReference);
        var input = new byte[
            LookupDomain.Length
            + 1
            + referenceLength
            + 16
            + 16];
        try
        {
            var offset = 0;
            LookupDomain.CopyTo(input, offset);
            offset += LookupDomain.Length;
            input[offset++] = checked((byte)referenceLength);
            offset += Encoding.ASCII.GetBytes(
                indexReference,
                input.AsSpan(offset));
            WriteGuid(
                tenantCapture.Tenant.Value,
                input.AsSpan(offset, 16));
            offset += 16;
            WriteGuid(
                directoryObjectId,
                input.AsSpan(offset, 16));
            return HMACSHA256.HashData(key, input);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(input);
        }
    }

    private static byte[] ComputeIndexKeyCommitment(
        ReadOnlySpan<byte> key,
        PostgreSqlTenantCapture tenantCapture,
        string indexReference)
    {
        var referenceLength =
            Encoding.ASCII.GetByteCount(indexReference);
        var input = new byte[
            IndexKeyDomain.Length
            + 1
            + referenceLength
            + 16];
        try
        {
            var offset = 0;
            IndexKeyDomain.CopyTo(input, offset);
            offset += IndexKeyDomain.Length;
            input[offset++] = checked((byte)referenceLength);
            offset += Encoding.ASCII.GetBytes(
                indexReference,
                input.AsSpan(offset));
            WriteGuid(
                tenantCapture.Tenant.Value,
                input.AsSpan(offset, 16));
            return HMACSHA256.HashData(key, input);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(input);
        }
    }

    private static byte[] EncodeIdentity(
        DirectoryIdentitySnapshot identity)
    {
        var displayLength = identity.DisplaySnapshot is null
            ? 0
            : StrictUtf8.GetByteCount(
                identity.DisplaySnapshot);
        if (displayLength > MaximumDisplayUtf8Length)
            throw Invalid();

        var plaintext =
            new byte[PlaintextHeaderLength + displayLength];
        plaintext[0] = 1;
        WriteGuid(
            identity.DirectoryObjectId,
            plaintext.AsSpan(1, 16));
        BinaryPrimitives.WriteUInt16BigEndian(
            plaintext.AsSpan(17, 2),
            checked((ushort)displayLength));
        if (identity.DisplaySnapshot is not null)
        {
            _ = StrictUtf8.GetBytes(
                identity.DisplaySnapshot,
                plaintext.AsSpan(PlaintextHeaderLength));
        }

        return plaintext;
    }

    private static DirectoryIdentitySnapshot DecodeIdentity(
        ReadOnlySpan<byte> plaintext)
    {
        if (plaintext.Length < PlaintextHeaderLength
            || plaintext[0] != 1)
        {
            throw Invalid();
        }

        var displayLength =
            BinaryPrimitives.ReadUInt16BigEndian(
                plaintext.Slice(17, 2));
        if (displayLength > MaximumDisplayUtf8Length
            || plaintext.Length
            != PlaintextHeaderLength + displayLength)
        {
            throw Invalid();
        }

        var directoryObjectId =
            new Guid(
                plaintext.Slice(1, 16),
                bigEndian: true);
        var display = displayLength == 0
            ? null
            : StrictUtf8.GetString(
                plaintext.Slice(
                    PlaintextHeaderLength,
                    displayLength));
        return new DirectoryIdentitySnapshot(
            directoryObjectId,
            display);
    }

    private static byte[] BuildAssociatedData(
        PostgreSqlTenantCapture tenantCapture,
        PersonKey personKey,
        string encryptionReference,
        string indexReference,
        ReadOnlySpan<byte> blindIndex)
    {
        if (blindIndex.Length != BlindIndexLength)
            throw Invalid();

        var encryptionLength =
            Encoding.ASCII.GetByteCount(encryptionReference);
        var indexLength =
            Encoding.ASCII.GetByteCount(indexReference);
        var aad = new byte[
            IdentityDomain.Length
            + 2
            + 16
            + 16
            + 1
            + encryptionLength
            + 1
            + indexLength
            + blindIndex.Length];
        var offset = 0;
        IdentityDomain.CopyTo(aad, offset);
        offset += IdentityDomain.Length;
        BinaryPrimitives.WriteInt16BigEndian(
            aad.AsSpan(offset, 2),
            ProtectionFormat);
        offset += 2;
        WriteGuid(
            tenantCapture.Tenant.Value,
            aad.AsSpan(offset, 16));
        offset += 16;
        WriteGuid(
            personKey.Value,
            aad.AsSpan(offset, 16));
        offset += 16;
        aad[offset++] = checked((byte)encryptionLength);
        offset += Encoding.ASCII.GetBytes(
            encryptionReference,
            aad.AsSpan(offset));
        aad[offset++] = checked((byte)indexLength);
        offset += Encoding.ASCII.GetBytes(
            indexReference,
            aad.AsSpan(offset));
        blindIndex.CopyTo(aad.AsSpan(offset));
        return aad;
    }

    private static string SecretName(
        PostgreSqlTenantCapture tenantCapture,
        string kind,
        string reference) =>
        $"ct-e19-{tenantCapture.Tenant.Value:N}-{kind}-{reference}";

    private static void WriteGuid(
        Guid value,
        Span<byte> destination)
    {
        if (!value.TryWriteBytes(
                destination,
                bigEndian: true,
                out var bytesWritten)
            || bytesWritten != 16)
        {
            throw Invalid();
        }
    }

    private static PostgreSqlTrustException Unavailable() =>
        new(PostgreSqlTrustDb.ProtectionUnavailable);

    private static PostgreSqlTrustException Invalid() =>
        new(PostgreSqlTrustDb.ProtectedIdentityInvalid);
}

internal sealed class PersonKeyLookup(
    string indexReference,
    byte[] blindIndex,
    byte[] indexKeyCommitment)
{
    internal string IndexReference { get; } =
        indexReference;

    internal byte[] BlindIndex { get; } =
        blindIndex;

    internal byte[] IndexKeyCommitment { get; } =
        indexKeyCommitment;
}

internal sealed class ProtectedPersonIdentity
{
    internal ProtectedPersonIdentity(
        short format,
        string encryptionReference,
        string indexReference,
        byte[] blindIndex,
        byte[] ciphertext,
        byte[] nonce,
        byte[] tag)
    {
        Format = format;
        EncryptionReference = encryptionReference;
        IndexReference = indexReference;
        BlindIndex = blindIndex;
        Ciphertext = ciphertext;
        Nonce = nonce;
        Tag = tag;
    }

    internal short Format { get; }
    internal string EncryptionReference { get; }
    internal string IndexReference { get; }
    internal byte[] BlindIndex { get; }
    internal byte[] Ciphertext { get; }
    internal byte[] Nonce { get; }
    internal byte[] Tag { get; }

    internal ProtectedPersonIdentity Clone() =>
        new(
            Format,
            EncryptionReference,
            IndexReference,
            BlindIndex.ToArray(),
            Ciphertext.ToArray(),
            Nonce.ToArray(),
            Tag.ToArray());

    internal void Clear()
    {
        CryptographicOperations.ZeroMemory(BlindIndex);
        CryptographicOperations.ZeroMemory(Ciphertext);
        CryptographicOperations.ZeroMemory(Nonce);
        CryptographicOperations.ZeroMemory(Tag);
    }
}
