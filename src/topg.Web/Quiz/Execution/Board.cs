namespace topg.Web.Quiz.Execution;

public class Board
{
    public int Order { get; init; }
    public required List<Question> Questions { get; init; }
}