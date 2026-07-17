using ControlTower.Adapters.InMemory;
using ControlTower.Host.Worker;
using ControlTower.Platform.DependencyInjection;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddControlTowerPlatform();

// DEV-001: dev-only port substitutes; production wires Azure Service Bus etc.
builder.Services.AddInMemoryAdapters();
builder.Services.AddHostedService<OutboxDispatcher>();

builder.Build().Run();
