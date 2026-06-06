namespace QuizMaker.DomainObjects;

public record Category
{
    public long Id { get; init; }
    public required string Name { get; init; }
}
