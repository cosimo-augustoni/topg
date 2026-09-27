using System.Globalization;

namespace topg.Web.Client.Creator.Components;

public static class RelativeTime
{
    // Invariant culture instead of the browser language, because the UI is English.
    public static string Absolute(DateTimeOffset time) =>
        time.ToLocalTime().ToString("d MMM yyyy, HH:mm", CultureInfo.InvariantCulture);

    public static string Format(DateTimeOffset time, DateTimeOffset? now = null)
    {
        var current = now ?? DateTimeOffset.Now;
        var elapsed = current - time;
        if (elapsed < TimeSpan.FromMinutes(1))
        {
            return "just now";
        }

        if (elapsed < TimeSpan.FromHours(1))
        {
            return $"{(int)elapsed.TotalMinutes} min ago";
        }

        var local = time.ToLocalTime();
        var localNow = current.ToLocalTime();
        if (local.Date == localNow.Date)
        {
            return $"{(int)elapsed.TotalHours} h ago";
        }

        if (local.Date == localNow.Date.AddDays(-1))
        {
            return "yesterday";
        }

        var format = local.Year == localNow.Year ? "MMM d" : "MMM d, yyyy";
        return local.ToString(format, CultureInfo.InvariantCulture);
    }
}
