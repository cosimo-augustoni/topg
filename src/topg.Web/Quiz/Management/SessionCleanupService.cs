namespace topg.Web.Quiz.Management;

public class SessionCleanupService(SessionHandler sessionHandler) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var unusedSessionIds = sessionHandler.Sessions
                .Where(kvp => !kvp.Value.IsInUse)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var sessionId in unusedSessionIds)
            {
                sessionHandler.Sessions.TryRemove(sessionId, out _);
            }

            await Task.Delay(600_000, stoppingToken);
        }
    }
}