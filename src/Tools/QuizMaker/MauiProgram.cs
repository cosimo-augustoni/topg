using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
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
                db.Database.EnsureCreated();
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
    }
}
