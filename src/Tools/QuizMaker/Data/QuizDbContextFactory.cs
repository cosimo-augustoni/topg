using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace QuizMaker.Data;

// Used only by the EF Core command-line tools (dotnet ef) at design time so that
// migrations can be scaffolded without spinning up the full MAUI host. The connection
// string here is a throwaway; migrations never touch a real database during scaffolding.
public class QuizDbContextFactory : IDesignTimeDbContextFactory<QuizDbContext>
{
    public QuizDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<QuizDbContext>()
            .UseSqlite("Data Source=quizmaker-design.db")
            .Options;
        return new QuizDbContext(options);
    }
}
