namespace topg.Web.Client.Creator.Settings;

public enum ThemeMode
{
    Dark,
    Light,
    System,
}

/// <summary>Browser-wide defaults of the creator (S-2). Existing projects keep their own values.</summary>
public record CreatorSettings
{
    public static readonly IReadOnlyList<int> StandardPoints = [100, 200, 300, 400, 500];

    /// <summary>Pre-filled as base URL of new projects.</summary>
    public string DefaultBaseUrl { get; init; } = "";

    /// <summary>Points of new questions: a new question gets the first value its category doesn't have yet.</summary>
    public IReadOnlyList<int> DefaultPoints { get; init; } = StandardPoints;

    public ThemeMode Theme { get; init; } = ThemeMode.Dark;

    /// <summary>The "projects are only stored in this browser" hint on S-1 was closed.</summary>
    public bool ProjectsHintDismissed { get; init; }

    public static string FormatPoints(IEnumerable<int> points) => string.Join(", ", points);

    /// <summary>Parses "100, 200, 300". Returns an error message for the settings form, or null if valid.</summary>
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
