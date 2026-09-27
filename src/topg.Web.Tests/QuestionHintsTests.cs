using AngleSharp.Dom;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using topg.Web.Client.Shared;
using topg.Web.Components.Pages;
using topg.Web.Components.Pages.Questions.Host;
using topg.Web.Components.Pages.Questions.Player;
using topg.Web.Quiz.Execution;
using topg.Web.Quiz.Management;
using Template = topg.Web.Templating.DomainObjects;

namespace topg.Web.Tests;

public class QuestionHintsTests : IAsyncLifetime
{
    private const long WithTextHints = 1;
    private const long WithoutHints = 2;
    private const long WithImageHints = 3;

    private readonly BunitContext context = new();
    private readonly SessionHandler sessionHandler = new();
    private readonly SessionId sessionId;

    public QuestionHintsTests()
    {
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddSingleton(sessionHandler);
        context.Services.AddSingleton(new ProtectedLocalStorage(context.JSInterop.JSRuntime, new EphemeralDataProtectionProvider()));

        var template = new Template.QuizTemplate { Name = "Quiz", Boards = [] };
        var board = new Template.Board { Template = template, Questions = [] };
        board.Questions.Add(Question(WithTextHints, 100, HintType.Text,
            // Stored out of order: the game has to follow Order, not the order the database returns.
            Hint(1, "Second"), Hint(0, "First"), Hint(2, "Third"), Hint(3, "Fourth")));
        board.Questions.Add(Question(WithoutHints, 200, null));
        board.Questions.Add(Question(WithImageHints, 300, HintType.Image,
            [.. Enumerable.Range(0, 6).Select(i => Hint(i, i == 0 ? "Caption" : "", $"https://cdn.example.com/quiz/{i}.png"))]));
        template.Boards.Add(board);
        sessionId = sessionHandler.CreateSession(template);
    }

    private static Template.TextQuestion Question(long id, int points, HintType? hintType, params Template.QuestionHint[] hints) => new()
    {
        Id = id,
        Points = points,
        Category = "Category",
        AnswerType = AnswerType.Buzzer,
        QuestionType = QuestionType.Text,
        QuestionText = "Which city?",
        CorrectAnswer = "Basel",
        HintType = hintType,
        Hints = [.. hints],
    };

    private static Template.QuestionHint Hint(int order, string text, string imageUri = "") =>
        new() { Order = order, Text = text, ImageUri = imageUri };

    public Task InitializeAsync() => Task.CompletedTask;

    // MudBlazor registers services that only support asynchronous disposal.
    public async Task DisposeAsync() => await context.DisposeAsync();

    private QuizSession Session => sessionHandler.Sessions[sessionId];

    private TextQuestion Open(long id)
    {
        var question = Session.Quiz.CurrentBoard.Questions.OfType<TextQuestion>().Single(q => q.Id == id);
        Session.SelectQuestion(question);
        return question;
    }

    private IRenderedComponent<QuestionHost> RenderHost() =>
        context.Render<QuestionHost>(parameters => parameters.Add(p => p.QuizSession, Session));

    private IRenderedComponent<QuizSpectator> RenderSpectator() =>
        context.Render<QuizSpectator>(parameters => parameters.Add(p => p.SessionKey, sessionId.Key));

    private static string[] RowNames(IRenderedComponent<QuestionHost> host) =>
        [.. host.FindAll("tbody tr").Select(row => row.QuerySelector("td")!.TextContent.Trim())];

    private static IElement ToggleOf(IRenderedComponent<QuestionHost> host, string rowName) =>
        host.FindAll("tbody tr").Single(row => row.QuerySelector("td")!.TextContent.Trim() == rowName).QuerySelector("button")!;

    private static string[] Tiles(IRenderedComponent<QuizSpectator> spectator) =>
        [.. spectator.FindAll(".hint-tile").Select(tile => tile.ClassList.Contains("hidden") ? $"#{tile.TextContent.Trim()}" : tile.TextContent.Trim())];

    [Fact]
    public void Host_lists_the_hints_between_the_question_and_the_answer_all_hidden_at_first()
    {
        Open(WithTextHints);

        var host = RenderHost();

        Assert.Equal(["Question Text", "Hints", "Hint 1", "Hint 2", "Hint 3", "Hint 4", "Answer", "Answer"], RowNames(host));
        Assert.Contains("Text · 0 of 4 shown", host.Find("tr.hints-section").TextContent);
        Assert.Equal("First", host.FindAll("td.hint-content")[0].TextContent.Trim());
    }

