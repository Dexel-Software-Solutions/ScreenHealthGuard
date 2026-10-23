using System.Runtime.InteropServices;

namespace ScreenHealthGuard.Native;

internal static class NativeMethods
{
    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DestroyIcon(IntPtr hIcon);

    /// <summary>
    /// Time since the last keyboard/mouse input in this session.
    /// Only the timestamp is read; no input content is ever accessed.
    /// </summary>
    public static TimeSpan GetIdleTime()
    {
        var info = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
        if (!GetLastInputInfo(ref info))
        {
            return TimeSpan.Zero;
        }

        uint now = unchecked((uint)Environment.TickCount);
        uint elapsedMs = unchecked(now - info.dwTime);
        return TimeSpan.FromMilliseconds(elapsedMs);
    }
}
