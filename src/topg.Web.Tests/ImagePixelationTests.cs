using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using topg.Web.Client.Shared;
using topg.Web.Components.Pages;
using topg.Web.Components.Pages.Questions.Host;
using topg.Web.Quiz.Execution;
using topg.Web.Quiz.Management;
using Template = topg.Web.Templating.DomainObjects;

namespace topg.Web.Tests;

public class ImagePixelationTests : IAsyncLifetime
{
    private const long Pixelated = 1;
    private const long Sharp = 2;
    private const string QuestionImage = "https://cdn.example.com/quiz/flag.png";
    private const string AnswerImage = "https://cdn.example.com/quiz/answer.png";

    private readonly BunitContext context = new();
    private readonly SessionHandler sessionHandler = new();
    private readonly SessionId sessionId;

    public ImagePixelationTests()
    {
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddSingleton(sessionHandler);
        context.Services.AddSingleton(new ProtectedLocalStorage(context.JSInterop.JSRuntime, new EphemeralDataProtectionProvider()));

        var template = new Template.QuizTemplate { Name = "Quiz", Boards = [] };
        var board = new Template.Board { Template = template, Questions = [] };
        board.Questions.Add(Question(Pixelated, 100, startPixelated: true));
        board.Questions.Add(Question(Sharp, 200, startPixelated: false));
        template.Boards.Add(board);
        sessionId = sessionHandler.CreateSession(template);
    }

    private static Template.ImageQuestion Question(long id, int points, bool startPixelated) => new()
    {
        Id = id,
        Points = points,
        Category = "Flags",
        AnswerType = AnswerType.Buzzer,
        QuestionType = QuestionType.Image,
        QuestionText = "Which flag?",
        QuestionImageUri = QuestionImage,
        AnswerText = "Switzerland",
        AnswerImageUri = AnswerImage,
        ImageSize = ImageSize.Medium,
        StartPixelated = startPixelated,
    };

    public Task InitializeAsync() => Task.CompletedTask;

    // MudBlazor registers services that only support asynchronous disposal.
    public async Task DisposeAsync() => await context.DisposeAsync();

    private QuizSession Session => sessionHandler.Sessions[sessionId];

    private ImageQuestion Open(long id, bool showImage = false)
    {
        var question = Session.Quiz.CurrentBoard.Questions.OfType<ImageQuestion>().Single(q => q.Id == id);
        Session.SelectQuestion(question);
        if (showImage)
        {
            question.DisplayState = ImageQuestionDisplayState.Image;
        }

        return question;
    }

    private IRenderedComponent<QuestionHost> RenderHost() =>
        context.Render<QuestionHost>(parameters => parameters.Add(p => p.QuizSession, Session));

    private IRenderedComponent<QuizSpectator> RenderSpectator() =>
        context.Render<QuizSpectator>(parameters => parameters.Add(p => p.SessionKey, sessionId.Key));

    private int[] DrawnBlockCounts() =>
        [.. context.JSInterop.Invocations["pixelateImage"].Select(invocation =>
        {
            Assert.IsType<ElementReference>(invocation.Arguments[0]);
            Assert.Equal(QuestionImage, invocation.Arguments[1]);
            return (int)invocation.Arguments[2]!;
        })];

    [Fact]
    public void The_steps_run_from_8_to_128_blocks_and_clear_is_past_the_end()
    {
        Assert.Equal([8, 16, 32, 64, 128], ImageQuestion.PixelationSteps);
        Assert.Equal(5, ImageQuestion.ClearStep);
    }

    [Fact]
    public void A_question_with_the_flag_starts_at_the_coarsest_step()
    {
        var question = Open(Pixelated);

        Assert.Equal(0, question.PixelationStep);
        Assert.Equal(8, question.PixelationBlocks);
    }

    [Fact]
    public void A_question_without_the_flag_starts_clear()
    {
        var question = Open(Sharp);

        Assert.Equal(ImageQuestion.ClearStep, question.PixelationStep);
        Assert.Null(question.PixelationBlocks);
    }

