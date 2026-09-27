using System.Globalization;

namespace topg.Web.Client.Creator.Model;

/// <summary>
/// The order in which the game shows things (principle P2 "what you see is what gets played"):
/// <c>Board.razor</c> groups by category and sorts the categories alphabetically, questions are sorted by points.
/// The editor always displays this order, so there is no manual reordering.
/// </summary>
public static class GameOrder
{
    // The server renders with the container's culture, which is invariant.
    private static readonly StringComparer CategoryComparer = StringComparer.Create(CultureInfo.InvariantCulture, ignoreCase: false);

    public static IReadOnlyList<CategoryDraft> Categories(BoardDraft board) =>
        board.Categories.OrderBy(c => c.Name.Trim(), CategoryComparer).ToList();

    /// <summary>Stable: questions with the same points keep their insertion order (the game's order is undefined then).</summary>
    public static IReadOnlyList<QuestionDraft> Questions(CategoryDraft category) =>
        category.Questions.OrderBy(q => q.Points).ToList();
}
