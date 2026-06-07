namespace topg.Web.Templating.DomainObjects;

public record ImageQuestion : Question
{
    public required string QuestionText { get; init; }
    public required string QuestionImageUri { get; init; }
    public required string AnswerText { get; init; }
    public required string AnswerImageUri { get; init; }
    public required ImageSize ImageSize { get; init; }
}