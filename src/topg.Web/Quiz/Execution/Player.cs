namespace topg.Web.Quiz.Execution;

public record Player
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required int Score { get; set; }
}