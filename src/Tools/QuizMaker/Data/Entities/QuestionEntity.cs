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
    public string? CorrectAnswer { get; set; }
    public string? ImageUri { get; set; }
}
