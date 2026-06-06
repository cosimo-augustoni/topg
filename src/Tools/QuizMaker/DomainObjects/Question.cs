namespace QuizMaker.DomainObjects;

public abstract record Question
{
    public long Id { get; init; }
    public QuestionType QuestionType { get; init; }
    public AnswerType AnswerType { get; init; }
    public int Points { get; init; }
    public required string Category { get; init; }
    public int Order => field == 0 ? Points : field;
}