using System.Globalization;
using System.Net;
using System.Text;
using ScreenHealthGuard.Infrastructure;
using ScreenHealthGuard.Models;

namespace ScreenHealthGuard.Services;

/// <summary>Builds the self-contained weekly HTML health report.</summary>
internal sealed class ReportService
{
    private readonly UsageStore _store;

    public ReportService(UsageStore store) => _store = store;

    /// <summary>Generates a report for the 7 days ending on <paramref name="endDate"/> and returns its path.</summary>
    public string Generate(DateOnly endDate)
    {
        _store.Save();
        var from = endDate.AddDays(-6);
        var days = _store.LoadRange(from, endDate);
        var summary = WeeklySummary.Create(days);

        Directory.CreateDirectory(AppPaths.Reports);
        var fileName = $"weekly-report-{endDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}.html";
        var path = Path.Combine(AppPaths.Reports, fileName);
        File.WriteAllText(path, Render(from, endDate, days, summary), new UTF8Encoding(false));
        Logger.Info($"Weekly report written: {path}");
        return path;
    }

    private static string E(string text) => WebUtility.HtmlEncode(text);

    private static string Render(DateOnly from, DateOnly to, IReadOnlyList<DailyUsage> days, WeeklySummary s)
    {
        var inv = CultureInfo.InvariantCulture;
        var sb = new StringBuilder(16 * 1024);

        sb.AppendLine("<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"utf-8\">");
        sb.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        sb.AppendLine($"<title>{E(AppInfo.Name)} - Weekly Report</title><style>{Css}</style></head><body><main>");

        // Header with score ring
        sb.AppendLine("<header>");
        sb.AppendLine($"<div class=\"ring\" style=\"--p:{s.HealthScore}\"><span>{s.HealthScore}</span></div>");
        sb.AppendLine("<div>");
        sb.AppendLine("<h1>Weekly Screen Health Report</h1>");
        sb.AppendLine($"<p class=\"sub\">{from.ToString("d MMM", inv)} &ndash; {to.ToString("d MMM yyyy", inv)} &middot; " +
                      $"Health score: <strong>{E(s.Grade)}</strong></p>");
        sb.AppendLine("</div></header>");

        // Stat cards
        sb.AppendLine("<section class=\"grid\">");
        Card(sb, Formatting.Duration(s.TotalActiveSeconds), "Total screen time");
        Card(sb, Formatting.Duration((int)s.AverageActiveSecondsPerDay), "Daily average");
        Card(sb, Formatting.Duration(s.LongestContinuousSeconds), "Longest non-stop stretch");
        Card(sb, s.BreaksCompleted.ToString(inv), "Reminded breaks taken");
        Card(sb, s.BreaksSkipped.ToString(inv), "Breaks skipped");
        Card(sb, s.NaturalBreaks.ToString(inv), "Self-initiated breaks");
        Card(sb, s.ComplianceRate is { } r ? $"{r * 100:0}%" : "n/a", "Break compliance");
        Card(sb, s.PeakHour is { } h ? $"{h:00}:00-{(h + 1) % 24:00}:00" : "n/a", "Busiest hour");
        sb.AppendLine("</section>");

        // Daily bars
        int max = Math.Max(1, days.Max(d => d.ActiveSeconds));
        sb.AppendLine("<section class=\"card\"><h2>Daily screen time</h2>");
        foreach (var d in days)
        {
            double pct = 100.0 * d.ActiveSeconds / max;
            sb.AppendLine("<div class=\"bar-row\">" +
                          $"<span>{E(d.Date.ToString("ddd d MMM", inv))}</span>" +
                          $"<div class=\"track\"><div class=\"fill\" style=\"width:{pct.ToString("0.0", inv)}%\"></div></div>" +
                          $"<span>{(d.ActiveSeconds > 0 ? E(Formatting.Duration(d.ActiveSeconds)) : "&ndash;")}</span></div>");
        }

        sb.AppendLine("</section><br>");

        // Hourly heat strip
        long hourMax = Math.Max(1, s.HourlyTotals.Max());
        sb.AppendLine("<section class=\"card\"><h2>When you use your computer</h2><div class=\"hours\">");
        for (int hour = 0; hour < 24; hour++)
        {
            double v = (double)s.HourlyTotals[hour] / hourMax;
            sb.Append($"<div class=\"hr\" style=\"--v:{v.ToString("0.00", inv)}\" " +
                      $"title=\"{hour:00}:00 - {Formatting.Duration((int)s.HourlyTotals[hour])}\"></div>");
        }

        sb.AppendLine("</div><div class=\"hl\">");
        for (int hour = 0; hour < 24; hour++)
        {
            sb.Append($"<span>{(hour % 3 == 0 ? hour.ToString("00", inv) : "")}</span>");
        }

        sb.AppendLine("</div></section><br>");

        // Insights
        sb.AppendLine("<section class=\"card\"><h2>Insights &amp; suggestions</h2><ul>");
        foreach (var tip in s.Insights)
        {
            sb.AppendLine($"<li>{E(tip)}</li>");
        }

        sb.AppendLine("</ul></section>");

        sb.AppendLine("<p class=\"note\">This report is general wellness information based on your computer activity, " +
                      "not medical advice. If you have persistent eye discomfort or headaches, consult an eye-care professional.</p>");

        sb.AppendLine("<footer>" +
                      $"{E(AppInfo.Name)} v{E(AppInfo.Version)} &middot; Developed by {E(AppInfo.DeveloperName)}<br>" +
                      $"<a href=\"{AppInfo.GitHubUrl}\">GitHub</a> &middot; " +
                      $"<a href=\"mailto:{AppInfo.PrimaryEmail}\">{E(AppInfo.PrimaryEmail)}</a> &middot; " +
                      $"<a href=\"mailto:{AppInfo.SecondaryEmail}\">{E(AppInfo.SecondaryEmail)}</a>" +
                      "</footer>");

        sb.AppendLine("</main></body></html>");
        return sb.ToString();
    }

