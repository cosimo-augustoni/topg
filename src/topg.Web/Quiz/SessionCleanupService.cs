namespace topg.Web.Quiz;

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

            await Task.Delay(120_000, stoppingToken);

        }
    }
}