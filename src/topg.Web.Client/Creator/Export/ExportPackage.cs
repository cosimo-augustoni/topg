using System.IO.Compression;
using System.Text;
using topg.Web.Client.Creator.Model;
using topg.Web.Client.Creator.Storage;

namespace topg.Web.Client.Creator.Export;

public class ExportException(string message) : Exception(message);

public static class ExportPackage
{
    public static string FileName(QuizProject project) => $"{project.Folder.Trim()}-export.zip";

    public static async Task<byte[]> BuildAsync(QuizProject project, ICreatorStorage storage, DateTimeOffset generatedAt)
    {
        var sql = SqlExport.Generate(project, generatedAt);
        var images = await ZipImages.LoadAsync(project, storage);
        var folder = project.Folder.Trim();

        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            ZipImages.Write(zip, SqlExport.FileName, Encoding.UTF8.GetBytes(sql), generatedAt);
            foreach (var (image, content) in images)
            {
                ZipImages.Write(zip, $"{folder}/{image.FileName}", content, generatedAt);
            }
        }

        return buffer.ToArray();
    }
}

internal static class ZipImages
{
    // Sorted by file name so the ZIP output is stable.
    public static async Task<List<(ImageRef Image, byte[] Content)>> LoadAsync(QuizProject project, ICreatorStorage storage)
    {
        var result = new List<(ImageRef, byte[])>();
        var missing = new List<string>();
        foreach (var image in project.AllImages().DistinctBy(i => i.FileName).OrderBy(i => i.FileName, StringComparer.Ordinal))
        {
            var content = await storage.GetImageBytesAsync(image.Hash);
            if (content is null)
            {
                missing.Add(image.OriginalFileName);
            }
            else
            {
                result.Add((image, content));
            }
        }

        if (missing.Count > 0)
        {
            throw new ExportException($"Missing in this browser's storage, upload again: {string.Join(", ", missing)}");
        }

        return result;
    }

    /// <summary>Images are already compressed, so they are stored; text is deflated.</summary>
    public static void Write(ZipArchive zip, string path, byte[] content, DateTimeOffset timestamp)
    {
        var level = path.EndsWith(".json", StringComparison.Ordinal) || path.EndsWith(".sql", StringComparison.Ordinal)
            ? CompressionLevel.Optimal
            : CompressionLevel.NoCompression;
        var entry = zip.CreateEntry(path, level);
        // ZIP timestamps have a 2 s resolution and start in 1980.
        entry.LastWriteTime = timestamp.Year < 1980 ? new DateTimeOffset(1980, 1, 1, 0, 0, 0, TimeSpan.Zero) : timestamp;
        using var stream = entry.Open();
        stream.Write(content);
    }
}
