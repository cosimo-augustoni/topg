using Microsoft.Extensions.Time.Testing;
using topg.Web.Quiz.Execution;

namespace topg.Web.IntegrationTests;

public class TimerStateTests
{
    private readonly FakeTimeProvider time = new();
    private readonly TimerState timer = new() { TimerDuration = 3 };
    private int expirations;

    private void Start() => timer.Start(time, () => expirations++);

    [Fact]
    public void Expires_once_when_the_duration_has_passed()
    {
        Start();

        time.Advance(TimeSpan.FromSeconds(2.9));
        Assert.True(timer.IsRunning);

        time.Advance(TimeSpan.FromSeconds(0.1));
        Assert.False(timer.IsRunning);
        Assert.Equal(1, expirations);

        time.Advance(TimeSpan.FromSeconds(10));
        Assert.Equal(1, expirations);
    }

    [Fact]
    public void Restarting_after_expiry_runs_for_the_full_duration_again()
    {
        Start();
        time.Advance(TimeSpan.FromSeconds(3));

        Start();
        time.Advance(TimeSpan.FromSeconds(2.9));
        Assert.True(timer.IsRunning);

        time.Advance(TimeSpan.FromSeconds(0.1));
        Assert.False(timer.IsRunning);
        Assert.Equal(2, expirations);
    }

    [Fact]
    public void Stopping_early_cancels_the_pending_expiry()
    {
        Start();
        time.Advance(TimeSpan.FromSeconds(1));
        timer.Stop();

        time.Advance(TimeSpan.FromSeconds(5));
        Assert.False(timer.IsRunning);
        Assert.Equal(0, expirations);
    }

    [Fact]
    public void A_stale_expiry_does_not_end_a_restarted_timer_early()
    {
        Start();
        time.Advance(TimeSpan.FromSeconds(2));
        timer.Stop();
        Start();

        time.Advance(TimeSpan.FromSeconds(1));
        Assert.True(timer.IsRunning);

        time.Advance(TimeSpan.FromSeconds(2));
        Assert.False(timer.IsRunning);
        Assert.Equal(1, expirations);
    }
}
