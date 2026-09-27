using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;
using topg.Web.Templating.Data;

namespace topg.Web.IntegrationTests;

/// <summary>
/// A throw-away PostgreSQL with the app's migrations applied. Needs Docker; without it the tests are skipped
/// (<see cref="DockerUnavailableReason"/>) instead of failing.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _container;

    public string ConnectionString => _container?.GetConnectionString() ?? throw new InvalidOperationException("Not started.");

    public string? DockerUnavailableReason { get; private set; }

    public async Task InitializeAsync()
    {
        try
        {
            _container = new PostgreSqlBuilder("postgres:17-alpine").Build();
            await _container.StartAsync();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            var causes = new List<string>();
            for (var e = ex; e is not null; e = e.InnerException)
            {
                causes.Add(e.Message);
            }

            DockerUnavailableReason = $"Docker is not available: {string.Join(" → ", causes)}";
            return;
        }

        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public QuizContext CreateContext() =>
        new(new DbContextOptionsBuilder<QuizContext>().UseNpgsql(ConnectionString).Options);

    /// <summary>Runs a script like psql would (no EF placeholder processing of braces).</summary>
    public async Task ExecuteScriptAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>Removes all quiz data between tests.</summary>
    public Task ResetAsync() => ExecuteScriptAsync("""TRUNCATE "Questions", "Boards", "Templates" RESTART IDENTITY CASCADE;""");

    public async Task DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }
}

[CollectionDefinition(Name)]
public class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "postgres";
}
