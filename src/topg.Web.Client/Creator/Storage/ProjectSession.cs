using topg.Web.Client.Creator.Model;
using topg.Web.Client.Creator.Validation;

namespace topg.Web.Client.Creator.Storage;

public enum EditorSelectionKind
{
    None,
    Category,
    Question,
}

/// <summary>What the inspector shows. Grid, outline, inspector and validation panel all read this one value.</summary>
public record EditorSelection(EditorSelectionKind Kind, Guid Id)
{
    public static readonly EditorSelection None = new(EditorSelectionKind.None, Guid.Empty);

    public static EditorSelection Question(Guid id) => new(EditorSelectionKind.Question, id);
    public static EditorSelection Category(Guid id) => new(EditorSelectionKind.Category, id);
}

/// <summary>
/// The project currently open in the editor, shared by the project pages and the app bar. Every edit goes through
/// <see cref="Change"/> / <see cref="ChangeWithUndo"/>, which revalidates, schedules the autosave and notifies the UI.
/// </summary>
public sealed class ProjectSession(ProjectStore store) : IAsyncDisposable
{
    private const int MaxUndoSteps = 20;

    private readonly Stack<(string Label, QuizProject Snapshot, EditorSelection Selection)> _undo = new();
    private Dictionary<Guid, List<ValidationIssue>> _issuesByTarget = [];
    private Dictionary<Guid, List<ValidationIssue>> _issuesByBoard = [];
    private Task<bool>? _opening;
    private Guid? _openingId;

    public QuizProject? Project { get; private set; }
    public ProjectAutosave? Autosave { get; private set; }
    public IReadOnlyList<ValidationIssue> Issues { get; private set; } = [];
    public int ErrorCount { get; private set; }
    public int WarningCount { get; private set; }

    /// <summary>Set when the stored project exists but could not be read.</summary>
    public string? LoadError { get; private set; }

    public EditorSelection Selection { get; private set; } = EditorSelection.None;

    /// <summary>Show required-field errors right away (the selection came from the validation panel).</summary>
    public bool ShowFieldErrors { get; private set; }

    /// <summary>Incremented when the inspector should focus its first field (e.g. after adding a question).</summary>
    public int FocusRequest { get; private set; }

    public string? UndoLabel => _undo.TryPeek(out var top) ? top.Label : null;

    /// <summary>Left pane of the board editor (toggled from the app bar).</summary>
    public bool OutlineOpen { get; private set; } = true;

    public event Action? Changed;

    /// <summary>Loads the project unless it is already open. Returns false if it doesn't exist or can't be read.</summary>
    public Task<bool> OpenAsync(Guid id)
    {
        if (Project?.Id == id)
        {
            return Task.FromResult(true);
        }

        if (_openingId == id && _opening is not null)
        {
            return _opening;
        }

        _openingId = id;
        return _opening = OpenCoreAsync(id);
    }

    private async Task<bool> OpenCoreAsync(Guid id)
    {
        try
        {
            await CloseAsync();
            try
            {
                Project = await store.GetAsync(id);
            }
            catch (System.Text.Json.JsonException ex)
            {
                LoadError = ex.Message;
            }

            if (Project is not null)
            {
                Autosave = new ProjectAutosave(store);
                Autosave.Changed += Notify;
                Revalidate();
            }

            Notify();
            return Project is not null;
        }
        finally
        {
            _opening = null;
            _openingId = null;
        }
    }

    /// <summary>Writes pending changes and forgets the project (e.g. before the project list edits the store directly).</summary>
    public async Task CloseAsync()
    {
        if (Autosave is not null)
        {
            Autosave.Changed -= Notify;
            await Autosave.DisposeAsync();
        }

        Project = null;
        Autosave = null;
        LoadError = null;
        Issues = [];
        _issuesByTarget = [];
        _issuesByBoard = [];
        ErrorCount = WarningCount = 0;
        _undo.Clear();
        Selection = EditorSelection.None;
    }

    /// <summary>Call after changing the project in place (e.g. a text field).</summary>
    public void Change(Action<QuizProject>? edit = null)
    {
        if (Project is null)
        {
            return;
        }

        edit?.Invoke(Project);
        Revalidate();
        Autosave?.ScheduleSave(Project);
        Notify();
    }

    /// <summary>A destructive change (delete, move) that the user can undo from the snackbar or with Ctrl+Z.</summary>
    public void ChangeWithUndo(string label, Action<QuizProject> edit)
    {
        if (Project is null)
        {
            return;
        }

        _undo.Push((label, CreatorJson.Clone(Project), Selection));
        TrimUndo();
        Change(edit);
    }

    /// <summary>Restores the state before the last undoable change. Returns its label, or null if there was nothing to undo.</summary>
    public string? Undo()
    {
        if (Project is null || !_undo.TryPop(out var step))
        {
            return null;
        }

        Project = step.Snapshot;
        Selection = step.Selection;
        Change();
        return step.Label;
    }

    public Task SaveNowAsync() => Autosave?.SaveNowAsync() ?? Task.CompletedTask;

    public void Select(EditorSelection selection, bool focus = false, bool showFieldErrors = false)
    {
        Selection = selection;
        ShowFieldErrors = showFieldErrors;
        if (focus)
        {
            FocusRequest++;
        }

        Notify();
    }

    public void ClearSelection() => Select(EditorSelection.None);

    public void ToggleOutline()
    {
        OutlineOpen = !OutlineOpen;
        Notify();
    }

    /// <summary>Board containing the selected question or category.</summary>
    public BoardDraft? SelectedBoard() => Project is null
        ? null
        : Selection.Kind switch
        {
            EditorSelectionKind.Question => Project.FindQuestion(Selection.Id)?.Board,
            EditorSelectionKind.Category => Project.FindCategory(Selection.Id)?.Board,
            _ => null,
        };

    public IReadOnlyList<ValidationIssue> IssuesFor(Guid targetId) =>
        _issuesByTarget.TryGetValue(targetId, out var issues) ? issues : [];

    /// <summary>All issues of a board, its categories and questions.</summary>
    public IReadOnlyList<ValidationIssue> IssuesInBoard(Guid boardId) =>
        _issuesByBoard.TryGetValue(boardId, out var issues) ? issues : [];

    private void Revalidate()
    {
        Issues = Project is null ? [] : ProjectValidator.Validate(Project);
        _issuesByTarget = Issues.GroupBy(i => i.TargetId).ToDictionary(g => g.Key, g => g.ToList());
        _issuesByBoard = Issues.Where(i => i.BoardId is not null).GroupBy(i => i.BoardId!.Value).ToDictionary(g => g.Key, g => g.ToList());
        ErrorCount = Issues.Count(i => i.Severity == ValidationSeverity.Error);
        WarningCount = Issues.Count - ErrorCount;
    }

    private void TrimUndo()
    {
        if (_undo.Count <= MaxUndoSteps)
        {
            return;
        }

        var keep = _undo.Take(MaxUndoSteps).Reverse().ToList();
        _undo.Clear();
        foreach (var step in keep)
        {
            _undo.Push(step);
        }
    }

    private void Notify() => Changed?.Invoke();

    public async ValueTask DisposeAsync() => await CloseAsync();
}
