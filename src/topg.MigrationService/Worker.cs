using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using topg.Web.Templating.Data;
using topg.Web.Templating.DomainObjects;

namespace topg.MigrationService;

internal record SeedingQuestion(string Question, string Answer, string? ImageUri = null);

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
            // Final attempt — let any exception propagate naturally
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
            CreateTemplate("Science & Nature",
            [
                // Board 1
                [
                    ("Physics",     [
                        new("What is the unit of force?",                      "Newton"),
                        new("What is the speed of light?",                     "299,792,458 m/s"),
                        new("What is Newton's first law?",                     "An object at rest stays at rest unless acted upon by a force"),
                        new("What is the formula for kinetic energy?",         "½mv²"),
                        new("What is the unit of electrical resistance?",      "Ohm"),
                        new("What is the SI unit of temperature?",             "Kelvin"),
                    ]),
                    ("Chemistry",   [
                        new("What is the chemical symbol for gold?",           "Au"),
                        new("What is the most abundant gas in Earth's atmosphere?", "Nitrogen"),
                        new("What is the atomic number of carbon?",            "6"),
                        new("What is the pH of pure water?",                   "7"),
                        new("What is the chemical formula for water?",         "H₂O"),
                        new("What is oxidation?",                              "The loss of electrons"),
                    ]),
                    ("Biology",     [
                        new("What is the powerhouse of the cell?",             "Mitochondria"),
                        new("How many chromosomes do humans have?",            "46"),
                        new("What is the process by which plants make food?",  "Photosynthesis"),
                        new("What carries oxygen in the blood?",               "Red blood cells / Haemoglobin"),
                        new("What is the largest organ in the human body?",    "Skin"),
                        new("What is DNA short for?",                          "Deoxyribonucleic acid"),
                    ]),
                    ("Astronomy",   [
                        new("What is the closest star to Earth?",              "The Sun"),
                        new("How many planets are in our solar system?",       "8"),
                        new("What is a light-year?",                           "The distance light travels in one year"),
                        new("What is the largest planet in our solar system?", "Jupiter"),
                        new("What is a black hole?",                           "A region of space where gravity is so strong nothing can escape"),
                        new("What galaxy do we live in?",                      "The Milky Way"),
                    ]),
                    ("Mathematics", [
                        new("What is pi rounded to two decimal places?",       "3.14"),
                        new("What is the square root of 144?",                 "12"),
                        new("What is the sum of angles in a triangle?",        "180 degrees"),
                        new("What is a prime number?",                         "A number divisible only by 1 and itself"),
                        new("What is 2 to the power of 10?",                   "1024"),
                        new("What is the Pythagorean theorem?",                "a² + b² = c²"),
                    ]),
                ],
                // Board 2
                [
                    ("Physics II",  [
                        new("What is the unit of power?",                      "Watt"),
                        new("What is the unit of electric charge?",            "Coulomb"),
                        new("What is the law of conservation of energy?",      "Energy cannot be created or destroyed"),
                        new("What is absolute zero in Celsius?",               "-273.15 °C"),
                        new("What is the speed of sound in air?",              "343 m/s"),
                        new("What is the unit of frequency?",                  "Hertz"),
                    ]),
                    ("Chemistry II",[
                        new("What is the chemical symbol for silver?",         "Ag"),
                        new("What is a covalent bond?",                        "A bond formed by sharing electrons"),
                        new("What is the atomic number of oxygen?",            "8"),
                        new("What is the most reactive metal?",                "Caesium"),
                        new("What gas do plants absorb during photosynthesis?","Carbon dioxide"),
                        new("What is an isotope?",                             "Atoms of the same element with different numbers of neutrons"),
                    ]),
                    ("Biology II",  [
                        new("What is the basic unit of life?",                 "The cell"),
                        new("What organ produces insulin?",                    "The pancreas"),
                        new("What is the function of white blood cells?",      "Fight infection / immune response"),
                        new("What is meiosis?",                                "Cell division that produces gametes"),
                        new("What is the largest bone in the body?",           "Femur"),
                        new("What is the role of ribosomes?",                  "Protein synthesis"),
                    ]),
                    ("Astronomy II",[
                        new("What is the hottest planet in our solar system?", "Venus"),
                        new("What is a nebula?",                               "A cloud of gas and dust in space"),
                        new("How long does light from the Sun take to reach Earth?", "About 8 minutes"),
                        new("What force keeps planets in orbit?",              "Gravity"),
                        new("What is a supernova?",                            "The explosion of a massive star"),
                        new("What planet has the most moons?",                 "Saturn"),
                    ]),
                    ("Mathematics II",[
                        new("What is the value of 0!?",                        "1"),
                        new("What is a right angle in degrees?",               "90 degrees"),
                        new("What is the area formula for a circle?",          "πr²"),
                        new("What is a rational number?",                      "A number that can be expressed as a fraction"),
                        new("What is the sum of the first 10 natural numbers?","55"),
                        new("What does the integral symbol represent?",        "The area under a curve"),
                    ]),
                ],
            ]),
            CreateTemplate("Pop Culture & History",
            [
                // Board 1
                [
                    ("Movies",      [
                        new("From which movie is this Character?",                                     "The Matrix", "https://thegeektwins.com/wp-content/uploads/2019/04/Matrizx-1999-Morpheus-Laurence-Fishburne-600x300-3.jpg"),
                        new("What year was the first Star Wars film released?",                "1977"),
                        new("Which film features the line 'You can't handle the truth!'?",     "A Few Good Men"),
                        new("Who played Iron Man in the MCU?",                                 "Robert Downey Jr."),
                        new("What is the highest-grossing film of all time?",                  "Avatar"),
                        new("Which Disney film features the song 'Let It Go'?",                "Frozen"),
                    ]),
                    ("Music",       [
                        new("Who is known as the King of Pop?",                "Michael Jackson"),
                        new("Which band performed 'Bohemian Rhapsody'?",       "Queen"),
                        new("What instrument has 88 keys?",                    "Piano"),
                        new("Who sang 'Rolling in the Deep'?",                 "Adele"),
                        new("Which artist released the album 'Thriller'?",     "Michael Jackson"),
                        new("What nationality is Ed Sheeran?",                 "British"),
                    ]), new
                    ("History",     [
                        new("In what year did World War II end?",              "1945"),
                        new("Who was the first president of the United States?","George Washington"),
                        new("What ancient wonder was located in Alexandria?",  "The Lighthouse of Alexandria"),
                        new("In which country did the French Revolution take place?", "France"),
                        new("Who wrote the Declaration of Independence?",      "Thomas Jefferson"),
                        new("What empire was ruled by Julius Caesar?",         "The Roman Empire"),
                    ]), new
                    ("Geography",   [
                        new("What is the capital of Australia?",               "Canberra"),
                        new("Which is the longest river in the world?",        "The Nile"),
                        new("On which continent is the Sahara Desert?",        "Africa"),
                        new("What is the smallest country in the world?",      "Vatican City"),
                        new("Which country has the most natural lakes?",       "Canada"),
                        new("What is the tallest mountain in the world?",      "Mount Everest"),
                    ]), new
                    ("Sports",      [
                        new("How many players are on a basketball team on the court?", "5"),
                        new("In which sport is the term 'love' used?",         "Tennis"),
                        new("How many holes are on a standard golf course?",   "18"),
                        new("What country invented the Olympic Games?",        "Greece"),
                        new("How long is a marathon in kilometres?",           "42.195 km"),
                        new("Which sport uses a puck?",                        "Ice hockey"),
                    ]),
                ],
                // Board 2
                [
                    ("Movies II",   [
                        new("Who played Jack in Titanic?",                     "Leonardo DiCaprio"),
                        new("What film features a character named Forrest Gump?", "Forrest Gump"),
                        new("Which superhero is also known as the Dark Knight?","Batman"),
                        new("What 1994 film features the line 'Life is like a box of chocolates'?", "Forrest Gump"),
                        new("Who directed The Dark Knight?",                   "Christopher Nolan"),
                        new("In which film does Simba appear?",                "The Lion King"),
                    ]), new
                    ("Music II",    [
                        new("Who sang 'Baby One More Time'?",                  "Britney Spears"),
                        new("Which band is known for the album 'Back in Black'?","AC/DC"),
                        new("What is the best-selling album of all time?",     "Thriller by Michael Jackson"),
                        new("Who is known as the Queen of Pop?",               "Madonna"),
                        new("Which country does ABBA come from?",              "Sweden"),
                        new("Who wrote the opera The Magic Flute?",            "Wolfgang Amadeus Mozart"),
                    ]), new
                    ("History II",  [
                        new("Who was the first man to walk on the Moon?",      "Neil Armstrong"),
                        new("In what year did the Berlin Wall fall?",          "1989"),
                        new("Who was the first female prime minister of the UK?","Margaret Thatcher"),
                        new("What year did Christopher Columbus reach the Americas?", "1492"),
                        new("Which country was the first to grant women the right to vote?", "New Zealand"),
                        new("Who was the last pharaoh of ancient Egypt?",      "Cleopatra VII"),
                    ]), new
                    ("Geography II",[
                        new("What is the capital of Canada?",                  "Ottawa"),
                        new("Which desert is the largest in the world?",       "The Antarctic Desert"),
                        new("What is the longest mountain range in the world?","The Andes"),
                        new("Which ocean is the largest?",                     "The Pacific Ocean"),
                        new("What country has the most time zones?",           "France"),
                        new("What is the capital of Brazil?",                  "Brasília"),
                    ]), new
                    ("Sports II",   [
                        new("How many players are on a football (soccer) team?","11"),
                        new("What is the maximum score in ten-pin bowling?",   "300"),
                        new("How long is an Olympic swimming pool?",           "50 metres"),
                        new("In what sport would you perform a slam dunk?",    "Basketball"),
                        new("Which country has won the most FIFA World Cups?", "Brazil"),
                        new("How many sets are in a standard volleyball match?","Best of 5"),
                    ]),
                ],
            ]),
        };

        dbContext.Templates.AddRange(templates);
        await dbContext.SaveChangesAsync(cancellationToken);

        QuizTemplate CreateTemplate(string name, (string category, SeedingQuestion[] entries)[][] boards)
        {
            var boardList = boards.Select((categories, boardIndex) =>
            {
                var questions = categories.SelectMany(c => c.entries.Select((e, i) =>
                {
                    if (e.ImageUri != null)
                    {
                        return (Question)new ImageQuestion()
                        {
                            QuestionType = QuestionType.Text,
                            AnswerType = AnswerType.Buzzer,
                            Category = c.category,
                            Points = pointValues[i],
                            ImageUri = e.ImageUri,
                            QuestionText = e.Question,
                            CorrectAnswer = e.Answer,
                        };
                    
                    }

                    return (Question)new TextQuestion
                    {
                        QuestionType = QuestionType.Text,
                        AnswerType = AnswerType.Buzzer,
                        Category = c.category,
                        Points = pointValues[i],
                        QuestionText = e.Question,
                        CorrectAnswer = e.Answer,
                    };
                })).ToList();

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
