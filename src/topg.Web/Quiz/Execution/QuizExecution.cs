using topg.Web.Templating.DomainObjects;

namespace topg.Web.Quiz.Execution;

public record QuizExecution
{
    public string Name { get; }
    private readonly List<Board> Boards;

    public long? CurrentQuestionId { get; set; }
    public Question? CurrentQuestion => CurrentQuestionId == null ? null : CurrentBoard.Questions.Single(q => q.Template.Id == CurrentQuestionId);

    public int CurrentBoardId = 0;
    public Board CurrentBoard => Boards[CurrentBoardId];

    public QuizExecution(QuizTemplate template)
    {
        Name = template.Name;
        Boards = template.Boards.Select(b => new Board
        {
            Order = b.Order,
            Questions = b.Questions.Select(q => new Question
            {
                Template = q,
                IsAnswered = false,
            }).ToList()
        }).ToList();
    }
}