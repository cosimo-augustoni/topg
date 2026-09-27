using System.Text.Json;
using topg.Web.Client.Creator.Model;
using topg.Web.Client.Creator.Storage;

namespace topg.Web.Client.Creator.Settings;

/// <summary>Loads and saves <see cref="CreatorSettings"/> in the IndexedDB <c>settings</c> store.</summary>
public class CreatorSettingsService(ICreatorStorage storage)
{
    private const string StorageKey = "creator-settings";

    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private Task? _loading;

    public CreatorSettings Current { get; private set; } = new();

    /// <summary>Set when the stored settings couldn't be loaded; the defaults are used instead.</summary>
    public string? LoadError { get; private set; }

    public event Action? Changed;

    /// <summary>Loads the settings once. Safe to call from every page.</summary>
    public Task EnsureLoadedAsync() => _loading ??= LoadAsync();

    /// <summary>
    /// Applies the settings right away and persists them. <see cref="Current"/> is updated before the write, so a
    /// second change made while the first is still being written (e.g. blur + click) builds on the first one.
    /// </summary>
    public async Task SaveAsync(CreatorSettings settings)
    {
        Current = settings;
        Changed?.Invoke();

        await _writeGate.WaitAsync();
        try
        {
            // Always write the latest state; an older write that was queued behind a newer change is harmless.
            await storage.SetSettingAsync(StorageKey, CreatorJson.Serialize(Current));
        }
        finally
        {
            _writeGate.Release();
        }
    }

    private async Task LoadAsync()
    {
        try
        {
            var json = await storage.GetSettingAsync(StorageKey);
            if (json is not null)
            {
                Current = CreatorJson.Deserialize<CreatorSettings>(json);
            }
        }
        catch (Exception ex) when (ex is JsonException or Microsoft.JSInterop.JSException)
        {
            LoadError = ex.Message;
        }

        Changed?.Invoke();
    }
}
