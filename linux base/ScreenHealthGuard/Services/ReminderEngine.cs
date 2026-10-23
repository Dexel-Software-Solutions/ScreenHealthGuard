using ScreenHealthGuard.Models;

namespace ScreenHealthGuard.Services;

/// <summary>
/// The 20-20-20 cycle, deliberately simple and predictable:
///   1. Count real time on the PC. Reading or watching without touching the keyboard still counts.
///   2. When the work interval (default 20 min) is reached, raise <see cref="BreakDue"/>.
///   3. The break (default 20 s) ends, the cycle restarts from zero.
/// Only a locked or sleeping PC pauses the clock. If that lasts as long as a break, it counts as rest
/// and the cycle restarts. Contains no UI code, so it is easy to unit test.
/// </summary>
internal sealed class ReminderEngine
{
    private readonly SettingsService _settings;
    private readonly UsageStore _usage;

    private double _elapsedSeconds;     // time since the last break
    private double _continuousSeconds;  // continuous active time (statistics)
    private double _awaySeconds;
    private bool _awayCounted;
    private bool _breakInProgress;
    private DateTime? _pausedUntil;

    public ReminderEngine(SettingsService settings, UsageStore usage)
    {
        _settings = settings;
        _usage = usage;
    }

    public event EventHandler<BreakRequest>? BreakDue;

    public bool BreakInProgress => _breakInProgress;

    public bool IsPaused => _pausedUntil is { } until && DateTime.Now < until;

    public DateTime? PausedUntil => IsPaused ? _pausedUntil : null;

    public TimeSpan TimeUntilNextBreak =>
        TimeSpan.FromSeconds(Math.Max(0, WorkSeconds - _elapsedSeconds));

    private double WorkSeconds => _settings.Current.WorkIntervalMinutes * 60.0;

    public void PauseFor(TimeSpan duration) => _pausedUntil = DateTime.Now + duration;

    public void Resume() => _pausedUntil = null;

    /// <summary>Called once per second by the activity monitor.</summary>
    public void OnTick(TimeSpan delta, Presence presence)
    {
        double seconds = delta.TotalSeconds;

        if (presence == Presence.Active)
        {
            _usage.AddActive(delta);
        }

        if (presence == Presence.Away)
        {
            _continuousSeconds = 0;
            _awaySeconds += seconds;

            if (!_awayCounted && !_breakInProgress && _awaySeconds >= _settings.Current.BreakSeconds)
            {
                _awayCounted = true;
                _elapsedSeconds = 0; // the user has rested; start a fresh cycle
                _usage.RegisterNaturalBreak();
            }

            return;
        }

        _awaySeconds = 0;
        _awayCounted = false;

        if (_breakInProgress) return;

        if (presence == Presence.Active)
        {
            _continuousSeconds += seconds;
            _usage.RegisterContinuous((int)_continuousSeconds);
        }

        if (IsPaused) return;

        _elapsedSeconds += seconds;
        if (_elapsedSeconds >= WorkSeconds)
        {
            Raise();
        }
    }

    /// <summary>Starts a break immediately. Returns false if one is already showing.</summary>
    public bool RequestBreak()
    {
        if (_breakInProgress) return false;
        Raise();
        return true;
    }

    /// <summary>Reports how the user responded to the break prompt.</summary>
    public void CompleteBreak(BreakOutcome outcome)
    {
        _breakInProgress = false;
        _usage.RegisterBreak(outcome);

        switch (outcome)
        {
            case BreakOutcome.Completed:
                _elapsedSeconds = 0;
                _continuousSeconds = 0;
                break;
            case BreakOutcome.Skipped:
                _elapsedSeconds = 0;
                break;
            case BreakOutcome.Snoozed:
                _elapsedSeconds = Math.Max(0, WorkSeconds - _settings.Current.SnoozeMinutes * 60.0);
                break;
        }
    }

    /// <summary>The prompt could not be shown. Retry in about a minute instead of getting stuck.</summary>
    public void CancelBreak()
    {
        _breakInProgress = false;
        _elapsedSeconds = Math.Max(0, WorkSeconds - 60);
    }

    private void Raise()
    {
        _breakInProgress = true;
        BreakDue?.Invoke(this, new BreakRequest(TimeSpan.FromSeconds(_settings.Current.BreakSeconds)));
    }
}
