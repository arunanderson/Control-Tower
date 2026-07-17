using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ControlTower.Platform;
using NetArchTest.Rules;
using Xunit;

namespace ControlTower.ArchitectureTests;

/// <summary>
/// DEV-001 / ADR-023 boundary: infrastructure adapters (incl. dev-only in-memory substitutes) are an
/// outer ring. The kernel and the bounded-context modules must depend only on the ports in Platform —
/// never on a concrete adapter — so a dev substitute can never become a production dependency.
/// </summary>
public class AdapterBoundaryTests
{
    private const string AdaptersNamespace = "ControlTower.Adapters";

    private static readonly (string Namespace, Assembly Assembly)[] Modules =
    [
        ("ControlTower.Modules.Ledger", typeof(Modules.Ledger.LedgerModule).Assembly),
        ("ControlTower.Modules.Governance", typeof(Modules.Governance.GovernanceModule).Assembly),
        ("ControlTower.Modules.Economics", typeof(Modules.Economics.EconomicsModule).Assembly),
        ("ControlTower.Modules.Providers", typeof(Modules.Providers.ProvidersModule).Assembly),
        ("ControlTower.Modules.EnterpriseContext", typeof(Modules.EnterpriseContext.EnterpriseContextModule).Assembly),
        ("ControlTower.Modules.Experience", typeof(Modules.Experience.ExperienceModule).Assembly),
        ("ControlTower.Modules.Trust", typeof(Modules.Trust.TrustModule).Assembly),
        ("ControlTower.Modules.Audit", typeof(Modules.Audit.AuditModule).Assembly),
    ];

    [Fact]
    public void Platform_kernel_must_not_depend_on_adapters()
    {
        var result = Types.InAssembly(typeof(IModule).Assembly)
            .Should().NotHaveDependencyOnAny(AdaptersNamespace).GetResult();
        Assert.True(result.IsSuccessful, Join(result.FailingTypeNames));
    }

    [Fact]
    public void Modules_must_not_depend_on_adapters()
    {
        foreach (var (ns, asm) in Modules)
        {
            var result = Types.InAssembly(asm).Should().NotHaveDependencyOnAny(AdaptersNamespace).GetResult();
            Assert.True(result.IsSuccessful, $"{ns} depends on an adapter: {Join(result.FailingTypeNames)}");
        }
    }

    private static string Join(IEnumerable<string>? names) => string.Join(", ", names ?? Enumerable.Empty<string>());
}
