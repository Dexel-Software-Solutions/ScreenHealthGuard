// ---------------------------------------------------------------------------
//  Screen Health Guard - Digital Eye-Strain & Screen Health Guard
//  Developer : Demiyan Dissanayake
//  GitHub    : https://github.com/dexel-software-solutions
//  Contact   : demiyandissanayake@gmail.com | dexelsoftwaresolutions@gmail.com
// ---------------------------------------------------------------------------
using ScreenHealthGuard.Infrastructure;
using ScreenHealthGuard.UI;

namespace ScreenHealthGuard;

internal static class Program
{
    private const string MutexName = @"Local\ScreenHealthGuard.7B1E0C4A-5D2F-4A8E-9C31-0F6E2B8A91D4";

    [STAThread]
    private static int Main()
    {
        using var singleInstance = new Mutex(initiallyOwned: true, MutexName, out bool isFirstInstance);
        if (!isFirstInstance)
        {
            MessageBox.Show(
                $"{AppInfo.Name} is already running. Look for its eye icon in the system tray.",
                AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return 1;
        }

        AppPaths.EnsureCreated();

        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => Logger.Error("Unhandled UI exception.", e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Logger.Error("Unhandled domain exception.", e.ExceptionObject as Exception);

        ApplicationConfiguration.Initialize();
        Logger.Info($"{AppInfo.Name} {AppInfo.Version} starting.");

        try
        {
            Application.Run(new TrayApplicationContext());
        }
        catch (Exception ex)
        {
            Logger.Error("Fatal error.", ex);
            MessageBox.Show($"A fatal error occurred:\n\n{ex.Message}\n\nSee log: {AppPaths.LogFile}",
                AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 2;
        }

        Logger.Info("Shut down cleanly.");
        return 0;
    }
}
