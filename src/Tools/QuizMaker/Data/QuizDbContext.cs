using Microsoft.EntityFrameworkCore;
using QuizMaker.Data.Entities;

namespace QuizMaker.Data;

public class QuizDbContext : DbContext
{
    public QuizDbContext(DbContextOptions<QuizDbContext> options) : base(options)
    {
    }

    public DbSet<TemplateEntity> Templates => Set<TemplateEntity>();
    public DbSet<BoardEntity> Boards => Set<BoardEntity>();
    public DbSet<CategoryEntity> Categories => Set<CategoryEntity>();
    public DbSet<QuestionEntity> Questions => Set<QuestionEntity>();
    public DbSet<MetadataEntity> Metadata => Set<MetadataEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TemplateEntity>(eb =>
        {
            eb.HasKey(t => t.Id);
            eb.Property(t => t.Name).IsRequired();
            eb.Property(t => t.SafeFolderName).IsRequired();
        });

        modelBuilder.Entity<BoardEntity>(eb =>
        {
            eb.HasKey(b => b.Id);
            eb.Property(b => b.Order).IsRequired();
            eb.HasOne<TemplateEntity>().WithMany(t => t.Boards).HasForeignKey(b => b.TemplateId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CategoryEntity>(eb =>
        {
            eb.HasKey(c => c.Id);
            eb.Property(c => c.Name).IsRequired();
            eb.HasOne<BoardEntity>().WithMany(b => b.Categories).HasForeignKey(c => c.BoardId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<QuestionEntity>(eb =>
        {
            eb.HasKey(q => q.Id);
            eb.Property(q => q.Points).IsRequired();
            eb.Property(q => q.QuestionType).IsRequired();
            eb.HasOne<CategoryEntity>().WithMany(c => c.Questions).HasForeignKey(q => q.CategoryId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MetadataEntity>(eb =>
        {
            eb.HasKey(m => m.Key);
            eb.Property(m => m.Value).IsRequired();
        });
    }
}
