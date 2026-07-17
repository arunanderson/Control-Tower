namespace ControlTower.Modules.Ledger.Domain;

/// <summary>
/// The controlled vocabulary of asset types (C1 aggregate, Stage 4 §2.5). Versioned; assets reference
/// scheme values. Additional reusable-asset types (prompt pack, connector, …) enter here as values —
/// not as new aggregates.
/// </summary>
public sealed class TaxonomyScheme
{
    private readonly HashSet<string> _values;

    public TaxonomyScheme(string schemeId, int version, IEnumerable<string> values)
    {
        if (string.IsNullOrWhiteSpace(schemeId)) throw new DomainException("Scheme id required.");
        SchemeId = schemeId;
        Version = version;
        _values = new HashSet<string>(values, StringComparer.OrdinalIgnoreCase);
        if (_values.Count == 0) throw new DomainException("Taxonomy scheme must define at least one value.");
    }

    public string SchemeId { get; }

    public int Version { get; }

    public bool IsValid(AssetType type) => _values.Contains(type.Value);

    /// <summary>The starting V1 vocabulary (Stage 4 §2.5). Extend by publishing a new version.</summary>
    public static TaxonomyScheme Default { get; } = new(
        "default", 1,
        [
            "agent", "declarative-agent", "copilot-agent", "flow", "automation",
            "model-deployment", "mcp-server", "connector", "prompt-pack",
            "knowledge-base", "external-ai-service"
        ]);
}
