using ControlTower.Platform.Tenancy;

namespace ControlTower.Platform.Audit;

/// <summary>A record that an individual-level (L2+) read occurred (ADR-015.9). Auditing observation,
/// not just mutation — makes L3/L4 "auditable diagnostics" real. On by default.</summary>
public sealed record PrivilegedReadRecord(
    TenantId Tenant,
    string Actor,
    string ResourceKind,
    string ResourceId,
    string Purpose,
    DateTimeOffset OccurredAt);

/// <summary>
/// Records privileged reads. Enabled by default for every tenant (ADR-015.9 / Stage 8 §12); a tenant
/// may disable it only with a recorded justification (configuration is a later phase).
/// </summary>
public interface IPrivilegedReadAuditor
{
    ValueTask RecordAsync(PrivilegedReadRecord record, CancellationToken ct = default);
}
