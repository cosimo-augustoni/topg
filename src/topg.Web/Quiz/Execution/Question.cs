namespace topg.Web.Quiz.Execution;

public class Question
{
    public required Templating.DomainObjects.Question Template { get; set; }
    public bool IsAnswered { get; set; }
}