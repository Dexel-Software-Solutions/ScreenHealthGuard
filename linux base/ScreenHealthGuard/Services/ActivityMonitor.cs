using Microsoft.Win32;
using ScreenHealthGuard.Models;
using ScreenHealthGuard.Native;

namespace ScreenHealthGuard.Services;

/// <summary>
/// Heartbeat of the app. Once per second it reports how much real time has passed and whether the
/// user is Active, Idle or Away. Must be created and used on the UI thread.
/// </summary>
internal sealed class ActivityMonitor : IDisposable
{
    private static readonly TimeSpan MaxTickGap = TimeSpan.FromSeconds(10); // bigger gap => sleep/hibernate

    private readonly SettingsService _settings;
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 1000 };
    private DateTime _lastTickUtc;
    private volatile bool _locked;

    public ActivityMonitor(SettingsService settings) => _settings = settings;

    /// <summary>Raised every second with the elapsed wall-clock time and the current presence.</summary>
    public event Action<TimeSpan, Presence>? Ticked;

    public Presence Current { get; private set; } = Presence.Active;

    public void Start()
    {
        _lastTickUtc = DateTime.UtcNow;
        SystemEvents.SessionSwitch += OnSessionSwitch;
        _timer.Tick += OnTick;
        _timer.Start();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        var now = DateTime.UtcNow;
        var delta = now - _lastTickUtc;
        _lastTickUtc = now;
        if (delta <= TimeSpan.Zero) return;

        Presence presence;
        if (_locked || delta > MaxTickGap)
        {
            presence = Presence.Away;
        }
        else
        {
            var idleLimit = TimeSpan.FromSeconds(_settings.Current.IdleThresholdSeconds);
            presence = NativeMethods.GetIdleTime() >= idleLimit ? Presence.Idle : Presence.Active;
        }

        Current = presence;
        Ticked?.Invoke(delta, presence);
    }

    private void OnSessionSwitch(object? sender, SessionSwitchEventArgs e)
    {
        switch (e.Reason)
        {
            case SessionSwitchReason.SessionLock:
            case SessionSwitchReason.ConsoleDisconnect:
            case SessionSwitchReason.RemoteDisconnect:
                _locked = true;
                break;
            case SessionSwitchReason.SessionUnlock:
            case SessionSwitchReason.ConsoleConnect:
            case SessionSwitchReason.RemoteConnect:
            case SessionSwitchReason.SessionLogon:
                _locked = false;
                break;
        }
    }

    public void Dispose()
    {
        SystemEvents.SessionSwitch -= OnSessionSwitch;
        _timer.Stop();
        _timer.Tick -= OnTick;
        _timer.Dispose();
    }
}
