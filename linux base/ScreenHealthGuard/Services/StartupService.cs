using Microsoft.Win32;
using ScreenHealthGuard.Infrastructure;

namespace ScreenHealthGuard.Services;

/// <summary>Registers the app to launch at user logon (HKCU, no admin rights needed).</summary>
internal static class StartupService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "ScreenHealthGuard";

    public static void Apply(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
                            ?? Registry.CurrentUser.CreateSubKey(RunKeyPath);
            if (key is null) return;

            if (!enabled)
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
                return;
            }

            var exe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exe) ||
                Path.GetFileNameWithoutExtension(exe).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            {
                Logger.Warn("Running under 'dotnet'; skipping startup registration. Publish the app first.");
                return;
            }

            key.SetValue(ValueName, $"\"{exe}\"");
        }
        catch (Exception ex)
        {
            Logger.Error("Could not update the startup registration.", ex);
        }
    }
}
