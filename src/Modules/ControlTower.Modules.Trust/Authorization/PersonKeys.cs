using System.Text;
using System.Text.Json.Serialization;
using ControlTower.Platform.Audit;
using ControlTower.Platform.Events;
using ControlTower.Platform.Identity;

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
