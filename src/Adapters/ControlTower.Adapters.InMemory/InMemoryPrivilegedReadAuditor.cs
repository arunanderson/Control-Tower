using System.Linq;
using ControlTower.Platform.Audit;

namespace ControlTower.Adapters.InMemory;

/// <summary>
/// DEV-ONLY (DEV-001) privileged-read record sink. The high-level interface remains implemented
/// only so the legacy generic adapter registration fails closed; successful reads must replace it
/// with C9's complete evidence auditor before protected data can be released.
/// </summary>
public sealed class InMemoryPrivilegedReadAuditor :
    IPrivilegedReadAuditor,
    IPrivilegedReadRecordSink
{
    private readonly List<PrivilegedReadRecord> _records = [];
    private readonly object _gate = new();

    public IReadOnlyList<PrivilegedReadRecord> Records
    {
        get { lock (_gate) return _records.ToList(); }
    }

    public ValueTask RecordAsync(
        PrivilegedReadRecord record,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        ct.ThrowIfCancellationRequested();
        throw new InvalidOperationException(
            "Privileged reads require the complete C9 evidence auditor.");
    }

    public ValueTask StoreAsync(
        PrivilegedReadRecord record,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        lock (_gate) _records.Add(record);
        return ValueTask.CompletedTask;
    }
}
