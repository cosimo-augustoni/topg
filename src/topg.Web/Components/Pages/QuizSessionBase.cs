using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using topg.Web.Quiz.Execution;

namespace topg.Web.Components.Pages;

public abstract class QuizSessionBase : ComponentBase, IAsyncDisposable
{
    [Inject]
    protected IJSRuntime JS { get; set; } = default!;

    protected QuizSession? Session;

    protected void SubscribeToSession(QuizSession session)
    {
        Session = session;
        Session.SessionStateChanged += OnSessionStateChangedAsync;
        Session.SoundEffectManager.SoundEffectRequested += PlaySoundAsync;
    }

    private async Task OnSessionStateChangedAsync(object sender, EventArgs e)
    {
        await InvokeAsync(StateHasChanged);
    }

    private async Task PlaySoundAsync(object sender, SoundEffectArgs e)
    {
        await JS.InvokeVoidAsync("playSound", $"/sounds/{e.SoundEffect.DisplayName}");
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
