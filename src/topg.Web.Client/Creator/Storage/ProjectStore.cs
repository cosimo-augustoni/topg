using System.Text.Json;
using topg.Web.Client.Creator.Model;
using topg.Web.Client.Creator.Validation;

namespace topg.Web.Client.Creator.Storage;

/// <summary>Row of the project list (S-1).</summary>
/// <param name="IsCorrupt">The stored JSON could not be read. The project can only be deleted.</param>
public record ProjectSummary(
    Guid Id,
    string Name,
    int BoardCount,
    int QuestionCount,
    DateTimeOffset UpdatedAt,
    bool IsCorrupt = false,
    IReadOnlyList<ValidationIssue>? Issues = null)
{
    public IReadOnlyList<ValidationIssue> Issues { get; init; } = Issues ?? [];
}

/// <param name="Images">Stored images that no project references.</param>
public record UnusedImages(IReadOnlyList<StoredImage> Images)
{
    public long TotalSize => Images.Sum(i => i.Size);
}

/// <summary>
/// Projects and images in browser storage. Images are shared between projects (stored once per hash),
/// so they are only deleted when no project references them anymore.
/// </summary>
public class ProjectStore(ICreatorStorage storage)
{
    public async Task<IReadOnlyList<ProjectSummary>> ListAsync()
    {
        var summaries = new List<ProjectSummary>();
        foreach (var json in await storage.ListProjectJsonAsync())
        {
            summaries.Add(TryDeserialize(json, out var project)
                ? new ProjectSummary(project.Id, project.Name, project.Boards.Count, project.AllQuestions().Count(), project.UpdatedAt,
                    Issues: ProjectValidator.Validate(project))
                : CorruptSummary(json));
        }

        return summaries.OrderByDescending(s => s.UpdatedAt).ToList();
    }

    public async Task<QuizProject?> GetAsync(Guid id)
    {
        var json = await storage.GetProjectJsonAsync(id);
        return json is null ? null : CreatorJson.Deserialize<QuizProject>(json);
    }

    /// <summary>Saves the project and sets <see cref="QuizProject.UpdatedAt"/>, unless <paramref name="touch"/> is false (imports).</summary>
    public async Task SaveAsync(QuizProject project, bool touch = true)
    {
        if (touch)
        {
            project.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await storage.SaveProjectJsonAsync(project.Id, CreatorJson.Serialize(project));
    }

    /// <summary>Deletes the project and the images only this project used.</summary>
    public async Task DeleteAsync(Guid id)
    {
        var project = await GetOrDefaultAsync(id);
        await storage.DeleteProjectAsync(id);

        if (project is null)
        {
            return;
        }

        // Unknown references (an unreadable project) → keep all images rather than risk deleting used ones.
        var stillUsed = await TryGetReferencedHashesAsync();
        if (stillUsed is null)
        {
            return;
        }

        var orphaned = project.AllImages().Select(i => i.Hash).Where(h => !stillUsed.Contains(h)).Distinct().ToList();
        if (orphaned.Count > 0)
        {
            await storage.DeleteImagesAsync(orphaned);
        }
    }

    public async Task<UnusedImages> FindUnusedImagesAsync()
    {
        var referenced = await TryGetReferencedHashesAsync()
            ?? throw new InvalidOperationException("A stored project could not be read, so unused images can't be determined safely.");
        var images = await storage.ListImagesAsync();
        return new UnusedImages(images.Where(i => !referenced.Contains(i.Hash)).ToList());
    }

    /// <summary>Deletes images no project references (e.g. replaced images, deleted questions).</summary>
    public async Task<UnusedImages> DeleteUnusedImagesAsync()
    {
        var unused = await FindUnusedImagesAsync();
        if (unused.Images.Count > 0)
        {
            await storage.DeleteImagesAsync(unused.Images.Select(i => i.Hash).ToList());
        }

        return unused;
    }

    private async Task<QuizProject?> GetOrDefaultAsync(Guid id)
    {
        var json = await storage.GetProjectJsonAsync(id);
        return json is not null && TryDeserialize(json, out var project) ? project : null;
    }

    /// <summary>Hashes referenced by any project, or null if a project is unreadable and its images can't be known.</summary>
    private async Task<HashSet<string>?> TryGetReferencedHashesAsync()
    {
        var hashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var json in await storage.ListProjectJsonAsync())
        {
            if (!TryDeserialize(json, out var project))
            {
                return null;
            }

            hashes.UnionWith(project.AllImages().Select(i => i.Hash));
        }

        return hashes;
    }

    private static bool TryDeserialize(string json, out QuizProject project)
    {
        try
        {
            project = CreatorJson.Deserialize<QuizProject>(json);
            return true;
        }
        catch (JsonException)
        {
            project = null!;
            return false;
        }
    }

    private static ProjectSummary CorruptSummary(string json)
    {
        // Try to at least recover the id so the entry can be deleted.
        var id = Guid.Empty;
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.TryGetProperty("id", out var idElement))
            {
                idElement.TryGetGuid(out id);
            }
        }
        catch (JsonException)
        {
        }

        return new ProjectSummary(id, "(unreadable project)", 0, 0, DateTimeOffset.MinValue, IsCorrupt: true);
    }
}
