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
}

[method: SetsRequiredMembers]
public class TextQuestion(Templating.DomainObjects.TextQuestion question) : Question(question)
{
    public required string QuestionText { get; init; } = question.QuestionText;
    public required string CorrectAnswer { get; init; } = question.CorrectAnswer;
    public TextQuestionDisplayState DisplayState { get; set; } = TextQuestionDisplayState.None;
}

[method: SetsRequiredMembers]
public class ImageQuestion(Templating.DomainObjects.ImageQuestion question) : Question(question)
{
    public required string QuestionText { get; init; } = question.QuestionText;
    public required Uri ImageUri { get; init; } = new Uri(question.ImageUri);
    public required string CorrectAnswer { get; init; } = question.CorrectAnswer;
    public ImageQuestionDisplayState DisplayState { get; set; }
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