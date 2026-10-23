using ScreenHealthGuard.Infrastructure;
using ScreenHealthGuard.Models;

namespace ScreenHealthGuard.Services;

/// <summary>Keeps today's statistics in memory and persists one JSON file per day. UI-thread only.</summary>
internal sealed class UsageStore
{
    private const int RetentionDays = 90;

    private DailyUsage _today;
    private double _carrySeconds;

    public UsageStore()
    {
        PurgeOldFiles();
        _today = LoadOrCreate(TodayDate());
    }

    public DailyUsage Today => EnsureToday();

    public void AddActive(TimeSpan delta)
    {
        var today = EnsureToday();
        _carrySeconds += delta.TotalSeconds;
        int whole = (int)_carrySeconds;
        if (whole <= 0) return;

        _carrySeconds -= whole;
        today.ActiveSeconds += whole;
        today.HourlyActiveSeconds[DateTime.Now.Hour] += whole;
    }

    public void RegisterContinuous(int seconds)
    {
        var today = EnsureToday();
        if (seconds > today.LongestContinuousSeconds)
        {
            today.LongestContinuousSeconds = seconds;
        }
    }

    public void RegisterBreak(BreakOutcome outcome)
    {
        var today = EnsureToday();
        switch (outcome)
        {
            case BreakOutcome.Completed: today.BreaksCompleted++; break;
            case BreakOutcome.Skipped: today.BreaksSkipped++; break;
            case BreakOutcome.Snoozed: today.BreaksSnoozed++; break;
        }
    }

    public void RegisterNaturalBreak() => EnsureToday().NaturalBreaks++;

    public void Save()
    {
        try
        {
            JsonStorage.WriteAtomic(PathFor(_today.Date), _today);
        }
        catch (Exception ex)
        {
            Logger.Error("Could not save usage data.", ex);
        }
    }

    /// <summary>Returns one entry per day in [from, to], using empty records for missing days.</summary>
    public IReadOnlyList<DailyUsage> LoadRange(DateOnly from, DateOnly to)
    {
        EnsureToday();
        var result = new List<DailyUsage>();
        for (var day = from; day <= to; day = day.AddDays(1))
        {
            result.Add(day == _today.Date ? _today : LoadOrCreate(day));
        }

        return result;
    }

    private DailyUsage EnsureToday()
    {
        var date = TodayDate();
        if (_today.Date != date)
        {
            Save();
            _today = LoadOrCreate(date);
            _carrySeconds = 0;
        }

        return _today;
    }

    private static DateOnly TodayDate() => DateOnly.FromDateTime(DateTime.Now);

    private static string PathFor(DateOnly date) =>
        Path.Combine(AppPaths.Data, $"usage-{date:yyyy-MM-dd}.json");

    private static DailyUsage LoadOrCreate(DateOnly date)
    {
        var usage = JsonStorage.TryRead<DailyUsage>(PathFor(date)) ?? new DailyUsage();
        usage.Date = date;
        if (usage.HourlyActiveSeconds is not { Length: 24 })
        {
            usage.HourlyActiveSeconds = new int[24];
        }

        return usage;
    }

    private static void PurgeOldFiles()
    {
        try
        {
            if (!Directory.Exists(AppPaths.Data)) return;
            var cutoff = DateTime.Now.AddDays(-RetentionDays);
            foreach (var file in Directory.EnumerateFiles(AppPaths.Data, "usage-*.json"))
            {
                if (File.GetLastWriteTime(file) < cutoff) File.Delete(file);
            }
        }
        catch (Exception ex)
        {
            Logger.Warn($"Old data cleanup failed: {ex.Message}");
        }
    }
}
