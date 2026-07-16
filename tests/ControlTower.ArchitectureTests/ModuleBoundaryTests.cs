using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ControlTower.Platform;
using NetArchTest.Rules;
using Xunit;

namespace ControlTower.ArchitectureTests;

/// <summary>
/// Machine-enforced architecture invariants (R-23 keystone). These guard the modular-monolith
/// boundaries and dependency direction from erosion — including agent drift.
/// </summary>
public class ModuleBoundaryTests
{
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

    private static readonly string[] HostNamespaces =
        ["ControlTower.Host.Web", "ControlTower.Host.Worker"];

    [Fact]
    public void Platform_kernel_must_not_depend_on_any_module()
    {
        var result = Types.InAssembly(typeof(IModule).Assembly)
            .Should()
            .NotHaveDependencyOnAny(Modules.Select(m => m.Namespace).ToArray())
            .GetResult();

        Assert.True(result.IsSuccessful, Fail("Platform", result.FailingTypeNames));
    }

    [Fact]
    public void No_module_may_depend_on_another_module()
    {
        foreach (var (ns, asm) in Modules)
        {
            var others = Modules.Where(m => m.Namespace != ns).Select(m => m.Namespace).ToArray();
            var result = Types.InAssembly(asm).Should().NotHaveDependencyOnAny(others).GetResult();
            Assert.True(result.IsSuccessful, Fail(ns, result.FailingTypeNames));
        }
    }

    [Fact]
    public void Modules_must_not_depend_on_hosts()
    {
        foreach (var (ns, asm) in Modules)
        {
            var result = Types.InAssembly(asm).Should().NotHaveDependencyOnAny(HostNamespaces).GetResult();
            Assert.True(result.IsSuccessful, Fail(ns, result.FailingTypeNames));
        }
    }

    private static string Fail(string subject, IEnumerable<string>? failing) =>
        $"{subject} violates a boundary. Offending types: {string.Join(", ", failing ?? Enumerable.Empty<string>())}";
}
