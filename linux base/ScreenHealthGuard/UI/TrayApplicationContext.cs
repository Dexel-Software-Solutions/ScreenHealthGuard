using Microsoft.Win32;
using ScreenHealthGuard.Infrastructure;
using ScreenHealthGuard.Models;
using ScreenHealthGuard.Services;

namespace ScreenHealthGuard.UI;

/// <summary>
/// Composition root and UI shell: owns the services, the tray icon and its menu.
/// The app has no main window; everything is driven from the system tray.
/// </summary>
internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly SettingsService _settings = new();
    private readonly UsageStore _usage = new();
    private readonly ReminderEngine _engine;
    private readonly ActivityMonitor _monitor;
    private readonly ReportService _reports;
    private readonly NotifyIcon _tray;
    private readonly Icon _activeIcon = TrayIconFactory.Create(paused: false);
    private readonly Icon _pausedIcon = TrayIconFactory.Create(paused: true);
    private readonly System.Windows.Forms.Timer _maintenance = new() { Interval = 15_000 };
    private readonly ToolStripMenuItem _statusItem = new("Starting...") { Enabled = false };
    private readonly ToolStripMenuItem _resumeItem = new("Resume reminders");

    private BreakForm? _breakForm;
    private string? _pendingReportPath;
    private int _ticks;
    private bool _shuttingDown;

    public TrayApplicationContext()
    {
        _engine = new ReminderEngine(_settings, _usage);
        _monitor = new ActivityMonitor(_settings);
        _reports = new ReportService(_usage);

        _tray = new NotifyIcon
        {
            Icon = _activeIcon,
            Text = AppInfo.Name,
            Visible = true,
            ContextMenuStrip = BuildMenu()
        };
        _tray.DoubleClick += (_, _) => ShowTodaySummary();
        _tray.MouseMove += (_, _) => UpdateTrayState(); // keeps the hover tooltip countdown accurate
        _tray.BalloonTipClicked += (_, _) =>
        {
            if (_pendingReportPath is not null) ShellLauncher.Open(_pendingReportPath);
            _pendingReportPath = null;
        };

        _engine.BreakDue += OnBreakDue;
        _monitor.Ticked += _engine.OnTick;
        _settings.Changed += OnSettingsChanged;
        SystemEvents.SessionEnding += OnSessionEnding;

        _maintenance.Tick += OnMaintenanceTick;
        _maintenance.Start();
        _monitor.Start();

        if (_settings.IsFirstRun)
        {
            _settings.Persist();
            StartupService.Apply(_settings.Current.StartWithWindows);
        }

        ShowBalloon($"{AppInfo.Name} is running",
            $"I'll remind you to rest your eyes every {_settings.Current.WorkIntervalMinutes} minutes.");
        Logger.Info($"Reminder cycle started: every {_settings.Current.WorkIntervalMinutes} min, " +
                    $"break {_settings.Current.BreakSeconds} s.");
        UpdateTrayState();
    }

    // ------------------------------------------------------------------ menu

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();

        var breakNow = new ToolStripMenuItem("Take a break now");
        breakNow.Click += (_, _) => _engine.RequestBreak();

        var pause = new ToolStripMenuItem("Pause reminders");
        foreach (var (label, minutes) in new[] { ("For 30 minutes", 30), ("For 1 hour", 60), ("For 2 hours", 120) })
        {
            pause.DropDownItems.Add(label, null, (_, _) => PauseFor(minutes));
        }

        _resumeItem.Click += (_, _) =>
        {
            _engine.Resume();
            UpdateTrayState();
        };

        var today = new ToolStripMenuItem("Today's summary");
        today.Click += (_, _) => ShowTodaySummary();

        var report = new ToolStripMenuItem("Weekly report (last 7 days)");
        report.Click += (_, _) => OpenReport();

        var settings = new ToolStripMenuItem("Settings...");
        settings.Click += (_, _) => OpenSettings();

        var dataFolder = new ToolStripMenuItem("Open data folder");
        dataFolder.Click += (_, _) => ShellLauncher.Open(AppPaths.Root);

        var github = new ToolStripMenuItem("Developer on GitHub");
        github.Click += (_, _) => ShellLauncher.Open(AppInfo.GitHubUrl);

        var about = new ToolStripMenuItem("About");
        about.Click += (_, _) => MessageBox.Show(AppInfo.AboutText, $"About {AppInfo.Name}",
            MessageBoxButtons.OK, MessageBoxIcon.Information);

        var exit = new ToolStripMenuItem("Exit");
        exit.Click += (_, _) => ExitThread();

        menu.Items.AddRange(new ToolStripItem[]
        {
            _statusItem, new ToolStripSeparator(),
            breakNow, pause, _resumeItem, new ToolStripSeparator(),
            today, report, new ToolStripSeparator(),
            settings, dataFolder, github, about, new ToolStripSeparator(),
            exit
        });

        menu.Opening += (_, _) => UpdateTrayState();
        return menu;
    }

    // ------------------------------------------------------------------ actions

    private void OnBreakDue(object? sender, BreakRequest request)
    {
        if (_shuttingDown) return;

        try
        {
            Logger.Info($"Break due ({request.Duration.TotalSeconds:0} s).");

            var form = new BreakForm(request, _settings.Current.SnoozeMinutes, _settings.Current.PlaySound);
            form.FormClosed += (_, _) =>
            {
                if (_shuttingDown) return;

                var outcome = form.Outcome;
                Logger.Info($"Break finished: {outcome}.");
                _engine.CompleteBreak(outcome);
                _breakForm = null;
                UpdateTrayState();

                if (outcome == BreakOutcome.Completed)
                {
                    ShowBalloon("Break complete",
                        $"Great job! Next reminder in {_settings.Current.WorkIntervalMinutes} minutes.");
                }
            };

            _breakForm = form;
            ShowBalloon("Time for an eye break",
                $"Look at something 20 feet (6 m) away for {request.Duration.TotalSeconds:0} seconds.");
            form.Show(); // modeless; never takes focus (see BreakForm)
        }
        catch (Exception ex)
        {
            Logger.Error("Could not show the break prompt.", ex);
            _breakForm = null;
            _engine.CancelBreak(); // never leave the engine stuck in "break in progress"
        }
    }

    private void PauseFor(int minutes)
    {
        _engine.PauseFor(TimeSpan.FromMinutes(minutes));
        UpdateTrayState();
        ShowBalloon("Reminders paused", $"I'll stay quiet until {DateTime.Now.AddMinutes(minutes):HH:mm}.");
    }

    private void OpenSettings()
    {
        using var dialog = new SettingsForm(_settings.Current.Clone());
        if (dialog.ShowDialog() == DialogResult.OK)
        {
            _settings.Update(dialog.Result);
        }
    }

    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        StartupService.Apply(_settings.Current.StartWithWindows);
        Logger.Info($"Settings updated: every {_settings.Current.WorkIntervalMinutes} min, " +
                    $"break {_settings.Current.BreakSeconds} s.");
        UpdateTrayState();
    }

    private void ShowTodaySummary()
    {
        var t = _usage.Today;
        ShowBalloon("Today so far",
            $"Screen time: {Formatting.Duration(t.ActiveSeconds)}\n" +
            $"Breaks taken: {t.BreaksCompleted} | Skipped: {t.BreaksSkipped}\n" +
            $"Longest stretch: {Formatting.Duration(t.LongestContinuousSeconds)}");
    }

    private void OpenReport()
    {
        try
        {
            ShellLauncher.Open(_reports.Generate(DateOnly.FromDateTime(DateTime.Now)));
        }
        catch (Exception ex)
        {
            Logger.Error("Could not generate the weekly report.", ex);
            MessageBox.Show("The report could not be created. See the log for details.", AppInfo.Name,
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void CheckWeeklyReport()
    {
        try
        {
            var s = _settings.Current;
            if (!s.AutoWeeklyReport) return;

            var today = DateOnly.FromDateTime(DateTime.Now);
            if (s.LastWeeklyReportDate is null)
            {
                s.LastWeeklyReportDate = today; // start counting from the first run
                _settings.Persist();
                return;
            }

            if (today.DayNumber - s.LastWeeklyReportDate.Value.DayNumber < 7) return;

            _pendingReportPath = _reports.Generate(today.AddDays(-1));
            s.LastWeeklyReportDate = today;
            _settings.Persist();
            ShowBalloon("Your weekly screen-health report is ready", "Click here to open it.");
        }
        catch (Exception ex)
        {
            Logger.Error("Automatic weekly report failed.", ex);
        }
    }

    // ------------------------------------------------------------------ state & lifecycle

    private void OnMaintenanceTick(object? sender, EventArgs e)
    {
        _ticks++;
        UpdateTrayState();
        if (_ticks % 4 == 0) _usage.Save();                // every minute
        if (_ticks % 240 == 1) CheckWeeklyReport();        // shortly after start, then hourly
    }

    private void UpdateTrayState()
    {
        bool paused = _engine.IsPaused;
        string status = BuildStatusText();

        _statusItem.Text = status;
        _resumeItem.Visible = paused;
        _tray.Icon = paused ? _pausedIcon : _activeIcon;
        _tray.Text = Truncate($"{AppInfo.Name} - {status}", 63); // NotifyIcon.Text limit
    }

    private string BuildStatusText()
    {
        if (_engine.PausedUntil is { } until) return $"Paused until {until:HH:mm}";
        if (_engine.BreakInProgress) return "Break in progress";
        if (_monitor.Current == Presence.Away) return "PC locked - timer on hold";

        int totalSeconds = (int)Math.Ceiling(_engine.TimeUntilNextBreak.TotalSeconds);
        return $"Next eye break in {totalSeconds / 60}:{totalSeconds % 60:00}";
    }

    private void ShowBalloon(string title, string text) =>
        _tray.ShowBalloonTip(8000, title, text, ToolTipIcon.Info);

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..(max - 1)] + "…";

    private void OnSessionEnding(object? sender, SessionEndingEventArgs e) => _usage.Save();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _shuttingDown = true;
            SystemEvents.SessionEnding -= OnSessionEnding;
            _maintenance.Stop();
            _maintenance.Dispose();
            _monitor.Dispose();
            _breakForm?.Close();
            _usage.Save();
            _tray.Visible = false;
            _tray.ContextMenuStrip?.Dispose();
            _tray.Dispose();
            _activeIcon.Dispose();
            _pausedIcon.Dispose();
        }

        base.Dispose(disposing);
    }
}
