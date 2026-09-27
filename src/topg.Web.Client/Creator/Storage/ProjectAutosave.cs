using topg.Web.Client.Creator.Model;

namespace topg.Web.Client.Creator.Storage;

public enum SaveStatus
{
    Saved,

    Saving,
    Failed,
}

public sealed class ProjectAutosave(ProjectStore store, TimeSpan? delay = null) : IAsyncDisposable
{
    public static readonly TimeSpan DefaultDelay = TimeSpan.FromSeconds(1);

    private readonly TimeSpan _delay = delay ?? DefaultDelay;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private CancellationTokenSource? _debounce;
    private QuizProject? _project;
    private long _changeVersion;
    private long _savedVersion;

    public SaveStatus Status { get; private set; } = SaveStatus.Saved;
    public DateTimeOffset? LastSavedAt { get; private set; }
    public string? Error { get; private set; }
    public bool HasPendingChanges => _changeVersion != _savedVersion;

    public event Action? Changed;

    public void ScheduleSave(QuizProject project)
    {
        _project = project;
        _changeVersion++;
        SetStatus(SaveStatus.Saving);

        _debounce?.Cancel();
        _debounce = new CancellationTokenSource();
        _ = SaveAfterDelayAsync(_debounce.Token);
    }

    public async Task SaveNowAsync()
    {
        _debounce?.Cancel();
        if (_project is null)
        {
            return;
        }

        await _gate.WaitAsync();
        try
        {
            if (!HasPendingChanges && Status != SaveStatus.Failed)
            {
                return;
            }

            var version = _changeVersion;
            SetStatus(SaveStatus.Saving);
            await store.SaveAsync(_project);
            _savedVersion = version;
            LastSavedAt = DateTimeOffset.Now;
            Error = null;
            SetStatus(HasPendingChanges ? SaveStatus.Saving : SaveStatus.Saved);
        }
        catch (Exception ex)
        {
            Error = ex.Message;
            SetStatus(SaveStatus.Failed);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task SaveAfterDelayAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(_delay, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        await SaveNowAsync();
    }

    private void SetStatus(SaveStatus status)
    {
        Status = status;
        Changed?.Invoke();
    }

    public async ValueTask DisposeAsync()
    {
        if (HasPendingChanges)
        {
            await SaveNowAsync();
        }

        _debounce?.Cancel();
        _debounce?.Dispose();
        _gate.Dispose();
    }
}
