using MudBlazor;
using topg.Web.Client.Shared;

namespace topg.Web.Client.Creator.Components;

public static class CreatorIcons
{
    public const string Board = Icons.Material.Outlined.GridView;
    public const string Category = Icons.Material.Outlined.ViewColumn;
    public const string Error = Icons.Material.Filled.Error;
    public const string Warning = Icons.Material.Filled.Warning;

    public static string For(QuestionType type) => type switch
    {
        QuestionType.Text => Icons.Material.Outlined.Notes,
        QuestionType.Image => Icons.Material.Outlined.Image,
        QuestionType.Sound => Icons.Material.Outlined.MusicNote,
        _ => Icons.Material.Outlined.HelpOutline,
    };

    public static string Label(QuestionType type) => type switch
    {
        QuestionType.Text => "Text",
        QuestionType.Image => "Image",
        QuestionType.Sound => "Sound",
        _ => type.ToString(),
    };

    public static string For(AnswerType type) => type switch
    {
        AnswerType.Buzzer => Icons.Material.Outlined.NotificationsActive,
        AnswerType.Text => Icons.Material.Outlined.Keyboard,
        _ => Icons.Material.Outlined.HelpOutline,
    };

    public static string Label(AnswerType type) => type switch
    {
        AnswerType.Buzzer => "Buzzer",
        AnswerType.Text => "Text input",
        _ => type.ToString(),
    };
}
