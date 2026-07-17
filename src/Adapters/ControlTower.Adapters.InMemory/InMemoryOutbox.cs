using System.Linq;
using ControlTower.Platform.Events;

namespace ControlTower.Adapters.InMemory;

/// <summary>DEV-ONLY (DEV-001) in-memory <see cref="IOutbox"/>. Production: Azure Service Bus.</summary>
public sealed class InMemoryOutbox : IOutbox
{
    private readonly List<OutboxMessage> _messages = [];
    private readonly HashSet<long> _acked = [];
    private readonly object _gate = new();
    private long _position;

    public ValueTask EnqueueAsync(string topic, ReadOnlyMemory<byte> payload, CancellationToken ct = default)
    {
        lock (_gate) _messages.Add(new OutboxMessage(++_position, topic, payload.ToArray()));
        return ValueTask.CompletedTask;
    }

    public ValueTask<IReadOnlyList<OutboxMessage>> DequeueBatchAsync(int max, CancellationToken ct = default)
    {
        lock (_gate)
        {
            IReadOnlyList<OutboxMessage> batch =
                _messages.Where(m => !_acked.Contains(m.Position)).OrderBy(m => m.Position).Take(max).ToList();
            return ValueTask.FromResult(batch);
        }
    }

    public ValueTask AcknowledgeAsync(long position, CancellationToken ct = default)
    {
        lock (_gate) _acked.Add(position);
        return ValueTask.CompletedTask;
    }
}
