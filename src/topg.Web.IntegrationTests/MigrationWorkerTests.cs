using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using topg.MigrationService;
using topg.Web.Client.Creator.Export;
using topg.Web.Templating.Data;
using DomainTextQuestion = topg.Web.Templating.DomainObjects.TextQuestion;

namespace topg.Web.IntegrationTests;

[Collection(PostgresCollection.Name)]
public class MigrationWorkerTests(PostgresFixture db) : IAsyncLifetime
{
    private const string MigrationBeforeLatest = "20260529090815_AddedImageQuestions";

    public Task InitializeAsync() => db.DockerUnavailableReason is null ? db.ResetAsync() : Task.CompletedTask;

    public Task DisposeAsync() => Task.CompletedTask;

    [SkippableFact]
    public async Task Running_the_worker_keeps_imported_quizzes_unchanged()
    {
        Skip.If(db.DockerUnavailableReason is not null, db.DockerUnavailableReason);
        await db.ExecuteScriptAsync(SqlExport.Generate(SampleProjects.Read("pop-culture-history.topgquiz"), DateTimeOffset.UtcNow));
        await db.ExecuteScriptAsync(SqlExport.Generate(SampleProjects.Read("science-nature.topgquiz"), DateTimeOffset.UtcNow));
        var before = await SnapshotAsync(db.ConnectionString);

        await RunWorkerAsync(db.ConnectionString);

        Assert.Equal(before, await SnapshotAsync(db.ConnectionString));
        Assert.Equal(2, before.Count(r => r.StartsWith("Templates ")));
        Assert.Equal(120, before.Count(r => r.StartsWith("Questions ")));
    }

    [SkippableFact]
    public async Task Pending_migration_is_applied_without_deleting_quizzes()
    {
        Skip.If(db.DockerUnavailableReason is not null, db.DockerUnavailableReason);
        var connectionString = await CreateDatabaseAsync("worker_pending");
        await using (var context = CreateContext(connectionString))
        {
            await context.Database.MigrateAsync(MigrationBeforeLatest);
        }

        await ExecuteAsync(connectionString, """
            INSERT INTO "Templates" ("Name") VALUES ('Imported quiz');
            INSERT INTO "Boards" ("Order", "TemplateId") SELECT 0, "Id" FROM "Templates";
            INSERT INTO "Questions" ("AnswerType", "BoardId", "Category", "Points", "QuestionType", "TextQuestion_QuestionText", "TextQuestion_CorrectAnswer")
            SELECT 0, "Id", 'Physics', 100, 0, 'What is the unit of force?', 'Newton' FROM "Boards";
            """);

        await RunWorkerAsync(connectionString);

        await using var migrated = CreateContext(connectionString);
        Assert.Empty(await migrated.Database.GetPendingMigrationsAsync());
        var template = await migrated.Templates.Include(t => t.Boards).ThenInclude(b => b.Questions).SingleAsync();
        Assert.Equal("Imported quiz", template.Name);
        // Only the fields the latest migration doesn't rename are compared: it moves TextQuestion_CorrectAnswer to
        // QuestionImageUri, so the answer of a text question stored before it doesn't survive in CorrectAnswer.
        var question = Assert.IsType<DomainTextQuestion>(Assert.Single(Assert.Single(template.Boards).Questions));
        Assert.Equal(("Physics", 100, "What is the unit of force?"), (question.Category, question.Points, question.QuestionText));
    }

    [SkippableFact]
    public async Task Empty_database_gets_the_schema_and_no_quizzes()
    {
        Skip.If(db.DockerUnavailableReason is not null, db.DockerUnavailableReason);
        var connectionString = await CreateDatabaseAsync("worker_empty");

        await RunWorkerAsync(connectionString);

        await using var context = CreateContext(connectionString);
        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
        Assert.Equal(0, await context.Templates.CountAsync());
        Assert.Equal(0, await context.Questions.CountAsync());
    }

    private static async Task RunWorkerAsync(string connectionString)
    {
        await using var services = new ServiceCollection()
            .AddDbContext<QuizContext>(o => o.UseNpgsql(connectionString))
            .BuildServiceProvider();
        var lifetime = new RecordingLifetime();
        using var worker = new Worker(services, lifetime);

        await worker.StartAsync(CancellationToken.None);
        await worker.ExecuteTask!;

        Assert.True(lifetime.StopRequested);
    }

    private static QuizContext CreateContext(string connectionString) =>
        new(new DbContextOptionsBuilder<QuizContext>().UseNpgsql(connectionString).Options);

    private async Task<string> CreateDatabaseAsync(string name)
    {
        await db.ExecuteScriptAsync($"""DROP DATABASE IF EXISTS "{name}" WITH (FORCE); CREATE DATABASE "{name}";""");
        return new NpgsqlConnectionStringBuilder(db.ConnectionString) { Database = name }.ConnectionString;
    }

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<List<string>> SnapshotAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        var rows = new List<string>();
        foreach (var table in new[] { "Templates", "Boards", "Questions" })
        {
            await using var command = new NpgsqlCommand($"""SELECT row_to_json(t)::text FROM "{table}" t ORDER BY "Id" """, connection);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                rows.Add($"{table} {reader.GetString(0)}");
            }
        }

        return rows;
    }

    private sealed class RecordingLifetime : IHostApplicationLifetime
    {
        public bool StopRequested { get; private set; }
        public CancellationToken ApplicationStarted => CancellationToken.None;
        public CancellationToken ApplicationStopping => CancellationToken.None;
        public CancellationToken ApplicationStopped => CancellationToken.None;
        public void StopApplication() => StopRequested = true;
    }
}
