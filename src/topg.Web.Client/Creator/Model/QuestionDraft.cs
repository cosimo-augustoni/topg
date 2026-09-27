using System.Text.Json.Serialization;
using topg.Web.Client.Shared;

namespace topg.Web.Client.Creator.Model;

/// <summary>
/// Base of all question types. Serialized with a <c>type</c> discriminator so new types (sound) and new
/// fields (hints) can be added without breaking existing projects.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(TextQuestionDraft), "text")]
[JsonDerivedType(typeof(ImageQuestionDraft), "image")]
public abstract class QuestionDraft
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Derived from the class; the JSON uses the <c>type</c> discriminator instead.</summary>
    [JsonIgnore]
    public abstract QuestionType Type { get; }

    public AnswerType AnswerType { get; set; } = AnswerType.Buzzer;
    public int Points { get; set; }
    public string QuestionText { get; set; } = "";

    public virtual IEnumerable<ImageRef> Images() => [];
}

public class TextQuestionDraft : QuestionDraft
{
    [JsonIgnore]
    public override QuestionType Type => QuestionType.Text;

    public string CorrectAnswer { get; set; } = "";
}

public class ImageQuestionDraft : QuestionDraft
{
    [JsonIgnore]
    public override QuestionType Type => QuestionType.Image;

    public ImageRef? QuestionImage { get; set; }
    public string AnswerText { get; set; } = "";
    public ImageRef? AnswerImage { get; set; }
    public ImageSize ImageSize { get; set; } = ImageSize.Medium;

    public override IEnumerable<ImageRef> Images()
    {
        if (QuestionImage is not null)
        {
            yield return QuestionImage;
        }

        if (AnswerImage is not null)
        {
            yield return AnswerImage;
        }
    }
}
