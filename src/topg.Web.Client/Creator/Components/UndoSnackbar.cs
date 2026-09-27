using MudBlazor;
using topg.Web.Client.Creator.Storage;

namespace topg.Web.Client.Creator.Components;

/// <summary>UX-4: destructive editor actions happen right away and offer "Undo" in a snackbar for 8 seconds.</summary>
public class UndoSnackbar(ISnackbar snackbar, ProjectSession session)
{
    public void Show(string message) =>
        snackbar.Add(message, Severity.Normal, config =>
        {
            config.Action = "Undo";
            config.ActionColor = Color.Primary;
            config.VisibleStateDuration = 8000;
            config.OnClick = _ =>
            {
                session.Undo();
                return Task.CompletedTask;
            };
        }, key: Guid.NewGuid().ToString());

    /// <summary>Ctrl+Z: undo the last destructive change and say what was restored.</summary>
    public void UndoLast()
    {
        var label = session.Undo();
        snackbar.Add(label is null ? "Nothing to undo" : $"Undone: {label}", Severity.Normal);
    }
}
