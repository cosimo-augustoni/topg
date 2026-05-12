using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using topg.Web.Data;
using topg.Web.Templating.DomainObjects;

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
            await SeedAsync(dbContext, cancellationToken);
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
            await dbContext.Database.MigrateAsync(cancellationToken);
        });
    }

    private static async Task SeedAsync(QuizContext dbContext, CancellationToken cancellationToken)
    {
        if (await dbContext.Templates.AnyAsync(cancellationToken))
            return;

        int[] pointValues = [100, 200, 300, 400, 500, 600];

        var templates = new List<QuizTemplate>
        {
            CreateTemplate("Science & Nature", [
                ("Physics",     ["What is the unit of force?", "What is the speed of light?", "What is Newton's first law?", "What is the formula for kinetic energy?", "What is the unit of electrical resistance?", "What is the SI unit of temperature?"]),
                ("Chemistry",   ["What is the chemical symbol for gold?", "What is the most abundant gas in Earth's atmosphere?", "What is the atomic number of carbon?", "What is the pH of pure water?", "What is the chemical formula for water?", "What is oxidation?"]),
                ("Biology",     ["What is the powerhouse of the cell?", "How many chromosomes do humans have?", "What is the process by which plants make food?", "What carries oxygen in the blood?", "What is the largest organ in the human body?", "What is DNA short for?"]),
                ("Astronomy",   ["What is the closest star to Earth?", "How many planets are in our solar system?", "What is a light-year?", "What is the largest planet in our solar system?", "What is a black hole?", "What galaxy do we live in?"]),
                ("Mathematics", ["What is pi rounded to two decimal places?", "What is the square root of 144?", "What is the sum of angles in a triangle?", "What is a prime number?", "What is 2 to the power of 10?", "What is the Pythagorean theorem?"]),
            ]),
            CreateTemplate("Pop Culture & History", [
                ("Movies",      ["Who directed Jurassic Park?", "What year was the first Star Wars film released?", "Which film features the line 'You can't handle the truth!'?", "Who played Iron Man in the MCU?", "What is the highest-grossing film of all time?", "Which Disney film features the song 'Let It Go'?"]),
                ("Music",       ["Who is known as the King of Pop?", "Which band performed 'Bohemian Rhapsody'?", "What instrument has 88 keys?", "Who sang 'Rolling in the Deep'?", "Which artist released the album 'Thriller'?", "What nationality is Ed Sheeran?"]),
                ("History",     ["In what year did World War II end?", "Who was the first president of the United States?", "What ancient wonder was located in Alexandria?", "In which country did the French Revolution take place?", "Who wrote the Declaration of Independence?", "What empire was ruled by Julius Caesar?"]),
                ("Geography",   ["What is the capital of Australia?", "Which is the longest river in the world?", "On which continent is the Sahara Desert?", "What is the smallest country in the world?", "Which country has the most natural lakes?", "What is the tallest mountain in the world?"]),
                ("Sports",      ["How many players are on a basketball team on the court?", "In which sport is the term 'love' used?", "How many holes are on a standard golf course?", "What country invented the Olympic Games?", "How long is a marathon in kilometres?", "Which sport uses a puck?"]),
            ]),
        };

        dbContext.Templates.AddRange(templates);
        await dbContext.SaveChangesAsync(cancellationToken);

        QuizTemplate CreateTemplate(string name, (string category, string[] questions)[] boards)
        {
            var boardList = boards.Select((b, boardIndex) =>
            {
                var questions = b.questions.Select((q, i) => (TextQuestion)(new TextQuestion
                {
                    QuestionType = QuestionType.Text,
                    AnswerType = AnswerType.Buzzer,
                    Category = b.category,
                    Points = pointValues[i],
                    QuestionText = q,
                })).Cast<Question>().ToList();

                return new Board
                {
                    Order = boardIndex + 1,
                    Questions = questions,
                    Template = null!,  // set by EF via the owning template
                };
            }).ToList();

            return new QuizTemplate
            {
                Name = name,
                Boards = boardList,
            };
        }
    }
}
