namespace QuizMaker.Data.Entities;

public class BoardEntity
{
    public long Id { get; set; }
    public long TemplateId { get; set; }
    public int Order { get; set; }

    public List<CategoryEntity> Categories { get; set; } = new();
}
