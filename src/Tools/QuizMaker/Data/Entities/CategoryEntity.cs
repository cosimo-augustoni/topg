namespace QuizMaker.Data.Entities;

public class CategoryEntity
{
    public long Id { get; set; }
    public long BoardId { get; set; }
    public required string Name { get; set; }

    public List<QuestionEntity> Questions { get; set; } = new();
}
