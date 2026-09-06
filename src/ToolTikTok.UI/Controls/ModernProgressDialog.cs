namespace ToolTikTokV12.Controls;

/// <summary>Reusable, non-blocking progress surface for long-running UI tasks.</summary>
public sealed class ModernProgressDialog : Form
{
    readonly Label _subtitle;
    readonly Label _percent;
    readonly ProgressBar _progress;
    readonly Label[] _steps;

    public ModernProgressDialog(string title, string subtitle, params string[] steps)
    {
        Text = title;
        Width = 520;
        Height = 390;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        MinimizeBox = false;
        MaximizeBox = false;
        ControlBox = false;
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = Color.White;
        Font = new Font("Segoe UI", 9.5F);

        var stepNames = steps.Length > 0 ? steps :
            ["Kết nối đến máy chủ", "Kiểm tra phiên bản", "Tải thông tin cập nhật", "Hoàn tất"];
        _steps = new Label[stepNames.Length];

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 6,
            Padding = new Padding(28, 24, 28, 24),
            BackColor = Color.White
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 66F));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46F));

        var icon = new Label
        {
            Text = "↻",
            AutoSize = false,
            Size = new Size(56, 56),
            Anchor = AnchorStyles.None,
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = UiTheme.PrimarySoft,
            ForeColor = UiTheme.Primary,
            Font = new Font("Segoe UI Symbol", 22F, FontStyle.Bold)
        };
        var titleLabel = new Label
        {
            Text = title,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            ForeColor = UiTheme.TextPrimary,
            Font = new Font("Segoe UI Semibold", 15F, FontStyle.Bold),
            Margin = new Padding(0, 8, 0, 3)
        };
        _subtitle = new Label
        {
            Text = subtitle,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            ForeColor = UiTheme.TextSecondary,
            Margin = new Padding(0, 0, 0, 10)
        };
        var progressRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(0, 10, 0, 6) };
        progressRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        progressRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 58F));
        _progress = new ProgressBar { Dock = DockStyle.Fill, Height = 12, Style = ProgressBarStyle.Continuous, Margin = new Padding(0, 6, 10, 6) };
        _percent = new Label { Text = "0%", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, ForeColor = UiTheme.TextSecondary };
        progressRow.Controls.Add(_progress, 0, 0);
        progressRow.Controls.Add(_percent, 1, 0);

        var stepList = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = stepNames.Length };
        for (var i = 0; i < stepNames.Length; i++)
        {
            stepList.RowStyles.Add(new RowStyle(SizeType.Percent, 100F / stepNames.Length));
            _steps[i] = new Label
            {
                Text = "○  " + stepNames[i],
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = UiTheme.TextSecondary,
                Font = new Font("Segoe UI", 9.5F)
            };
            stepList.Controls.Add(_steps[i], 0, i);
        }
        var close = new Button { Text = "Ẩn tiến trình", Dock = DockStyle.Right, Width = 128 };
        UiTheme.StyleButton(close, UiButtonKind.Neutral);
        close.Click += (_, _) => Close();

        root.Controls.Add(icon, 0, 0);
        root.Controls.Add(titleLabel, 0, 1);
        root.Controls.Add(_subtitle, 0, 2);
        root.Controls.Add(progressRow, 0, 3);
        root.Controls.Add(stepList, 0, 4);
        root.Controls.Add(close, 0, 5);
        Controls.Add(root);
    }

    public void SetProgress(int percent, string subtitle, int activeStep)
    {
        if (IsDisposed) return;
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => SetProgress(percent, subtitle, activeStep)));
            return;
        }
        var value = Math.Clamp(percent, 0, 100);
        _progress.Value = value;
        _percent.Text = value + "%";
        _subtitle.Text = subtitle;
        for (var i = 0; i < _steps.Length; i++)
        {
            var text = _steps[i].Text.Length > 3 ? _steps[i].Text[3..] : _steps[i].Text;
            _steps[i].Text = (i < activeStep ? "✓  " : i == activeStep ? "●  " : "○  ") + text;
            _steps[i].ForeColor = i < activeStep ? UiTheme.Success : i == activeStep ? UiTheme.Primary : UiTheme.TextSecondary;
        }
    }

    public void CloseSafely()
    {
        if (IsDisposed) return;
        if (InvokeRequired) BeginInvoke(new Action(CloseSafely));
        else Close();
    }
}
