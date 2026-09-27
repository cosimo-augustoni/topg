using topg.Web.Quiz.Execution;
using topg.Web.Quiz.Management;
using Template = topg.Web.Templating.DomainObjects;

namespace topg.Web.IntegrationTests;

public class QuizSessionTurnOrderTests
{
    private readonly QuizSession session = new()
    {
        SessionId = SessionId.Create(),
        Quiz = new QuizExecution(new Template.QuizTemplate { Name = "Test", Boards = [] }),
    };

    private Player Join(string name)
    {
        session.TryAddPlayer(name, out var playerId);
        session.TryGetPlayer(playerId, out var player);
        return player!;
    }

    [Fact]
    public void A_new_session_plays_forward()
    {
        Assert.Equal(TurnDirection.Forward, session.TurnDirection);
    }

    [Fact]
    public void Forward_follows_the_join_order_and_wraps_to_the_first_player()
    {
        var a = Join("A");
        var b = Join("B");
        var c = Join("C");
        session.SetActivePlayer(a);

        session.MarkCurrentQuestionAsAnswered();
        Assert.Equal(b, session.ActivePlayer);

        session.MarkCurrentQuestionAsAnswered();
        Assert.Equal(c, session.ActivePlayer);

        session.MarkCurrentQuestionAsAnswered();
        Assert.Equal(a, session.ActivePlayer);
    }

    [Fact]
    public void Reverse_goes_against_the_join_order_and_wraps_to_the_last_player()
    {
        var a = Join("A");
        var b = Join("B");
        var c = Join("C");
        session.SetTurnDirection(TurnDirection.Reverse);
        session.SetActivePlayer(c);

        session.MarkCurrentQuestionAsAnswered();
        Assert.Equal(b, session.ActivePlayer);

        session.MarkCurrentQuestionAsAnswered();
        Assert.Equal(a, session.ActivePlayer);

        session.MarkCurrentQuestionAsAnswered();
        Assert.Equal(c, session.ActivePlayer);
    }

    [Fact]
    public void Switching_the_direction_keeps_the_active_player_and_notifies_every_screen()
    {
        Join("A");
        var b = Join("B");
        Join("C");
        session.SetActivePlayer(b);
        var stateChanges = 0;
        session.SessionStateChanged += (_, _) =>
        {
            stateChanges++;
            return Task.CompletedTask;
        };

        session.SetTurnDirection(TurnDirection.Reverse);

        Assert.Equal(b, session.ActivePlayer);
        Assert.Equal(TurnDirection.Reverse, session.TurnDirection);
        Assert.Equal(1, stateChanges);
    }

    [Fact]
    public void Advancing_continues_from_a_hand_picked_player_in_the_current_direction()
    {
        var a = Join("A");
        var b = Join("B");
        Join("C");
        session.SetTurnDirection(TurnDirection.Reverse);

        session.SetActivePlayer(b);
        session.MarkCurrentQuestionAsAnswered();

        Assert.Equal(a, session.ActivePlayer);
    }

    [Theory]
    [InlineData(TurnDirection.Forward)]
    [InlineData(TurnDirection.Reverse)]
    public void A_single_player_stays_active(TurnDirection direction)
    {
        var a = Join("A");
        session.SetTurnDirection(direction);

        session.MarkCurrentQuestionAsAnswered();

        Assert.Equal(a, session.ActivePlayer);
    }

    [Fact]
    public void The_direction_is_kept_on_the_next_board()
    {
        var template = new Template.QuizTemplate { Name = "Test", Boards = [] };
        template.Boards.Add(new Template.Board { Template = template, Order = 0, Questions = [] });
        template.Boards.Add(new Template.Board { Template = template, Order = 1, Questions = [] });
        var twoBoardSession = new QuizSession { SessionId = SessionId.Create(), Quiz = new QuizExecution(template) };
        twoBoardSession.SetTurnDirection(TurnDirection.Reverse);

        twoBoardSession.SelectNextBoard();

        Assert.Equal(1, twoBoardSession.Quiz.CurrentBoardId);
        Assert.Equal(TurnDirection.Reverse, twoBoardSession.TurnDirection);
    }
}
