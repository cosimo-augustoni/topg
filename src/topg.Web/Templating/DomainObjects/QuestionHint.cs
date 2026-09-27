namespace topg.Web.Templating.DomainObjects;

public record QuestionHint
{
    public long Id { get; init; }
    public int Order { get; init; }

    // The hint text, or the caption of an image hint.
    public required string Text { get; init; }

    // Empty for text hints, like AnswerImageUri for image questions without an answer image.
    public required string ImageUri { get; init; }
}
