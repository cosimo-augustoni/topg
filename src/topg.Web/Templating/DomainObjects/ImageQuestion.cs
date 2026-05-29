namespace topg.Web.Templating.DomainObjects;

public record ImageQuestion : Question
{
    public required string ImageUri { get; init; }
    public required string QuestionText { get; init; }
    public required string CorrectAnswer { get; init; }
}