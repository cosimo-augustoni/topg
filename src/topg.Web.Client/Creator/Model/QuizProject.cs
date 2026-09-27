namespace topg.Web.Client.Creator.Model;

public class QuizProject
{
    /// <summary>Bump when the JSON shape changes in a way old readers can't handle.</summary>
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; set; } = Guid.NewGuid();
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    public string Name { get; set; } = "";

    public string Folder { get; set; } = "";

    public string BaseUrl { get; set; } = "";

    public bool ReplaceExisting { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastExportedAt { get; set; }
    public DateTimeOffset? LastProjectFileExportAt { get; set; }

    public List<BoardDraft> Boards { get; set; } = [];

    public static QuizProject Create(string name, string baseUrl) => new()
    {
        Name = name.Trim(),
        Folder = Slug.FromName(name),
        BaseUrl = baseUrl.Trim(),
    };

    public IEnumerable<QuestionDraft> AllQuestions() =>
        Boards.SelectMany(b => b.Categories).SelectMany(c => c.Questions);

    public IEnumerable<ImageRef> AllImages() => AllQuestions().SelectMany(q => q.Images());
}

public class BoardDraft
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public List<CategoryDraft> Categories { get; set; } = [];
}

/// <summary>
/// Categories only exist in the creator. On export the name is written to every question's <c>Category</c> column.
/// </summary>
public class CategoryDraft
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public List<QuestionDraft> Questions { get; set; } = [];
}
