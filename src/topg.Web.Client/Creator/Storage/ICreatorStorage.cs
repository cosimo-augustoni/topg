namespace topg.Web.Client.Creator.Storage;

/// <summary>
/// Raw browser storage of the creator (IndexedDB, see <c>wwwroot/creator-storage.js</c>).
/// Higher-level rules (image cleanup, summaries) live in <see cref="ProjectStore"/>.
/// </summary>
public interface ICreatorStorage
{
    Task<IReadOnlyList<string>> ListProjectJsonAsync();
    Task<string?> GetProjectJsonAsync(Guid id);
    Task SaveProjectJsonAsync(Guid id, string json);
    Task DeleteProjectAsync(Guid id);

    /// <summary>Stores the image unless one with the same hash exists, and returns the stored metadata.</summary>
    Task<StoredImage> PutImageAsync(string hash, string extension, string contentType, byte[] content);
    Task<StoredImage?> GetImageAsync(string hash);
    Task<IReadOnlyList<StoredImage>> ListImagesAsync();
    Task<byte[]?> GetImageBytesAsync(string hash);
    Task DeleteImagesAsync(IReadOnlyCollection<string> hashes);

    /// <summary>Object URL for previewing an image. Revoke it with <see cref="RevokeObjectUrlAsync"/>.</summary>
    Task<string?> CreateObjectUrlAsync(string hash);
    Task RevokeObjectUrlAsync(string url);

    Task<string?> GetSettingAsync(string key);
    Task SetSettingAsync(string key, string value);

    Task<StorageEstimate> GetStorageEstimateAsync();

    /// <summary>Asks the browser not to evict the creator's data. Returns whether it was granted.</summary>
    Task<bool> RequestPersistenceAsync();
}

/// <summary>Metadata of an image in the image store (the bytes are fetched separately).</summary>
public record StoredImage(
    string Hash,
    string Extension,
    string ContentType,
    long Size,
    int? Width,
    int? Height,
    DateTimeOffset CreatedAt);

/// <param name="Usage">Bytes used by this origin, if the browser reports it.</param>
/// <param name="Quota">Bytes available to this origin, if the browser reports it.</param>
public record StorageEstimate(long? Usage, long? Quota, bool Persisted, bool CanPersist);
