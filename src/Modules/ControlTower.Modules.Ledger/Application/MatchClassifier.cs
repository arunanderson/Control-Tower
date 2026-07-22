using ControlTower.Modules.Ledger.Domain;

namespace ControlTower.Modules.Ledger.Application;

/// <summary>What resolution should do with an incoming observation, given the candidate assets it matched.</summary>
public enum MatchOutcome
{
    /// <summary>No candidate — create a new asset.</summary>
    NoMatch,

    /// <summary>Exactly one candidate, strong enough to link automatically.</summary>
    AutoLink,

    /// <summary>A candidate exists but confidence is too low to auto-link — send to the manual merge queue.</summary>
    Review,

    /// <summary>The identifier maps to more than one asset — a collision the queue must resolve.</summary>
    Collision,
}

/// <summary>The classifier's decision: what to do, the target (for AutoLink), and the graded confidence/method.</summary>
public sealed record MatchDecision(
    MatchOutcome Outcome,
    MatchConfidence Confidence,
    MatchMethod Method,
    LedgerAssetId? Target,
    IReadOnlyList<LedgerAssetId> Candidates,
    string Reason);

/// <summary>
/// Decides, from the candidate assets an identifier matched, whether to auto-link, create, review, or flag
/// a collision. This is the seam where the confidence RULE TABLE lives; implementations are deliberately
/// pluggable so the PoC-gated Microsoft cross-surface rules (PoC-1/2) can replace the provisional default
/// without touching the resolution mechanism.
/// </summary>
public interface IMatchClassifier
{
    MatchDecision Classify(NativeIdentifier primary, IReadOnlyList<AIAsset> candidates);
}

/// <summary>
/// The provisional, provider-agnostic default (Stage 5 §confidence, ⛔PoC for cross-surface rules): exact
/// native-identifier equality is a DocumentedJoin → High and may auto-link; two or more candidates is a
/// collision; none is a no-match (create). It makes NO Microsoft-specific assumptions and never invents a
/// cross-surface mapping — heuristic (Medium) and weak (Low) matching stay for the PoC-gated rule set and
/// are handled conservatively (Review, never auto-link) if a future classifier emits them.
/// </summary>
public sealed class DeterministicMatchClassifier : IMatchClassifier
{
    public MatchDecision Classify(NativeIdentifier primary, IReadOnlyList<AIAsset> candidates)
    {
        var distinct = candidates.Select(c => c.Id).Distinct().ToList();
        return distinct.Count switch
        {
            0 => new MatchDecision(MatchOutcome.NoMatch, MatchConfidence.High, MatchMethod.DocumentedJoin, null, distinct,
                "No existing asset carries this identifier — deterministic new-asset creation."),
            1 => new MatchDecision(MatchOutcome.AutoLink, MatchConfidence.High, MatchMethod.DocumentedJoin, distinct[0], distinct,
                "Exact native-identifier match to a single asset (documented join)."),
            _ => new MatchDecision(MatchOutcome.Collision, MatchConfidence.Low, MatchMethod.Heuristic, null, distinct,
                $"Identifier {primary.System}:{primary.IdentifierType} maps to {distinct.Count} assets — collision."),
        };
    }
}
