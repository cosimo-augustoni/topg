using topg.Web.Client.Shared;
using topg.Web.Quiz.Execution;
using topg.Web.Quiz.Management;
using Template = topg.Web.Templating.DomainObjects;

namespace topg.Web.IntegrationTests;

public class BonusPointsTests
{
    private readonly Template.QuizTemplate template = new() { Name = "Quiz", Boards = [] };
    private QuizSession session = null!;

    private void AddBoard(params int[] points)
    {
        var board = new Template.Board { Template = template, Order = template.Boards.Count, Questions = [] };
        foreach (var (p, index) in points.Select((p, i) => (p, i)))
        {
            board.Questions.Add(new Template.TextQuestion
            {
                Id = template.Boards.Count * 100 + index,
                Points = p,
                Category = $"Category {index}",
                AnswerType = AnswerType.Buzzer,
                QuestionType = QuestionType.Text,
                QuestionText = "Question",
                CorrectAnswer = "Answer",
            });
        }
        template.Boards.Add(board);
    }

    private void Start()
    {
        session = new QuizSession { SessionId = SessionId.Create(), Quiz = new QuizExecution(template) };
    }

    private List<Question> Questions => session.Quiz.CurrentBoard.Questions;

    private Question Question(int points) => Questions.Single(q => q.Points == points);

    private void Answer(Question question)
    {
        session.SelectQuestion(question);
        session.MarkCurrentQuestionAsAnswered();
    }

    private int[] DisplayedPoints() => [.. Questions.Select(session.PointsFor)];

    [Fact]
    public void A_new_session_starts_enabled_with_three()
    {
        AddBoard(100);
        Start();

        Assert.True(session.BonusPointsState.IsEnabled);
        Assert.Equal(3, session.BonusPointsState.LastQuestions);
    }

    [Fact]
    public void Nothing_gets_a_bonus_while_more_than_the_last_questions_are_unanswered()
    {
        AddBoard(100, 200, 300, 400);
        Start();

        Assert.All(Questions, q => Assert.False(session.HasBonus(q)));
        Assert.Equal([100, 200, 300, 400], DisplayedPoints());
        Assert.Equal(1, session.QuestionsUntilBonus);
    }

    [Fact]
    public void The_last_questions_give_one_and_a_half_times_their_points()
    {
        AddBoard(100, 200, 300, 400);
        Start();

        Answer(Question(100));

        Assert.Equal([100, 300, 450, 600], DisplayedPoints());
        Assert.False(session.HasBonus(Question(100)));
        Assert.True(session.HasBonus(Question(400)));
        Assert.Equal(0, session.QuestionsUntilBonus);
    }

    [Fact]
    public void Bonus_points_round_halves_up()
    {
        AddBoard(125);
        Start();

        Assert.Equal(188, session.PointsFor(Question(125)));
    }

    [Fact]
    public void An_answered_question_falls_back_to_its_normal_points()
    {
        AddBoard(100, 200, 400);
        Start();
        Assert.Equal(600, session.PointsFor(Question(400)));

        Answer(Question(400));

        Assert.False(session.HasBonus(Question(400)));
        Assert.Equal(400, session.PointsFor(Question(400)));
    }

    [Fact]
    public void Going_back_to_the_board_keeps_the_same_questions_boosted()
    {
        AddBoard(100, 200, 300, 400);
        Start();
        Answer(Question(100));

        session.SelectQuestion(Question(400));
        Assert.True(session.HasBonus(session.Quiz.CurrentQuestion!));
        session.ReturnToBoard();

        Assert.Equal([100, 300, 450, 600], DisplayedPoints());
    }

    [Fact]
    public void Awarding_a_boosted_question_uses_the_boosted_points()
    {
        AddBoard(100, 200, 400);
        Start();
        session.TryAddPlayer("Anna", out var annaId);
        session.TryGetPlayer(annaId, out var anna);

        session.SelectQuestion(Question(400));
        session.AdjustPlayerScore(anna!, session.PointsFor(session.Quiz.CurrentQuestion!));

        Assert.Equal(600, anna!.Score);
    }

    [Fact]
    public void Disabling_the_rule_shows_normal_points_everywhere()
    {
        AddBoard(100, 200, 400);
        Start();

        session.SetBonusPoints(false, 3);

        Assert.Equal([100, 200, 400], DisplayedPoints());
    }

    [Fact]
    public void Raising_the_last_questions_boosts_the_remaining_questions_right_away_and_keeps_awarded_scores()
    {
        AddBoard(100, 200, 300, 400, 500);
        Start();
        session.TryAddPlayer("Anna", out var annaId);
        session.TryGetPlayer(annaId, out var anna);
        session.SelectQuestion(Question(100));
        session.AdjustPlayerScore(anna!, session.PointsFor(Question(100)));
        session.MarkCurrentQuestionAsAnswered();
        var stateChanges = 0;
        session.SessionStateChanged += (_, _) =>
        {
            stateChanges++;
            return Task.CompletedTask;
        };

        session.SetBonusPoints(true, 5);

        Assert.Equal([100, 300, 450, 600, 750], DisplayedPoints());
        Assert.Equal(100, anna!.Score);
        Assert.True(stateChanges > 0);
    }

    [Fact]
    public void The_next_board_keeps_the_setting_and_starts_with_normal_points()
    {
        AddBoard(100);
        AddBoard(100, 200, 300, 400, 500, 600);
        Start();
        session.SetBonusPoints(true, 5);
        Answer(Question(100));

        session.SelectNextBoard();

        Assert.True(session.BonusPointsState.IsEnabled);
        Assert.Equal(5, session.BonusPointsState.LastQuestions);
        Assert.Equal([100, 200, 300, 400, 500, 600], DisplayedPoints());

        Answer(Question(100));

        Assert.Equal([100, 300, 450, 600, 750, 900], DisplayedPoints());
    }

    [Fact]
    public void A_board_with_no_more_questions_than_the_last_questions_starts_boosted()
    {
        AddBoard(100, 200, 300);
        Start();

        Assert.Equal([150, 300, 450], DisplayedPoints());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-4)]
    public void Last_questions_below_one_are_corrected_to_one(int lastQuestions)
    {
        AddBoard(100);
        Start();

        session.SetBonusPoints(true, lastQuestions);

        Assert.Equal(1, session.BonusPointsState.LastQuestions);
    }

    [Fact]
    public void A_new_session_does_not_inherit_the_setting_of_another()
    {
        AddBoard(100);
        Start();
        session.SetBonusPoints(false, 7);

        Start();

        Assert.True(session.BonusPointsState.IsEnabled);
        Assert.Equal(3, session.BonusPointsState.LastQuestions);
    }
}
