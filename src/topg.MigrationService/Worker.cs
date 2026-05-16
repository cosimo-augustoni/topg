using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using topg.Web.Templating.Data;
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
                        ("What is the unit of force?",                      "Newton"),
                        ("What is the speed of light?",                     "299,792,458 m/s"),
                        ("What is Newton's first law?",                     "An object at rest stays at rest unless acted upon by a force"),
                        ("What is the formula for kinetic energy?",         "½mv²"),
                        ("What is the unit of electrical resistance?",      "Ohm"),
                        ("What is the SI unit of temperature?",             "Kelvin"),
                    ]),
                    ("Chemistry",   [
                        ("What is the chemical symbol for gold?",           "Au"),
                        ("What is the most abundant gas in Earth's atmosphere?", "Nitrogen"),
                        ("What is the atomic number of carbon?",            "6"),
                        ("What is the pH of pure water?",                   "7"),
                        ("What is the chemical formula for water?",         "H₂O"),
                        ("What is oxidation?",                              "The loss of electrons"),
                    ]),
                    ("Biology",     [
                        ("What is the powerhouse of the cell?",             "Mitochondria"),
                        ("How many chromosomes do humans have?",            "46"),
                        ("What is the process by which plants make food?",  "Photosynthesis"),
                        ("What carries oxygen in the blood?",               "Red blood cells / Haemoglobin"),
                        ("What is the largest organ in the human body?",    "Skin"),
                        ("What is DNA short for?",                          "Deoxyribonucleic acid"),
                    ]),
                    ("Astronomy",   [
                        ("What is the closest star to Earth?",              "The Sun"),
                        ("How many planets are in our solar system?",       "8"),
                        ("What is a light-year?",                           "The distance light travels in one year"),
                        ("What is the largest planet in our solar system?", "Jupiter"),
                        ("What is a black hole?",                           "A region of space where gravity is so strong nothing can escape"),
                        ("What galaxy do we live in?",                      "The Milky Way"),
                    ]),
                    ("Mathematics", [
                        ("What is pi rounded to two decimal places?",       "3.14"),
                        ("What is the square root of 144?",                 "12"),
                        ("What is the sum of angles in a triangle?",        "180 degrees"),
                        ("What is a prime number?",                         "A number divisible only by 1 and itself"),
                        ("What is 2 to the power of 10?",                   "1024"),
                        ("What is the Pythagorean theorem?",                "a² + b² = c²"),
                    ]),
                ],
                // Board 2
                [
                    ("Physics II",  [
                        ("What is the unit of power?",                      "Watt"),
                        ("What is the unit of electric charge?",            "Coulomb"),
                        ("What is the law of conservation of energy?",      "Energy cannot be created or destroyed"),
                        ("What is absolute zero in Celsius?",               "-273.15 °C"),
                        ("What is the speed of sound in air?",              "343 m/s"),
                        ("What is the unit of frequency?",                  "Hertz"),
                    ]),
                    ("Chemistry II",[
                        ("What is the chemical symbol for silver?",         "Ag"),
                        ("What is a covalent bond?",                        "A bond formed by sharing electrons"),
                        ("What is the atomic number of oxygen?",            "8"),
                        ("What is the most reactive metal?",                "Caesium"),
                        ("What gas do plants absorb during photosynthesis?","Carbon dioxide"),
                        ("What is an isotope?",                             "Atoms of the same element with different numbers of neutrons"),
                    ]),
                    ("Biology II",  [
                        ("What is the basic unit of life?",                 "The cell"),
                        ("What organ produces insulin?",                    "The pancreas"),
                        ("What is the function of white blood cells?",      "Fight infection / immune response"),
                        ("What is meiosis?",                                "Cell division that produces gametes"),
                        ("What is the largest bone in the body?",           "Femur"),
                        ("What is the role of ribosomes?",                  "Protein synthesis"),
                    ]),
                    ("Astronomy II",[
                        ("What is the hottest planet in our solar system?", "Venus"),
                        ("What is a nebula?",                               "A cloud of gas and dust in space"),
                        ("How long does light from the Sun take to reach Earth?", "About 8 minutes"),
                        ("What force keeps planets in orbit?",              "Gravity"),
                        ("What is a supernova?",                            "The explosion of a massive star"),
                        ("What planet has the most moons?",                 "Saturn"),
                    ]),
                    ("Mathematics II",[
                        ("What is the value of 0!?",                        "1"),
                        ("What is a right angle in degrees?",               "90 degrees"),
                        ("What is the area formula for a circle?",          "πr²"),
                        ("What is a rational number?",                      "A number that can be expressed as a fraction"),
                        ("What is the sum of the first 10 natural numbers?","55"),
                        ("What does the integral symbol represent?",        "The area under a curve"),
                    ]),
                ],
            ]),
            CreateTemplate("Pop Culture & History",
            [
                // Board 1
                [
                    ("Movies",      [
                        ("Who directed Jurassic Park?",                                     "Steven Spielberg"),
                        ("What year was the first Star Wars film released?",                "1977"),
                        ("Which film features the line 'You can't handle the truth!'?",     "A Few Good Men"),
                        ("Who played Iron Man in the MCU?",                                 "Robert Downey Jr."),
                        ("What is the highest-grossing film of all time?",                  "Avatar"),
                        ("Which Disney film features the song 'Let It Go'?",                "Frozen"),
                    ]),
                    ("Music",       [
                        ("Who is known as the King of Pop?",                "Michael Jackson"),
                        ("Which band performed 'Bohemian Rhapsody'?",       "Queen"),
                        ("What instrument has 88 keys?",                    "Piano"),
                        ("Who sang 'Rolling in the Deep'?",                 "Adele"),
                        ("Which artist released the album 'Thriller'?",     "Michael Jackson"),
                        ("What nationality is Ed Sheeran?",                 "British"),
                    ]),
                    ("History",     [
                        ("In what year did World War II end?",              "1945"),
                        ("Who was the first president of the United States?","George Washington"),
                        ("What ancient wonder was located in Alexandria?",  "The Lighthouse of Alexandria"),
                        ("In which country did the French Revolution take place?", "France"),
                        ("Who wrote the Declaration of Independence?",      "Thomas Jefferson"),
                        ("What empire was ruled by Julius Caesar?",         "The Roman Empire"),
                    ]),
                    ("Geography",   [
                        ("What is the capital of Australia?",               "Canberra"),
                        ("Which is the longest river in the world?",        "The Nile"),
                        ("On which continent is the Sahara Desert?",        "Africa"),
                        ("What is the smallest country in the world?",      "Vatican City"),
                        ("Which country has the most natural lakes?",       "Canada"),
                        ("What is the tallest mountain in the world?",      "Mount Everest"),
                    ]),
                    ("Sports",      [
                        ("How many players are on a basketball team on the court?", "5"),
                        ("In which sport is the term 'love' used?",         "Tennis"),
                        ("How many holes are on a standard golf course?",   "18"),
                        ("What country invented the Olympic Games?",        "Greece"),
                        ("How long is a marathon in kilometres?",           "42.195 km"),
                        ("Which sport uses a puck?",                        "Ice hockey"),
                    ]),
                ],
                // Board 2
                [
                    ("Movies II",   [
                        ("Who played Jack in Titanic?",                     "Leonardo DiCaprio"),
                        ("What film features a character named Forrest Gump?", "Forrest Gump"),
                        ("Which superhero is also known as the Dark Knight?","Batman"),
                        ("What 1994 film features the line 'Life is like a box of chocolates'?", "Forrest Gump"),
                        ("Who directed The Dark Knight?",                   "Christopher Nolan"),
                        ("In which film does Simba appear?",                "The Lion King"),
                    ]),
                    ("Music II",    [
                        ("Who sang 'Baby One More Time'?",                  "Britney Spears"),
                        ("Which band is known for the album 'Back in Black'?","AC/DC"),
                        ("What is the best-selling album of all time?",     "Thriller by Michael Jackson"),
                        ("Who is known as the Queen of Pop?",               "Madonna"),
                        ("Which country does ABBA come from?",              "Sweden"),
                        ("Who wrote the opera The Magic Flute?",            "Wolfgang Amadeus Mozart"),
                    ]),
                    ("History II",  [
                        ("Who was the first man to walk on the Moon?",      "Neil Armstrong"),
                        ("In what year did the Berlin Wall fall?",          "1989"),
                        ("Who was the first female prime minister of the UK?","Margaret Thatcher"),
                        ("What year did Christopher Columbus reach the Americas?", "1492"),
                        ("Which country was the first to grant women the right to vote?", "New Zealand"),
                        ("Who was the last pharaoh of ancient Egypt?",      "Cleopatra VII"),
                    ]),
                    ("Geography II",[
                        ("What is the capital of Canada?",                  "Ottawa"),
                        ("Which desert is the largest in the world?",       "The Antarctic Desert"),
                        ("What is the longest mountain range in the world?","The Andes"),
                        ("Which ocean is the largest?",                     "The Pacific Ocean"),
                        ("What country has the most time zones?",           "France"),
                        ("What is the capital of Brazil?",                  "Brasília"),
                    ]),
                    ("Sports II",   [
                        ("How many players are on a football (soccer) team?","11"),
                        ("What is the maximum score in ten-pin bowling?",   "300"),
                        ("How long is an Olympic swimming pool?",           "50 metres"),
                        ("In what sport would you perform a slam dunk?",    "Basketball"),
                        ("Which country has won the most FIFA World Cups?", "Brazil"),
                        ("How many sets are in a standard volleyball match?","Best of 5"),
                    ]),
                ],
            ]),
        };

        dbContext.Templates.AddRange(templates);
        await dbContext.SaveChangesAsync(cancellationToken);

        QuizTemplate CreateTemplate(string name, (string category, (string question, string answer)[] entries)[][] boards)
        {
            var boardList = boards.Select((categories, boardIndex) =>
            {
                var questions = categories.SelectMany(c => c.entries.Select((e, i) => (Question)new TextQuestion
                {
                    QuestionType = QuestionType.Text,
                    AnswerType = AnswerType.Buzzer,
                    Category = c.category,
                    Points = pointValues[i],
                    QuestionText = e.question,
                    CorrectAnswer = e.answer,
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
