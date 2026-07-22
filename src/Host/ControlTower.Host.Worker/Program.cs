using ControlTower.Adapters.InMemory;
using ControlTower.Host.Worker;
using ControlTower.Modules.Ledger;
using ControlTower.Modules.Providers;
using ControlTower.Platform.DependencyInjection;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddControlTowerPlatform();

// DEV-001: dev-only port substitutes; production wires Azure Service Bus etc.
builder.Services.AddInMemoryAdapters();

// C1 resolution consumes the C4 ObservationIngested topic off the outbox. The Ledger module registers
// its integration-event handler; the dispatcher (below) routes messages to it. The host composes the
// C4→C1 seam — no module references another. (Dev in-memory stores are per-process; production shares
// PostgreSQL/Service Bus so the web and worker hosts see the same state.)
builder.Services.AddLedgerModule();
builder.Services.AddProviderFramework();
builder.Services.AddHostedService<OutboxDispatcher>();

builder.Build().Run();
