using System.Reflection;
using ControlTower.Platform;
using ControlTower.Platform.Privacy;

namespace ControlTower.ArchitectureTests;

public sealed class PrivacyBoundaryTests
{
    private static readonly Assembly PlatformAssembly =
        typeof(IModule).Assembly;

    private static readonly Assembly[] ProductionModuleAssemblies =
    [
        typeof(Modules.Ledger.LedgerModule).Assembly,
        typeof(Modules.Governance.GovernanceModule).Assembly,
        typeof(Modules.Economics.EconomicsModule).Assembly,
        typeof(Modules.Providers.ProvidersModule).Assembly,
        typeof(Modules.EnterpriseContext.EnterpriseContextModule).Assembly,
        typeof(Modules.Experience.ExperienceModule).Assembly,
        typeof(Modules.Trust.TrustModule).Assembly,
        typeof(Modules.Audit.AuditModule).Assembly,
    ];

    private static readonly Type[] SharedPrivacyReferenceTypes =
    [
        typeof(JurisdictionRef),
        typeof(PopulationRef),
        typeof(TelemetryCapabilityRef),
        typeof(RetentionPolicyRef),
    ];

    [Fact]
    public void Platform_owns_the_only_L1_to_L4_privacy_marking_enum()
    {
        var privacyLevelEnums = ProductionAssemblies()
            .SelectMany(assembly => assembly.GetTypes())
            .Where(IsL1ToL4PrivacyLevelEnum)
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(new[] { typeof(PrivacyMarking) }, privacyLevelEnums);
        Assert.Equal(PlatformAssembly, typeof(PrivacyMarking).Assembly);
        Assert.Equal(typeof(int), Enum.GetUnderlyingType(typeof(PrivacyMarking)));
    }

    [Fact]
    public void Modules_do_not_redeclare_shared_privacy_reference_or_capability_types()
    {
        var sharedNames = SharedPrivacyReferenceTypes
            .Select(type => type.Name)
            .ToHashSet(StringComparer.Ordinal);
        var moduleDeclarations = ProductionModuleAssemblies
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => sharedNames.Contains(type.Name))
            .Select(type => type.FullName)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(moduleDeclarations);
        Assert.All(
            SharedPrivacyReferenceTypes,
            type => Assert.Equal(PlatformAssembly, type.Assembly));
    }

    private static IReadOnlyList<Assembly> ProductionAssemblies() =>
        ProductionModuleAssemblies
            .Append(PlatformAssembly)
            .Distinct()
            .ToArray();

    private static bool IsL1ToL4PrivacyLevelEnum(Type type)
    {
        if (!type.IsEnum)
            return false;

        var names = Enum.GetNames(type);
        if (!names.SequenceEqual(
                new[] { "L1", "L2", "L3", "L4" },
                StringComparer.Ordinal))
        {
            return false;
        }

        return Enum.GetValues(type)
            .Cast<object>()
            .Select(Convert.ToInt64)
            .SequenceEqual([0L, 1L, 2L, 3L]);
    }
}
