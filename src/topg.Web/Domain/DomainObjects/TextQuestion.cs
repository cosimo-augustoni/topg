namespace topg.Web.Domain.DomainObjects;

public record TextQuestion : Question
{
    public required string QuestionText { get; init; }
}