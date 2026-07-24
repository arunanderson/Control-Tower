using ControlTower.Adapters.PostgreSql;
using ControlTower.Platform.Identity;
using ControlTower.Platform.Tenancy;
using Npgsql;
using NpgsqlTypes;

namespace ControlTower.Adapters.PostgreSql.Trust;

/// <summary>Bounded failure for the durable C8 perimeter. It never carries provider or SQL detail.</summary>
public sealed class PostgreSqlTrustException(string message)
    : Exception(message);

internal static class PostgreSqlTrustDb
{
    internal const string RoleAssignmentRejected =
        "The role-assignment operation was rejected.";
    internal const string PersonKeyRejected =
        "The person-key operation was rejected.";
    internal const string ProtectionUnavailable =
        "Person-key protection is unavailable.";
    internal const string ProtectedIdentityInvalid =
        "Protected identity validation failed.";

    internal static void EnsureAmbientTenant(
        ITenantContextAccessor tenants,
        PostgreSqlTenantCapture capture)
    {
        try
        {
            if (tenants.Current == capture.Tenant)
                return;
        }
        catch (InvalidOperationException)
        {
            // Fall through to the bounded rejection.
        }

        throw new PostgreSqlTrustException(
            "The database tenant context is invalid.");
    }

    internal static async ValueTask<PostgreSqlTenantTransaction>
        BindAsync(
            NpgsqlConnection connection,
            NpgsqlTransaction transaction,
            PostgreSqlTenantCapture capture,
            CancellationToken ct) =>
        await PostgreSqlTenantTransaction.BindAsync(
                connection,
                transaction,
                capture,
                ct)
            .ConfigureAwait(false);

    internal static async Task CommitAsync(
        NpgsqlTransaction transaction,
        ITenantContextAccessor tenants,
        PostgreSqlTenantCapture capture,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        EnsureAmbientTenant(tenants, capture);
        await transaction.CommitAsync(CancellationToken.None)
            .ConfigureAwait(false);
    }

    internal static void AddTenant(
        NpgsqlCommand command,
        TenantId tenant) =>
        command.Parameters.Add(
            "tenant_id",
            NpgsqlDbType.Uuid).Value = tenant.Value;

    internal static void AddActor(
        NpgsqlCommand command,
        string prefix,
        AuditActor actor)
    {
        command.Parameters.Add(
            $"{prefix}_kind",
            NpgsqlDbType.Smallint).Value = (short)actor.Kind;
        command.Parameters.Add(
            $"{prefix}_person_key",
            NpgsqlDbType.Uuid).Value =
            actor.PersonKey is { } personKey
                ? personKey.Value
                : DBNull.Value;
        command.Parameters.Add(
            $"{prefix}_workload_id",
            NpgsqlDbType.Varchar).Value =
            actor.WorkloadId is { } workloadId
                ? workloadId
                : DBNull.Value;
    }

    internal static AuditActor ReadActor(
        NpgsqlDataReader reader,
        int kindOrdinal,
        int personKeyOrdinal,
        int workloadOrdinal)
    {
        var kind =
            (AuditActorKind)reader.GetInt16(kindOrdinal);
        return kind switch
        {
            AuditActorKind.Human
                when !reader.IsDBNull(personKeyOrdinal)
                     && reader.IsDBNull(workloadOrdinal) =>
                AuditActor.Person(
                    new PersonKey(
                        reader.GetGuid(personKeyOrdinal))),
            AuditActorKind.System
                when reader.IsDBNull(personKeyOrdinal)
                     && !reader.IsDBNull(workloadOrdinal) =>
                AuditActor.System(
                    reader.GetString(workloadOrdinal)),
            AuditActorKind.Provider
                when reader.IsDBNull(personKeyOrdinal)
                     && !reader.IsDBNull(workloadOrdinal) =>
                AuditActor.Provider(
                    reader.GetString(workloadOrdinal)),
            _ => throw new PostgreSqlTrustException(
                RoleAssignmentRejected),
        };
    }

    internal static DateTimeOffset ReadTimestamp(
        NpgsqlDataReader reader,
        int ordinal)
    {
        var value = reader.GetDateTime(ordinal);
        var utc = value.Kind == DateTimeKind.Utc
            ? value
            : DateTime.SpecifyKind(value, DateTimeKind.Utc);
        return new DateTimeOffset(utc);
    }
}
