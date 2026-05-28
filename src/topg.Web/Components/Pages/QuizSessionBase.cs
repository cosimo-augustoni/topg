using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.JSInterop;
using topg.Web.Quiz.Execution;
using topg.Web.Quiz.Management;

namespace topg.Web.Components.Pages;

public abstract class QuizSessionBase : ComponentBase, IAsyncDisposable
{
    [Inject]
    protected IJSRuntime JS { get; set; } = null!;

    [Inject]
    protected ProtectedLocalStorage ProtectedLocalStorage { get; set; } = null!;

    protected QuizSession? Session;

    protected double Volume { get; set; } = 0.5;

    protected async Task SaveVolumeAsync()
    {
        await ProtectedLocalStorage.SetAsync(SessionConstants.VolumeStorageId, Volume);
    }

    protected void SubscribeToSession(QuizSession session)
    {
        Session = session;
        Session.SessionStateChanged += OnSessionStateChangedAsync;
        Session.SoundEffectManager.SoundEffectRequested += PlaySoundAsync;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            try
            {
                var volume = await ProtectedLocalStorage.GetAsync<double>(SessionConstants.VolumeStorageId);
                if (volume.Success)
                {
                    Volume = volume.Value;
                    StateHasChanged();
                }
            }
            catch
            {
                // Use default volume if storage is unavailable
            }
        }
        await base.OnAfterRenderAsync(firstRender);
    }

    private async Task OnSessionStateChangedAsync(object sender, EventArgs e)
    {
        await InvokeAsync(StateHasChanged);
    }

    private async Task PlaySoundAsync(object sender, SoundEffectArgs e)
    {
        await JS.InvokeVoidAsync("playSound", $"/sounds/{e.SoundEffect.DisplayName}", Volume);
    }

    public virtual ValueTask DisposeAsync()
    {
        if (Session != null)
        {
            Session.SessionStateChanged -= OnSessionStateChangedAsync;
            Session.SoundEffectManager.SoundEffectRequested -= PlaySoundAsync;
        }
        return ValueTask.CompletedTask;
    }
}
