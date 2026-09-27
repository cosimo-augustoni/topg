namespace topg.Web.Client.Creator.Settings;

public enum ThemeMode
{
    Dark,
    Light,
    System,
}

public record CreatorSettings
{
    public static readonly IReadOnlyList<int> StandardPoints = [100, 200, 300, 400, 500];

    public string DefaultBaseUrl { get; init; } = "";

    public IReadOnlyList<int> DefaultPoints { get; init; } = StandardPoints;

    public ThemeMode Theme { get; init; } = ThemeMode.Dark;

    public bool ProjectsHintDismissed { get; init; }

    public static string FormatPoints(IEnumerable<int> points) => string.Join(", ", points);

    public static string? TryParsePoints(string? text, out IReadOnlyList<int> points)
    {
        points = [];
        var parts = (text ?? "").Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return "Enter at least one value.";
        }

        var values = new List<int>();
        foreach (var part in parts)
        {
            if (!int.TryParse(part, out var value) || value <= 0)
            {
                return $"\"{part}\" is not a positive whole number.";
            }

            values.Add(value);
        }

        points = values;
        return null;
    }
}
