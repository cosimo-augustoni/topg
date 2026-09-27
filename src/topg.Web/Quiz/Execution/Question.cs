using topg.Web.Client.Shared;
using System.Diagnostics.CodeAnalysis;
using topg.Web.Templating.DomainObjects;

namespace topg.Web.Quiz.Execution;

[method: SetsRequiredMembers]
public abstract class Question(Templating.DomainObjects.Question question)
{
    public long Id { get; init; } = question.Id;
    public int Points { get; init; } = question.Points;
    public required string Category { get; init; } = question.Category;
    public int Order { get; init; } = question.Order;
    public AnswerType AnswerType { get; init; } = question.AnswerType;
    public bool IsAnswered { get; set; } = false;

    public virtual void ResetDisplayState()
    {
    }
}

[method: SetsRequiredMembers]
public class TextQuestion(Templating.DomainObjects.TextQuestion question) : Question(question)
{
    public required string QuestionText { get; init; } = question.QuestionText;
    public required string CorrectAnswer { get; init; } = question.CorrectAnswer;
    public TextQuestionDisplayState DisplayState { get; set; } = TextQuestionDisplayState.None;
    public HintType? HintType { get; init; } = question.Hints.Count == 0 ? null : question.HintType ?? Client.Shared.HintType.Text;

    // The DB returns hints in no guaranteed order.
    public IReadOnlyList<QuestionHint> Hints { get; init; } = question.Hints.OrderBy(h => h.Order).Select(h => new QuestionHint(h)).ToList();

    public override void ResetDisplayState()
    {
        DisplayState = TextQuestionDisplayState.None;
        foreach (var hint in Hints)
        {
            hint.IsVisible = false;
        }
    }
}

// Ten hints toggle independently, which a [Flags] display state can't hold, so each hint carries its own visibility.
public class QuestionHint(Templating.DomainObjects.QuestionHint hint)
{
    public string Text { get; } = hint.Text;
    public Uri? ImageUri { get; } = string.IsNullOrEmpty(hint.ImageUri) ? null : new Uri(hint.ImageUri);
    public bool IsVisible { get; set; }
}

[method: SetsRequiredMembers]
public class ImageQuestion(Templating.DomainObjects.ImageQuestion question) : Question(question)
{
    public required string QuestionText { get; init; } = question.QuestionText;
    public required Uri QuestionImageUri { get; init; } = new Uri(question.QuestionImageUri);
    public required string AnswerText { get; init; } = question.AnswerText;
    public required Uri? AnswerImageUri { get; init; } = string.IsNullOrEmpty(question.AnswerImageUri) ? null : new Uri(question.AnswerImageUri);
    public required ImageSize ImageSize { get; init; } = question.ImageSize;
    public ImageQuestionDisplayState DisplayState { get; set; }

    // Strongest first. The steps get tuned after playtesting, so the slider, the starting step and the renderer all
    // derive from this list, and the session stores an index into it rather than the level itself.
    public static readonly ObscureLevel[] ObscureLevels =
    [
        new(Turns: 3, BlurWidth: 16, Grey: 1),
        new(Turns: 1.8, BlurWidth: 32, Grey: 0.6),
        new(Turns: 1, BlurWidth: 64, Grey: 0),
        new(Turns: 0.5, BlurWidth: 160, Grey: 0),
        new(Turns: 0.2, BlurWidth: null, Grey: 0),
    ];

    public static int ClearStep => ObscureLevels.Length;

    public bool StartObscured { get; init; } = question.StartObscured;

    public int StartStep => StartObscured ? 0 : ClearStep;

    public int ObscureStep { get; set; } = question.StartObscured ? 0 : ClearStep;

    public ObscureLevel? CurrentObscureLevel => ObscureStep < ClearStep ? ObscureLevels[ObscureStep] : null;

    public override void ResetDisplayState()
    {
        DisplayState = ImageQuestionDisplayState.None;
        ObscureStep = StartStep;
    }
}

[Flags]
public enum ImageQuestionDisplayState
{
    None = 0,
    Image = 1,
    Text = 2,
    Answer = 4,
}

[method: SetsRequiredMembers]
public class SoundQuestion(Templating.DomainObjects.SoundQuestion question) : Question(question)
{

}

[Flags]
public enum TextQuestionDisplayState
{
    None = 0,
    Question = 1,
    Answer = 2,
}