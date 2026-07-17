using System.Text.RegularExpressions;

namespace ControlTower.Modules.Providers.Domain;

public sealed record ValidationResult(bool IsValid, IReadOnlyList<string> Errors)
{
    public static ValidationResult Ok { get; } = new(true, []);
}

/// <summary>
/// Validates that a provider conforms to the C4.5 contract before it is admitted (ADR-007). This is how
/// "treat every provider equally" is enforced — the same checks apply to Microsoft, CSV or any future
/// provider. No provider-specific logic lives here.
/// </summary>
public static partial class ProviderContractValidator
{
    [GeneratedRegex(@"^\d+\.\d+\.\d+$")]
    private static partial Regex SemVer();

    public static ValidationResult ValidateManifest(ProviderManifest manifest)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(manifest.SurfaceId)) errors.Add("SurfaceId is required.");
        if (string.IsNullOrWhiteSpace(manifest.DisplayName)) errors.Add("DisplayName is required.");
        if (string.IsNullOrWhiteSpace(manifest.Version) || !SemVer().IsMatch(manifest.Version))
            errors.Add("Version must be semantic (MAJOR.MINOR.PATCH).");
        if (manifest.Capabilities.Count == 0) errors.Add("At least one capability must be declared.");
        if (manifest.NativeIdentifierTypes.Count == 0) errors.Add("At least one native identifier type must be declared.");
        if (manifest.PayloadSchemaVersion <= 0) errors.Add("PayloadSchemaVersion must be positive.");
        if (manifest.Auth is null) errors.Add("An auth requirement (possibly None) must be declared.");
        if (manifest.FreshnessExpectation <= TimeSpan.Zero) errors.Add("FreshnessExpectation must be positive.");
        return errors.Count == 0 ? ValidationResult.Ok : new ValidationResult(false, errors);
    }

    public static ValidationResult ValidateProvider(IProvider provider) => ValidateManifest(provider.Manifest);
}
