namespace topg.Web.Templating.DomainObjects;

public record TextQuestion : Question
{
    public required string QuestionText { get; init; }
}