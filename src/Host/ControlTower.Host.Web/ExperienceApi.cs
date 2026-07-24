using ControlTower.Modules.Economics.Application;
using ControlTower.Modules.Economics.Domain;
using ControlTower.Modules.Audit;
using ControlTower.Modules.Governance.Application;
using ControlTower.Modules.Ledger.Application;
using ControlTower.Modules.Ledger.Domain;
using ControlTower.Modules.Providers.Application;
using ControlTower.Host.Web.Authentication;
using ControlTower.Host.Web.Authorization;
using ControlTower.Modules.Trust.Authorization;
using ControlTower.Platform.Identity;
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
        var api = app.MapGroup("/api")
            .RequireAuthorization();

        // Tenant gate for the whole API surface.
        api.AddEndpointFilter(async (context, next) =>
        {
            var tenants = context.HttpContext.RequestServices.GetRequiredService<ITenantContextAccessor>();
            return tenants.HasTenant
                ? await next(context)
                : Results.Unauthorized();
        });

        // Portfolio + the single polymorphic Asset Record.
        api.MapGet("/portfolio/assets", async (IAssetLedgerReadModel ledger) =>
                Results.Ok(await ledger.QueryAsync()))
            .RequireControlTowerCapability(ControlTowerCapability.PortfolioRead);
        api.MapGet("/portfolio/assets/{id:guid}", async (Guid id, IAssetLedgerReadModel ledger) =>
        {
            var record = await ledger.GetAsync(new LedgerAssetId(id));
            return record is null ? Results.NotFound() : Results.Ok(record);
        }).RequireControlTowerCapability(ControlTowerCapability.PortfolioRead);

        // Economics — one model, many read models.
        api.MapGet("/economics/executive", async (EconomicsProjectionService e) =>
                Results.Ok(await e.ExecutiveAsync(DateTimeOffset.UtcNow)))
            .RequireControlTowerCapability(
                ControlTowerCapability.EconomicsExecutiveRead);
        api.MapGet("/economics/portfolio", async (EconomicsProjectionService e) =>
                Results.Ok(await e.PortfolioRoiAsync(DateTimeOffset.UtcNow)))
            .RequireControlTowerCapability(
                ControlTowerCapability.EconomicsPortfolioRead);
        api.MapGet("/economics/departments", async (EconomicsProjectionService e) =>
                Results.Ok(await e.DepartmentRoiAsync(DateTimeOffset.UtcNow)))
            .RequireControlTowerCapability(
                ControlTowerCapability.EconomicsDetailRead);
        api.MapGet("/economics/agents", async (EconomicsProjectionService e) =>
                Results.Ok(await e.AgentRoiAsync(DateTimeOffset.UtcNow)))
            .RequireControlTowerCapability(
                ControlTowerCapability.EconomicsDetailRead);
        MapReportingPeriods(api);

        // Governance workbench.
        api.MapGet("/governance/cases", async (GovernanceService g) =>
                Results.Ok(await g.CasesAsync(DateTimeOffset.UtcNow)))
            .RequireControlTowerCapability(ControlTowerCapability.GovernanceRead);
        api.MapGet("/governance/debt", async (GovernanceService g) =>
                Results.Ok(await g.DebtAsync()))
            .RequireControlTowerCapability(ControlTowerCapability.GovernanceRead);

        // Trust & coverage (honest coverage/freshness).
        api.MapGet("/trust/coverage", async (ICoverageReadModel coverage) =>
                Results.Ok(await coverage.GetAsync()))
            .RequireControlTowerCapability(
                ControlTowerCapability.TrustCoverageRead);
        api.MapGet("/trust/privileged-access", async (PrivilegedAccessService audit) =>
                Results.Ok((await audit.ListAsync()).Select(x => new
                {
                    x.AccessId,
                    actor = x.Record.Actor.ToString(),
                    x.Record.Purpose,
                    resource = x.Record.ResourceId,
                    x.Record.OccurredAt,
                    policyApplicable =
                        x.Record.Policy.Kind
                        == ControlTower.Platform.Audit
                            .PrivilegedReadPolicyKind.Applied,
                    policyVersion =
                        x.Record.Policy.Version?.ToString(),
                    correlationId =
                        x.Record.CorrelationReference.ToString(),
                })))
            .RequireControlTowerCapability(
                ControlTowerCapability.PrivilegedAccessRead)
            .AuditPrivilegedRead("trust.privileged-access-log");
        MapLegalHolds(api);

        // Administration summary.
        api.MapGet("/admin/summary", (ITenantContextAccessor tenants) => Results.Ok(new
        {
            tenant = tenants.Current.ToString(),
            areas = new[] { "Portfolio", "Economics", "Governance", "Trust", "Administration" },
            readModelOnly = true,
        })).RequireControlTowerCapability(
            ControlTowerCapability.AdministrationRead);

        // Registered providers (C4.5 discovery / metadata) — manifests only.
        api.MapGet("/admin/providers", (IProviderRegistry registry) =>
                Results.Ok(registry.Discover()))
            .RequireControlTowerCapability(
                ControlTowerCapability.AdministrationRead);

        MapResolutionWorkbench(api);
    }

    /// <summary>C9 legal-hold lifecycle; active matching holds take precedence over retention.</summary>
    private static void MapLegalHolds(RouteGroupBuilder api)
    {
        api.MapGet("/trust/legal-holds", async (LegalHoldService service) =>
                Results.Ok(await service.ListAsync()))
            .RequireControlTowerCapability(ControlTowerCapability.LegalHoldsRead);
        api.MapPost("/trust/legal-holds", (PlaceLegalHoldRequest request, LegalHoldService service, HttpContext http) =>
            LegalHoldGuard(async () =>
            {
                if (!Enum.TryParse<RetentionDataClass>(request.DataClass, true, out var dataClass))
                    throw new LegalHoldException("Unknown retention data class.");
                return new
                {
                    holdId = await service.PlaceAsync(
                        new LegalHoldScope(dataClass, request.ResourceReference),
                        request.Reason,
                        RequiredLegalHoldOperator(http)),
                };
            })).RequireControlTowerCapability(
                ControlTowerCapability.LegalHoldsManage);
        api.MapPost("/trust/legal-holds/{id:guid}/release", (Guid id, ReleaseLegalHoldRequest request, LegalHoldService service, HttpContext http) =>
            LegalHoldGuard(() => service.ReleaseAsync(
                id,
                RequiredLegalHoldOperator(http),
                request.Reason,
                RequiredApprovalReference(http))))
            .RequireControlTowerCapability(
                ControlTowerCapability.LegalHoldsManage);
    }

    /// <summary>Reporting-period commands and immutable C3 snapshot read models (ADR-016).</summary>
    private static void MapReportingPeriods(RouteGroupBuilder api)
    {
        api.MapGet("/economics/reporting-periods", async (ReportingSnapshotService service) =>
                Results.Ok(await service.PeriodsAsync()))
            .RequireControlTowerCapability(
                ControlTowerCapability.ReportingPeriodsRead);
        api.MapGet("/economics/reporting-periods/{id:guid}/snapshots", (Guid id, ReportingSnapshotService service) =>
                EconomicsGuard(() => service.SnapshotsAsync(id)))
            .RequireControlTowerCapability(
                ControlTowerCapability.ReportingPeriodsRead);

        api.MapPost("/economics/reporting-periods", (CreateReportingPeriodRequest request, ReportingSnapshotService service, HttpContext http) =>
            EconomicsGuard(async () =>
            {
                _ = RequiredOperator(http);
                return new { periodId = await service.CreatePeriodAsync(request.Start, request.End) };
            })).RequireControlTowerCapability(
                ControlTowerCapability.ReportingPeriodsManage);
        api.MapPost("/economics/reporting-periods/{id:guid}/closing", (Guid id, ReportingSnapshotService service, HttpContext http) =>
            EconomicsGuard(async () =>
            {
                _ = RequiredOperator(http);
                await service.BeginClosingAsync(id);
            })).RequireControlTowerCapability(
                ControlTowerCapability.ReportingPeriodsManage);
        api.MapPost("/economics/reporting-periods/{id:guid}/freeze", (Guid id, SnapshotRequest request, ReportingSnapshotService service, HttpContext http) =>
                EconomicsGuard(() => service.FreezeAsync(
                    id,
                    request.PayloadJson,
                    request.InputBasis,
                    RequiredOperator(http))))
            .RequireControlTowerCapability(
                ControlTowerCapability.ReportingPeriodsManage);
        api.MapPost("/economics/reporting-periods/{id:guid}/restate", (Guid id, RestatementRequest request, ReportingSnapshotService service, HttpContext http) =>
                EconomicsGuard(() => service.RestateAsync(
                    id,
                    request.PayloadJson,
                    request.InputBasis,
                    RequiredOperator(http),
                    request.Reason)))
            .RequireControlTowerCapability(
                ControlTowerCapability.ReportingPeriodsManage);
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
        api.MapGet("/resolution/merge-cases", async (ResolutionWorkbenchReadModel wb) =>
                Results.Ok(await wb.OpenMergeCasesAsync()))
            .RequireControlTowerCapability(ControlTowerCapability.ResolutionRead);
        api.MapGet("/resolution/assets/{id:guid}", async (Guid id, ResolutionWorkbenchReadModel wb) =>
        {
            var view = await wb.AssetResolutionAsync(new LedgerAssetId(id));
            return view is null ? Results.NotFound() : Results.Ok(view);
        }).RequireControlTowerCapability(ControlTowerCapability.ResolutionRead);

        // Operator actions (event-driven, auditable; the UI just invokes them).
        api.MapPost("/resolution/merge", (MergeRequest req, EntityResolutionService svc, HttpContext http) =>
                Guard(() => svc.MergeAsync(
                    new LedgerAssetId(req.TargetId),
                    new LedgerAssetId(req.SourceId),
                    Operator(http))))
            .RequireControlTowerCapability(ControlTowerCapability.ResolutionManage);

        api.MapPost("/resolution/split", (SplitRequest req, EntityResolutionService svc, HttpContext http) =>
            Guard(async () =>
            {
                var newId = await svc.SplitAsync(new LedgerAssetId(req.AssetId), req.LinkIds, req.NewDisplayName, req.NewAssetType, Operator(http));
                return new { newAssetId = newId.Value };
            })).RequireControlTowerCapability(
                ControlTowerCapability.ResolutionManage);

        api.MapPost("/resolution/manual-link", (ManualLinkRequest req, EntityResolutionService svc, HttpContext http) =>
            Guard(async () =>
            {
                var id = await svc.ApproveManualLinkAsync(
                    new LedgerAssetId(req.AssetId),
                    NativeIdentifierSet.Of(new NativeIdentifier(req.System, req.IdentifierType, req.Value)),
                    req.ObservationRef,
                    Operator(http));
                return new { assetId = id.Value };
            })).RequireControlTowerCapability(
                ControlTowerCapability.ResolutionManage);

        api.MapPost("/resolution/merge-cases/{id:guid}/resolve", (Guid id, ResolveMergeCaseRequest req, EntityResolutionService svc, HttpContext http) =>
                Guard(() => svc.ResolveMergeCaseAsync(id, req.Outcome, Operator(http))))
            .RequireControlTowerCapability(
                ControlTowerCapability.ResolutionManage);
    }

    private static AuditActor Operator(HttpContext http)
    {
        var human = AuthenticatedHumanContext.Require(http);
        var tenants = http.RequestServices
            .GetRequiredService<ITenantContextAccessor>();
        return http.RequestServices
            .GetRequiredService<CurrentEffectiveAccess>()
            .RequireActor(tenants.Current, human.ObjectId);
    }

    private static AuditActor RequiredOperator(HttpContext http) =>
        Operator(http);

    private static AuditActor RequiredLegalHoldOperator(
        HttpContext http) =>
        Operator(http);

    private static string RequiredApprovalReference(HttpContext http) =>
        RequestBusinessContext.TryGetApprovalReference(http, out var approval)
            ? approval
            : throw new LegalHoldException(
                "A valid X-Approval-Reference is required to release a legal hold.");

    private static async Task<IResult> LegalHoldGuard(Func<Task> action)
    {
        try { await action(); return Results.Ok(new { ok = true }); }
        catch (LegalHoldException ex) { return Results.BadRequest(new { error = ex.Message }); }
    }

    private static async Task<IResult> LegalHoldGuard<T>(Func<Task<T>> action)
    {
        try { return Results.Ok(await action()); }
        catch (LegalHoldException ex) { return Results.BadRequest(new { error = ex.Message }); }
    }

    private static async Task<IResult> EconomicsGuard(Func<Task> action)
    {
        try { await action(); return Results.Ok(new { ok = true }); }
        catch (EconomicsException ex) { return Results.BadRequest(new { error = ex.Message }); }
    }

    private static async Task<IResult> EconomicsGuard<T>(Func<Task<T>> action)
    {
        try { return Results.Ok(await action()); }
        catch (EconomicsException ex) { return Results.BadRequest(new { error = ex.Message }); }
    }

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
    private sealed record CreateReportingPeriodRequest(DateTimeOffset Start, DateTimeOffset End);
    private sealed record SnapshotRequest(string PayloadJson, ReportInputBasis InputBasis);
    private sealed record RestatementRequest(string PayloadJson, ReportInputBasis InputBasis, string Reason);
    private sealed record PlaceLegalHoldRequest(string DataClass, string? ResourceReference, string Reason);
    private sealed record ReleaseLegalHoldRequest(string Reason);
}
