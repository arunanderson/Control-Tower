using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ControlTower.Platform.Audit;
using ControlTower.Platform.Events;
using ControlTower.Platform.Tenancy;
using Xunit;

namespace ControlTower.Platform.Tests;

public class EventBackboneTests
{
    private static readonly TenantId Tenant = new(Guid.NewGuid());
    private static byte[] Payload(string s) => Encoding.UTF8.GetBytes(s);

    [Fact]
    public void Hash_chain_is_deterministic_and_depends_on_previous_and_payload()
    {
        var chain = new Sha256HashChain();
        var a = chain.ComputeNext("prev", Payload("x"));
        var b = chain.ComputeNext("prev", Payload("x"));
        var c = chain.ComputeNext("other", Payload("x"));
        var d = chain.ComputeNext("prev", Payload("y"));

        Assert.Equal(a, b);       // deterministic
        Assert.NotEqual(a, c);    // depends on previous hash
        Assert.NotEqual(a, d);    // depends on payload
    }

    [Fact]
    public async Task Append_assigns_monotonic_positions_and_links_the_chain()
    {
        var store = new InMemoryEventStore(Tenant);
        var e1 = await store.AppendAsync(new TestEvent(), Payload("one"));
        var e2 = await store.AppendAsync(new TestEvent(), Payload("two"));

        Assert.Equal(1, e1.Position);
        Assert.Equal(2, e2.Position);
        Assert.Equal(Sha256HashChain.Genesis, e1.PreviousHash);
        Assert.Equal(e1.Hash, e2.PreviousHash); // each links to the prior
    }

    [Fact]
    public async Task Verifier_accepts_an_untampered_stream()
    {
        var store = new InMemoryEventStore(Tenant);
        for (var i = 0; i < 5; i++) await store.AppendAsync(new TestEvent(), Payload($"e{i}"));

        var verifier = new HashChainVerifier(new Sha256HashChain());
        var result = verifier.Verify(await store.ReadAllAsync());

        Assert.True(result.IsIntact);
        Assert.Null(result.FirstBrokenPosition);
    }

    [Fact]
    public async Task Verifier_detects_a_tampered_payload()
    {
        var store = new InMemoryEventStore(Tenant);
        for (var i = 0; i < 5; i++) await store.AppendAsync(new TestEvent(), Payload($"e{i}"));

        var stream = (await store.ReadAllAsync()).ToList();
        // Tamper with the payload of the 3rd event while leaving its stored hash unchanged.
        stream[2] = stream[2] with { Payload = Payload("TAMPERED") };

        var result = new HashChainVerifier(new Sha256HashChain()).Verify(stream);

        Assert.False(result.IsIntact);
        Assert.Equal(3, result.FirstBrokenPosition);
    }

    [Fact]
    public async Task Outbox_drains_in_order_and_acknowledged_messages_do_not_reappear()
    {
        var outbox = new InMemoryOutbox();
        await outbox.EnqueueAsync("assets", Payload("a"));
        await outbox.EnqueueAsync("assets", Payload("b"));

        var batch = await outbox.DequeueBatchAsync(10);
        Assert.Equal(new long[] { 1, 2 }, batch.Select(m => m.Position).ToArray());

        await outbox.AcknowledgeAsync(1);
        var remaining = await outbox.DequeueBatchAsync(10);
        Assert.Equal(new long[] { 2 }, remaining.Select(m => m.Position).ToArray());
    }

    [Fact]
    public async Task Privileged_read_is_recorded()
    {
        var auditor = new InMemoryPrivilegedReadAuditor();
        await auditor.RecordAsync(new PrivilegedReadRecord(
            Tenant, "user@corp", "AssetUsage", "asset-123", "quarterly-review", DateTimeOffset.UtcNow));

        Assert.Single(auditor.Records);
        Assert.Equal("asset-123", auditor.Records[0].ResourceId);
    }

    // ---- in-memory test doubles for the storage contracts (production impls are Azure-backed, later) ----

    private sealed record TestEvent : IDomainEvent
    {
        public Guid EventId { get; } = Guid.NewGuid();
        public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
    }

    private sealed class InMemoryEventStore(TenantId tenant) : IEventStore
    {
        private readonly List<StoredEvent> _events = [];
        private readonly IHashChain _chain = new Sha256HashChain();

        public ValueTask<StoredEvent> AppendAsync(IDomainEvent @event, ReadOnlyMemory<byte> payload, CancellationToken ct = default)
        {
            var previous = _events.Count == 0 ? Sha256HashChain.Genesis : _events[^1].Hash;
            var stored = new StoredEvent(
                _events.Count + 1, @event.EventId, @event.OccurredAt, tenant,
                previous, _chain.ComputeNext(previous, payload), payload.ToArray());
            _events.Add(stored);
            return ValueTask.FromResult(stored);
        }

        public ValueTask<IReadOnlyList<StoredEvent>> ReadAllAsync(CancellationToken ct = default) =>
            ValueTask.FromResult<IReadOnlyList<StoredEvent>>(_events);
    }

    private sealed class InMemoryOutbox : IOutbox
    {
        private readonly List<OutboxMessage> _messages = [];
        private readonly HashSet<long> _acked = [];
        private long _position;

        public ValueTask EnqueueAsync(string topic, ReadOnlyMemory<byte> payload, CancellationToken ct = default)
        {
            _messages.Add(new OutboxMessage(++_position, topic, payload.ToArray()));
            return ValueTask.CompletedTask;
        }

        public ValueTask<IReadOnlyList<OutboxMessage>> DequeueBatchAsync(int max, CancellationToken ct = default) =>
            ValueTask.FromResult<IReadOnlyList<OutboxMessage>>(
                _messages.Where(m => !_acked.Contains(m.Position)).OrderBy(m => m.Position).Take(max).ToList());

        public ValueTask AcknowledgeAsync(long position, CancellationToken ct = default)
        {
            _acked.Add(position);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class InMemoryPrivilegedReadAuditor : IPrivilegedReadAuditor
    {
        public List<PrivilegedReadRecord> Records { get; } = [];

        public ValueTask RecordAsync(PrivilegedReadRecord record, CancellationToken ct = default)
        {
            Records.Add(record);
            return ValueTask.CompletedTask;
        }
    }
}
