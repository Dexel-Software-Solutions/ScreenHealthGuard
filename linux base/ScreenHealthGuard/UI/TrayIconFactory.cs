using System.Drawing.Drawing2D;
using ScreenHealthGuard.Native;

namespace ScreenHealthGuard.UI;

/// <summary>Draws the tray icon in code so the app needs no external resources.</summary>
internal static class TrayIconFactory
{
    public static Icon Create(bool paused)
    {
        using var bitmap = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            var eye = new Rectangle(2, 7, 28, 18);
            using var white = new SolidBrush(Color.White);
            using var outline = new Pen(Color.FromArgb(40, 48, 64), 2.5f);
            using var iris = new SolidBrush(paused ? Color.FromArgb(150, 155, 165) : Color.FromArgb(46, 170, 112));
            using var pupil = new SolidBrush(Color.FromArgb(25, 30, 40));

            g.FillEllipse(white, eye);
            g.FillEllipse(iris, 9, 9, 14, 14);
            g.FillEllipse(pupil, 13, 13, 6, 6);
            g.DrawEllipse(outline, eye);
        }

        IntPtr handle = bitmap.GetHicon();
        try
        {
            using var temp = Icon.FromHandle(handle);
            return (Icon)temp.Clone(); // independent copy; the native handle is released below
        }
        finally
        {
            NativeMethods.DestroyIcon(handle);
        }
    }
}
