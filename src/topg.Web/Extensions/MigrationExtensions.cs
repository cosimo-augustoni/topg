using Microsoft.EntityFrameworkCore;
using topg.Web.Templating.Data;

namespace topg.Web.Extensions;

public static class MigrationExtensions
{
    public static async Task WaitForMigrationsAsync(this WebApplication app, int maxAttempts = 30, int delayMs = 3000)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QuizContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<QuizContext>>();

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                var pending = await db.Database.GetPendingMigrationsAsync();
                if (!pending.Any())
                {
                    logger.LogInformation("All migrations applied, starting web application.");
                    return;
                }
                logger.LogInformation("Waiting for migrations ({Attempt}/{Max})...", attempt, maxAttempts);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not reach database ({Attempt}/{Max}), retrying...", attempt, maxAttempts);
            }

            if (attempt == maxAttempts)
                throw new TimeoutException("Database migrations did not complete in time.");

            await Task.Delay(delayMs);
        }
    }
}
