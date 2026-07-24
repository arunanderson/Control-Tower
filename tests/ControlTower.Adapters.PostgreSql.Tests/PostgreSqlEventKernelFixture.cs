using System.Security.Cryptography;
using Npgsql;

namespace ControlTower.Adapters.PostgreSql.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PostgreSqlEventKernelCollection
    : ICollectionFixture<PostgreSqlEventKernelFixture>
{
    public const string Name = "PostgreSQL E20 kernel";
}

public sealed class PostgreSqlEventKernelFixture : IAsyncLifetime
{
    internal const string Sentinel = "P1-T07-EPHEMERAL-ONLY";
    private const string RuntimeRole = "control_tower_runtime";
    private NpgsqlDataSource? _adminDataSource;
    private string? _migratorPassword;
    private string? _runtimePassword;

    public bool Enabled { get; private set; }
    public bool MigrationCycleVerified { get; private set; }
    public string? ServerVersion { get; private set; }
    public string DatabaseName { get; private set; } = string.Empty;
    public string MigratorRole { get; private set; } = string.Empty;
    public NpgsqlDataSource MigrationDataSource { get; private set; } = null!;
    public NpgsqlDataSource RuntimeDataSource { get; private set; } = null!;
    public string RuntimeConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        var marker =
            Environment.GetEnvironmentVariable(
                "CONTROL_TOWER_POSTGRES_EPHEMERAL");
        if (marker is null)
            return;
        if (!string.Equals(marker, Sentinel, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The PostgreSQL test sentinel is invalid.");
        }

        var adminConnectionString =
            Environment.GetEnvironmentVariable(
                "CONTROL_TOWER_POSTGRES_ADMIN")
            ?? throw new InvalidOperationException(
                "The ephemeral PostgreSQL admin connection is missing.");
        var adminBuilder =
            new NpgsqlConnectionStringBuilder(adminConnectionString);
        ValidateLoopbackAdmin(adminBuilder);
        adminBuilder.IncludeErrorDetail = false;
        adminBuilder.LogParameters = false;
        adminBuilder.NoResetOnClose = false;
        _adminDataSource =
            NpgsqlDataSource.Create(adminBuilder.ConnectionString);

        var suffix = Guid.NewGuid().ToString("N")[..12];
        DatabaseName = $"ct_p1_t07_{suffix}";
        MigratorRole = $"ct_p1_t07_m_{suffix}";
        _migratorPassword = RandomSecret();
        _runtimePassword = RandomSecret();

        await using (var admin =
            await _adminDataSource.OpenConnectionAsync())
        {
            ServerVersion =
                (string?)await new NpgsqlCommand(
                        "SHOW server_version;",
                        admin)
                    .ExecuteScalarAsync();
            if (!string.Equals(
                    ServerVersion,
                    "16.14",
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "P1-T07 requires the approved PostgreSQL 16.14 image.");
            }

            await ExecuteFormattedDdlAsync(
                admin,
                """
                CREATE ROLE %I WITH
                    LOGIN PASSWORD %L
                    NOSUPERUSER NOCREATEDB NOCREATEROLE
                    NOINHERIT NOREPLICATION NOBYPASSRLS
                """,
                MigratorRole,
                _migratorPassword);
            await ExecuteFormattedDdlAsync(
                admin,
                """
                CREATE ROLE %I WITH
                    LOGIN PASSWORD %L
                    NOSUPERUSER NOCREATEDB NOCREATEROLE
                    NOINHERIT NOREPLICATION NOBYPASSRLS
                """,
                RuntimeRole,
                _runtimePassword);
            await ExecuteFormattedDdlAsync(
                admin,
                """
                CREATE DATABASE %I
                    OWNER %I
                    TEMPLATE template0
                    ENCODING 'UTF8'
                """,
                DatabaseName,
                MigratorRole);
        }

        MigrationDataSource = NpgsqlDataSource.Create(
            BuildDatabaseConnectionString(
                adminBuilder,
                MigratorRole,
                _migratorPassword));
        RuntimeConnectionString = BuildDatabaseConnectionString(
            adminBuilder,
            RuntimeRole,
            _runtimePassword);
        RuntimeDataSource =
            NpgsqlDataSource.Create(RuntimeConnectionString);

        var forward = ReadMigration("0001_event_kernel.sql");
        var verify = ReadMigration("0001_event_kernel.verify.sql");
        var rollback = ReadMigration("0001_event_kernel.down.sql");

        await ExecuteMigrationAsync(
            MigrationDataSource,
            forward);
        await ExecuteMigrationAsync(
            MigrationDataSource,
            verify);
        var firstFingerprint =
            await CaptureCatalogFingerprintAsync();

        await using (var migrator =
            await MigrationDataSource.OpenConnectionAsync())
        {
            await using var guard = new NpgsqlCommand(
                """
                SELECT set_config(
                    'control_tower.ephemeral_migration_guard',
                    @guard,
                    false);
                """,
                migrator);
            guard.Parameters.AddWithValue("guard", Sentinel);
            _ = await guard.ExecuteScalarAsync();
            await using var down = new NpgsqlCommand(
                rollback,
                migrator)
            {
                CommandTimeout = 60,
            };
            _ = await down.ExecuteNonQueryAsync();

            await using var absence = new NpgsqlCommand(
                """
                SELECT to_regnamespace('event_store') IS NULL;
                """,
                migrator);
            if (!Equals(
                    await absence.ExecuteScalarAsync(),
                    true))
            {
                throw new InvalidOperationException(
                    "Migration 0001 rollback left schema objects behind.");
            }
        }

        await ExecuteMigrationAsync(
            MigrationDataSource,
            forward);
        await ExecuteMigrationAsync(
            MigrationDataSource,
            verify);
        var secondFingerprint =
            await CaptureCatalogFingerprintAsync();
        if (!string.Equals(
                firstFingerprint,
                secondFingerprint,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Migration 0001 schema drifted after rollback and re-apply.");
        }

        MigrationCycleVerified = true;
        Enabled = true;
    }

    public async Task DisposeAsync()
    {
        if (RuntimeDataSource is not null)
            await RuntimeDataSource.DisposeAsync();
        if (MigrationDataSource is not null)
            await MigrationDataSource.DisposeAsync();

        if (_adminDataSource is null)
            return;

        try
        {
            await using var admin =
                await _adminDataSource.OpenConnectionAsync();
            if (DatabaseName.Length > 0)
            {
                await using (var terminate = new NpgsqlCommand(
                    """
                    SELECT pg_terminate_backend(pid)
                    FROM pg_stat_activity
                    WHERE datname = @database_name
                      AND pid <> pg_backend_pid();
                    """,
                    admin))
                {
                    terminate.Parameters.AddWithValue(
                        "database_name",
                        DatabaseName);
                    _ = await terminate.ExecuteNonQueryAsync();
                }
                await ExecuteFormattedDdlAsync(
                    admin,
                    "DROP DATABASE IF EXISTS %I;",
                    DatabaseName);
            }

            await ExecuteFormattedDdlAsync(
                admin,
                "DROP ROLE IF EXISTS %I;",
                RuntimeRole);
            if (MigratorRole.Length > 0)
            {
                await ExecuteFormattedDdlAsync(
                    admin,
                    "DROP ROLE IF EXISTS %I;",
                    MigratorRole);
            }
        }
        finally
        {
            await _adminDataSource.DisposeAsync();
        }
    }

    private static void ValidateLoopbackAdmin(
        NpgsqlConnectionStringBuilder builder)
    {
        var allowedHosts = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase)
        {
            "localhost",
            "127.0.0.1",
            "::1",
        };
        if (string.IsNullOrWhiteSpace(builder.Host)
            || builder.Host.Contains(',')
            || !allowedHosts.Contains(builder.Host)
            || !string.Equals(
                builder.Database,
                "postgres",
                StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(builder.Username)
            || string.IsNullOrEmpty(builder.Password))
        {
            throw new InvalidOperationException(
                "P1-T07 database tests refuse non-loopback or non-ephemeral admin connections.");
        }
    }

    private string BuildDatabaseConnectionString(
        NpgsqlConnectionStringBuilder admin,
        string username,
        string password)
    {
        var builder = new NpgsqlConnectionStringBuilder(
            admin.ConnectionString)
        {
            Database = DatabaseName,
            Username = username,
            Password = password,
            IncludeErrorDetail = false,
            LogParameters = false,
            NoResetOnClose = false,
            Pooling = true,
            MaxPoolSize = 40,
            ApplicationName = "ControlTower-P1-T07-Ephemeral",
        };
        return builder.ConnectionString;
    }

    private static async Task ExecuteFormattedDdlAsync(
        NpgsqlConnection connection,
        string format,
        params string[] values)
    {
        var placeholders = string.Join(
            ", ",
            values.Select((_, index) => $"@value{index}"));
        await using var formatCommand = new NpgsqlCommand(
            $"SELECT format(@format, {placeholders});",
            connection);
        formatCommand.Parameters.AddWithValue("format", format);
        for (var index = 0; index < values.Length; index++)
        {
            formatCommand.Parameters.AddWithValue(
                $"value{index}",
                values[index]);
        }
        var sql =
            (string?)await formatCommand.ExecuteScalarAsync()
            ?? throw new InvalidOperationException(
                "PostgreSQL did not format the ephemeral DDL.");
        await using var command = new NpgsqlCommand(sql, connection)
        {
            CommandTimeout = 60,
        };
        _ = await command.ExecuteNonQueryAsync();
    }

    private static async Task ExecuteMigrationAsync(
        NpgsqlDataSource dataSource,
        string sql)
    {
        await using var connection =
            await dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(sql, connection)
        {
            CommandTimeout = 60,
        };
        _ = await command.ExecuteNonQueryAsync();
    }

    private async Task<string> CaptureCatalogFingerprintAsync()
    {
        const string catalogQuery =
            """
            WITH catalog_rows AS (
                SELECT
                    'column|' || table_name || '|' ||
                    lpad(ordinal_position::text, 4, '0') || '|' ||
                    column_name || '|' || data_type || '|' ||
                    udt_name || '|' || is_nullable || '|' ||
                    coalesce(character_maximum_length::text, '') AS value
                FROM information_schema.columns
                WHERE table_schema = 'event_store'

                UNION ALL

                SELECT
                    'constraint|' || relation.relname || '|' ||
                    constraint_record.conname || '|' ||
                    pg_get_constraintdef(
                        constraint_record.oid,
                        true)
                FROM pg_constraint AS constraint_record
                INNER JOIN pg_class AS relation
                    ON relation.oid = constraint_record.conrelid
                INNER JOIN pg_namespace AS namespace
                    ON namespace.oid = relation.relnamespace
                WHERE namespace.nspname = 'event_store'

                UNION ALL

                SELECT
                    'relation|' || relation.relname || '|' ||
                    relation.relkind::text || '|' ||
                    relation.relrowsecurity::text || '|' ||
                    relation.relforcerowsecurity::text || '|' ||
                    coalesce(relation.relacl::text, '')
                FROM pg_class AS relation
                INNER JOIN pg_namespace AS namespace
                    ON namespace.oid = relation.relnamespace
                WHERE namespace.nspname = 'event_store'
                  AND relation.relkind IN ('r', 'i')

                UNION ALL

                SELECT
                    'policy|' || tablename || '|' ||
                    policyname || '|' || cmd || '|' ||
                    coalesce(qual, '') || '|' ||
                    coalesce(with_check, '')
                FROM pg_policies
                WHERE schemaname = 'event_store'

                UNION ALL

                SELECT
                    'trigger|' || relation.relname || '|' ||
                    pg_get_triggerdef(trigger.oid, true)
                FROM pg_trigger AS trigger
                INNER JOIN pg_class AS relation
                    ON relation.oid = trigger.tgrelid
                INNER JOIN pg_namespace AS namespace
                    ON namespace.oid = relation.relnamespace
                WHERE namespace.nspname = 'event_store'
                  AND NOT trigger.tgisinternal

                UNION ALL

                SELECT
                    'function|' || procedure.proname || '|' ||
                    pg_get_function_identity_arguments(procedure.oid) ||
                    '|' || procedure.prosecdef::text || '|' ||
                    coalesce(procedure.proconfig::text, '') || '|' ||
                    coalesce(procedure.proacl::text, '') || '|' ||
                    pg_get_functiondef(procedure.oid)
                FROM pg_proc AS procedure
                INNER JOIN pg_namespace AS namespace
                    ON namespace.oid = procedure.pronamespace
                WHERE namespace.nspname = 'event_store'
            )
            SELECT string_agg(value, E'\n' ORDER BY value)
            FROM catalog_rows;
            """;

        await using var connection =
            await MigrationDataSource.OpenConnectionAsync();
        await using var command =
            new NpgsqlCommand(catalogQuery, connection);
        return
            (string?)await command.ExecuteScalarAsync()
            ?? throw new InvalidOperationException(
                "The event-kernel catalog fingerprint is empty.");
    }

    private static string ReadMigration(string name)
    {
        for (var directory =
                new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            var path = Path.Combine(
                directory.FullName,
                "db",
                "migrations",
                name);
            if (File.Exists(path))
                return File.ReadAllText(path);
        }

        throw new FileNotFoundException(
            $"Could not locate checked-in migration {name}.");
    }

    private static string RandomSecret() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
}