    [Fact]
    public void Showing_a_hint_reveals_only_that_hint_and_hiding_it_brings_back_its_number()
    {
        Open(WithTextHints);
        var host = RenderHost();
        var spectator = RenderSpectator();
        Assert.Equal(["#1", "#2", "#3", "#4"], Tiles(spectator));

        ToggleOf(host, "Hint 3").Click();

        spectator.WaitForAssertion(() => Assert.Equal(["#1", "#2", "Third", "#4"], Tiles(spectator)));
        Assert.Contains("Text · 1 of 4 shown", host.Find("tr.hints-section").TextContent);

        ToggleOf(host, "Hint 3").Click();

        spectator.WaitForAssertion(() => Assert.Equal(["#1", "#2", "#3", "#4"], Tiles(spectator)));
    }

    [Fact]
    public void A_spectator_opening_the_page_later_sees_the_hints_already_shown()
    {
        var question = Open(WithTextHints);
        question.Hints[0].IsVisible = true;
        question.Hints[1].IsVisible = true;

        var spectator = RenderSpectator();

        Assert.Equal(["First", "Second", "#3", "#4"], Tiles(spectator));
    }

    [Fact]
    public void Question_text_toggles_on_its_own_and_is_a_header_above_the_hints()
    {
        var question = Open(WithTextHints);
        question.Hints[0].IsVisible = true;
        var host = RenderHost();
        var spectator = RenderSpectator();

        ToggleOf(host, "Question Text").Click();

        spectator.WaitForAssertion(() => Assert.Equal("Which city?", spectator.Find(".hint-header").TextContent.Trim()));
        Assert.Empty(spectator.FindAll("h1"));
        Assert.Equal(["First", "#2", "#3", "#4"], Tiles(spectator));
    }

    [Fact]
    public void Returning_to_the_board_hides_the_hints_again()
    {
        var question = Open(WithTextHints);
        question.Hints[2].IsVisible = true;

        Session.ReturnToBoard();

        Assert.All(question.Hints, h => Assert.False(h.IsVisible));
    }

    [Fact]
    public void Question_without_hints_looks_like_before()
    {
        var question = Open(WithoutHints);
        question.DisplayState = TextQuestionDisplayState.Question;

        var host = RenderHost();
        var spectator = RenderSpectator();

        Assert.Equal(["Question Text", "Answer", "Answer"], RowNames(host));
        Assert.Empty(spectator.FindAll(".hint-area"));
        Assert.Empty(spectator.FindAll(".hint-header"));
        Assert.Equal("Which city?", spectator.Find("h1").TextContent.Trim());
    }

    [Fact]
    public void Image_hints_show_a_thumbnail_for_the_host_and_the_image_with_its_caption_when_shown()
    {
        var question = Open(WithImageHints);
        question.Hints[0].IsVisible = true;

        var host = RenderHost();
        var spectator = RenderSpectator();

        Assert.Contains("Image · 1 of 6 shown", host.Find("tr.hints-section").TextContent);
        Assert.Equal("https://cdn.example.com/quiz/0.png", host.Find("td.hint-content img").GetAttribute("src"));
        var shown = spectator.Find(".hint-tile.shown");
        Assert.Equal("https://cdn.example.com/quiz/0.png", shown.QuerySelector("img")!.GetAttribute("src"));
        Assert.Equal("Caption", shown.QuerySelector(".hint-caption")!.TextContent.Trim());
        Assert.Equal([3, 3], spectator.FindAll(".hint-row").Select(row => row.QuerySelectorAll(".hint-tile").Length));
    }

    [Theory]
    [InlineData(false, 1, null)]
    [InlineData(false, 3, null)]
    [InlineData(false, 4, new[] { 4 })]
    [InlineData(false, 5, new[] { 5 })]
    [InlineData(true, 1, new[] { 1 })]
    [InlineData(true, 3, new[] { 3 })]
    [InlineData(true, 5, new[] { 5 })]
    [InlineData(false, 6, new[] { 3, 3 })]
    [InlineData(false, 7, new[] { 4, 3 })]
    [InlineData(true, 9, new[] { 5, 4 })]
    [InlineData(true, 10, new[] { 5, 5 })]
    public void Rows_follow_the_concept_layout(bool isImage, int count, int[]? rows)
    {
        Assert.Equal(rows, HintGrid.RowsFor(isImage, count));
    }
}
