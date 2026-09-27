using Microsoft.JSInterop;

namespace topg.Web.Client.Creator.Export;

public sealed class FileDownloader(IJSRuntime js) : IAsyncDisposable
{
    private Task<IJSObjectReference>? _module;

    public async Task DownloadAsync(string fileName, string contentType, byte[] content)
    {
        _module ??= js.InvokeAsync<IJSObjectReference>("import", "./creator-files.js").AsTask();
        await (await _module).InvokeVoidAsync("downloadFile", fileName, contentType, content);
    }

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
