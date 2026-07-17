using ControlTower.Adapters.InMemory;
using ControlTower.Host.Web;
using ControlTower.Modules.Economics;
using ControlTower.Modules.Economics.Application;
using ControlTower.Modules.Ledger;
using ControlTower.Modules.Ledger.Application;
using ControlTower.Platform.DependencyInjection;
using ControlTower.Platform.Tenancy;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControlTowerPlatform();

// DEV-001: in-memory port substitutes + dev module wiring are registered for local/dev only.
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddInMemoryAdapters();
    builder.Services.AddLedgerModule();
    builder.Services.AddEconomicsModule();
}

var app = builder.Build();

// Resolve the tenant for each request and open an ambient scope (real Entra token validation later).
app.UseMiddleware<TenantResolutionMiddleware>();

// Liveness/readiness are tenant-independent.
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
app.MapGet("/ready", () => Results.Ok(new { status = "ready" }));

// A tenant-scoped probe: proves scoping-by-construction end to end.
app.MapGet("/whoami", (ITenantContextAccessor tenants) =>
    tenants.HasTenant
        ? Results.Ok(new { tenant = tenants.Current.ToString() })
        : Results.BadRequest(new { error = "tenant context required" }));

// Minimal read-model contract for the Asset Ledger (dev-only; production read APIs are a later phase).
if (app.Environment.IsDevelopment())
{
    app.MapGet("/assets", async (IAssetLedgerReadModel readModel, ITenantContextAccessor tenants) =>
        tenants.HasTenant
            ? Results.Ok(await readModel.QueryAsync())
            : Results.BadRequest(new { error = "tenant context required" }));

    // Minimal economics read-model contract (dev-only). Every figure carries its evidence fields.
    app.MapGet("/economics/portfolio", async (EconomicsProjectionService economics, ITenantContextAccessor tenants) =>
        tenants.HasTenant
            ? Results.Ok(await economics.PortfolioRoiAsync(DateTimeOffset.UtcNow))
            : Results.BadRequest(new { error = "tenant context required" }));

    app.MapGet("/economics/executive", async (EconomicsProjectionService economics, ITenantContextAccessor tenants) =>
        tenants.HasTenant
            ? Results.Ok(await economics.ExecutiveAsync(DateTimeOffset.UtcNow))
            : Results.BadRequest(new { error = "tenant context required" }));
}

app.Run();

// Exposed for WebApplicationFactory integration tests.
public partial class Program { }
