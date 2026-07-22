using ControlTower.Platform.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ControlTower.Host.Worker;

/// <summary>
/// Drains the transactional outbox and dispatches each message to the integration-event handlers
/// registered for its topic (Stage 7 §6 background processing). This is the host-composed C4→C1 seam:
/// producing and consuming modules never reference each other; the host wires delivery through the
/// registered <see cref="IIntegrationEventHandler"/>s. Idempotent by construction — acknowledged
/// messages are never re-delivered, and handlers are themselves idempotent. Production dispatch target
/// is Azure Service Bus; the drain loop is the same.
/// </summary>
public sealed class OutboxDispatcher(IServiceScopeFactory scopes, IOutbox outbox, ILogger<OutboxDispatcher> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await DrainOnceAsync(stoppingToken);
            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }

    /// <summary>Drains one batch, dispatching to matching handlers; returns the number dispatched. Extracted for testability.</summary>
    public async Task<int> DrainOnceAsync(CancellationToken ct)
    {
        var batch = await outbox.DequeueBatchAsync(100, ct);
        foreach (var message in batch)
        {
            // A fresh DI scope per message: handlers are scoped and establish their own tenant scope.
            using var scope = scopes.CreateScope();
            var handlers = scope.ServiceProvider.GetServices<IIntegrationEventHandler>()
                .Where(h => h.Topic == message.Topic)
                .ToList();

            foreach (var handler in handlers)
                await handler.HandleAsync(message.Payload, ct);

            logger.LogInformation(
                "Dispatched outbox message {Position} on topic {Topic} to {HandlerCount} handler(s)",
                message.Position, message.Topic, handlers.Count);
            await outbox.AcknowledgeAsync(message.Position, ct);
        }

        return batch.Count;
    }
}
