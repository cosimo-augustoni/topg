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
    public void Timer_stops_and_notifies_when_the_duration_has_passed()
    {
        session.ToggleTimer();
        var changesAfterStart = stateChanges;

        time.Advance(TimeSpan.FromSeconds(2.9));
        Assert.True(session.TimerState.IsRunning);

        time.Advance(TimeSpan.FromSeconds(0.1));
        Assert.False(session.TimerState.IsRunning);
        Assert.Equal(changesAfterStart + 1, stateChanges);
    }

    [Fact]
    public void Restarting_after_expiry_runs_for_the_full_duration_again()
    {
        session.ToggleTimer();
        time.Advance(TimeSpan.FromSeconds(3));

        session.ToggleTimer();
        time.Advance(TimeSpan.FromSeconds(2.9));
        Assert.True(session.TimerState.IsRunning);

        time.Advance(TimeSpan.FromSeconds(0.1));
        Assert.False(session.TimerState.IsRunning);
    }

    [Fact]
    public void Stopping_early_cancels_the_pending_expiry()
    {
        session.ToggleTimer();
        time.Advance(TimeSpan.FromSeconds(1));
        session.ToggleTimer();
        var changesAfterStop = stateChanges;

        time.Advance(TimeSpan.FromSeconds(5));
        Assert.False(session.TimerState.IsRunning);
        Assert.Equal(changesAfterStop, stateChanges);
    }

    [Fact]
    public void A_stale_expiry_does_not_end_a_restarted_timer_early()
    {
        session.ToggleTimer();
        time.Advance(TimeSpan.FromSeconds(2));
        session.ToggleTimer();
        session.ToggleTimer();

        time.Advance(TimeSpan.FromSeconds(1));
        Assert.True(session.TimerState.IsRunning);

        time.Advance(TimeSpan.FromSeconds(2));
        Assert.False(session.TimerState.IsRunning);
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