    [Theory]
    [InlineData(Pixelated, 0)]
    [InlineData(Sharp, 5)]
    public void Back_to_board_resets_the_slider_to_the_starting_step(long id, int startStep)
    {
        var question = Open(id);
        Session.UpdateQuestion(question, q => q.PixelationStep = 2);

        Session.ReturnToBoard();

        Assert.Equal(startStep, question.PixelationStep);
    }

    [Fact]
    public void Host_slider_has_a_tick_per_step_and_moving_it_changes_the_step()
    {
        var question = Open(Pixelated);
        var host = RenderHost();

        var slider = host.Find("input[type=range]");
        Assert.Equal("0", slider.GetAttribute("min"));
        Assert.Equal("5", slider.GetAttribute("max"));
        Assert.Equal(["8", "16", "32", "64", "128", "Clear"], host.FindAll(".mud-slider-tickmarks .mud-typography").Select(label => label.TextContent.Trim()));

        slider.Input("2");

        Assert.Equal(2, question.PixelationStep);
    }

    [Fact]
    public void A_pixelated_image_is_drawn_on_a_canvas_and_never_as_a_sharp_img()
    {
        Open(Pixelated, showImage: true);

        var spectator = RenderSpectator();

        Assert.Single(spectator.FindAll("canvas[aria-label='Question Image']"));
        Assert.Empty(spectator.FindAll("img[alt='Question Image']"));
        Assert.Equal([8], DrawnBlockCounts());
    }

    [Fact]
    public void Moving_the_slider_redraws_every_screen_with_the_new_block_count()
    {
        var question = Open(Pixelated, showImage: true);
        Session.UpdateQuestion(question, q => q.PixelationStep = 1);
        var spectator = RenderSpectator();

        Session.UpdateQuestion(question, q => q.PixelationStep = 2);

        spectator.WaitForAssertion(() => Assert.Equal([16, 32], DrawnBlockCounts()));
    }

    [Fact]
    public void Other_session_changes_do_not_redraw_the_canvas()
    {
        var question = Open(Pixelated, showImage: true);
        var spectator = RenderSpectator();

        Session.UpdateQuestion(question, q => q.DisplayState |= ImageQuestionDisplayState.Text);

        spectator.WaitForAssertion(() => Assert.NotEmpty(spectator.FindAll("._question-text")));
        Assert.Equal([8], DrawnBlockCounts());
    }

    [Fact]
    public void At_clear_the_original_image_is_shown_as_before()
    {
        var question = Open(Pixelated, showImage: true);
        var spectator = RenderSpectator();

        Session.UpdateQuestion(question, q => q.PixelationStep = ImageQuestion.ClearStep);

        spectator.WaitForAssertion(() => Assert.Equal(QuestionImage, spectator.Find("img[alt='Question Image']").GetAttribute("src")));
        Assert.Empty(spectator.FindAll("canvas"));
    }

    [Fact]
    public void The_host_can_pixelate_a_question_without_the_flag_before_showing_it()
    {
        var question = Open(Sharp);
        var spectator = RenderSpectator();

        Session.UpdateQuestion(question, q => q.PixelationStep = 1);
        Assert.Empty(spectator.FindAll("canvas"));
        Assert.Empty(spectator.FindAll("img[alt='Question Image']"));
        Assert.DoesNotContain(context.JSInterop.Invocations, i => i.Identifier == "pixelateImage");

        Session.UpdateQuestion(question, q => q.DisplayState |= ImageQuestionDisplayState.Image);

        spectator.WaitForAssertion(() => Assert.Single(spectator.FindAll("canvas[aria-label='Question Image']")));
        Assert.Equal([16], DrawnBlockCounts());
    }

    [Fact]
    public void The_answer_image_is_shown_sharp_over_the_pixelated_question_image()
    {
        Open(Pixelated, showImage: true).DisplayState |= ImageQuestionDisplayState.Answer;

        var spectator = RenderSpectator();

        Assert.Equal(AnswerImage, spectator.Find("img[alt='Answer Image']").GetAttribute("src"));
        Assert.Single(spectator.FindAll("canvas[aria-label='Question Image']"));
    }
}
