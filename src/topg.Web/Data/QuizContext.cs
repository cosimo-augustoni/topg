using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace topg.Web.Data
{
    public class QuizContext(DbContextOptions<QuizContext> options) : DbContext(options)
    {
        public DbSet<QuizTemplate> Templates { get; set; }
        public DbSet<Board> Boards { get; set; }
        public DbSet<Question> Questions { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Question>()
                .HasDiscriminator(x => x.QuestionType)
                .HasValue<TextQuestion>(QuestionType.Text)
                .HasValue<SoundQuestion>(QuestionType.Sound)
                .HasValue<ImageQuestion>(QuestionType.Image);
        }
    }

    public abstract record Question
    {
        public long Id { get; init; }
        public QuestionType QuestionType { get; init; }
        public AnswerType AnswerType { get; init; }
        public int Points { get; init; }
        public required string Category { get; init; }
        public int Order => field == 0 ? Points : field;
    }

    public record TextQuestion : Question
    {
        public required string QuestionText { get; init; }
    }

    public record SoundQuestion : Question
    {

    }

    public record ImageQuestion : Question
    {

    }

    public enum QuestionType
    {
        Text = 0,
        Sound = 1,
        Image = 2,
    }

    public class QuizContextFactory : IDesignTimeDbContextFactory<QuizContext>
    {
        public QuizContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<QuizContext>();
            optionsBuilder.UseNpgsql("Host=localhost;Database=topg;Username=postgres;Password=postgres");
            return new QuizContext(optionsBuilder.Options);
        }
    }

    public enum AnswerType
    {
        Buzzer = 0,
        Text = 1
    }

    public record Board
    {
        public long Id { get; init; }
        public required QuizTemplate Template { get; init; }
        public int Order { get; init; }
    }

    public record QuizTemplate
    {
        public long Id { get; init; }
        public required string Name { get; init; }
        public required List<Board> Boards { get; init; }
    }
}
