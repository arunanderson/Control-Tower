using System.Text.Json;
using ControlTower.Platform.Audit;
using ControlTower.Platform.Events;
using ControlTower.Platform.Identity;
using ControlTower.Platform.Tenancy;

namespace ControlTower.Modules.Audit;

public sealed record PrivilegedAccessLogEntry(
    PrivilegedReadRecord Record)
{
    public Guid AccessId => Record.AccessId;
}

[DomainEventContract("PrivilegedReadRecorded", EventPrivilege.Privileged)]
public sealed record PrivilegedReadRecorded : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();

    public DateTimeOffset OccurredAt { get; init; } =
        DateTimeOffset.UtcNow;

    public required Guid AccessId { get; init; }

    public required AuditActor Actor { get; init; }

    public required string Purpose { get; init; }

    public required EventReference Resource { get; init; }

    public required PrivilegedReadPolicy Policy { get; init; }

    public required EventReference CorrelationReference { get; init; }
}

public interface IPrivilegedAccessProjection
{
    Task ProjectAsync(
        PrivilegedAccessLogEntry entry,
        CancellationToken ct = default);

    Task<IReadOnlyList<PrivilegedAccessLogEntry>> ListAsync(
        CancellationToken ct = default);
}

/// <summary>
/// One C9 evidence path for every privileged read. The inner sink is adapter-owned; C9 appends the
/// integrity-covered event and updates the customer-visible projection before the caller may release
/// protected data.
/// </summary>
public sealed class PrivilegedReadEvidenceAuditor(
    IPrivilegedReadRecordSink sink,
    IPrivilegedAccessProjection projection,
    IEventStore events,
    ITenantContextAccessor tenants) : IPrivilegedReadAuditor
{
    public async ValueTask RecordAsync(
        PrivilegedReadRecord record,
        CancellationToken ct = default)
    {
        if (record.Tenant != tenants.Current)
            throw new InvalidOperationException(
                "Cross-tenant privileged-read evidence denied.");

        var @event = new PrivilegedReadRecorded
        {
            AccessId = record.AccessId,
            Actor = record.Actor,
            Purpose = record.Purpose,
            Resource = record.Resource,
            Policy = record.Policy,
            CorrelationReference =
                record.CorrelationReference,
            OccurredAt = record.OccurredAt,
        };
        var metadata = new EventAppendMetadata(
            EventReference.For(
                "privileged-read",
                record.AccessId),
            record.Actor,
            record.Purpose,
            record.CorrelationReference);
        var payload = JsonSerializer.SerializeToUtf8Bytes(@event);

        await events.AppendAsync(
            @event,
            metadata,
            payload,
            ct);
        await sink.StoreAsync(record, ct);
        await projection.ProjectAsync(
            new PrivilegedAccessLogEntry(record),
            ct);
    }
}

public sealed class PrivilegedAccessService(
    IPrivilegedReadAuditor auditor,
    IPrivilegedAccessProjection projection,
    ITenantContextAccessor tenants,
    TimeProvider clock)
{
    public async Task RecordReadAsync(
        AuditActor actor,
        string purpose,
        EventReference resource,
        PrivilegedReadPolicy policy,
        EventReference correlationReference,
        CancellationToken ct = default)
    {
        var accessId = Guid.NewGuid();
        var occurredAt =
            EventEnvelopeCanonicalizer.NormalizeTimestamp(
                clock.GetUtcNow());
        var record = new PrivilegedReadRecord(
            accessId,
            tenants.Current,
            actor,
            resource,
            purpose,
            policy,
            correlationReference,
            occurredAt);
        await auditor.RecordAsync(record, ct);
    }

    public Task<IReadOnlyList<PrivilegedAccessLogEntry>>
        ListAsync(CancellationToken ct = default) =>
        projection.ListAsync(ct);
}
