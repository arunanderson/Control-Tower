namespace ControlTower.Adapters.PostgreSql.Tests;

public sealed class PostgreSqlWorkflowGuardTests
{
    [Fact]
    public void Build_workflow_pins_the_approved_ephemeral_database()
    {
        var workflow = ReadRepositoryFile(
            ".github",
            "workflows",
            "build-test.yml");
        Assert.Contains(
            "postgres:16.14-alpine3.24",
            workflow,
            StringComparison.Ordinal);
        Assert.Contains(
            "CONTROL_TOWER_POSTGRES_EPHEMERAL",
            workflow,
            StringComparison.Ordinal);
        Assert.Contains(
            "CONTROL_TOWER_POSTGRES_ADMIN",
            workflow,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "POSTGRES_HOST_AUTH_METHOD",
            workflow,
            StringComparison.Ordinal);
    }

    private static string ReadRepositoryFile(
        params string[] relativePath)
    {
        for (var directory =
                new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            var path = relativePath.Aggregate(
                directory.FullName,
                Path.Combine);
            if (File.Exists(path))
                return File.ReadAllText(path);
        }

        throw new FileNotFoundException(
            "Could not locate repository build workflow.");
    }
}
