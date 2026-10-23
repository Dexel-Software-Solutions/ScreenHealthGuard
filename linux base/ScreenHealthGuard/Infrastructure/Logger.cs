namespace ScreenHealthGuard.Infrastructure;

/// <summary>Minimal, thread-safe file logger with size-based rotation. Never throws.</summary>
internal static class Logger
{
    private const long MaxBytes = 1_000_000;
    private static readonly object Gate = new();

    public static void Info(string message) => Write("INFO", message);
    public static void Warn(string message) => Write("WARN", message);

    public static void Error(string message, Exception? ex = null) =>
        Write("ERROR", ex is null ? message : $"{message}{Environment.NewLine}{ex}");

    private static void Write(string level, string message)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(AppPaths.Logs);
                var file = AppPaths.LogFile;
                var info = new FileInfo(file);
                if (info.Exists && info.Length > MaxBytes)
                {
                    File.Move(file, file + ".old", overwrite: true);
                }

                File.AppendAllText(file,
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}{Environment.NewLine}");
            }
        }
        catch
        {
            // Logging must never take the application down.
        }
    }
}
