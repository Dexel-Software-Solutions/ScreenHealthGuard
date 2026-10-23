namespace ScreenHealthGuard.Infrastructure;

internal static class AppPaths
{
    public static string Root { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ScreenHealthGuard");

    public static string Data => Path.Combine(Root, "data");
    public static string Reports => Path.Combine(Root, "reports");
    public static string Logs => Path.Combine(Root, "logs");
    public static string SettingsFile => Path.Combine(Root, "settings.json");
    public static string LogFile => Path.Combine(Logs, "app.log");

    public static void EnsureCreated()
    {
        Directory.CreateDirectory(Data);
        Directory.CreateDirectory(Reports);
        Directory.CreateDirectory(Logs);
    }
}
