using ScreenHealthGuard.Infrastructure;
using ScreenHealthGuard.Models;

namespace ScreenHealthGuard.UI;

internal sealed class SettingsForm : Form
{
    private readonly AppSettings _draft;

    private readonly NumericUpDown _work = Number(1, 120);
    private readonly NumericUpDown _break = Number(10, 120);
    private readonly NumericUpDown _idle = Number(30, 900);
    private readonly NumericUpDown _snooze = Number(1, 60);
    private readonly CheckBox _startup = new() { Text = "Start with Windows", AutoSize = true };
    private readonly CheckBox _sound = new() { Text = "Play a sound when a break starts and ends", AutoSize = true };
    private readonly CheckBox _weekly = new() { Text = "Create a weekly health report automatically", AutoSize = true };

    public SettingsForm(AppSettings current)
    {
        _draft = current;

        AutoScaleMode = AutoScaleMode.Dpi;
        Text = $"{AppInfo.Name} - Settings";
        Font = new Font("Segoe UI", 9f);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = true;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;

        var grid = new TableLayoutPanel
        {
            AutoSize = true,
            ColumnCount = 3,
            Padding = new Padding(16, 16, 16, 8)
        };

        AddRow(grid, "Remind me every", _work, "minutes");
        AddRow(grid, "Eye break lasts", _break, "seconds");
        AddRow(grid, "Snooze for", _snooze, "minutes");
        AddRow(grid, "Record me as idle after", _idle, "seconds (statistics only)");
        AddWide(grid, _startup);
        AddWide(grid, _sound);
        AddWide(grid, _weekly);

        var ok = new Button { Text = "Save", DialogResult = DialogResult.OK, AutoSize = true, Padding = new Padding(10, 2, 10, 2) };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true, Padding = new Padding(10, 2, 10, 2) };
        var reset = new Button { Text = "Defaults", AutoSize = true, Padding = new Padding(10, 2, 10, 2) };
        reset.Click += (_, _) => Bind(new AppSettings());
        ok.Click += (_, _) => Collect();

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            Padding = new Padding(12, 4, 12, 12)
        };
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(ok);
        buttons.Controls.Add(reset);

        Controls.Add(grid);
        Controls.Add(buttons);
        AcceptButton = ok;
        CancelButton = cancel;

        Bind(_draft);
    }

    public AppSettings Result => _draft;

    private static NumericUpDown Number(int min, int max) =>
        new() { Minimum = min, Maximum = max, Width = 80, TextAlign = HorizontalAlignment.Right };

    private static void AddRow(TableLayoutPanel grid, string label, Control input, string unit)
    {
        int row = grid.RowCount++;
        grid.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 8, 12, 8) }, 0, row);
        grid.Controls.Add(input, 1, row);
        grid.Controls.Add(new Label { Text = unit, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(6, 8, 3, 8) }, 2, row);
    }

    private static void AddWide(TableLayoutPanel grid, Control control)
    {
        int row = grid.RowCount++;
        control.Margin = new Padding(3, 6, 3, 6);
        grid.Controls.Add(control, 0, row);
        grid.SetColumnSpan(control, 3);
    }

    private void Bind(AppSettings s)
    {
        _work.Value = s.WorkIntervalMinutes;
        _break.Value = s.BreakSeconds;
        _idle.Value = s.IdleThresholdSeconds;
        _snooze.Value = s.SnoozeMinutes;
        _startup.Checked = s.StartWithWindows;
        _sound.Checked = s.PlaySound;
        _weekly.Checked = s.AutoWeeklyReport;
    }

    private void Collect()
    {
        _draft.WorkIntervalMinutes = (int)_work.Value;
        _draft.BreakSeconds = (int)_break.Value;
        _draft.IdleThresholdSeconds = (int)_idle.Value;
        _draft.SnoozeMinutes = (int)_snooze.Value;
        _draft.StartWithWindows = _startup.Checked;
        _draft.PlaySound = _sound.Checked;
        _draft.AutoWeeklyReport = _weekly.Checked;
        _draft.Normalize();
    }
}
