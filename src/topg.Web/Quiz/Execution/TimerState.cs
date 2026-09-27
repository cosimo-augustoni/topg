namespace topg.Web.Quiz.Execution;

public class TimerState
{
    private readonly Lock gate = new();
    private ITimer? expiryTimer;
    // Identifies the current run so an expiry callback already queued for a stopped or restarted run is ignored.
    private object? currentRun;

    public bool IsRunning
    {
        get
        {
            lock (gate)
                return currentRun != null;
        }
    }

    public int TimerDuration { get; set; } = 10;

    public void Start(TimeProvider timeProvider, Action onExpired)
    {
        lock (gate)
        {
            StopCore();
            var run = new object();
            currentRun = run;
            expiryTimer = timeProvider.CreateTimer(_ => Expire(run, onExpired), null,
                TimeSpan.FromSeconds(TimerDuration), Timeout.InfiniteTimeSpan);
        }
    }

    public void Stop()
    {
        lock (gate)
            StopCore();
    }

    private void Expire(object run, Action onExpired)
    {
        lock (gate)
        {
            if (currentRun != run)
                return;
            StopCore();
        }

        onExpired();
    }

    private void StopCore()
    {
        currentRun = null;
        expiryTimer?.Dispose();
        expiryTimer = null;
    }
}
