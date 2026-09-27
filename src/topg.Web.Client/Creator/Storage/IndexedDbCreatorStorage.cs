using Microsoft.JSInterop;

namespace topg.Web.Client.Creator.Storage;

public sealed class IndexedDbCreatorStorage(IJSRuntime js) : ICreatorStorage, IAsyncDisposable
{
    private const string ModulePath = "./creator-storage.js";

    private Task<IJSObjectReference>? _module;

    private Task<IJSObjectReference> Module => _module ??= js.InvokeAsync<IJSObjectReference>("import", ModulePath).AsTask();

    private async Task<T> Invoke<T>(string identifier, params object?[] args) =>
        await (await Module).InvokeAsync<T>(identifier, args);

    private async Task InvokeVoid(string identifier, params object?[] args) =>
        await (await Module).InvokeVoidAsync(identifier, args);

    public async Task<IReadOnlyList<string>> ListProjectJsonAsync() => await Invoke<string[]>("listProjects");

    public Task<string?> GetProjectJsonAsync(Guid id) => Invoke<string?>("getProject", id.ToString());

    public Task SaveProjectJsonAsync(Guid id, string json) => InvokeVoid("saveProject", id.ToString(), json);

    public Task DeleteProjectAsync(Guid id) => InvokeVoid("deleteProject", id.ToString());

    public Task<StoredImage> PutImageAsync(string hash, string extension, string contentType, byte[] content) =>
        Invoke<StoredImage>("putImage", hash, extension, contentType, content);

    public Task<StoredImage?> GetImageAsync(string hash) => Invoke<StoredImage?>("getImageMetadata", hash);

    public async Task<IReadOnlyList<StoredImage>> ListImagesAsync() => await Invoke<StoredImage[]>("listImages");

    public Task<byte[]?> GetImageBytesAsync(string hash) => Invoke<byte[]?>("getImageBytes", hash);

    public Task DeleteImagesAsync(IReadOnlyCollection<string> hashes) => InvokeVoid("deleteImages", hashes);

    public Task<string?> CreateObjectUrlAsync(string hash) => Invoke<string?>("createObjectUrl", hash);

    public Task RevokeObjectUrlAsync(string url) => InvokeVoid("revokeObjectUrl", url);

    public Task<string?> GetSettingAsync(string key) => Invoke<string?>("getSetting", key);

    public Task SetSettingAsync(string key, string value) => InvokeVoid("setSetting", key, value);

    public Task<StorageEstimate> GetStorageEstimateAsync() => Invoke<StorageEstimate>("getStorageEstimate");

    public Task<bool> RequestPersistenceAsync() => Invoke<bool>("requestPersistence");

    public async ValueTask DisposeAsync()
    {
        if (_module is { IsCompletedSuccessfully: true })
        {
            try
            {
                await _module.Result.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
            }
        }
    }
}
