using topg.Web.Client.Creator.Storage;

namespace topg.Web.Client.Creator.Images;

/// <summary>
/// Object URLs for grid thumbnails, created once per image hash. The URLs live as long as the page (the images are
/// small in number and shared between cells), so they are not revoked individually.
/// </summary>
public class ImagePreviewCache(ICreatorStorage storage)
{
    private readonly Dictionary<string, Task<string?>> _urls = new(StringComparer.OrdinalIgnoreCase);

    public Task<string?> GetUrlAsync(string hash)
    {
        if (!_urls.TryGetValue(hash, out var url) || url.IsFaulted)
        {
            _urls[hash] = url = storage.CreateObjectUrlAsync(hash);
        }

        return url;
    }

    /// <summary>The URL if it was already created, otherwise null (and starts creating it).</summary>
    public string? TryGetUrl(string hash)
    {
        var task = GetUrlAsync(hash);
        return task.IsCompletedSuccessfully ? task.Result : null;
    }
}
