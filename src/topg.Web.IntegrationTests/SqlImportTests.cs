using Microsoft.EntityFrameworkCore;
using topg.Web.Client.Creator.Export;
using topg.Web.Client.Creator.Model;
using topg.Web.Client.Shared;
using topg.Web.Quiz.Execution;
using topg.Web.Templating;
using DomainImageQuestion = topg.Web.Templating.DomainObjects.ImageQuestion;
using DomainTextQuestion = topg.Web.Templating.DomainObjects.TextQuestion;

namespace topg.Web.IntegrationTests;

/// <summary>
/// Runs the generated import.sql against the real schema (EF migrations), read back the way the game reads it.
/// Catches wrong column names, relative image URIs and enum mismatches.
/// </summary>
[Collection(PostgresCollection.Name)]
public class SqlImportTests(PostgresFixture db) : IAsyncLifetime
{
    private static readonly DateTimeOffset GeneratedAt = new(2026, 9, 27, 12, 0, 0, TimeSpan.Zero);

    private static ImageRef Image(string hash) => new(hash, "png", "image/png", $"{hash}.png");

    private static QuizProject Sample(bool replace = true)
    {
        var project = QuizProject.Create("Pub Quiz – O'Brien's", "https://cdn.example.com/quiz/");
        project.Folder = "pub-quiz";
        project.ReplaceExisting = replace;
        project.Boards.Add(new BoardDraft
        {
            Categories =
            [
                new CategoryDraft
                {
                    Name = "Zürich",
                    Questions =
                    [
                        new TextQuestionDraft { Points = 200, QuestionText = "Line 1\nLine 2 – ünïcödé 🎉", CorrectAnswer = "C:\\path 'quoted'" },
                        new TextQuestionDraft { Points = 100, QuestionText = "Q1", CorrectAnswer = "A1", AnswerType = AnswerType.Text },
                    ],
                },
                new CategoryDraft
                {
                    Name = "Animals",
                    Questions =
                    [
                        new ImageQuestionDraft
                        {
                            Points = 100, QuestionText = "Which flag?", QuestionImage = Image("0123456789abcdef"), AnswerText = "CH",
                            AnswerImage = Image("fedcba9876543210"), ImageSize = ImageSize.Large,
                        },
                        new ImageQuestionDraft { Points = 200, QuestionText = "No answer image", QuestionImage = Image("0123456789abcdef") },
                    ],
                },
            ],
        });
        project.Boards.Add(new BoardDraft
        {
            Categories = [new CategoryDraft { Name = "Second board", Questions = [new TextQuestionDraft { Points = 500, QuestionText = "Q", CorrectAnswer = "A" }] }],
        });
        return project;
    }

    public Task InitializeAsync() => db.DockerUnavailableReason is null ? db.ResetAsync() : Task.CompletedTask;

    public Task DisposeAsync() => Task.CompletedTask;

    [SkippableFact]
    public async Task Imported_quiz_loads_like_the_game_loads_it()
    {
        Skip.If(db.DockerUnavailableReason is not null, db.DockerUnavailableReason);
        var project = Sample();

        await db.ExecuteScriptAsync(SqlExport.Generate(project, GeneratedAt));

        await using var context = db.CreateContext();
        var template = Assert.Single(await new TemplateService(context).GetAllTemplatesAsync());
        Assert.Equal("Pub Quiz – O'Brien's", template.Name);
        Assert.Equal([0, 1], template.Boards.Select(b => b.Order).Order());

        var board1 = template.Boards.Single(b => b.Order == 0);
        Assert.Equal(4, board1.Questions.Count);

        var text = board1.Questions.OfType<DomainTextQuestion>().Single(q => q.Points == 200);
        Assert.Equal("Zürich", text.Category);
        Assert.Equal("Line 1\nLine 2 – ünïcödé 🎉", text.QuestionText);
        Assert.Equal("C:\\path 'quoted'", text.CorrectAnswer);
        Assert.Equal(AnswerType.Text, board1.Questions.OfType<DomainTextQuestion>().Single(q => q.Points == 100).AnswerType);

        var image = board1.Questions.OfType<DomainImageQuestion>().Single(q => q.Points == 100);
        Assert.Equal("https://cdn.example.com/quiz/pub-quiz/0123456789abcdef.png", image.QuestionImageUri);
        Assert.Equal("https://cdn.example.com/quiz/pub-quiz/fedcba9876543210.png", image.AnswerImageUri);
        Assert.Equal(ImageSize.Large, image.ImageSize);
        Assert.Equal("", board1.Questions.OfType<DomainImageQuestion>().Single(q => q.Points == 200).AnswerImageUri);

        // The game converts the data on hosting; this throws for relative URIs or unknown types.
        var execution = new QuizExecution(template);
        var imageQuestion = execution.CurrentBoard.Questions.OfType<Quiz.Execution.ImageQuestion>().Single(q => q.Points == 200);
        Assert.Null(imageQuestion.AnswerImageUri);
        Assert.True(execution.HasNextBoard);
    }

    [SkippableFact]
    public async Task Replace_mode_twice_leaves_exactly_one_template_without_orphans()
    {
        Skip.If(db.DockerUnavailableReason is not null, db.DockerUnavailableReason);
        var sql = SqlExport.Generate(Sample(replace: true), GeneratedAt);

        await db.ExecuteScriptAsync(sql);
        await db.ExecuteScriptAsync(sql);

        await using var context = db.CreateContext();
        Assert.Equal(1, await context.Templates.CountAsync());
        Assert.Equal(2, await context.Boards.CountAsync());
        Assert.Equal(5, await context.Questions.CountAsync());
    }

    [SkippableFact]
    public async Task Replace_mode_keeps_other_templates()
    {
        Skip.If(db.DockerUnavailableReason is not null, db.DockerUnavailableReason);
        var other = Sample();
        other.Name = "Other quiz";

        await db.ExecuteScriptAsync(SqlExport.Generate(other, GeneratedAt));
        await db.ExecuteScriptAsync(SqlExport.Generate(Sample(), GeneratedAt));
        await db.ExecuteScriptAsync(SqlExport.Generate(Sample(), GeneratedAt));

        await using var context = db.CreateContext();
        Assert.Equal(["Other quiz", "Pub Quiz – O'Brien's"], await context.Templates.Select(t => t.Name).OrderBy(n => n).ToListAsync());
        Assert.Equal(10, await context.Questions.CountAsync());
    }

    [SkippableFact]
    public async Task Insert_only_mode_twice_creates_two_templates()
    {
        Skip.If(db.DockerUnavailableReason is not null, db.DockerUnavailableReason);
        var sql = SqlExport.Generate(Sample(replace: false), GeneratedAt);

        await db.ExecuteScriptAsync(sql);
        await db.ExecuteScriptAsync(sql);

        await using var context = db.CreateContext();
        Assert.Equal(2, await context.Templates.CountAsync());
    }

    [SkippableFact]
    public async Task Text_containing_the_dollar_quote_tag_is_stored_verbatim()
    {
        Skip.If(db.DockerUnavailableReason is not null, db.DockerUnavailableReason);
        var project = Sample();
        project.Boards[1].Categories[0].Questions[0].QuestionText = "What is $topg$ and $$?";

        await db.ExecuteScriptAsync(SqlExport.Generate(project, GeneratedAt));

        await using var context = db.CreateContext();
        var question = await context.Questions.OfType<DomainTextQuestion>().SingleAsync(q => q.Points == 500);
        Assert.Equal("What is $topg$ and $$?", question.QuestionText);
    }
}
