using ScreenHealthGuard.Infrastructure;
using ScreenHealthGuard.Models;

namespace ScreenHealthGuard.Services;

internal sealed class SettingsService
{
    public AppSettings Current { get; private set; }
    public bool IsFirstRun { get; }
    public event EventHandler? Changed;

    public SettingsService()
    {
        IsFirstRun = !File.Exists(AppPaths.SettingsFile);
        Current = JsonStorage.TryRead<AppSettings>(AppPaths.SettingsFile) ?? new AppSettings();
        Current.Normalize();
    }

    public void Update(AppSettings settings)
    {
        settings.Normalize();
        Current = settings;
        Persist();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Persist()
    {
        try
        {
            JsonStorage.WriteAtomic(AppPaths.SettingsFile, Current);
        }
        catch (Exception ex)
        {
            Logger.Error("Could not save settings.", ex);
        }
    }
}