    private static void Card(StringBuilder sb, string value, string label) =>
        sb.AppendLine($"<div class=\"card\"><div class=\"v\">{E(value)}</div><div class=\"l\">{E(label)}</div></div>");

    private const string Css = """
:root{--bg:#f5f7fa;--card:#fff;--text:#1d2430;--muted:#6b7686;--accent:#2fa76f;--track:#e6eaf0}
@media (prefers-color-scheme:dark){:root{--bg:#141922;--card:#1d2430;--text:#e8edf4;--muted:#93a0b3;--accent:#4cc38a;--track:#2a3342}}
*{box-sizing:border-box}
body{margin:0;font-family:"Segoe UI",system-ui,sans-serif;background:var(--bg);color:var(--text);line-height:1.5}
main{max-width:960px;margin:0 auto;padding:32px 20px}
header{display:flex;gap:24px;align-items:center;flex-wrap:wrap;margin-bottom:24px}
h1{margin:0;font-size:1.6rem}h2{font-size:1.1rem;margin:0 0 12px}
.sub{color:var(--muted);margin:4px 0 0}
.ring{width:110px;height:110px;border-radius:50%;display:grid;place-items:center;background:conic-gradient(var(--accent) calc(var(--p)*1%),var(--track) 0)}
.ring span{width:84px;height:84px;border-radius:50%;background:var(--card);display:grid;place-items:center;font-size:1.7rem;font-weight:700}
.grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(170px,1fr));gap:14px;margin-bottom:20px}
.card{background:var(--card);border-radius:14px;padding:16px 18px;box-shadow:0 1px 3px rgba(0,0,0,.08)}
.card .v{font-size:1.5rem;font-weight:700}.card .l{color:var(--muted);font-size:.85rem}
.bar-row{display:grid;grid-template-columns:90px 1fr 70px;gap:10px;align-items:center;margin:8px 0}
.track{background:var(--track);border-radius:6px;height:12px;overflow:hidden}
.fill{background:var(--accent);height:100%;border-radius:6px}
.hours,.hl{display:grid;grid-template-columns:repeat(24,1fr);gap:3px}
.hr{height:34px;border-radius:4px;background:var(--accent);opacity:calc(.1 + var(--v)*.9)}
.hl{color:var(--muted);font-size:.65rem;text-align:center;margin-top:4px}
ul{padding-left:20px;margin:0}li{margin:6px 0}
.note{font-size:.8rem;color:var(--muted);margin-top:20px}
footer{color:var(--muted);font-size:.85rem;margin-top:24px;text-align:center}a{color:var(--accent)}
""";
}

