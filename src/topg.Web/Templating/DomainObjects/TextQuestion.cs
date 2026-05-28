namespace topg.Web.Templating.DomainObjects;

public record TextQuestion : Question
{
    public required string QuestionText { get; init; }

    public required string CorrectAnswer { get; init; }
}