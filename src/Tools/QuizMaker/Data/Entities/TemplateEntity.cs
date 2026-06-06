namespace QuizMaker.Data.Entities;

public class TemplateEntity
{
    public long Id { get; set; }
    public required string Name { get; set; }
    // safe folder name for media storage
    public required string SafeFolderName { get; set; }
    // next media number
    public int MediaCounter { get; set; }

    public List<BoardEntity> Boards { get; set; } = new();
}
