using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Media;
using ScreenHealthGuard.Infrastructure;
using ScreenHealthGuard.Models;

namespace ScreenHealthGuard.UI;

/// <summary>
/// Break prompt with a countdown. Always on top but never steals keyboard focus,
/// so whatever you are typing is not interrupted.
/// </summary>
internal sealed class BreakForm : Form
{
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_NOACTIVATE = 0x08000000;

    private static readonly Color Surface = Color.FromArgb(30, 36, 48);
    private static readonly Color Accent = Color.FromArgb(76, 195, 138);
    private static readonly Color ButtonColor = Color.FromArgb(48, 56, 74);

    private static readonly string[] Tips =
    {
        "Look at something about 20 feet (6 m) away, such as a window or a distant wall.",
        "Let your eyes relax and blink slowly a few times.",
        "Close your eyes gently for a few seconds, then look far away.",
        "Soften your gaze and unclench your jaw and shoulders."
    };

    private readonly BreakRequest _request;
    private readonly bool _playSound;
    private readonly Label _countdown = new();
    private readonly ThinProgressBar _progress = new();
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 200 };
    private DateTime _endUtc;
    private bool _finished;

    public BreakOutcome Outcome { get; private set; } = BreakOutcome.Skipped;

    public BreakForm(BreakRequest request, int snoozeMinutes, bool playSound)
    {
        _request = request;
        _playSound = playSound;

        SuspendLayout();
        AutoScaleMode = AutoScaleMode.Dpi;
        Text = AppInfo.Name;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        DoubleBuffered = true;
        BackColor = Surface;
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 10f);
        ClientSize = new Size(460, 270);

        var title = new Label
        {
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 15f),
            Text = "Time for an eye break",
            Margin = new Padding(0, 0, 0, 8)
        };

        var tip = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(412, 0),
            ForeColor = Color.FromArgb(200, 207, 220),
            Text = Tips[Random.Shared.Next(Tips.Length)],
            Margin = new Padding(0, 0, 0, 8)
        };

        _countdown.AutoSize = true;
        _countdown.Font = new Font("Segoe UI", 30f, FontStyle.Bold);
        _countdown.ForeColor = Accent;
        _countdown.Margin = new Padding(0, 0, 0, 6);

        _progress.Dock = DockStyle.Fill;
        _progress.Height = 8;
        _progress.Margin = new Padding(0, 0, 0, 14);

        var skip = CreateButton("Skip", BreakOutcome.Skipped);
        var snooze = CreateButton($"Snooze {snoozeMinutes} min", BreakOutcome.Snoozed);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            AutoSize = true
        };
        buttons.Controls.Add(skip);
        buttons.Controls.Add(snooze);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(24),
            ColumnCount = 1,
            RowCount = 5
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < 5; i++) layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(title, 0, 0);
        layout.Controls.Add(tip, 0, 1);
        layout.Controls.Add(_countdown, 0, 2);
        layout.Controls.Add(_progress, 0, 3);
        layout.Controls.Add(buttons, 0, 4);
        Controls.Add(layout);

        ResumeLayout(performLayout: true);
        _timer.Tick += OnTick;
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE; // no focus theft, hidden from Alt+Tab
            return cp;
        }
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        var area = Screen.FromPoint(Cursor.Position).WorkingArea; // the monitor the user is looking at
        Location = new Point(area.Right - Width - 24, area.Bottom - Height - 24);

        _endUtc = DateTime.UtcNow + _request.Duration;
        UpdateCountdown(_request.Duration);
        _timer.Start();

        if (_playSound) SystemSounds.Asterisk.Play();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var pen = new Pen(Accent, 2f);
        e.Graphics.DrawRectangle(pen, 1, 1, Width - 3, Height - 3);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Stop();
            _timer.Dispose();
        }

        base.Dispose(disposing);
    }

    private Button CreateButton(string text, BreakOutcome outcome)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = true,
            FlatStyle = FlatStyle.Flat,
            BackColor = ButtonColor,
            ForeColor = Color.White,
            Cursor = Cursors.Hand,
            Padding = new Padding(10, 4, 10, 4),
            Margin = new Padding(8, 0, 0, 0),
            UseVisualStyleBackColor = false
        };
        button.FlatAppearance.BorderColor = Color.FromArgb(70, 80, 100);
        button.Click += (_, _) => Finish(outcome);
        return button;
    }

    private void OnTick(object? sender, EventArgs e)
    {
        var remaining = _endUtc - DateTime.UtcNow;
        if (remaining <= TimeSpan.Zero)
        {
            if (_playSound) SystemSounds.Exclamation.Play(); // "break over"
            Finish(BreakOutcome.Completed);
            return;
        }

        UpdateCountdown(remaining);
        _progress.Value = 1.0 - remaining.TotalSeconds / _request.Duration.TotalSeconds;
    }

    private void UpdateCountdown(TimeSpan remaining)
    {
        var rounded = TimeSpan.FromSeconds(Math.Ceiling(remaining.TotalSeconds));
        _countdown.Text = $"{(int)rounded.TotalMinutes:00}:{rounded.Seconds:00}";
    }

    private void Finish(BreakOutcome outcome)
    {
        if (_finished) return;
        _finished = true;
        Outcome = outcome;
        _timer.Stop();
        Close();
    }
}

/// <summary>Slim, flicker-free rounded progress bar.</summary>
internal sealed class ThinProgressBar : Control
{
    private double _value;

    public ThinProgressBar()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Height = 8;
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public double Value
    {
        get => _value;
        set
        {
            _value = Math.Clamp(value, 0, 1);
            Invalidate();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Parent?.BackColor ?? Color.Black);

        var track = new RectangleF(0, 0, Width - 1, Height - 1);
        using (var trackBrush = new SolidBrush(Color.FromArgb(55, 64, 82)))
        using (var trackPath = Rounded(track))
        {
            g.FillPath(trackBrush, trackPath);
        }

        float fillWidth = (float)(track.Width * _value);
        if (fillWidth >= track.Height)
        {
            using var fillBrush = new SolidBrush(Color.FromArgb(76, 195, 138));
            using var fillPath = Rounded(new RectangleF(0, 0, fillWidth, track.Height));
            g.FillPath(fillBrush, fillPath);
        }
    }

    private static GraphicsPath Rounded(RectangleF r)
    {
        float d = r.Height;
        var path = new GraphicsPath();
        path.AddArc(r.X, r.Y, d, d, 90, 180);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 180);
        path.CloseFigure();
        return path;
    }
}
