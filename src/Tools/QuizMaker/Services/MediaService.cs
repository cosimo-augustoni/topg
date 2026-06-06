using System.IO.Compression;
using QuizMaker.Data;
using QuizMaker.Data.Repositories;

namespace QuizMaker.Services;

public class MediaService
{
    private readonly QuizDbContext _db;

    public MediaService(QuizDbContext db)
    {
        _db = db;
    }

    public string GetMediaRoot()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var root = Path.Combine(appData, "QuizMaker", "Media");
        Directory.CreateDirectory(root);
        return root;
    }

    public string GetTemplateFolderPath(string safeFolderName)
    {
        var root = GetMediaRoot();
        var path = Path.Combine(root, safeFolderName);
        Directory.CreateDirectory(path);
        return path;
    }

    public async Task<string> AddMediaAsync(long templateId, string sourceFilePath, string safeFolderName)
    {
        // determine next number
        var template = await _db.Templates.FindAsync(templateId);
        if (template == null) throw new InvalidOperationException("Template not found");
        template.MediaCounter++;
        await _db.SaveChangesAsync();

        var ext = Path.GetExtension(sourceFilePath);
        var filename = template.MediaCounter + ext;
        var folder = GetTemplateFolderPath(safeFolderName);
        var dest = Path.Combine(folder, filename);
        File.Copy(sourceFilePath, dest, overwrite: true);
        return filename;
    }

    public async Task<string> AddMediaAsync(long templateId, Stream sourceStream, string safeFolderName, string originalFileName)
    {
        // determine next number
        var template = await _db.Templates.FindAsync(templateId);
        if (template == null) throw new InvalidOperationException("Template not found");
        template.MediaCounter++;
        await _db.SaveChangesAsync();

        var ext = Path.GetExtension(originalFileName);
        var filename = template.MediaCounter + ext;
        var folder = GetTemplateFolderPath(safeFolderName);
        Directory.CreateDirectory(folder);
        var dest = Path.Combine(folder, filename);

        using var fs = File.Create(dest);
        await sourceStream.CopyToAsync(fs);
        return filename;
    }

    public void RenameTemplateFolder(string oldSafe, string newSafe)
    {
        var root = GetMediaRoot();
        var oldPath = Path.Combine(root, oldSafe);
        var newPath = Path.Combine(root, newSafe);
        if (Directory.Exists(oldPath) && !Directory.Exists(newPath))
        {
            Directory.Move(oldPath, newPath);
        }
        else
        {
            Directory.CreateDirectory(newPath);
        }
    }

    public string BuildMediaUri(string baseUrl, string safeFolderName, string filename)
    {
        return baseUrl.TrimEnd('/') + "/" + safeFolderName + "/" + filename;
    }

    public async Task<string> GetMediaUrlAsync(long templateId, string filename)
    {
        var t = await _db.Templates.FindAsync(templateId);
        var safe = t?.SafeFolderName ?? ("template-" + templateId);
        var m = await _db.Metadata.FindAsync("BaseUrl");
        var baseUrl = m?.Value ?? "http://localhost/media";
        return BuildMediaUri(baseUrl, safe, filename);
    }

    public void ExportMediaZip(string safeFolderName, string outputZip)
    {
        var folder = GetTemplateFolderPath(safeFolderName);
        if (!Directory.Exists(folder)) throw new InvalidOperationException("Template media folder not found");
        if (File.Exists(outputZip)) File.Delete(outputZip);

        // Create a zip where files are nested under a top-level folder named after the safeFolderName
        using (var zipToOpen = new FileStream(outputZip, FileMode.Create))
        using (var archive = new System.IO.Compression.ZipArchive(zipToOpen, System.IO.Compression.ZipArchiveMode.Create))
        {
            var files = Directory.GetFiles(folder);
            foreach (var file in files)
            {
                var entryName = Path.Combine(safeFolderName, Path.GetFileName(file)).Replace("\\", "/");
                var entry = archive.CreateEntry(entryName, System.IO.Compression.CompressionLevel.Optimal);
                using var entryStream = entry.Open();
                using var fs = File.OpenRead(file);
                fs.CopyTo(entryStream);
            }
        }
    }
}
