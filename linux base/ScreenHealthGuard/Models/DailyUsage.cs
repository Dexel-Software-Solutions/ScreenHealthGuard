namespace ScreenHealthGuard.Models;

/// <summary>Aggregated screen-usage statistics for a single calendar day.</summary>
internal sealed class DailyUsage
{
    public DateOnly Date { get; set; }
    public int ActiveSeconds { get; set; }
    public int LongestContinuousSeconds { get; set; }
    public int BreaksCompleted { get; set; }
    public int BreaksSkipped { get; set; }
    public int BreaksSnoozed { get; set; }

    /// <summary>Times the PC was locked or asleep long enough to count as a rest.</summary>
    public int NaturalBreaks { get; set; }

    public int[] HourlyActiveSeconds { get; set; } = new int[24];
}
