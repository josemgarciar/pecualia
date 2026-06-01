using Npgsql;
using Microsoft.Extensions.Options;
using Pecualia.Api.Configuration;
using System.Data;

namespace Pecualia.Api.Services;

public interface IDatabaseBootstrapper
{
    Task BootstrapAsync(CancellationToken cancellationToken);
}

public sealed class DatabaseBootstrapper(
    IConfiguration configuration,
    IHostEnvironment environment,
    IOptions<DatabaseBootstrapOptions> options,
    ILogger<DatabaseBootstrapper> logger) : IDatabaseBootstrapper
{
    private const long AdvisoryLockKey = 482_001_337;
    private const string MigrationsTableName = "_pecualia_sql_migrations";

    public async Task BootstrapAsync(CancellationToken cancellationToken)
    {
        if (!options.Value.BootstrapOnStartup)
        {
            logger.LogInformation("Database bootstrap disabled. Skipping startup SQL initialization.");
            return;
        }

        var connectionString = PostgresConnectionStringResolver.RequireNormalized(configuration);

        var dbDirectory = ResolveDbDirectory(environment.ContentRootPath);
        var initScripts = BuildInitScripts(dbDirectory);
        var migrationScripts = BuildMigrationScripts(dbDirectory);
        var allScripts = initScripts.Concat(migrationScripts).ToList();
        var executableScripts = BuildExecutableScripts(dbDirectory, options.Value.SeedDemoData);

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await AcquireAdvisoryLockAsync(connection, cancellationToken);
        try
        {
            await EnsureMigrationsTableAsync(connection, cancellationToken);

            var schemaExists = await TableExistsAsync(connection, "app_user", cancellationToken);
            var appliedScripts = await LoadAppliedScriptsAsync(connection, cancellationToken);

            if (schemaExists && appliedScripts.Count == 0)
            {
                logger.LogWarning(
                    "Detected an existing schema without migration tracking. Recording init scripts as baseline and applying pending migrations.");
                await RecordBaselineAsync(connection, initScripts, cancellationToken);
                appliedScripts = await LoadAppliedScriptsAsync(connection, cancellationToken);
            }

            foreach (var script in executableScripts)
            {
                if (appliedScripts.Contains(script.Id))
                {
                    continue;
                }

                if (script.Id == "001_schema.sql" && schemaExists)
                {
                    await RecordAppliedScriptAsync(connection, script.Id, cancellationToken);
                    continue;
                }

                logger.LogInformation("Applying SQL script {ScriptId}.", script.Id);
                var sql = await File.ReadAllTextAsync(script.Path, cancellationToken);
                await using var command = new NpgsqlCommand(sql, connection);
                await command.ExecuteNonQueryAsync(cancellationToken);
                await RecordAppliedScriptAsync(connection, script.Id, cancellationToken);
            }
        }
        catch
        {
            await RollbackIfNeededAsync(connection, cancellationToken);
            throw;
        }
        finally
        {
            await ReleaseAdvisoryLockAsync(connection, cancellationToken);
        }
    }

    private static IReadOnlyList<SqlScript> BuildInitScripts(string dbDirectory)
    {
        return new List<SqlScript>
        {
            new("001_schema.sql", Path.Combine(dbDirectory, "init", "001_schema.sql")),
            new("002_seed_demo.sql", Path.Combine(dbDirectory, "init", "002_seed_demo.sql"))
        };
    }

    private static IReadOnlyList<SqlScript> BuildMigrationScripts(string dbDirectory)
    {
        return Directory.GetFiles(Path.Combine(dbDirectory, "migrations"), "*.sql")
            .OrderBy(Path.GetFileName, StringComparer.Ordinal)
            .Select(path => new SqlScript(Path.GetFileName(path), path))
            .ToList();
    }

    private static IReadOnlyList<SqlScript> BuildAllKnownScripts(string dbDirectory)
    {
        return BuildInitScripts(dbDirectory)
            .Concat(BuildMigrationScripts(dbDirectory))
            .ToList();
    }

    private static IReadOnlyList<SqlScript> BuildExecutableScripts(string dbDirectory, bool seedDemoData)
    {
        return BuildAllKnownScripts(dbDirectory)
            .Where(script => seedDemoData || !IsSeedScript(script.Id))
            .ToList();
    }

    private static bool IsSeedScript(string scriptId)
    {
        return scriptId.Equals("002_seed_demo.sql", StringComparison.OrdinalIgnoreCase) ||
            scriptId.Contains("seed_demo", StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveDbDirectory(string contentRootPath)
    {
        var directory = new DirectoryInfo(contentRootPath);

        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "db", "init", "001_schema.sql");
            if (File.Exists(candidate))
            {
                return Path.Combine(directory.FullName, "db");
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("No se ha podido localizar la carpeta db/ con los scripts SQL necesarios para el despliegue.");
    }

    private static async Task<bool> TableExistsAsync(NpgsqlConnection connection, string tableName, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT EXISTS (
                SELECT 1
                FROM information_schema.tables
                WHERE table_schema = 'public'
                  AND table_name = @tableName
            );
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("tableName", tableName);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is true;
    }

    private static async Task EnsureMigrationsTableAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        var sql = $"""
            CREATE TABLE IF NOT EXISTS {MigrationsTableName} (
                script_id VARCHAR(255) PRIMARY KEY,
                applied_at TIMESTAMPTZ NOT NULL DEFAULT now()
            );
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<HashSet<string>> LoadAppliedScriptsAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        var scripts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sql = $"SELECT script_id FROM {MigrationsTableName};";

        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            scripts.Add(reader.GetString(0));
        }

        return scripts;
    }

    private static async Task RecordBaselineAsync(
        NpgsqlConnection connection,
        IReadOnlyList<SqlScript> allScripts,
        CancellationToken cancellationToken)
    {
        foreach (var script in allScripts)
        {
            await RecordAppliedScriptAsync(connection, script.Id, cancellationToken);
        }
    }

    private static async Task RecordAppliedScriptAsync(NpgsqlConnection connection, string scriptId, CancellationToken cancellationToken)
    {
        var sql = $"""
            INSERT INTO {MigrationsTableName} (script_id)
            VALUES (@scriptId)
            ON CONFLICT (script_id) DO NOTHING;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("scriptId", scriptId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task AcquireAdvisoryLockAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("SELECT pg_advisory_lock(@lockKey);", connection);
        command.Parameters.AddWithValue("lockKey", AdvisoryLockKey);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task ReleaseAdvisoryLockAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("SELECT pg_advisory_unlock(@lockKey);", connection);
        command.Parameters.AddWithValue("lockKey", AdvisoryLockKey);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task RollbackIfNeededAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        if ((connection.FullState & ConnectionState.Open) == 0)
        {
            return;
        }

        await using var command = new NpgsqlCommand("ROLLBACK;", connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private sealed record SqlScript(string Id, string Path);
}
