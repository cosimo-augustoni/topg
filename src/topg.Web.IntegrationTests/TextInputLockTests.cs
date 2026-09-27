using topg.Web.Client.Shared;
using topg.Web.Quiz.Execution;
using topg.Web.Quiz.Management;
using Template = topg.Web.Templating.DomainObjects;

namespace topg.Web.IntegrationTests;

public class TextInputLockTests
{
    private static (QuizSession Session, Player Player) CreateSessionWithTextQuestion()
    {
        var template = new Template.QuizTemplate { Name = "Quiz", Boards = [] };
        var board = new Template.Board { Template = template, Questions = [] };
        board.Questions.Add(new Template.TextQuestion
        {
            Id = 1,
            Points = 100,
            Category = "Category",
            AnswerType = AnswerType.Text,
            QuestionType = QuestionType.Text,
            QuestionText = "Question",
            CorrectAnswer = "Answer",
        });
        template.Boards.Add(board);

        var session = new QuizSession { SessionId = new SessionId("0001"), Quiz = new QuizExecution(template) };
        session.TryAddPlayer("Alice", out var playerId);
        session.TryGetPlayer(playerId, out var player);
        session.SelectQuestion(session.Quiz.CurrentBoard.Questions.Single());
        return (session, player!);
    }

    [Fact]
    public void Opening_a_question_starts_with_unlocked_inputs()
    {
        var (session, player) = CreateSessionWithTextQuestion();

        session.UpdateTextInput(player, "typed");

        Assert.False(session.TextInputState.IsLocked);
        Assert.Equal("typed", session.TextInputState.GetTextByPlayer(player));
    }

    [Fact]
    public void Locked_inputs_keep_their_text_and_reject_edits_until_unlocked()
    {
        var (session, player) = CreateSessionWithTextQuestion();
        session.UpdateTextInput(player, "first");

        session.LockTextInputs();
        session.UpdateTextInput(player, "changed");

        Assert.Equal("first", session.TextInputState.GetTextByPlayer(player));

        session.UnlockTextInputs();
        session.UpdateTextInput(player, "changed");

        Assert.Equal("changed", session.TextInputState.GetTextByPlayer(player));
    }

    [Fact]
    public void Showing_and_clearing_do_not_change_the_lock()
    {
        var (session, player) = CreateSessionWithTextQuestion();
        session.UpdateTextInput(player, "answer");
        session.LockTextInputs();

        session.RevealTextInput();
        Assert.True(session.TextInputState.IsLocked);

        session.ClearTextInputs();
        Assert.True(session.TextInputState.IsLocked);
        Assert.Equal(string.Empty, session.TextInputState.GetTextByPlayer(player));
    }

    [Fact]
    public void Showing_unlocked_inputs_keeps_them_editable()
    {
        var (session, player) = CreateSessionWithTextQuestion();

        session.RevealTextInput();
        session.UpdateTextInput(player, "edited");

        Assert.False(session.TextInputState.IsLocked);
        Assert.Equal("edited", session.TextInputState.GetTextByPlayer(player));
    }

    [Fact]
    public void Finishing_a_question_unlocks_the_inputs()
    {
        var (session, _) = CreateSessionWithTextQuestion();
        session.LockTextInputs();

        session.MarkCurrentQuestionAsAnswered();

        Assert.False(session.TextInputState.IsLocked);
    }
}
