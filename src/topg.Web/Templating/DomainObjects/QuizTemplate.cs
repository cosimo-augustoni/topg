namespace topg.Web.Templating.DomainObjects;

public record QuizTemplate
{
    public long Id { get; init; }
    public required string Name { get; init; }
    public required List<Board> Boards { get; init; }
}