using topg.Web.Templating.DomainObjects;

namespace topg.Web.Quiz.Execution;

public class QuizExecution
{
    public string Name { get; }
    private readonly List<Board> Boards;

    public long? CurrentQuestionId { get; set; }
    public Question? CurrentQuestion => CurrentQuestionId == null ? null : CurrentBoard.Questions.Single(q => q.Id == CurrentQuestionId);

    public int CurrentBoardId = 0;
    public Board CurrentBoard => Boards[CurrentBoardId];
    public bool HasNextBoard => Boards.Count > CurrentBoardId + 1;

    public QuizExecution(QuizTemplate template)
    {
        Name = template.Name;
        Boards = template.Boards.Select(b => new Board
        {
            Order = b.Order,
            Questions = b.Questions.Select<Templating.DomainObjects.Question, Question>(q => q switch
            {
                Templating.DomainObjects.ImageQuestion imageQuestion => new ImageQuestion(imageQuestion),
                Templating.DomainObjects.SoundQuestion soundQuestion => new SoundQuestion(soundQuestion),
                Templating.DomainObjects.TextQuestion textQuestion => new TextQuestion(textQuestion),
                _ => throw new ArgumentOutOfRangeException(nameof(q))
            }).ToList()
        }).ToList();
    }
}