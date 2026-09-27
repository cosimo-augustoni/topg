using AngleSharp.Dom;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using topg.Web.Client.Shared;
using topg.Web.Components.Pages;
using topg.Web.Quiz.Execution;
using topg.Web.Quiz.Management;
using Template = topg.Web.Templating.DomainObjects;

namespace topg.Web.Tests;

public class QuizHostTurnDirectionTests : IAsyncLifetime
{
    private readonly BunitContext context = new();
    private readonly SessionHandler sessionHandler = new();
    private readonly SessionId sessionId;

    public QuizHostTurnDirectionTests()
    {
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddSingleton(sessionHandler);
        context.Services.AddSingleton(new ProtectedLocalStorage(context.JSInterop.JSRuntime, new EphemeralDataProtectionProvider()));

        // The board component needs at least one question to lay out its grid.
        var template = new Template.QuizTemplate { Name = "Quiz", Boards = [] };
        var board = new Template.Board { Template = template, Questions = [] };
        board.Questions.Add(new Template.TextQuestion
        {
            Id = 1,
            Points = 100,
            Category = "Category",
            AnswerType = AnswerType.Buzzer,
            QuestionType = QuestionType.Text,
            QuestionText = "Question",
            CorrectAnswer = "Answer",
        });
        template.Boards.Add(board);
        sessionId = sessionHandler.CreateSession(template);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    // MudBlazor registers services that only support asynchronous disposal.
    public async Task DisposeAsync() => await context.DisposeAsync();

    private IRenderedComponent<QuizHost> RenderHost() =>
        context.Render<QuizHost>(parameters => parameters.Add(p => p.SessionKey, sessionId.Key));

    private static IElement DirectionButton(IRenderedComponent<QuizHost> host, string label) =>
        host.FindAll("button").Single(b => b.TextContent.Trim() == label);

    [Fact]
    public void The_active_player_panel_highlights_forward_in_a_new_session()
    {
        var host = RenderHost();

        Assert.True(DirectionButton(host, "Forward").HasAttribute("disabled"));
        Assert.False(DirectionButton(host, "Reverse").HasAttribute("disabled"));
    }

    [Fact]
    public void Switching_the_direction_in_one_host_view_updates_the_other()
    {
        var first = RenderHost();
        var second = RenderHost();

        DirectionButton(first, "Reverse").Click();

        Assert.Equal(TurnDirection.Reverse, sessionHandler.Sessions[sessionId].TurnDirection);
        second.WaitForAssertion(() =>
        {
            Assert.True(DirectionButton(second, "Reverse").HasAttribute("disabled"));
            Assert.False(DirectionButton(second, "Forward").HasAttribute("disabled"));
        });
    }
}
