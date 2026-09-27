using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace topg.Web.Client.Creator.Model;

/// <summary>CDN folder slugs: lowercase letters, digits and single dashes.</summary>
public static partial class Slug
{
    public const string Fallback = "quiz";

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex ValidPattern();

    public static bool IsValid(string? value) => value is not null && ValidPattern().IsMatch(value);

    /// <summary>"Pub Quiz – Sept. 2026!" → "pub-quiz-sept-2026". German umlauts become ae/oe/ue, other accents are dropped.</summary>
    public static string FromName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Fallback;
        }

        var text = name.Trim().ToLowerInvariant()
            .Replace("ä", "ae").Replace("ö", "oe").Replace("ü", "ue").Replace("ß", "ss");

        var builder = new StringBuilder(text.Length);
        var pendingDash = false;
        foreach (var c in text.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (c is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                if (pendingDash && builder.Length > 0)
                {
                    builder.Append('-');
                }

                builder.Append(c);
                pendingDash = false;
            }
            else
            {
                pendingDash = true;
            }
        }

        return builder.Length == 0 ? Fallback : builder.ToString();
    }
}
