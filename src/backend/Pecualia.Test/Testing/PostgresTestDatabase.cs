using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using Pecualia.Api.Data;

namespace Pecualia.Test.Testing;

public sealed class PostgresFactAttribute : FactAttribute
{
    public const string ConnectionVariable = "PECUALIA_TEST_POSTGRES";

    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ConnectionVariable)))
        {
            Skip = $"Set {ConnectionVariable} to a local PostgreSQL test database.";
        }
    }
}

// Each test owns a new schema. Existing tables and data are never used or removed.
public sealed class PostgresTestDatabase : IAsyncDisposable
{
    private readonly string _schema = $"movement_test_{Guid.NewGuid():N}";
    private readonly string _adminConnectionString = Environment.GetEnvironmentVariable(PostgresFactAttribute.ConnectionVariable)!;

    public string ConnectionString => new NpgsqlConnectionStringBuilder(_adminConnectionString)
    {
        SearchPath = _schema
    }.ConnectionString;

    public async Task InitializeAsync(bool useCurrentModel = false)
    {
        await using var connection = new NpgsqlConnection(_adminConnectionString);
        await connection.OpenAsync();
        await using var create = new NpgsqlCommand($"CREATE SCHEMA {_schema}", connection);
        await create.ExecuteNonQueryAsync();

        await using var schemaConnection = new NpgsqlConnection(ConnectionString);
        await schemaConnection.OpenAsync();
        await using var context = CreateContext();
        var sql = useCurrentModel
            ? context.Database.GenerateCreateScript()
            : await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "db", "init", "001_schema.sql"));
        await using var initialize = new NpgsqlCommand(sql, schemaConnection);
        await initialize.ExecuteNonQueryAsync();
    }

    public PecualiaDbContext CreateContext(params IInterceptor[] interceptors) => new(
        new DbContextOptionsBuilder<PecualiaDbContext>()
            .UseNpgsql(ConnectionString)
            .AddInterceptors(interceptors)
            .Options);

    public async ValueTask DisposeAsync()
    {
        await using var connection = new NpgsqlConnection(_adminConnectionString);
        await connection.OpenAsync();
        await using var drop = new NpgsqlCommand($"DROP SCHEMA IF EXISTS {_schema} CASCADE", connection);
        await drop.ExecuteNonQueryAsync();
    }
}
