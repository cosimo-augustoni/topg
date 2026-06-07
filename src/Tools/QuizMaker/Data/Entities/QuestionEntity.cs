using QuizMaker.DomainObjects;

namespace QuizMaker.Data.Entities;

public class QuestionEntity
{
    public long Id { get; set; }
    public long CategoryId { get; set; }
    public QuestionType QuestionType { get; set; }
    public AnswerType AnswerType { get; set; }
    public int Points { get; set; }
    public int Order { get; set; }

    // Common fields
    public string? QuestionText { get; set; }

    // Text question fields
    public string? CorrectAnswer { get; set; }

    // Image question fields
    public string? QuestionImageUri { get; set; }
    public string? AnswerText { get; set; }
    public string? AnswerImageUri { get; set; }
    public ImageSize? ImageSize { get; set; }
}
