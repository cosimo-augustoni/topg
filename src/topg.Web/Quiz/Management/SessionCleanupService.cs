namespace topg.Web.Quiz.Management;

public class SessionCleanupService(SessionHandler sessionHandler) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var unusedSessions = sessionHandler.Sessions.Values.Where(s => !s.IsInUse);
            foreach (var unusedSession in unusedSessions)
            {
                sessionHandler.Sessions.Remove(unusedSession.SessionId);
            }

            await Task.Delay(600_000, stoppingToken);

        }
    }
}