namespace QuizMaker.DomainObjects;

public record Board
{
    public long Id { get; init; }
    public required QuizTemplate Template { get; init; }
    public int Order { get; init; }
    public required List<Question> Questions { get; init; }
    public required List<Category> Categories { get; init; }
}