using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using ControlTower.Platform.Audit;
using ControlTower.Platform.Events;
using ControlTower.Platform.Identity;
using ControlTower.Platform.Tenancy;

namespace ControlTower.Modules.Trust.Authorization;

/// <summary>
/// The only representation that may carry raw directory person identity. It must never cross the E19
/// port into another aggregate, event payload, read model or API response.
/// </summary>
public sealed record DirectoryIdentitySnapshot
{
    private const int MaximumDisplayLength = 256;

    [JsonConstructor]
    public DirectoryIdentitySnapshot(
        Guid directoryObjectId,
        string? displaySnapshot = null)
    {
        if (directoryObjectId == Guid.Empty)
            throw new ArgumentException(
                "A non-empty directory object ID is required.",
                nameof(directoryObjectId));

        var canonicalDisplay = displaySnapshot?.Trim();
        if (displaySnapshot is not null
            && (!string.Equals(
                    displaySnapshot,
                    canonicalDisplay,
                    StringComparison.Ordinal)
                || canonicalDisplay!.Length == 0
                || canonicalDisplay.Length > MaximumDisplayLength
                || canonicalDisplay.Any(char.IsControl)
                || !PersonKeyText.IsWellFormedUnicode(
                    canonicalDisplay)))
        {
            throw new ArgumentException(
                "A present display snapshot must be bounded non-empty text.",
                nameof(displaySnapshot));
        }

        DirectoryObjectId = directoryObjectId;
        DisplaySnapshot = canonicalDisplay;
    }

    public Guid DirectoryObjectId { get; }

    public string? DisplaySnapshot { get; }
}

/// <summary>
/// Mandatory privileged-zone context. Its actor is already opaque; purpose and correlation are
/// bounded; policy applicability is represented honestly.
/// </summary>
public sealed record PersonKeyAccessContext
{
    [JsonConstructor]
    public PersonKeyAccessContext(
        AuditActor actor,
        string purpose,
        EventReference correlationReference,
        PrivilegedReadPolicy policy)
    {
        if (!actor.IsValid)
            throw new ArgumentException(
                "A valid opaque access actor is required.",
                nameof(actor));
        if (!correlationReference.IsValid)
            throw new ArgumentException(
                "A valid access correlation is required.",
                nameof(correlationReference));
        if (!policy.IsValid)
            throw new ArgumentException(
                "A valid access policy context is required.",
                nameof(policy));

        var canonicalPurpose = purpose?.Trim();
        if (!string.Equals(
                purpose,
                canonicalPurpose,
                StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(canonicalPurpose)
            || canonicalPurpose.Length > 512
            || canonicalPurpose.Any(char.IsControl)
            || !PersonKeyText.IsWellFormedUnicode(
                canonicalPurpose))
        {
            throw new ArgumentException(
                "A bounded person-key access purpose is required.",
                nameof(purpose));
        }

        Actor = actor;
        Purpose = canonicalPurpose;
        CorrelationReference = correlationReference;
        Policy = policy;
    }

    public AuditActor Actor { get; }

    public string Purpose { get; }

    public EventReference CorrelationReference { get; }

    public PrivilegedReadPolicy Policy { get; }
}

internal static class PersonKeyText
{
    private static readonly Encoding StrictUtf8 =
        new UTF8Encoding(
            encoderShouldEmitUTF8Identifier: false,
            throwOnInvalidBytes: true);

    public static bool IsWellFormedUnicode(string value)
    {
        try
        {
            _ = StrictUtf8.GetByteCount(value);
            return true;
        }
        catch (EncoderFallbackException)
        {
            return false;
        }
    }
}

public enum PersonKeyMutationStatus
{
    Created,
    Existing,
}

public sealed record PersonKeyMutationResult(
    PersonKeyMutationStatus Status,
    PersonKey PersonKey,
    long Version);

public enum PersonKeySeverStatus
{
    Severed,
    AlreadySevered,
    NotFound,
    Conflict,
}

public sealed record PersonKeySeverResult(
    PersonKeySeverStatus Status,
    PersonKey? PersonKey,
    long? Version);

public interface IPersonKeyReader
{
    Task<PersonKey?> FindAsync(
        Guid directoryObjectId,
        PersonKeyAccessContext access,
        CancellationToken ct = default);

    Task<DirectoryIdentitySnapshot?> GetAsync(
        PersonKey personKey,
        PersonKeyAccessContext access,
        CancellationToken ct = default);
}

/// <summary>
/// C8 E19 raw-identity perimeter. Every operation is tenant-scoped and privileged-audited; writes
/// are evented; severance destroys both identity directions in constant time.
/// </summary>
public interface IPersonKeyMap : IPersonKeyReader
{
    Task<PersonKeyMutationResult> GetOrCreateAsync(
        DirectoryIdentitySnapshot identity,
        PersonKeyAccessContext access,
        CancellationToken ct = default);

