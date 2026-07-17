using ControlTower.Platform.Tenancy;

namespace ControlTower.Modules.Ledger.Domain;

/// <summary>Platform-issued ledger identity — never a native provider id (ADR-012).</summary>
public readonly record struct LedgerAssetId(Guid Value)
{
    public static LedgerAssetId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}

/// <summary>The ledger's relationship to the asset (Stage 4 §4).</summary>
public enum RegistrationStatus
{
    Discovered,
    Triaged,
    Registered,
    Rejected,
    Retired,
    Merged,
}

/// <summary>The asset's own life (Stage 4 §4).</summary>
public enum OperationalLifecycleState
{
    Draft,
    Pilot,
    Production,
    UnderReview,
    Suspended,
    Retired,
}

/// <summary>How sure we are that records are one asset (ADR-012). Categorical, never numeric.</summary>
public enum MatchConfidence
{
    Manual,
    Low,
    Medium,
    High,
}

public enum OwnershipRole
{
    Owner,
    Delegate,
    Sponsor,
}

public enum OwnershipStatus
{
    Active,
    Lapsed,
}

public enum MatchMethod
{
    DocumentedJoin,
    Heuristic,
    Manual,
}

/// <summary>A taxonomy value (e.g. "agent"). Distinct types differ by taxonomy, not behaviour (Stage 4 §11.1).</summary>
public readonly record struct AssetType
{
    public string Value { get; }

    public AssetType(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException("AssetType is required.");
        Value = value.Trim();
    }

    public override string ToString() => Value;
}

/// <summary>A reference to a person — people are references, never aggregates (Stage 4 §11.2).</summary>
public sealed record PersonRef(string EntraObjectId, string DisplayName);

/// <summary>One native identifier as a provider surface reports it.</summary>
public sealed record NativeIdentifier(string System, string IdentifierType, string Value);

/// <summary>The set of native ids a resolution link binds to (the alias-graph edge payload).</summary>
public sealed record NativeIdentifierSet(IReadOnlyList<NativeIdentifier> Identifiers)
{
    public static NativeIdentifierSet Of(params NativeIdentifier[] identifiers) => new(identifiers);
}

/// <summary>Raised when a domain invariant or an illegal state transition is attempted.</summary>
public sealed class DomainException(string message) : Exception(message);
