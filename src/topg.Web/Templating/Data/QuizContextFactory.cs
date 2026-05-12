using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace topg.Web.Templating.Data;

public class QuizContextFactory : IDesignTimeDbContextFactory<QuizContext>
{
    public QuizContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<QuizContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Database=topg;Username=postgres;Password=postgres");
        return new QuizContext(optionsBuilder.Options);
    }
}