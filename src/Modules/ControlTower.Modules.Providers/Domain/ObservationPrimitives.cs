using System.Security.Cryptography;
using System.Text;

namespace ControlTower.Modules.Providers.Domain;

/// <summary>
/// The kind of an immutable observation (Stage 5 E2). Distinct from <see cref="ProviderCapability"/>:
/// capabilities describe what a provider can <em>do</em> (including the non-observational Control), while
/// a kind describes what a persisted observation <em>is</em>. Control acquisition produces a Signal.
/// </summary>
public enum ObservationKind
{
    Inventory,
    Usage,
    Cost,
    Identity,
    Signal,
}

/// <summary>
/// Privacy marking set once, at ingestion, and never changed (ADR-014 Gate 1). L1 is the default; higher
/// levels are applied by policy. Re-masking happens at read (ADR-021) — the marking on the stored
/// observation is immutable.
/// </summary>
public enum PrivacyMarking
{
    L1,
    L2,
    L3,
    L4,
}

/// <summary>
/// Delta status of an observation relative to the last one seen for the same entity on the same
/// connection (Stage 5 E2). Unchanged observations are suppressed — not re-appended — so the stream
/// records only what actually changed.
/// </summary>
public enum DeltaStatus
{
    New,
    Changed,
    Unchanged,
}

/// <summary>
/// The C4 normalization helpers used at the ingestion boundary. No provider-specific logic — the same
/// mapping and hashing apply to Microsoft, CSV or any future provider.
/// </summary>
public static class ObservationNormalization
{
    /// <summary>Maps an acquisition capability to the kind the persisted observation carries.</summary>
    public static ObservationKind KindFor(ProviderCapability capability) => capability switch
    {
        ProviderCapability.Inventory => ObservationKind.Inventory,
        ProviderCapability.Usage => ObservationKind.Usage,
        ProviderCapability.Cost => ObservationKind.Cost,
        ProviderCapability.Identity => ObservationKind.Identity,
        ProviderCapability.Control => ObservationKind.Signal,
        _ => ObservationKind.Signal,
    };

    /// <summary>
    /// The entity key used for delta suppression: the connection plus the primary native identifier.
    /// It deliberately excludes <c>ObservedAt</c> so the same entity seen across sweeps can be compared.
    /// </summary>
    public static string DeltaKeyFor(string connectionRef, RawObservation raw)
    {
        var primary = raw.NativeIdentifiers.Count > 0
            ? $"{raw.NativeIdentifiers[0].System}:{raw.NativeIdentifiers[0].IdentifierType}:{raw.NativeIdentifiers[0].Value}"
            : "(none)";
        return $"{connectionRef}|{raw.SurfaceId}|{primary}";
    }

    /// <summary>
    /// A stable content hash over the observation's material content — sorted native identifiers,
    /// sorted attributes, and kind. Excludes timestamps so an unchanged entity hashes identically
    /// across sweeps (the basis for delta suppression).
    /// </summary>
    public static string ContentHash(RawObservation raw)
    {
        var sb = new StringBuilder();
        sb.Append(KindFor(raw.Capability)).Append('\n');
        foreach (var id in raw.NativeIdentifiers.OrderBy(i => i.System).ThenBy(i => i.IdentifierType).ThenBy(i => i.Value))
            sb.Append(id.System).Append(':').Append(id.IdentifierType).Append(':').Append(id.Value).Append('\n');
        foreach (var kv in raw.Attributes.OrderBy(a => a.Key, StringComparer.Ordinal))
            sb.Append(kv.Key).Append('=').Append(kv.Value).Append('\n');
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexString(bytes);
    }
}
