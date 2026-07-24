using System.Security.Cryptography;
using Npgsql;

namespace ControlTower.Adapters.PostgreSql.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PostgreSqlTrustStoreCollection
    : ICollectionFixture<PostgreSqlTrustStoreFixture>
{
    public const string Name =
        "PostgreSQL C8 trust store";
}

public sealed class PostgreSqlTrustStoreFixture
    : IAsyncLifetime
{
    internal const string RollbackSentinel =
        "P1-T08-EPHEMERAL-ONLY";
    internal const string NormalRole =
        "control_tower_runtime";
    internal const string PrivilegedRole =
        "control_tower_privileged_runtime";
    private NpgsqlDataSource? _adminDataSource;
    private string? _migratorPassword;
    private string? _normalPassword;
    private string? _privilegedPassword;
    private bool _migratorRoleCreated;
    private bool _normalRoleCreated;
    private bool _privilegedRoleCreated;
    private bool _databaseCreated;

    public bool Enabled { get; private set; }
    public bool BaselineRestored { get; private set; }
    public bool MigrationCycleVerified { get; private set; }
    public string? ServerVersion { get; private set; }
    public string DatabaseName { get; private set; } =
        string.Empty;
    public string MigratorRole { get; private set; } =
        string.Empty;
    public NpgsqlDataSource MigrationDataSource { get; private set; } =
        null!;
    public NpgsqlDataSource NormalDataSource { get; private set; } =
        null!;
    public NpgsqlDataSource PrivilegedDataSource { get; private set; } =
        null!;
    public NpgsqlDataSource PrivilegedAuditDataSource { get; private set; } =
        null!;
    public string NormalConnectionString { get; private set; } =
        string.Empty;
    public string PrivilegedConnectionString { get; private set; } =
        string.Empty;

    public async Task InitializeAsync()
    {
        var marker = Environment.GetEnvironmentVariable(
            "CONTROL_TOWER_POSTGRES_EPHEMERAL");
        if (marker is null)
            return;
        if (!string.Equals(
                marker,
                PostgreSqlEventKernelFixture.Sentinel,
                StringComparison.Ordinal))
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
            new NpgsqlConnectionStringBuilder(
                adminConnectionString);
        ValidateLoopbackAdmin(adminBuilder);
        adminBuilder.IncludeErrorDetail = false;
        adminBuilder.LogParameters = false;
        adminBuilder.NoResetOnClose = false;
        _adminDataSource = NpgsqlDataSource.Create(
            adminBuilder.ConnectionString);

        var suffix = Guid.NewGuid().ToString("N")[..12];
        DatabaseName = $"ct_p1_t08_{suffix}";
        MigratorRole = $"ct_p1_t08_m_{suffix}";
        _migratorPassword = RandomSecret();
        _normalPassword = RandomSecret();
        _privilegedPassword = RandomSecret();

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
                    "P1-T08 requires the approved PostgreSQL 16.14 image.");
            }

            await using (var roleCollision =
                new NpgsqlCommand(
                    """
                    SELECT EXISTS (
                        SELECT 1
                        FROM pg_roles
                        WHERE rolname IN (
                            @normal_role,
                            @privileged_role));
                    """,
                    admin))
            {
                roleCollision.Parameters.AddWithValue(
                    "normal_role",
                    NormalRole);
                roleCollision.Parameters.AddWithValue(
                    "privileged_role",
                    PrivilegedRole);
                if (Equals(
                        await roleCollision.ExecuteScalarAsync(),
                        true))
                {
                    throw new InvalidOperationException(
                        "An ephemeral PostgreSQL runtime role already exists.");
                }
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
            _migratorRoleCreated = true;
            await ExecuteFormattedDdlAsync(
                admin,
                """
                CREATE ROLE %I WITH
                    LOGIN PASSWORD %L
                    NOSUPERUSER NOCREATEDB NOCREATEROLE
                    NOINHERIT NOREPLICATION NOBYPASSRLS
                """,
                NormalRole,
                _normalPassword);
            _normalRoleCreated = true;
            await ExecuteFormattedDdlAsync(
                admin,
                """
                CREATE ROLE %I WITH
                    LOGIN PASSWORD %L
                    NOSUPERUSER NOCREATEDB NOCREATEROLE
                    NOINHERIT NOREPLICATION NOBYPASSRLS
                """,
                PrivilegedRole,
                _privilegedPassword);
            _privilegedRoleCreated = true;
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
            _databaseCreated = true;
        }

        MigrationDataSource = NpgsqlDataSource.Create(
            BuildDatabaseConnectionString(
                adminBuilder,
                MigratorRole,
                _migratorPassword));
        NormalConnectionString =
            BuildDatabaseConnectionString(
                adminBuilder,
                NormalRole,
                _normalPassword);
        PrivilegedConnectionString =
            BuildDatabaseConnectionString(
                adminBuilder,
                PrivilegedRole,
                _privilegedPassword);
        NormalDataSource = NpgsqlDataSource.Create(
            NormalConnectionString);
        PrivilegedDataSource = NpgsqlDataSource.Create(
            PrivilegedConnectionString);
        PrivilegedAuditDataSource = NpgsqlDataSource.Create(
            PrivilegedConnectionString);

        var eventForward =
            ReadMigration("0001_event_kernel.sql");
        var eventVerify =
            ReadMigration("0001_event_kernel.verify.sql");
        var trustForward =
            ReadMigration(
                "0002_c8_identity_authorization.sql");
        var trustVerify =
            ReadMigration(
                "0002_c8_identity_authorization.verify.sql");
        var trustRollback =
            ReadMigration(
                "0002_c8_identity_authorization.down.sql");

        await ExecuteMigrationAsync(
            MigrationDataSource,
            eventForward);
        await ExecuteMigrationAsync(
            MigrationDataSource,
            eventVerify);
        var eventBaseline =
            await CaptureCatalogFingerprintAsync();

        await ExecuteMigrationAsync(
            MigrationDataSource,
            trustForward);
        await ExecuteMigrationAsync(
            MigrationDataSource,
            trustVerify);
        var firstCombined =
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
            guard.Parameters.AddWithValue(
                "guard",
                RollbackSentinel);
            _ = await guard.ExecuteScalarAsync();

            await using var down = new NpgsqlCommand(
                trustRollback,
                migrator)
            {
                CommandTimeout = 60,
            };
            _ = await down.ExecuteNonQueryAsync();

            await using var absence = new NpgsqlCommand(
                """
                SELECT
                    to_regnamespace('trust_store') IS NULL;
                """,
                migrator);
            if (!Equals(
                    await absence.ExecuteScalarAsync(),
                    true))
            {
                throw new InvalidOperationException(
                    "Migration 0002 rollback left trust objects behind.");
            }
        }

        await ExecuteMigrationAsync(
            MigrationDataSource,
            eventVerify);
        var postRollback =
            await CaptureCatalogFingerprintAsync();
        if (!string.Equals(
                eventBaseline,
                postRollback,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Migration 0002 rollback changed the migration 0001 baseline.");
        }
        BaselineRestored = true;

        await ExecuteMigrationAsync(
            MigrationDataSource,
            trustForward);
        await ExecuteMigrationAsync(
            MigrationDataSource,
            eventVerify);
        await ExecuteMigrationAsync(
            MigrationDataSource,
            trustVerify);
        var secondCombined =
            await CaptureCatalogFingerprintAsync();
        if (!string.Equals(
                firstCombined,
                secondCombined,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Migration 0002 schema drifted after rollback and re-apply.");
        }

        MigrationCycleVerified = true;
        Enabled = true;
    }

    public async Task DisposeAsync()
    {
        if (PrivilegedAuditDataSource is not null)
            await PrivilegedAuditDataSource.DisposeAsync();
        if (PrivilegedDataSource is not null)
            await PrivilegedDataSource.DisposeAsync();
        if (NormalDataSource is not null)
            await NormalDataSource.DisposeAsync();
        if (MigrationDataSource is not null)
            await MigrationDataSource.DisposeAsync();

        if (_adminDataSource is null)
            return;

        try
        {
            await using var admin =
                await _adminDataSource.OpenConnectionAsync();
            if (_databaseCreated
                && DatabaseName.Length > 0)
            {
                await using (var terminate =
                    new NpgsqlCommand(
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
                _databaseCreated = false;
            }

            if (_privilegedRoleCreated)
            {
                await ExecuteFormattedDdlAsync(
                    admin,
                    "DROP ROLE IF EXISTS %I;",
                    PrivilegedRole);
                _privilegedRoleCreated = false;
            }
            if (_normalRoleCreated)
            {
                await ExecuteFormattedDdlAsync(
                    admin,
                    "DROP ROLE IF EXISTS %I;",
                    NormalRole);
                _normalRoleCreated = false;
            }
            if (_migratorRoleCreated
                && MigratorRole.Length > 0)
            {
                await ExecuteFormattedDdlAsync(
                    admin,
                    "DROP ROLE IF EXISTS %I;",
                    MigratorRole);
                _migratorRoleCreated = false;
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
                "P1-T08 database tests refuse non-loopback or non-ephemeral admin connections.");
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
            MaxPoolSize = 80,
            ApplicationName =
                "ControlTower-P1-T08-Ephemeral",
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
            values.Select(
                (_, index) => $"@value{index}"));
        await using var formatCommand = new NpgsqlCommand(
            $"SELECT format(@format, {placeholders});",
            connection);
        formatCommand.Parameters.AddWithValue(
            "format",
            format);
        for (var index = 0;
             index < values.Length;
             index++)
        {
            formatCommand.Parameters.AddWithValue(
                $"value{index}",
                values[index]);
        }

        var sql =
            (string?)await formatCommand.ExecuteScalarAsync()
            ?? throw new InvalidOperationException(
                "PostgreSQL did not format the ephemeral DDL.");
        await using var command =
            new NpgsqlCommand(sql, connection)
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
        await using var command =
            new NpgsqlCommand(sql, connection)
            {
                CommandTimeout = 60,
            };
        _ = await command.ExecuteNonQueryAsync();
    }

    private async Task<string>
        CaptureCatalogFingerprintAsync()
    {
        const string catalogQuery =
            """
            WITH catalog_rows AS (
                SELECT
                    'schema|' || namespace.nspname || '|' ||
                    coalesce(namespace.nspacl::text, '') AS value
                FROM pg_namespace AS namespace
                WHERE namespace.nspname IN (
                    'event_store',
                    'trust_store')

                UNION ALL

                SELECT
                    'column|' || table_schema || '|' ||
                    table_name || '|' ||
                    lpad(
                        ordinal_position::text,
                        4,
                        '0') || '|' ||
                    column_name || '|' || data_type || '|' ||
                    udt_name || '|' || is_nullable || '|' ||
                    coalesce(
                        character_maximum_length::text,
                        '') AS value
                FROM information_schema.columns
                WHERE table_schema IN (
                    'event_store',
                    'trust_store')

                UNION ALL

                SELECT
                    'constraint|' || namespace.nspname || '|' ||
                    relation.relname || '|' ||
                    constraint_record.conname || '|' ||
                    pg_get_constraintdef(
                        constraint_record.oid,
                        true)
                FROM pg_constraint AS constraint_record
                INNER JOIN pg_class AS relation
                    ON relation.oid =
                        constraint_record.conrelid
                INNER JOIN pg_namespace AS namespace
                    ON namespace.oid =
                        relation.relnamespace
                WHERE namespace.nspname IN (
                    'event_store',
                    'trust_store')

                UNION ALL

                SELECT
                    'relation|' || namespace.nspname || '|' ||
                    relation.relname || '|' ||
                    relation.relkind::text || '|' ||
                    relation.relrowsecurity::text || '|' ||
                    relation.relforcerowsecurity::text || '|' ||
                    coalesce(relation.relacl::text, '') || '|' ||
                    coalesce(
                        pg_get_indexdef(relation.oid),
                        '') || '|' ||
                    coalesce(
                        index_record.indisunique::text,
                        '') || '|' ||
                    coalesce(
                        index_record.indisprimary::text,
                        '') || '|' ||
                    coalesce(
                        index_record.indisvalid::text,
                        '') || '|' ||
                    coalesce(
                        index_record.indisready::text,
                        '')
                FROM pg_class AS relation
                INNER JOIN pg_namespace AS namespace
                    ON namespace.oid =
                        relation.relnamespace
                LEFT JOIN pg_index AS index_record
                    ON index_record.indexrelid =
                        relation.oid
                WHERE namespace.nspname IN (
                    'event_store',
                    'trust_store')
                  AND relation.relkind IN ('r', 'i')

                UNION ALL

                SELECT
                    'policy|' || schemaname || '|' ||
                    tablename || '|' || policyname || '|' ||
                    permissive || '|' ||
                    array_to_string(roles, ',') || '|' ||
                    cmd || '|' || coalesce(qual, '') || '|' ||
                    coalesce(with_check, '')
                FROM pg_policies
                WHERE schemaname IN (
                    'event_store',
                    'trust_store')

                UNION ALL

                SELECT
                    'trigger|' || namespace.nspname || '|' ||
                    relation.relname || '|' ||
                    trigger.tgenabled::text || '|' ||
                    pg_get_triggerdef(trigger.oid, true)
                FROM pg_trigger AS trigger
                INNER JOIN pg_class AS relation
                    ON relation.oid = trigger.tgrelid
                INNER JOIN pg_namespace AS namespace
                    ON namespace.oid =
                        relation.relnamespace
                WHERE namespace.nspname IN (
                    'event_store',
                    'trust_store')
                  AND NOT trigger.tgisinternal

                UNION ALL

                SELECT
                    'function|' || namespace.nspname || '|' ||
                    procedure.proname || '|' ||
                    pg_get_function_identity_arguments(
                        procedure.oid) || '|' ||
                    procedure.prosecdef::text || '|' ||
                    coalesce(
                        procedure.proconfig::text,
                        '') || '|' ||
                    coalesce(
                        procedure.proacl::text,
                        '') || '|' ||
                    pg_get_functiondef(procedure.oid)
                FROM pg_proc AS procedure
                INNER JOIN pg_namespace AS namespace
                    ON namespace.oid =
                        procedure.pronamespace
                WHERE namespace.nspname IN (
                    'event_store',
                    'trust_store')
            )
            SELECT string_agg(
                value,
                E'\n'
                ORDER BY value)
            FROM catalog_rows;
            """;

        await using var connection =
            await MigrationDataSource.OpenConnectionAsync();
        await using var command =
            new NpgsqlCommand(catalogQuery, connection);
        return
            (string?)await command.ExecuteScalarAsync()
            ?? throw new InvalidOperationException(
                "The C8 catalog fingerprint is empty.");
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
        Convert.ToBase64String(
            RandomNumberGenerator.GetBytes(32));
}
