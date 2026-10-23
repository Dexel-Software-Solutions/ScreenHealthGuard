using System.Diagnostics;

namespace ScreenHealthGuard.Infrastructure;

internal static class ShellLauncher
{
    /// <summary>Opens a file, folder or URL with its default handler.</summary>
    public static void Open(string target)
    {
        try
        {
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Logger.Error($"Could not open '{target}'.", ex);
            MessageBox.Show($"Could not open:\n{target}", AppInfo.Name,
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
