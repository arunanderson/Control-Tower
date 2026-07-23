using ControlTower.Adapters.InMemory;
using ControlTower.Host.Web;
using ControlTower.Host.Web.Authentication;
using ControlTower.Modules.Economics;
using ControlTower.Modules.Audit;
using ControlTower.Modules.Governance;
using ControlTower.Modules.Ledger;
using ControlTower.Modules.Providers;
using ControlTower.Platform.DependencyInjection;
using ControlTower.Platform.Tenancy;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControlTowerAuthentication(builder.Configuration);
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
    builder.Services.AddAuditModule();
}

var app = builder.Build();

app.UseAuthentication();
app.UseMiddleware<AuthenticatedTenantContextMiddleware>();
app.UseAuthorization();

// Liveness/readiness are tenant-independent.
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }))
    .AllowAnonymous();
app.MapGet("/ready", () => Results.Ok(new { status = "ready" }))
    .AllowAnonymous();

// A tenant-scoped probe: proves scoping-by-construction end to end.
app.MapGet("/whoami", (HttpContext http, ITenantContextAccessor tenants) =>
    {
        var human = AuthenticatedHumanContext.Require(http);
        return Results.Ok(new
        {
            tenant = tenants.Current.ToString(),
            directoryTenant = human.DirectoryTenantId,
            actor = human.CanonicalActor,
        });
    })
    .RequireAuthorization();

// The Experience-Layer API contract (read-model-only, I4). Backed by the registered modules.
if (experienceApiEnabled)
{
    app.MapExperienceApi();
}

app.Run();

// Exposed for WebApplicationFactory integration tests.
public partial class Program { }
