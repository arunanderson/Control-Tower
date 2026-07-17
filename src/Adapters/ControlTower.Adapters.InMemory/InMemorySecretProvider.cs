using ControlTower.Platform.Ports;

namespace ControlTower.Adapters.InMemory;

/// <summary>DEV-ONLY (DEV-001) <see cref="ISecretProvider"/>. Production: Azure Key Vault.</summary>
public sealed class InMemorySecretProvider(IReadOnlyDictionary<string, string>? secrets = null) : ISecretProvider
{
    private readonly IReadOnlyDictionary<string, string> _secrets = secrets ?? new Dictionary<string, string>();

    public ValueTask<string> GetSecretAsync(string name, CancellationToken ct = default) =>
        _secrets.TryGetValue(name, out var value)
            ? ValueTask.FromResult(value)
            : throw new KeyNotFoundException($"Dev secret '{name}' is not configured.");
}
