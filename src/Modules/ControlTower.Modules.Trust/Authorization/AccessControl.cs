using System.Collections.Frozen;
using ControlTower.Platform.Identity;

namespace ControlTower.Modules.Trust.Authorization;

/// <summary>The four curated C8.2 role bundles fixed for V1.</summary>
public enum ControlTowerRole
{
    Viewer,
    Operator,
    Administrator,
    ExecutiveScope,
}

/// <summary>
/// Fine-grained grants for the existing C7 areas and actions. Callers can receive these only through
/// a curated role bundle; direct capability assignments are deliberately not modelled.
/// </summary>
public enum ControlTowerCapability
{
    PortfolioRead,
    EconomicsExecutiveRead,
    EconomicsPortfolioRead,
    EconomicsDetailRead,
    ReportingPeriodsRead,
    ReportingPeriodsManage,
    GovernanceRead,
    TrustCoverageRead,
    PrivilegedAccessRead,
    LegalHoldsRead,
    LegalHoldsManage,
    AdministrationRead,
    ResolutionRead,
    ResolutionManage,
    LedgerManage,
}

/// <summary>V1 has one organisation scope. BU/delegated scopes are intentionally deferred.</summary>
public enum OrganizationScope
{
    TenantWide,
}

public sealed class EffectiveAccess
{
    internal EffectiveAccess(
        PersonKey? subjectPersonKey,
        OrganizationScope organizationScope,
        IReadOnlyList<ControlTowerRole> roles,
        IReadOnlySet<ControlTowerCapability> capabilities)
    {
        if (subjectPersonKey is { } personKey
            && !personKey.IsValid)
        {
            throw new ArgumentException(
                "The effective-access PersonKey is invalid.",
                nameof(subjectPersonKey));
        }

        SubjectPersonKey = subjectPersonKey;
        OrganizationScope = organizationScope;
        Roles = roles;
        Capabilities = capabilities;
    }

    public PersonKey? SubjectPersonKey { get; }
    public OrganizationScope OrganizationScope { get; }
    public IReadOnlyList<ControlTowerRole> Roles { get; }
    public IReadOnlySet<ControlTowerCapability> Capabilities { get; }

    public bool Allows(ControlTowerCapability capability) =>
        Capabilities.Contains(capability);
}

/// <summary>Single immutable role-to-capability authority for C8.2.</summary>
public static class ControlTowerAccessCatalog
{
    private static readonly FrozenDictionary<ControlTowerRole, FrozenSet<ControlTowerCapability>>
        Bundles = new Dictionary<ControlTowerRole, FrozenSet<ControlTowerCapability>>
        {
            [ControlTowerRole.Viewer] = Bundle(
                ControlTowerCapability.PortfolioRead,
                ControlTowerCapability.EconomicsExecutiveRead,
                ControlTowerCapability.EconomicsPortfolioRead,
                ControlTowerCapability.EconomicsDetailRead,
                ControlTowerCapability.ReportingPeriodsRead,
                ControlTowerCapability.GovernanceRead,
                ControlTowerCapability.TrustCoverageRead,
                ControlTowerCapability.ResolutionRead),
            [ControlTowerRole.Operator] = Bundle(
                ControlTowerCapability.PortfolioRead,
                ControlTowerCapability.EconomicsExecutiveRead,
                ControlTowerCapability.EconomicsPortfolioRead,
                ControlTowerCapability.EconomicsDetailRead,
                ControlTowerCapability.ReportingPeriodsRead,
                ControlTowerCapability.ReportingPeriodsManage,
                ControlTowerCapability.GovernanceRead,
                ControlTowerCapability.TrustCoverageRead,
                ControlTowerCapability.ResolutionRead,
                ControlTowerCapability.ResolutionManage,
                ControlTowerCapability.LedgerManage),
            [ControlTowerRole.Administrator] = Bundle(
                ControlTowerCapability.TrustCoverageRead,
                ControlTowerCapability.PrivilegedAccessRead,
                ControlTowerCapability.LegalHoldsRead,
                ControlTowerCapability.LegalHoldsManage,
                ControlTowerCapability.AdministrationRead),
            [ControlTowerRole.ExecutiveScope] = Bundle(
                ControlTowerCapability.PortfolioRead,
                ControlTowerCapability.EconomicsExecutiveRead,
                ControlTowerCapability.EconomicsPortfolioRead,
                ControlTowerCapability.ReportingPeriodsRead,
                ControlTowerCapability.TrustCoverageRead),
        }.ToFrozenDictionary();

    public static EffectiveAccess Resolve(IEnumerable<ControlTowerRole> assignedRoles)
        => Resolve(null, assignedRoles);

    public static EffectiveAccess Resolve(
        PersonKey? subjectPersonKey,
        IEnumerable<ControlTowerRole> assignedRoles)
    {
        var roles = assignedRoles
            .Where(Enum.IsDefined)
            .Distinct()
            .OrderBy(role => role)
            .ToArray();
        var capabilities = roles
            .SelectMany(role => Bundles[role])
            .ToFrozenSet();
        return new(
            subjectPersonKey,
            OrganizationScope.TenantWide,
            Array.AsReadOnly(roles),
            capabilities);
    }

    public static string Name(ControlTowerRole role) =>
        role switch
        {
            ControlTowerRole.Viewer => "Viewer",
            ControlTowerRole.Operator => "Operator",
            ControlTowerRole.Administrator => "Administrator",
            ControlTowerRole.ExecutiveScope => "Executive-scope",
            _ => throw new ArgumentOutOfRangeException(nameof(role)),
        };

    public static string Name(ControlTowerCapability capability) =>
        capability switch
        {
            ControlTowerCapability.PortfolioRead => "portfolio.read",
            ControlTowerCapability.EconomicsExecutiveRead => "economics.executive.read",
            ControlTowerCapability.EconomicsPortfolioRead => "economics.portfolio.read",
            ControlTowerCapability.EconomicsDetailRead => "economics.detail.read",
            ControlTowerCapability.ReportingPeriodsRead => "economics.reporting-periods.read",
            ControlTowerCapability.ReportingPeriodsManage => "economics.reporting-periods.manage",
            ControlTowerCapability.GovernanceRead => "governance.read",
            ControlTowerCapability.TrustCoverageRead => "trust.coverage.read",
            ControlTowerCapability.PrivilegedAccessRead => "trust.privileged-access.read",
            ControlTowerCapability.LegalHoldsRead => "trust.legal-holds.read",
            ControlTowerCapability.LegalHoldsManage => "trust.legal-holds.manage",
            ControlTowerCapability.AdministrationRead => "administration.read",
            ControlTowerCapability.ResolutionRead => "resolution.read",
            ControlTowerCapability.ResolutionManage => "resolution.manage",
            ControlTowerCapability.LedgerManage => "ledger.manage",
            _ => throw new ArgumentOutOfRangeException(nameof(capability)),
        };

    private static FrozenSet<ControlTowerCapability> Bundle(
        params ControlTowerCapability[] capabilities) =>
        capabilities.ToFrozenSet();
}
