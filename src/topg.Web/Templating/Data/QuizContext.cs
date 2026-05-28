using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using topg.Web.Templating.DomainObjects;

namespace topg.Web.Templating.Data
{
    public class QuizContext(DbContextOptions<QuizContext> options) : DbContext(options), IDataProtectionKeyContext
    {
        public DbSet<QuizTemplate> Templates { get; set; }
        public DbSet<Board> Boards { get; set; }
        public DbSet<Question> Questions { get; set; }
        public DbSet<DataProtectionKey> DataProtectionKeys { get; set; }

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
}
