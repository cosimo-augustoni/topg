namespace topg.Web.Quiz.Execution;

public class TextInputState
{
    private Dictionary<string, string> TextInputsByPlayer { get; } = new();
    public bool IsRevealed { get; set; }

    public void UpdateTextInput(Player player, string? text)
    {
        TextInputsByPlayer[player.Id] = text ?? string.Empty;
    }

    public string GetTextByPlayer(Player player)
    {
        return TextInputsByPlayer.TryGetValue(player.Id, out var textInput) ? textInput : string.Empty;
    }

    public void Clear()
    {
        TextInputsByPlayer.Clear();
    }
}