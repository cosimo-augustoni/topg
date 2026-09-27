using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using topg.Web.Templating.Data;

namespace topg.MigrationService;

public class Worker(IServiceProvider serviceProvider,
    IHostApplicationLifetime hostApplicationLifetime) : BackgroundService
{
    public const string ActivitySourceName = "Migrations";
    private static readonly ActivitySource activitySource = new(ActivitySourceName);

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        using var activity = activitySource.StartActivity("Migrating database", ActivityKind.Client);

        try
        {
            using var scope = serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<QuizContext>();

            await RunMigrationAsync(dbContext, cancellationToken);
        }
        catch (Exception ex)
        {
            activity?.AddException(ex);
            throw;
        }

        hostApplicationLifetime.StopApplication();
    }

    private static async Task RunMigrationAsync(QuizContext dbContext, CancellationToken cancellationToken)
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            const int maxAttempts = 30;
            const int delayMs = 3000;
            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    await dbContext.Database.MigrateAsync(cancellationToken);
                    return;
                }
                catch (Exception) when (attempt < maxAttempts)
                {
                    await Task.Delay(delayMs, cancellationToken);
                }
            }
            await dbContext.Database.MigrateAsync(cancellationToken);
        });
    }
}
