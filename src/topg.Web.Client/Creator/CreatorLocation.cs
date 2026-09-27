namespace topg.Web.Client.Creator;

public enum CreatorSection
{
    Boards,
    QuizSettings,
    Export,
}

/// <summary>
/// Routes of the creator (UX-2) and parsing of the current location, so the layout and the app bar
/// know which project and section are active without the pages having to tell them.
/// </summary>
public record CreatorLocation(Guid? ProjectId, CreatorSection? Section)
{
    public const string Root = "/create";
    public const string Settings = "/create/settings";

    public static string Project(Guid projectId) => $"{Root}/{projectId}";
    public static string Board(Guid projectId, int boardNumber) => $"{Root}/{projectId}/board/{boardNumber}";
    public static string QuizSettings(Guid projectId) => $"{Root}/{projectId}/quiz";
    public static string Export(Guid projectId) => $"{Root}/{projectId}/export";

    public static string ForSection(Guid projectId, CreatorSection section) => section switch
    {
        CreatorSection.Boards => Project(projectId),
        CreatorSection.QuizSettings => QuizSettings(projectId),
        CreatorSection.Export => Export(projectId),
        _ => throw new ArgumentOutOfRangeException(nameof(section)),
    };

    /// <param name="relativePath">Path relative to the base URI, e.g. <c>create/{id}/board/1</c>.</param>
    public static CreatorLocation Parse(string relativePath)
    {
        var path = relativePath.Split('?', '#')[0];
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length < 2
            || !segments[0].Equals("create", StringComparison.OrdinalIgnoreCase)
            || !Guid.TryParse(segments[1], out var projectId))
        {
            return new CreatorLocation(null, null);
        }

        var section = segments.ElementAtOrDefault(2)?.ToLowerInvariant() switch
        {
            null or "board" => CreatorSection.Boards,
            "quiz" => CreatorSection.QuizSettings,
            "export" => CreatorSection.Export,
            _ => (CreatorSection?)null,
        };

        return new CreatorLocation(projectId, section);
    }
}
