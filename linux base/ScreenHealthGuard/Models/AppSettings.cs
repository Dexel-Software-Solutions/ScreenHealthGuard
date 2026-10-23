namespace ScreenHealthGuard.Models;

/// <summary>User-configurable settings. Call <see cref="Normalize"/> after loading or editing.</summary>
internal sealed class AppSettings
{
    /// <summary>Minutes of screen time between eye breaks (the first "20" in 20-20-20).</summary>
    public int WorkIntervalMinutes { get; set; } = 20;

    /// <summary>Length of the eye break in seconds (the last "20" in 20-20-20).</summary>
    public int BreakSeconds { get; set; } = 20;

    /// <summary>No input for this long is recorded as idle time. Used for statistics only.</summary>
    public int IdleThresholdSeconds { get; set; } = 120;

    public int SnoozeMinutes { get; set; } = 5;
    public bool StartWithWindows { get; set; } = true;
    public bool PlaySound { get; set; } = true;
    public bool AutoWeeklyReport { get; set; } = true;
    public DateOnly? LastWeeklyReportDate { get; set; }

    public AppSettings Clone() => (AppSettings)MemberwiseClone();

    public void Normalize()
    {
        WorkIntervalMinutes = Math.Clamp(WorkIntervalMinutes, 1, 120);
        BreakSeconds = Math.Clamp(BreakSeconds, 10, 120);
        IdleThresholdSeconds = Math.Clamp(IdleThresholdSeconds, 30, 900);
        SnoozeMinutes = Math.Clamp(SnoozeMinutes, 1, 60);
    }
}
