using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;
using CommunityToolkit.Maui;
using QuizMaker.Data;
using QuizMaker.Data.Repositories;
using QuizMaker.Services;

namespace QuizMaker
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseMauiCommunityToolkit()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                });

            builder.Services.AddMauiBlazorWebView();

            // DbContext
            var dbAppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var dbFolder = System.IO.Path.Combine(dbAppData, "QuizMaker");
            System.IO.Directory.CreateDirectory(dbFolder);
            var dbPath = System.IO.Path.Combine(dbFolder, "quizzes.db");
            builder.Services.AddDbContext<QuizMaker.Data.QuizDbContext>(options => options.UseSqlite($"Data Source={dbPath}"));

            // Repositories & services
            builder.Services.AddScoped<QuizMaker.Data.Repositories.IQuizRepository, QuizMaker.Data.Repositories.QuizRepository>();
            builder.Services.AddScoped<QuizMaker.Services.MediaService>();
            builder.Services.AddScoped<QuizMaker.Services.SqlExportService>();
            builder.Services.AddScoped<QuizMaker.Services.ConfigService>();

#if DEBUG
    		builder.Services.AddBlazorWebViewDeveloperTools();
    		builder.Logging.AddDebug();
#endif

            var app = builder.Build();

            // Ensure database created on first run
            try
            {
                using var scope = app.Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<QuizDbContext>();
                InitializeDatabase(db);
            }
            catch (Exception ex)
            {
                try
                {
                    var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                    var folder = System.IO.Path.Combine(appData, "QuizMaker");
                    System.IO.Directory.CreateDirectory(folder);
                    var logPath = System.IO.Path.Combine(folder, "last-error.log");
                    System.IO.File.WriteAllText(logPath, ex.ToString());
                }
                catch { }
            }

            // Register global exception handlers to capture unobserved exceptions during runtime
            try
            {
                var logAppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var folder = System.IO.Path.Combine(logAppData, "QuizMaker");
                System.IO.Directory.CreateDirectory(folder);
                var logPath = System.IO.Path.Combine(folder, "last-error.log");

                AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                {
                    try { System.IO.File.WriteAllText(logPath, e.ExceptionObject?.ToString() ?? "UnhandledException"); } catch { }
                };

                TaskScheduler.UnobservedTaskException += (s, e) =>
                {
                    try { System.IO.File.WriteAllText(logPath, e.Exception.ToString()); } catch { }
                };
            }
            catch { }

            return app;
        }

        // Applies EF Core migrations to bring the local SQLite database up to date.
        // Databases created by an older build (which used EnsureCreated()) have no migrations
        // history, so Migrate() would try to recreate existing tables and fail. In that case we
        // first baseline the database to the InitialCreate migration — using EF's own history
        // scripts — so that Migrate() only applies the remaining migrations and existing data is
        // preserved. Brand-new databases are created entirely by Migrate().
        private const string InitialMigrationId = "20260607113542_InitialCreate";

        private static void InitializeDatabase(QuizDbContext db)
        {
            var creator = db.Database.GetService<IRelationalDatabaseCreator>();
            var history = db.Database.GetService<IHistoryRepository>();

            var databaseExists = creator.Exists();
            var hasLegacySchema = databaseExists && creator.HasTables() && !history.Exists();

            if (hasLegacySchema)
            {
                // Pre-migrations database created by EnsureCreated(): register the create-table
                // baseline as already applied without re-running it.
                db.Database.ExecuteSqlRaw(history.GetCreateScript());
                db.Database.ExecuteSqlRaw(history.GetInsertScript(new HistoryRow(InitialMigrationId, ProductInfo.GetVersion())));
            }

            db.Database.Migrate();
        }
    }
}