    Task<PersonKeySeverResult> SeverAsync(
        PersonKey personKey,
        long expectedVersion,
        PersonKeyAccessContext access,
        CancellationToken ct = default);
}

/// <summary>
/// Shared C8 invariants for every E19 adapter. The helpers keep raw identity out of evidence and
/// construct the one canonical privileged read and mutation-event shape without exposing another
/// identity model.
/// </summary>
public static class PersonKeyMapSemantics
{
    public static void RejectRawIdentityContext(
        PersonKeyAccessContext access,
        Guid directoryObjectId,
        string? displaySnapshot)
    {
        ArgumentNullException.ThrowIfNull(access);
        if (directoryObjectId == Guid.Empty)
            return;

        Span<char> directoryId = stackalloc char[36];
        Span<char> compactDirectoryId = stackalloc char[32];
        if (!directoryObjectId.TryFormat(
                directoryId,
                out var directoryLength,
                "D")
            || directoryLength != directoryId.Length
            || !directoryObjectId.TryFormat(
                compactDirectoryId,
                out var compactLength,
                "N")
            || compactLength != compactDirectoryId.Length)
        {
            throw new InvalidOperationException(
                "Raw directory identity validation failed.");
        }

        try
        {
            foreach (var value in ContextValues(access))
            {
                if (value.AsSpan().Contains(
                        directoryId,
                        StringComparison.OrdinalIgnoreCase)
                    || value.AsSpan().Contains(
                        compactDirectoryId,
                        StringComparison.OrdinalIgnoreCase)
                    || (displaySnapshot is not null
                        && value.Contains(
                            displaySnapshot,
                            StringComparison.OrdinalIgnoreCase)))
                {
                    throw new InvalidOperationException(
                        "Raw directory identity is forbidden in person-key access evidence.");
                }
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(
                MemoryMarshal.AsBytes(directoryId));
            CryptographicOperations.ZeroMemory(
                MemoryMarshal.AsBytes(compactDirectoryId));
        }
    }

    public static PrivilegedReadRecord ReadRecord(
        TenantId tenant,
        PersonKeyAccessContext access,
        EventReference resource,
        DateTimeOffset occurredAt) =>
        new(
            Guid.NewGuid(),
            tenant,
            access.Actor,
            resource,
            access.Purpose,
            access.Policy,
            access.CorrelationReference,
            occurredAt);

    public static PersonKeyMapChanged Created(
        PersonKey personKey,
        DateTimeOffset occurredAt) =>
        Changed(personKey, version: 1, "Created", occurredAt);

    public static PersonKeyMapChanged Severed(
        PersonKey personKey,
        DateTimeOffset occurredAt) =>
        Changed(personKey, version: 2, "Severed", occurredAt);

    public static EventAppendMetadata Metadata(
        PersonKey personKey,
        PersonKeyAccessContext access) =>
        new(
            EventReference.For(
                "person-key",
                personKey.Value),
            access.Actor,
            access.Purpose,
            access.CorrelationReference);

    private static PersonKeyMapChanged Changed(
        PersonKey personKey,
        long version,
        string change,
        DateTimeOffset occurredAt)
    {
        if (!personKey.IsValid)
            throw new ArgumentException(
                "A non-empty PersonKey is required.",
                nameof(personKey));

        return new PersonKeyMapChanged
        {
            PersonKey = personKey,
            Version = version,
            Change = change,
            OccurredAt = occurredAt,
        };
    }

    private static IEnumerable<string> ContextValues(
        PersonKeyAccessContext access)
    {
        yield return access.Actor.OpaqueId;
        yield return access.Purpose;
        yield return access.CorrelationReference.Kind;
        yield return access.CorrelationReference.Value;
        if (access.Policy.Version is { } version)
        {
            yield return version.Kind;
            yield return version.Value;
        }
    }
}

/// <summary>Production-safe default until the field-protected durable E19 adapter is composed.</summary>
public sealed class DenyAllPersonKeyReader : IPersonKeyReader
{
    public Task<PersonKey?> FindAsync(
        Guid directoryObjectId,
        PersonKeyAccessContext access,
        CancellationToken ct = default) =>
        Task.FromResult<PersonKey?>(null);

    public Task<DirectoryIdentitySnapshot?> GetAsync(
        PersonKey personKey,
        PersonKeyAccessContext access,
        CancellationToken ct = default) =>
        Task.FromResult<DirectoryIdentitySnapshot?>(null);
}

[DomainEventContract("PersonKeyMapChanged", EventPrivilege.Privileged)]
public sealed record PersonKeyMapChanged : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();

    public DateTimeOffset OccurredAt { get; init; } =
        DateTimeOffset.UtcNow;

    public required PersonKey PersonKey { get; init; }

    public required long Version { get; init; }

    public required string Change { get; init; }
}
