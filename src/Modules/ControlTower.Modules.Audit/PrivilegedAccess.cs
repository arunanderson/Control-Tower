using System.Text.Json;
using ControlTower.Platform.Audit;
using ControlTower.Platform.Events;
using ControlTower.Platform.Tenancy;

namespace ControlTower.Modules.Audit;

public sealed record PrivilegedAccessLogEntry(
    Guid AccessId,
    PrivilegedReadRecord Record,
    string CorrelationId);

public sealed record PrivilegedReadRecorded : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
    public required Guid AccessId { get; init; }
    public required string Actor { get; init; }
    public required string Purpose { get; init; }
    public required string Resource { get; init; }
    public required string CorrelationId { get; init; }
}

public interface IPrivilegedAccessProjection
{
    Task ProjectAsync(PrivilegedAccessLogEntry entry, CancellationToken ct = default);
    Task<IReadOnlyList<PrivilegedAccessLogEntry>> ListAsync(CancellationToken ct = default);
}

public sealed class PrivilegedAccessService(
    IPrivilegedReadAuditor auditor,
    IPrivilegedAccessProjection projection,
    IEventStore events,
    ITenantContextAccessor tenants)
{
    public async Task RecordReadAsync(
        string actor,
        string purpose,
        string resource,
        string correlationId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(actor)) throw new ArgumentException("Actor is required.", nameof(actor));
        if (string.IsNullOrWhiteSpace(purpose)) throw new ArgumentException("Purpose is required.", nameof(purpose));
        if (string.IsNullOrWhiteSpace(resource)) throw new ArgumentException("Resource is required.", nameof(resource));

        var accessId = Guid.NewGuid();
        var record = new PrivilegedReadRecord(
            tenants.Current, actor.Trim(), "ExperienceRead", resource.Trim(), purpose.Trim(), DateTimeOffset.UtcNow);
        await auditor.RecordAsync(record, ct);

        var @event = new PrivilegedReadRecorded
        {
            AccessId = accessId,
            Actor = record.Actor,
            Purpose = record.Purpose,
            Resource = record.ResourceId,
            CorrelationId = correlationId,
            OccurredAt = record.OccurredAt,
        };
        await events.AppendAsync(@event, JsonSerializer.SerializeToUtf8Bytes(@event), ct);
        await projection.ProjectAsync(new PrivilegedAccessLogEntry(accessId, record, correlationId), ct);
    }

    public Task<IReadOnlyList<PrivilegedAccessLogEntry>> ListAsync(CancellationToken ct = default) =>
        projection.ListAsync(ct);
}
