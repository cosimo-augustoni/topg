namespace topg.Web.Client.Creator.Model;

/// <summary>
/// A quiz being edited in the creator. Saved as JSON in the browser (IndexedDB) and in project files.
/// Board order is the order of <see cref="Boards"/>; it becomes <c>Boards.Order</c> on export.
/// </summary>
public class QuizProject
{
    /// <summary>Bump when the JSON shape changes in a way old readers can't handle.</summary>
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; set; } = Guid.NewGuid();
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    /// <summary>Template name in the game.</summary>
    public string Name { get; set; } = "";

    /// <summary>CDN folder slug. Defaults to a slug of the name but is never renamed automatically.</summary>
    public string Folder { get; set; } = "";

    /// <summary>Absolute http(s) URL the image URLs are prefixed with.</summary>
    public string BaseUrl { get; set; } = "";

    /// <summary>Delete existing templates with the same name before inserting (insert or replace).</summary>
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

    /// <summary>All images referenced by questions of this project.</summary>
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
