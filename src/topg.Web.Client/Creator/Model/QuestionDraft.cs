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

    public const int MaxHints = 10;

    // Null while the question has no hints; the first hint added decides the type.
    public HintType? HintType { get; set; }

    public List<HintDraft> Hints { get; set; } = [];

    public override IEnumerable<ImageRef> Images() => Hints.Select(h => h.Image).OfType<ImageRef>();
}

public class HintDraft
{
    // The game tile is sized for this length: ten hints of 250 characters still fit on a 1920 × 1080 screen.
    public const int MaxTextLength = 250;

    public Guid Id { get; set; } = Guid.NewGuid();

    // The hint text, or the caption of an image hint.
    public string Text { get; set; } = "";

    public ImageRef? Image { get; set; }
}

public class ImageQuestionDraft : QuestionDraft
{
    [JsonIgnore]
    public override QuestionType Type => QuestionType.Image;

    public ImageRef? QuestionImage { get; set; }
    public string AnswerText { get; set; } = "";
    public ImageRef? AnswerImage { get; set; }
    public ImageSize ImageSize { get; set; } = ImageSize.Medium;
    public bool StartPixelated { get; set; }

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
