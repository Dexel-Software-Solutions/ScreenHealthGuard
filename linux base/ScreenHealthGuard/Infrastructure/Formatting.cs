namespace ScreenHealthGuard.Infrastructure;

internal static class Formatting
{
    /// <summary>Formats seconds as "3h 05m", "42m" or "&lt;1m".</summary>
    public static string Duration(int totalSeconds)
    {
        if (totalSeconds <= 0) return "0m";
        var span = TimeSpan.FromSeconds(totalSeconds);
        if (span.TotalMinutes < 1) return "<1m";
        if (span.TotalHours < 1) return $"{span.Minutes}m";
        return $"{(int)span.TotalHours}h {span.Minutes:00}m";
    }
}
