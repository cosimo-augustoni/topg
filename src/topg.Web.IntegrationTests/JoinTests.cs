using topg.Web.Quiz.Management;
using topg.Web.Templating.DomainObjects;

namespace topg.Web.IntegrationTests;

public class JoinTests
{
    private readonly SessionHandler handler = new();
    private readonly SessionId sessionId;

    public JoinTests()
    {
        sessionId = handler.CreateSession(new QuizTemplate { Name = "Quiz", Boards = [] });
    }

    [Fact]
    public void Join_UnknownGame_ReportsGameNotFound()
    {
        var unknown = new SessionId(sessionId.Key == "9999" ? "0000" : "9999");

        Assert.Equal(JoinResult.GameNotFound, handler.Join(unknown, "Alice", out var playerId));
        Assert.Null(playerId);
    }

    [Fact]
    public void Join_FreeName_Joins()
    {
        Assert.Equal(JoinResult.Joined, handler.Join(sessionId, "Alice", out var playerId));
        Assert.NotNull(playerId);
    }

    [Theory]
    [InlineData("Alice")]
    [InlineData("Alice ")]
    [InlineData(" Alice")]
    [InlineData("alice")]
    [InlineData("ALICE")]
    public void Join_NameDifferingOnlyInCaseOrWhitespace_ReportsNameTaken(string name)
    {
        handler.Join(sessionId, "Alice", out _);

        Assert.Equal(JoinResult.NameTaken, handler.Join(sessionId, name, out var playerId));
        Assert.Null(playerId);
        Assert.Single(handler.Sessions[sessionId].Players);
    }

    [Fact]
    public void Join_SimilarButDifferentName_Joins()
    {
        handler.Join(sessionId, "Alice", out _);

        Assert.Equal(JoinResult.Joined, handler.Join(sessionId, "Alicia", out _));
    }

    [Fact]
    public void Join_NameWithSurroundingWhitespace_IsStoredTrimmed()
    {
        handler.Join(sessionId, "  Bob  ", out var playerId);

        var player = Assert.Single(handler.Sessions[sessionId].Players);
        Assert.Equal("Bob", player.Name);
        Assert.StartsWith("Bob.", playerId);
    }
}
