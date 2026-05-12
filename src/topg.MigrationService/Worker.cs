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
        await dbContext.Questions.ExecuteDeleteAsync(cancellationToken);
        await dbContext.Boards.ExecuteDeleteAsync(cancellationToken);
        await dbContext.Templates.ExecuteDeleteAsync(cancellationToken);

        int[] pointValues = [100, 200, 300, 400, 500, 600];

        var templates = new List<QuizTemplate>
        {
            CreateTemplate("Science & Nature", [
                ("Physics",     [
                    ("What is the unit of force?",                       "Newton"),
                    ("What is the speed of light?",                      "299,792,458 m/s"),
                    ("What is Newton's first law?",                      "An object at rest stays at rest unless acted upon by a force"),
                    ("What is the formula for kinetic energy?",          "½mv²"),
                    ("What is the unit of electrical resistance?",       "Ohm"),
                    ("What is the SI unit of temperature?",              "Kelvin"),
                ]),
                ("Chemistry",   [
                    ("What is the chemical symbol for gold?",            "Au"),
                    ("What is the most abundant gas in Earth's atmosphere?", "Nitrogen"),
                    ("What is the atomic number of carbon?",             "6"),
                    ("What is the pH of pure water?",                    "7"),
                    ("What is the chemical formula for water?",          "H₂O"),
                    ("What is oxidation?",                               "The loss of electrons"),
                ]),
                ("Biology",     [
                    ("What is the powerhouse of the cell?",              "Mitochondria"),
                    ("How many chromosomes do humans have?",             "46"),
                    ("What is the process by which plants make food?",   "Photosynthesis"),
                    ("What carries oxygen in the blood?",                "Red blood cells / Haemoglobin"),
                    ("What is the largest organ in the human body?",     "Skin"),
                    ("What is DNA short for?",                           "Deoxyribonucleic acid"),
                ]),
                ("Astronomy",   [
                    ("What is the closest star to Earth?",               "The Sun"),
                    ("How many planets are in our solar system?",        "8"),
                    ("What is a light-year?",                            "The distance light travels in one year"),
                    ("What is the largest planet in our solar system?",  "Jupiter"),
                    ("What is a black hole?",                            "A region of space where gravity is so strong nothing can escape"),
                    ("What galaxy do we live in?",                       "The Milky Way"),
                ]),
                ("Mathematics", [
                    ("What is pi rounded to two decimal places?",        "3.14"),
                    ("What is the square root of 144?",                  "12"),
                    ("What is the sum of angles in a triangle?",         "180 degrees"),
                    ("What is a prime number?",                          "A number divisible only by 1 and itself"),
                    ("What is 2 to the power of 10?",                    "1024"),
                    ("What is the Pythagorean theorem?",                 "a² + b² = c²"),
                ]),
            ]),
            CreateTemplate("Pop Culture & History", [
                ("Movies",      [
                    ("Who directed Jurassic Park?",                                          "Steven Spielberg"),
                    ("What year was the first Star Wars film released?",                     "1977"),
                    ("Which film features the line 'You can't handle the truth!'?",          "A Few Good Men"),
                    ("Who played Iron Man in the MCU?",                                      "Robert Downey Jr."),
                    ("What is the highest-grossing film of all time?",                       "Avatar"),
                    ("Which Disney film features the song 'Let It Go'?",                     "Frozen"),
                ]),
                ("Music",       [
                    ("Who is known as the King of Pop?",                 "Michael Jackson"),
                    ("Which band performed 'Bohemian Rhapsody'?",        "Queen"),
                    ("What instrument has 88 keys?",                     "Piano"),
                    ("Who sang 'Rolling in the Deep'?",                  "Adele"),
                    ("Which artist released the album 'Thriller'?",      "Michael Jackson"),
                    ("What nationality is Ed Sheeran?",                  "British"),
                ]),
                ("History",     [
                    ("In what year did World War II end?",               "1945"),
                    ("Who was the first president of the United States?","George Washington"),
                    ("What ancient wonder was located in Alexandria?",   "The Lighthouse of Alexandria"),
                    ("In which country did the French Revolution take place?", "France"),
                    ("Who wrote the Declaration of Independence?",       "Thomas Jefferson"),
                    ("What empire was ruled by Julius Caesar?",          "The Roman Empire"),
                ]),
                ("Geography",   [
                    ("What is the capital of Australia?",                "Canberra"),
                    ("Which is the longest river in the world?",         "The Nile"),
                    ("On which continent is the Sahara Desert?",         "Africa"),
                    ("What is the smallest country in the world?",       "Vatican City"),
                    ("Which country has the most natural lakes?",        "Canada"),
                    ("What is the tallest mountain in the world?",       "Mount Everest"),
                ]),
                ("Sports",      [
                    ("How many players are on a basketball team on the court?", "5"),
                    ("In which sport is the term 'love' used?",          "Tennis"),
                    ("How many holes are on a standard golf course?",    "18"),
                    ("What country invented the Olympic Games?",         "Greece"),
                    ("How long is a marathon in kilometres?",            "42.195 km"),
                    ("Which sport uses a puck?",                         "Ice hockey"),
                ]),
            ]),
        };

        dbContext.Templates.AddRange(templates);
        await dbContext.SaveChangesAsync(cancellationToken);

        QuizTemplate CreateTemplate(string name, (string category, (string question, string answer)[] entries)[] boards)
        {
            var boardList = boards.Select((b, boardIndex) =>
            {
                var questions = b.entries.Select((e, i) => (Question)new TextQuestion
                {
                    QuestionType = QuestionType.Text,
                    AnswerType = AnswerType.Buzzer,
                    Category = b.category,
                    Points = pointValues[i],
                    QuestionText = e.question,
                    CorrectAnswer = e.answer,
                }).ToList();

                return new Board
                {
                    Order = boardIndex + 1,
                    Questions = questions,
                    Template = null!,
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
