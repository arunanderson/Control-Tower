namespace ControlTower.Modules.Ledger.Domain;

/// <summary>
/// A link from the asset to a provider observation stream (Stage 4 §2.3). The set of ResolutionLinks
/// and their native-identifier sets is the alias graph. Resolution never rewrites source observations;
/// it only points at them. Per-link confidence rolls up to the asset's <see cref="MatchConfidence"/>.
/// </summary>
public sealed class ResolutionLink
{
    public ResolutionLink(NativeIdentifierSet identifiers, MatchMethod method, MatchConfidence confidence, string linkedBy, DateTimeOffset linkedAt)
    {
        Id = Guid.NewGuid();
        Identifiers = identifiers;
        Method = method;
        Confidence = confidence;
        LinkedBy = linkedBy;
        LinkedAt = linkedAt;
    }

    public Guid Id { get; }

    public NativeIdentifierSet Identifiers { get; }

    public MatchMethod Method { get; }

    public MatchConfidence Confidence { get; }

    public string LinkedBy { get; }

    public DateTimeOffset LinkedAt { get; }
}
