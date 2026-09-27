using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using topg.Web.Client.Shared;
using topg.Web.Components.Pages;
using topg.Web.Quiz.Execution;
using topg.Web.Quiz.Management;
using BoardComponent = topg.Web.Components.Pages.Board;
using Template = topg.Web.Templating.DomainObjects;

namespace topg.Web.Tests;

public class BonusPointsRenderingTests : IAsyncLifetime
{
    private readonly BunitContext context = new();
    private readonly SessionHandler sessionHandler = new();
    private readonly SessionId sessionId;

    public BonusPointsRenderingTests()
    {
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddSingleton(sessionHandler);
        context.Services.AddSingleton(new ProtectedLocalStorage(context.JSInterop.JSRuntime, new EphemeralDataProtectionProvider()));

        var template = new Template.QuizTemplate { Name = "Quiz", Boards = [] };
        var board = new Template.Board { Template = template, Questions = [] };
        foreach (var (points, index) in new[] { 100, 200, 300, 400 }.Select((p, i) => (p, i)))
        {
            board.Questions.Add(new Template.TextQuestion
            {
                Id = index + 1,
                Points = points,
                Category = $"Category {index}",
                AnswerType = AnswerType.Buzzer,
                QuestionType = QuestionType.Text,
                QuestionText = "Question",
                CorrectAnswer = "Answer",
            });
        }
        template.Boards.Add(board);
        sessionId = sessionHandler.CreateSession(template);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    // MudBlazor registers services that only support asynchronous disposal.
    public async Task DisposeAsync() => await context.DisposeAsync();

    private QuizSession Session => sessionHandler.Sessions[sessionId];

    private Question Question(int points) => Session.Quiz.CurrentBoard.Questions.Single(q => q.Points == points);

    private IRenderedComponent<BoardComponent> RenderBoard() =>
        context.Render<BoardComponent>(parameters => parameters
            .Add(p => p.QuizSession, Session)
            .Add(p => p.IsReadOnly, true));

    private IRenderedComponent<QuizHost> RenderHost() =>
        context.Render<QuizHost>(parameters => parameters.Add(p => p.SessionKey, sessionId.Key));

    private static string[] Tiles(IRenderedComponent<BoardComponent> board) =>
        [.. board.FindAll("button").Select(b => string.Join(" ", b.TextContent.Split((char[])null!, StringSplitOptions.RemoveEmptyEntries)))];

    private void AnswerFirstQuestion()
    {
        Session.SelectQuestion(Question(100));
        Session.MarkCurrentQuestionAsAnswered();
    }

    [Fact]
    public void Tiles_show_normal_points_until_only_the_last_questions_are_left()
    {
        var board = RenderBoard();

        Assert.Equal(["100", "200", "300", "400"], Tiles(board));
        Assert.Empty(board.FindAll(".bonus-badge"));
    }

    [Fact]
    public void Boosted_tiles_show_one_and_a_half_times_their_points_with_a_badge_and_answered_tiles_do_not()
    {
        AnswerFirstQuestion();

        var board = RenderBoard();

        Assert.Equal(["100", "300 ×1.5", "450 ×1.5", "600 ×1.5"], Tiles(board));
        Assert.Equal(3, board.FindAll(".bonus-tile").Count);
    }

    [Fact]
    public void An_open_boosted_question_shows_the_boosted_points_in_the_banner_and_on_the_score_buttons()
    {
        AnswerFirstQuestion();
        Session.TryAddPlayer("Anna", out _);
        Session.SelectQuestion(Question(400));

        var host = RenderHost();

        var banner = host.Find(".points-banner");
        Assert.Contains("600", banner.TextContent);
        Assert.Contains("×1.5", banner.TextContent);
        var buttons = host.FindAll("button").Select(b => b.TextContent.Trim()).ToList();
        Assert.Contains("+ 600", buttons);
        Assert.Contains("− 300", buttons);

        host.FindAll("button").Single(b => b.TextContent.Trim() == "+ 600").Click();

        Assert.Equal(600, Session.Players.Single().Score);
    }

    [Fact]
    public void Host_panel_tells_when_the_bonus_starts_and_when_it_is_active()
    {
        var host = RenderHost();

        Assert.Equal("Starts in 1 question.", host.Find(".bonus-points-status").TextContent.Trim());

        AnswerFirstQuestion();

        host.WaitForAssertion(() =>
            Assert.Equal("Active: the last 3 questions give 1.5× points.", host.Find(".bonus-points-status").TextContent.Trim()));
    }
}
