using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using topg.Web.Client.Creator.Images;
using topg.Web.Client.Creator.Model;
using topg.Web.Client.Creator.Storage;

namespace topg.Web.Client.Creator.Export;

public class ProjectFileException(string message) : Exception(message);

public record ProjectFileContent(QuizProject Project, IReadOnlyDictionary<string, byte[]> ImagesByHash)
{
    public long ImageBytes => ImagesByHash.Values.Sum(i => (long)i.Length);
}

public static class ProjectFile
{
    public const string Extension = ".topgquiz";
    public const string ProjectEntry = "project.json";
    public const string ImagesFolder = "images/";
    public const long MaxFileSize = 1024L * 1024 * 1024;

    private static readonly JsonSerializerOptions Indented = new(CreatorJson.Options) { WriteIndented = true };

    public static string FileName(QuizProject project) => $"{project.Folder.Trim()}{Extension}";

    /// <summary>
    /// Entries use the project's <see cref="QuizProject.UpdatedAt"/> as timestamp, so exporting an unchanged project
    /// twice (or after importing it in another browser) gives the same bytes.
    /// </summary>
    public static async Task<byte[]> CreateAsync(QuizProject project, ICreatorStorage storage)
    {
        var images = await ZipImages.LoadAsync(project, storage);

        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            var json = JsonSerializer.Serialize(project, Indented);
            ZipImages.Write(zip, ProjectEntry, Encoding.UTF8.GetBytes(json), project.UpdatedAt);
            foreach (var (image, content) in images)
            {
                ZipImages.Write(zip, ImagesFolder + image.FileName, content, project.UpdatedAt);
            }
        }

        return buffer.ToArray();
    }

    public static ProjectFileContent Read(Stream file)
    {
        ZipArchive zip;
        try
        {
            zip = new ZipArchive(file, ZipArchiveMode.Read, leaveOpen: true);
        }
        catch (InvalidDataException)
        {
            throw new ProjectFileException("Not a topg project file.");
        }

        using (zip)
        {
            var project = ReadProject(zip);
            var images = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            foreach (var image in project.AllImages().DistinctBy(i => i.FileName))
            {
                var entry = zip.GetEntry(ImagesFolder + image.FileName)
                    ?? throw new ProjectFileException($"Image {image.FileName} ({image.OriginalFileName}) is missing in the file.");
                var content = ReadAll(entry);
                if (!string.Equals(ImageNaming.ComputeHash(content), image.Hash, StringComparison.OrdinalIgnoreCase))
                {
                    throw new ProjectFileException($"Image {image.FileName} is corrupt (hash mismatch).");
                }

                images[image.Hash] = content;
            }

            return new ProjectFileContent(project, images);
        }
    }

    private static QuizProject ReadProject(ZipArchive zip)
    {
        var entry = zip.GetEntry(ProjectEntry) ?? throw new ProjectFileException("Not a topg project file.");
        JsonObject json;
        try
        {
            json = JsonNode.Parse(ReadAll(entry)) as JsonObject ?? throw new ProjectFileException("Not a topg project file.");
        }
        catch (JsonException)
        {
            throw new ProjectFileException("Not a topg project file.");
        }

        var version = json["schemaVersion"]?.GetValueKind() == JsonValueKind.Number ? json["schemaVersion"]!.GetValue<int>() : 0;
        if (version < 1)
        {
            throw new ProjectFileException("Not a topg project file.");
        }

        if (version > QuizProject.CurrentSchemaVersion)
        {
            throw new ProjectFileException($"Created by a newer version (schema {version}) – please update.");
        }

        Migrate(json, version);

        try
        {
            return json.Deserialize<QuizProject>(CreatorJson.Options) ?? throw new ProjectFileException("Not a topg project file.");
        }
        catch (JsonException ex)
        {
            throw new ProjectFileException($"The project data is invalid: {ex.Message}");
        }
    }

    /// <summary>
    /// Upgrades older files step by step to <see cref="QuizProject.CurrentSchemaVersion"/>. Add a case per version bump
    /// (e.g. hints or sound questions) so old files stay readable.
    /// </summary>
    private static void Migrate(JsonObject json, int fromVersion)
    {
        for (var version = fromVersion; version < QuizProject.CurrentSchemaVersion; version++)
        {
            switch (version)
            {
                // case 1: migrate 1 → 2 here.
                default:
                    break;
            }
        }

        json["schemaVersion"] = QuizProject.CurrentSchemaVersion;
    }

    private static byte[] ReadAll(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    public static async Task<QuizProject> ImportAsync(ProjectFileContent content, bool asCopy, ICreatorStorage storage, ProjectStore store)
    {
        foreach (var image in content.Project.AllImages().DistinctBy(i => i.Hash))
        {
            await storage.PutImageAsync(image.Hash, image.Extension, image.ContentType, content.ImagesByHash[image.Hash]);
        }

        var project = asCopy ? content.Project.Duplicate() : content.Project;
        // An overwrite keeps the file's "last edited" so the imported project is identical to the exported one.
        await store.SaveAsync(project, touch: asCopy);
        return project;
    }
}
