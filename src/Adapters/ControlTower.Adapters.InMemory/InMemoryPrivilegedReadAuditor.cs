using System.Linq;
using ControlTower.Platform.Audit;

namespace ControlTower.Adapters.InMemory;

/// <summary>DEV-ONLY (DEV-001) in-memory <see cref="IPrivilegedReadAuditor"/>. Production: append-only audit store.</summary>
public sealed class InMemoryPrivilegedReadAuditor : IPrivilegedReadAuditor
{
    private readonly List<PrivilegedReadRecord> _records = [];
    private readonly object _gate = new();

    public IReadOnlyList<PrivilegedReadRecord> Records
    {
        get { lock (_gate) return _records.ToList(); }
    }

    public ValueTask RecordAsync(PrivilegedReadRecord record, CancellationToken ct = default)
    {
        lock (_gate) _records.Add(record);
        return ValueTask.CompletedTask;
    }
}
