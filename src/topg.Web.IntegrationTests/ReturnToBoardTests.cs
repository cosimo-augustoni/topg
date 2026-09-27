using topg.Web.Client.Shared;
using topg.Web.Quiz.Execution;
using topg.Web.Quiz.Management;
using Template = topg.Web.Templating.DomainObjects;

namespace topg.Web.IntegrationTests;

public class ReturnToBoardTests
{
    private readonly QuizSession session;
    private readonly Player alice;
    private readonly Player bob;
    private readonly TextQuestion question;

    public ReturnToBoardTests()
    {
        var template = new Template.QuizTemplate { Name = "Quiz", Boards = [] };
        var board = new Template.Board { Template = template, Questions = [] };
        board.Questions.Add(new Template.TextQuestion
        {
            Id = 1,
            Points = 100,
            Category = "Astronomy",
            AnswerType = AnswerType.Text,
            QuestionType = QuestionType.Text,
            QuestionText = "Question",
            CorrectAnswer = "Answer",
        });
        template.Boards.Add(board);

        session = new QuizSession { SessionId = SessionId.Create(), Quiz = new QuizExecution(template) };
        alice = Join("Alice");
        bob = Join("Bob");
        question = (TextQuestion)session.Quiz.CurrentBoard.Questions.Single();
        session.SelectQuestion(question);
    }

    private Player Join(string name)
    {
        session.TryAddPlayer(name, out var playerId);
        session.TryGetPlayer(playerId, out var player);
        return player!;
    }

    [Fact]
    public void Going_back_shows_the_board_and_leaves_the_question_selectable()
    {
        var stateChanges = 0;
        session.SessionStateChanged += (_, _) =>
        {
            stateChanges++;
            return Task.CompletedTask;
        };

        session.ReturnToBoard();

        Assert.Null(session.Quiz.CurrentQuestion);
        Assert.False(question.IsAnswered);
        Assert.True(stateChanges > 0);
    }

    [Fact]
    public void Going_back_keeps_the_active_player()
    {
        Assert.Equal(alice, session.ActivePlayer);

        session.ReturnToBoard();

        Assert.Equal(alice, session.ActivePlayer);
    }

    [Fact]
    public void Going_back_resets_buzzer_text_inputs_and_timer()
    {
        session.Buzz(bob);
        session.UpdateTextInput(bob, "typed");
        session.RevealTextInput();
        session.LockTextInputs();
        session.ToggleTimer();

        session.ReturnToBoard();

        Assert.False(session.BuzzerState.IsLocked);
        Assert.Null(session.BuzzerState.BuzzeredPlayer);
        Assert.Equal(string.Empty, session.TextInputState.GetTextByPlayer(bob));
        Assert.False(session.TextInputState.IsRevealed);
        Assert.False(session.TextInputState.IsLocked);
        Assert.Equal(ControlDisplayState.None, session.ControlDisplayState);
        Assert.False(session.TimerState.IsRunning);
    }

    [Fact]
    public void Reopening_the_question_hides_the_text_again()
    {
        session.UpdateQuestion(question, q => q.DisplayState |= TextQuestionDisplayState.Question);

        session.ReturnToBoard();
        session.SelectQuestion(question);

        Assert.Equal(TextQuestionDisplayState.None, question.DisplayState);
    }

    [Fact]
    public void Going_back_keeps_the_scores()
    {
        session.AdjustPlayerScore(bob, 100);

        session.ReturnToBoard();

        Assert.Equal(100, bob.Score);
    }

    [Fact]
    public void Next_still_marks_the_question_answered_and_advances()
    {
        session.MarkCurrentQuestionAsAnswered();

        Assert.True(question.IsAnswered);
        Assert.Equal(bob, session.ActivePlayer);
    }
}
