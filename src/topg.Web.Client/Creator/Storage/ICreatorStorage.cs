namespace topg.Web.Client.Creator.Storage;

public interface ICreatorStorage
{
    Task<IReadOnlyList<string>> ListProjectJsonAsync();
    Task<string?> GetProjectJsonAsync(Guid id);
    Task SaveProjectJsonAsync(Guid id, string json);
    Task DeleteProjectAsync(Guid id);

    Task<StoredImage> PutImageAsync(string hash, string extension, string contentType, byte[] content);
    Task<StoredImage?> GetImageAsync(string hash);
    Task<IReadOnlyList<StoredImage>> ListImagesAsync();
    Task<byte[]?> GetImageBytesAsync(string hash);
    Task DeleteImagesAsync(IReadOnlyCollection<string> hashes);

    Task<string?> CreateObjectUrlAsync(string hash);
    Task RevokeObjectUrlAsync(string url);

    Task<string?> GetSettingAsync(string key);
    Task SetSettingAsync(string key, string value);

    Task<StorageEstimate> GetStorageEstimateAsync();

    Task<bool> RequestPersistenceAsync();
}

public record StoredImage(
    string Hash,
    string Extension,
    string ContentType,
    long Size,
    int? Width,
    int? Height,
    DateTimeOffset CreatedAt);

public record StorageEstimate(long? Usage, long? Quota, bool Persisted, bool CanPersist);
