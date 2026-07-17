using ControlTower.Adapters.InMemory;
using ControlTower.Host.Web;
using ControlTower.Modules.Economics;
using ControlTower.Modules.Governance;
using ControlTower.Modules.Ledger;
using ControlTower.Modules.Providers;
using ControlTower.Platform.DependencyInjection;
using ControlTower.Platform.Tenancy;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControlTowerPlatform();

// DEV-001: in-memory port substitutes + module wiring are registered for local/dev only.
// Production registers the same modules backed by Azure adapters (a later, tenant-gated train).
var experienceApiEnabled = builder.Environment.IsDevelopment();
if (experienceApiEnabled)
{
    builder.Services.AddInMemoryAdapters();
    builder.Services.AddLedgerModule();
    builder.Services.AddEconomicsModule();
    builder.Services.AddGovernanceModule();
    builder.Services.AddProviderFramework();
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

// The Experience-Layer API contract (read-model-only, I4). Backed by the registered modules.
if (experienceApiEnabled)
{
    app.MapExperienceApi();
}

app.Run();

// Exposed for WebApplicationFactory integration tests.
public partial class Program { }
