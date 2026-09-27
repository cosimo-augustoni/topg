using MudBlazor;
using topg.Web.Client.Creator.Storage;

namespace topg.Web.Client.Creator.Components;

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

    public void UndoLast()
    {
        var label = session.Undo();
        snackbar.Add(label is null ? "Nothing to undo" : $"Undone: {label}", Severity.Normal);
    }
}
