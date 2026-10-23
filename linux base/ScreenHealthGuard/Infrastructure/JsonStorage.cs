using System.Text.Json;

namespace ScreenHealthGuard.Infrastructure;

/// <summary>JSON persistence with atomic writes (temp file + replace) to avoid corrupted files.</summary>
internal static class JsonStorage
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public static T? TryRead<T>(string path) where T : class
    {
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            using var stream = File.OpenRead(path);
            return JsonSerializer.Deserialize<T>(stream, Options);
        }
        catch (Exception ex)
        {
            Logger.Error($"Could not read '{path}'. A fresh file will be used.", ex);
            return null;
        }
    }

    public static void WriteAtomic<T>(string path, T value)
    {
        var directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);
        var temp = path + ".tmp";

        using (var stream = File.Create(temp))
        {
            JsonSerializer.Serialize(stream, value, Options);
        }

        File.Move(temp, path, overwrite: true);
    }
}
