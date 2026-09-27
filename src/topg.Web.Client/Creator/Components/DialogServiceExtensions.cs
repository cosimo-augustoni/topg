using MudBlazor;

namespace topg.Web.Client.Creator.Components;

public static class DialogServiceExtensions
{
    /// <summary>Shows C-6 and returns true if the user confirmed.</summary>
    public static async Task<bool> ShowConfirmAsync(this IDialogService dialogs, string title, string message, string confirmText,
        bool destructive = false, string? requiredText = null)
    {
        var parameters = new DialogParameters<ConfirmDialog>
        {
            { d => d.Message, message },
            { d => d.ConfirmText, confirmText },
            { d => d.Destructive, destructive },
            { d => d.RequiredText, requiredText },
        };
        var dialog = await dialogs.ShowAsync<ConfirmDialog>(title, parameters, SmallDialog);
        var result = await dialog.Result;
        return result is { Canceled: false };
    }

    /// <summary>Asks for one line of text. Returns null if cancelled.</summary>
    public static async Task<string?> ShowTextPromptAsync(this IDialogService dialogs, string title, string label, string initialValue,
        string confirmText = "Save")
    {
        var parameters = new DialogParameters<TextPromptDialog>
        {
            { d => d.Label, label },
            { d => d.InitialValue, initialValue },
            { d => d.ConfirmText, confirmText },
        };
        var dialog = await dialogs.ShowAsync<TextPromptDialog>(title, parameters, SmallDialog);
        var result = await dialog.Result;
        return result is { Canceled: false, Data: string text } ? text : null;
    }

    public static readonly DialogOptions SmallDialog = new()
    {
        MaxWidth = MaxWidth.ExtraSmall,
        FullWidth = true,
        CloseOnEscapeKey = true,
    };
}
