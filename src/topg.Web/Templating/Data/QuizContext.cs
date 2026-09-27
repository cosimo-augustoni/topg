using topg.Web.Client.Shared;
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

            // Only image questions have this column, so it is nullable in the shared table. Import scripts exported
            // before the flag existed leave it out, and the default keeps those rows loadable as a non-nullable bool.
            modelBuilder.Entity<ImageQuestion>()
                .Property(x => x.StartObscured)
                .HasDefaultValue(false);

            // The import script's "replace existing" deletes questions and relies on their hints going with them.
            modelBuilder.Entity<TextQuestion>()
                .HasMany(x => x.Hints)
                .WithOne()
                .HasForeignKey("QuestionId")
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<QuestionHint>(hint =>
            {
                hint.ToTable("QuestionHints");
                hint.HasIndex("QuestionId", nameof(QuestionHint.Order)).IsUnique();
            });
        }
    }
}
