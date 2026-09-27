using Microsoft.Extensions.Time.Testing;
using topg.Web.Quiz.Execution;
using topg.Web.Quiz.Management;
using topg.Web.Templating.DomainObjects;

namespace topg.Web.IntegrationTests;

public class QuizSessionTimerTests
{
    private readonly FakeTimeProvider time = new();
    private readonly QuizSession session;
    private int stateChanges;

    public QuizSessionTimerTests()
    {
        session = new QuizSession
        {
            SessionId = SessionId.Create(),
            Quiz = new QuizExecution(new QuizTemplate { Name = "Test", Boards = [] }),
            TimeProvider = time,
        };
        session.SessionStateChanged += (_, _) =>
        {
            Interlocked.Increment(ref stateChanges);
            return Task.CompletedTask;
        };
        session.SetTimerDuration(3);
    }

    [Fact]
    public void Expiry_notifies_every_screen()
    {
        session.ToggleTimer();
        var changesAfterStart = stateChanges;

        time.Advance(TimeSpan.FromSeconds(3));

        Assert.False(session.TimerState.IsRunning);
        Assert.Equal(changesAfterStart + 1, stateChanges);
    }

    [Fact]
    public void Buzzing_stops_the_timer()
    {
        session.TryAddPlayer("Alice", out _);
        session.ToggleTimer();

        session.Buzz(session.Players.First!.Value);

        Assert.False(session.TimerState.IsRunning);
    }

    [Fact]
    public void Moving_on_stops_the_timer()
    {
        session.ToggleTimer();

        session.MarkCurrentQuestionAsAnswered();
        var changesAfterMovingOn = stateChanges;
        time.Advance(TimeSpan.FromSeconds(5));

        Assert.False(session.TimerState.IsRunning);
        Assert.Equal(changesAfterMovingOn, stateChanges);
    }
}
