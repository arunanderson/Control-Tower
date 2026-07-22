using ControlTower.Modules.Economics.Application;
using ControlTower.Modules.Governance.Application;
using ControlTower.Modules.Ledger.Application;
using ControlTower.Modules.Ledger.Domain;
using ControlTower.Modules.Providers.Application;
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

        // Registered providers (C4.5 discovery / metadata) — manifests only.
        api.MapGet("/admin/providers", (IProviderRegistry registry) => Results.Ok(registry.Discover()));

        MapResolutionWorkbench(api);
    }

    /// <summary>
    /// Resolution &amp; Merge Workbench (C7). Read endpoints are read-model-only (I4): the alias graph,
    /// resolution links, merge cases and confidence labels. Operator actions (merge/split/manual-link/
    /// resolve-case) are commands routed to the C1 resolution service, which emits immutable audit events
    /// — the UI holds no business logic and never touches provider observations.
    /// </summary>
    private static void MapResolutionWorkbench(RouteGroupBuilder api)
    {
        // Reads (read-model-only).
        api.MapGet("/resolution/merge-cases", async (ResolutionWorkbenchReadModel wb) => Results.Ok(await wb.OpenMergeCasesAsync()));
        api.MapGet("/resolution/assets/{id:guid}", async (Guid id, ResolutionWorkbenchReadModel wb) =>
        {
            var view = await wb.AssetResolutionAsync(new LedgerAssetId(id));
            return view is null ? Results.NotFound() : Results.Ok(view);
        });

        // Operator actions (event-driven, auditable; the UI just invokes them).
        api.MapPost("/resolution/merge", (MergeRequest req, EntityResolutionService svc, HttpContext http) =>
            Guard(() => svc.MergeAsync(new LedgerAssetId(req.TargetId), new LedgerAssetId(req.SourceId), Operator(http))));

        api.MapPost("/resolution/split", (SplitRequest req, EntityResolutionService svc, HttpContext http) =>
            Guard(async () =>
            {
                var newId = await svc.SplitAsync(new LedgerAssetId(req.AssetId), req.LinkIds, req.NewDisplayName, req.NewAssetType, Operator(http));
                return new { newAssetId = newId.Value };
            }));

        api.MapPost("/resolution/manual-link", (ManualLinkRequest req, EntityResolutionService svc, HttpContext http) =>
            Guard(async () =>
            {
                var id = await svc.ApproveManualLinkAsync(
                    new LedgerAssetId(req.AssetId),
                    NativeIdentifierSet.Of(new NativeIdentifier(req.System, req.IdentifierType, req.Value)),
                    req.ObservationRef,
                    Operator(http));
                return new { assetId = id.Value };
            }));

        api.MapPost("/resolution/merge-cases/{id:guid}/resolve", (Guid id, ResolveMergeCaseRequest req, EntityResolutionService svc, HttpContext http) =>
            Guard(() => svc.ResolveMergeCaseAsync(id, req.Outcome, Operator(http))));
    }

    private static string Operator(HttpContext http) =>
        http.Request.Headers.TryGetValue("X-Operator", out var op) && !string.IsNullOrWhiteSpace(op)
            ? op.ToString()
            : "operator";

    private static async Task<IResult> Guard(Func<Task> action)
    {
        try { await action(); return Results.Ok(new { ok = true }); }
        catch (DomainException ex) { return Results.BadRequest(new { error = ex.Message }); }
    }

    private static async Task<IResult> Guard<T>(Func<Task<T>> action)
    {
        try { return Results.Ok(await action()); }
        catch (DomainException ex) { return Results.BadRequest(new { error = ex.Message }); }
    }

    private sealed record MergeRequest(Guid TargetId, Guid SourceId);
    private sealed record SplitRequest(Guid AssetId, IReadOnlyList<Guid> LinkIds, string NewDisplayName, string NewAssetType);
    private sealed record ManualLinkRequest(Guid AssetId, string System, string IdentifierType, string Value, Guid? ObservationRef);
    private sealed record ResolveMergeCaseRequest(string Outcome);
}
