namespace topg.Web.Quiz.Execution;

public class BonusPointsState
{
    public const int DefaultLastQuestions = 3;

    // 1.5 instead of 2 so the last questions help players who are behind without outweighing the rest of the board.
    public const decimal Multiplier = 1.5m;

    public bool IsEnabled { get; set; } = true;
    public int LastQuestions { get; set; } = DefaultLastQuestions;
}
