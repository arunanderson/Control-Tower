using ControlTower.Modules.Economics.Application;
using ControlTower.Modules.Governance.Application;
using ControlTower.Modules.Ledger.Application;
using ControlTower.Modules.Ledger.Domain;
using ControlTower.Platform.Tenancy;
using Microsoft.Extensions.DependencyInjection;

namespace ControlTower.Host.Web;

/// <summary>
/// The Experience-Layer API contract (C7). Read-model-only (I4): every endpoint returns an existing
/// projection, performs no calculation, and never touches domain aggregates. Tenant context is
/// required for all of them (enforced once, as a group filter). These are the contracts the SPA's five
/// areas consume — Portfolio, Economics, Governance, Trust, Administration.
/// </summary>
public static class ExperienceApi
{
    public static void MapExperienceApi(this WebApplication app)
    {
        var api = app.MapGroup("/api");

        // Tenant gate for the whole API surface.
        api.AddEndpointFilter(async (context, next) =>
        {
            var tenants = context.HttpContext.RequestServices.GetRequiredService<ITenantContextAccessor>();
            return tenants.HasTenant
                ? await next(context)
                : Results.BadRequest(new { error = "tenant context required" });
        });

        // Portfolio + the single polymorphic Asset Record.
        api.MapGet("/portfolio/assets", async (IAssetLedgerReadModel ledger) => Results.Ok(await ledger.QueryAsync()));
        api.MapGet("/portfolio/assets/{id:guid}", async (Guid id, IAssetLedgerReadModel ledger) =>
        {
            var record = await ledger.GetAsync(new LedgerAssetId(id));
            return record is null ? Results.NotFound() : Results.Ok(record);
        });

        // Economics — one model, many read models.
        api.MapGet("/economics/executive", async (EconomicsProjectionService e) => Results.Ok(await e.ExecutiveAsync(DateTimeOffset.UtcNow)));
        api.MapGet("/economics/portfolio", async (EconomicsProjectionService e) => Results.Ok(await e.PortfolioRoiAsync(DateTimeOffset.UtcNow)));
        api.MapGet("/economics/departments", async (EconomicsProjectionService e) => Results.Ok(await e.DepartmentRoiAsync(DateTimeOffset.UtcNow)));
        api.MapGet("/economics/agents", async (EconomicsProjectionService e) => Results.Ok(await e.AgentRoiAsync(DateTimeOffset.UtcNow)));

        // Governance workbench.
        api.MapGet("/governance/cases", async (GovernanceService g) => Results.Ok(await g.CasesAsync(DateTimeOffset.UtcNow)));
        api.MapGet("/governance/debt", async (GovernanceService g) => Results.Ok(await g.DebtAsync()));

        // Trust & coverage (honest coverage/freshness).
        api.MapGet("/trust/coverage", async (ICoverageReadModel coverage) => Results.Ok(await coverage.GetAsync()));

        // Administration summary.
        api.MapGet("/admin/summary", (ITenantContextAccessor tenants) => Results.Ok(new
        {
            tenant = tenants.Current.ToString(),
            areas = new[] { "Portfolio", "Economics", "Governance", "Trust", "Administration" },
            readModelOnly = true,
        }));
    }
}
