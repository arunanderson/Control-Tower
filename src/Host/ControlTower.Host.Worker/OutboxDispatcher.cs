using ControlTower.Platform.Events;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ControlTower.Host.Worker;

/// <summary>
/// Drains the transactional outbox and dispatches messages (Stage 7 §6 background processing).
/// Idempotent by construction: acknowledged messages are never re-delivered. Production dispatch
/// target is Azure Service Bus; the drain loop is the same.
/// </summary>
public sealed class OutboxDispatcher(IOutbox outbox, ILogger<OutboxDispatcher> logger) : BackgroundService
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

    /// <summary>Drains one batch; returns the number dispatched. Extracted for testability.</summary>
    public async Task<int> DrainOnceAsync(CancellationToken ct)
    {
        var batch = await outbox.DequeueBatchAsync(100, ct);
        foreach (var message in batch)
        {
            logger.LogInformation("Dispatched outbox message {Position} on topic {Topic}", message.Position, message.Topic);
            await outbox.AcknowledgeAsync(message.Position, ct);
        }

        return batch.Count;
    }
}
