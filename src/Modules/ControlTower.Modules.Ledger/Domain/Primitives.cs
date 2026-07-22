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

/// <summary>
/// Lifecycle of a resolution link (Stage 5 E6). Links are never deleted or rewritten: a link that no
/// longer holds is <see cref="Severed"/>, and a link replaced by a merge/split is <see cref="Superseded"/>.
/// Only <see cref="Active"/> links are material to confidence roll-up.
/// </summary>
public enum LinkStatus
{
    Active,
    Severed,
    Superseded,
}

/// <summary>Where an identity alias came from (Stage 5 E5). Observed aliases are provider-reported; operator/import are human-sourced.</summary>
public enum AliasProvenance
{
    Observed,
    Operator,
    Import,
}

/// <summary>
/// A provider-scoped identity alias (Stage 5 E5): a native identifier plus its provenance. Aliases are
/// created from observations and carried on resolution links; the set of aliases across an asset's active
/// links is its slice of the alias graph. Provider-specific alias TYPES (e.g. cross-surface joins) remain
/// PoC-gated (PoC-1/2) — this record is the generic container, not a finalized Microsoft mapping.
/// </summary>
public sealed record IdentityAlias(string System, string IdentifierType, string Value, AliasProvenance Provenance)
{
    public static IdentityAlias Observed(NativeIdentifier id) => new(id.System, id.IdentifierType, id.Value, AliasProvenance.Observed);

    public bool Matches(NativeIdentifier id) => id.System == System && id.IdentifierType == IdentifierType && id.Value == Value;
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