/// <summary>Computed weekly statistics, health score and plain-language insights.</summary>
internal sealed class WeeklySummary
{
    public int TotalActiveSeconds { get; private init; }
    public double AverageActiveSecondsPerDay { get; private init; }
    public int LongestContinuousSeconds { get; private init; }
    public int BreaksCompleted { get; private init; }
    public int BreaksSkipped { get; private init; }
    public int NaturalBreaks { get; private init; }
    public double? ComplianceRate { get; private init; }
    public int? PeakHour { get; private init; }
    public long[] HourlyTotals { get; private init; } = new long[24];
    public int HealthScore { get; private init; }
    public string Grade { get; private init; } = "Not enough data yet";
    public IReadOnlyList<string> Insights { get; private init; } = Array.Empty<string>();

    public static WeeklySummary Create(IReadOnlyList<DailyUsage> days)
    {
        var tracked = days.Where(d => d.ActiveSeconds > 0).ToList();
        int total = days.Sum(d => d.ActiveSeconds);
        double average = tracked.Count == 0 ? 0 : (double)total / tracked.Count;
        int longest = days.Max(d => d.LongestContinuousSeconds);
        int completed = days.Sum(d => d.BreaksCompleted);
        int skipped = days.Sum(d => d.BreaksSkipped);
        int natural = days.Sum(d => d.NaturalBreaks);

        var hourly = new long[24];
        foreach (var d in days)
            for (int h = 0; h < 24; h++)
                hourly[h] += d.HourlyActiveSeconds[h];

        long hourlyMax = hourly.Max();
        int? peak = hourlyMax > 0 ? Array.IndexOf(hourly, hourlyMax) : null;

        int prompted = completed + skipped;
        double? compliance = prompted == 0 ? null : (double)completed / prompted;

        int score = 0;
        string grade = "Not enough data yet";
        if (tracked.Count > 0)
        {
            double breakScore = 50 * (compliance ?? 1.0);
            double avgHours = average / 3600.0;
            double loadScore = 30 * Math.Clamp((12 - avgHours) / 6, 0, 1);              // <=6h full marks, >=12h zero
            double stretchScore = 20 * Math.Clamp((180 - longest / 60.0) / 120, 0, 1);  // <=60min full, >=180min zero
            score = (int)Math.Round(breakScore + loadScore + stretchScore);
            grade = score >= 85 ? "Excellent" : score >= 70 ? "Good" : score >= 50 ? "Needs attention" : "At risk";
        }

        return new WeeklySummary
        {
            TotalActiveSeconds = total,
            AverageActiveSecondsPerDay = average,
            LongestContinuousSeconds = longest,
            BreaksCompleted = completed,
            BreaksSkipped = skipped,
            NaturalBreaks = natural,
            ComplianceRate = compliance,
            PeakHour = peak,
            HourlyTotals = hourly,
            HealthScore = score,
            Grade = grade,
            Insights = BuildInsights(tracked.Count, average, longest, compliance, skipped, peak, score)
        };
    }

    private static List<string> BuildInsights(int trackedDays, double avgSeconds, int longest,
        double? compliance, int skipped, int? peak, int score)
    {
        var tips = new List<string>();
        if (trackedDays == 0)
        {
            tips.Add("No activity was recorded this week. Keep the app running in the tray and your report will fill up.");
            return tips;
        }

        double avgHours = avgSeconds / 3600.0;
        if (score >= 85) tips.Add("Great week! Your break habits are protecting your eyes. Keep it up.");
        if (compliance is < 0.5 && skipped > 0)
            tips.Add($"You skipped {skipped} break reminder(s). Even a 20-second look into the distance relaxes your focusing muscles.");
        else if (compliance is >= 0.8)
            tips.Add("You responded to most break reminders. That consistency is the best defence against digital eye strain.");
        if (avgHours > 9)
            tips.Add($"You averaged {avgHours:0.0} hours of active screen time per day. Try to move some tasks away from the screen (calls, reading, planning on paper).");
        if (longest > 120)
            tips.Add($"Your longest non-stop stretch was {Formatting.Duration(longest)}. Aim to stand up and move at least once every hour.");
        if (peak is >= 22 or < 5)
            tips.Add("Your busiest hour is late at night. Evening screen light can delay sleep; consider Night light and avoid screens 30-60 minutes before bed.");

        tips.Add("Follow the 20-20-20 rule: every 20 minutes, look at something 20 feet (6 m) away for 20 seconds.");
        tips.Add("Blink fully and often, keep the screen slightly below eye level at arm's length, and match screen brightness to your room.");
        tips.Add("Stay hydrated and stretch your neck, shoulders and wrists during longer breaks.");
        return tips;
    }
}
