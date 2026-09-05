namespace ToolTikTokV12.Controls;

public enum UiButtonKind { Primary, Neutral, Success, Warning, Danger, Ghost }

/// <summary>A lightweight WinForms gradient surface used in place of flat backgrounds.</summary>
public sealed class ModernGradientPanel : Panel
{
    public Color StartColor { get; set; } = Color.FromArgb(248, 250, 252);
    public Color EndColor { get; set; } = Color.FromArgb(238, 242, 255);
    public float Angle { get; set; } = 110F;

    public ModernGradientPanel()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        if (ClientRectangle.Width <= 0 || ClientRectangle.Height <= 0) return;
        using var brush = new System.Drawing.Drawing2D.LinearGradientBrush(
            ClientRectangle, StartColor, EndColor, Angle);
        e.Graphics.FillRectangle(brush, ClientRectangle);
    }
}

/// <summary>Rounded, softly bordered surface for dashboard sections and statistic cards.</summary>
public sealed class ModernCardPanel : Panel
{
    public int CornerRadius { get; set; } = 14;
    public Color BorderColor { get; set; } = Color.FromArgb(226, 232, 240);
    public int BorderThickness { get; set; } = 1;
    public Color GradientStart { get; set; } = Color.Empty;
    public Color GradientEnd { get; set; } = Color.Empty;

    public ModernCardPanel()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        BackColor = Color.White;
        Padding = new Padding(1);
    }

    protected override void OnResize(EventArgs eventargs)
    {
        base.OnResize(eventargs);
        if (Width <= 0 || Height <= 0) return;
        using var path = RoundedPath(new Rectangle(0, 0, Width - 1, Height - 1), CornerRadius);
        var previous = Region;
        Region = new Region(path);
        previous?.Dispose();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var path = RoundedPath(new Rectangle(0, 0, Width - 1, Height - 1), CornerRadius);
        using var pen = new Pen(BorderColor, BorderThickness);
        e.Graphics.DrawPath(pen, path);
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        if (GradientStart.IsEmpty || GradientEnd.IsEmpty || ClientRectangle.Width <= 0 || ClientRectangle.Height <= 0)
        {
            base.OnPaintBackground(e);
            return;
        }

        using var brush = new System.Drawing.Drawing2D.LinearGradientBrush(
            ClientRectangle, GradientStart, GradientEnd, 0F);
        e.Graphics.FillRectangle(brush, ClientRectangle);
    }

    static System.Drawing.Drawing2D.GraphicsPath RoundedPath(Rectangle bounds, int radius)
    {
        var path = new System.Drawing.Drawing2D.GraphicsPath();
        var diameter = Math.Max(2, Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height)));
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}

/// <summary>Small native WinForms theme shared by Manager and profile tabs; no owner draw or animation.</summary>
public static class UiTheme
{
    public static readonly Color Canvas = Color.FromArgb(245, 247, 251);
    public static readonly Color Card = Color.White;
    public static readonly Color Border = Color.FromArgb(229, 234, 242);
    public static readonly Color Primary = Color.FromArgb(79, 70, 229);
    public static readonly Color PrimaryHover = Color.FromArgb(67, 56, 202);
    public static readonly Color PrimarySoft = Color.FromArgb(238, 242, 255);
    public static readonly Color TextPrimary = Color.FromArgb(15, 23, 42);
    public static readonly Color TextSecondary = Color.FromArgb(100, 116, 139);
    public static readonly Color Sidebar = Color.FromArgb(15, 23, 42);
    public static readonly Color Success = Color.FromArgb(22, 163, 74);
    public static readonly Color Warning = Color.FromArgb(245, 158, 11);
    public static readonly Color Danger = Color.FromArgb(239, 68, 68);
    public static readonly Color Purple = Color.FromArgb(168, 85, 247);
    public static readonly Color Cyan = Color.FromArgb(14, 165, 233);

    public static void Apply(Control root)
    {
        root.Font = new Font("Segoe UI", 9.5F);
        if (root is Form or UserControl) root.BackColor = Canvas;
        foreach (Control child in root.Controls)
        {
            if (child is GroupBox group)
            {
                group.BackColor = Card;
                group.ForeColor = TextPrimary;
                group.Font = new Font("Segoe UI Semibold", 9.5F);
                group.Padding = new Padding(Math.Max(12, group.Padding.Left), 12, Math.Max(12, group.Padding.Right), Math.Max(12, group.Padding.Bottom));
            }
            else if (child is TabPage page) page.BackColor = Canvas;
            // Respect controls that were intentionally styled by their feature.
            // Default WinForms buttons still receive the shared modern treatment.
            else if (child is Button button && button.FlatStyle != FlatStyle.Flat) StyleButton(button);
            else if (child is DataGridView grid) StyleGrid(grid);
            else if (child is TextBox textBox)
            {
                textBox.BackColor = Card;
                textBox.ForeColor = TextPrimary;
                textBox.BorderStyle = BorderStyle.FixedSingle;
            }
            else if (child is ComboBox comboBox)
            {
                comboBox.BackColor = Card;
                comboBox.ForeColor = TextPrimary;
                comboBox.FlatStyle = FlatStyle.Flat;
            }
            else if (child is NumericUpDown numeric)
            {
                numeric.BackColor = Card;
                numeric.ForeColor = TextPrimary;
                numeric.BorderStyle = BorderStyle.FixedSingle;
            }
            else if (child is CheckBox checkBox)
            {
                checkBox.ForeColor = TextPrimary;
            }
            Apply(child);
        }
    }

    public static void StyleButton(Button button, UiButtonKind kind = UiButtonKind.Neutral)
    {
        button.AutoSize = true;
        button.Height = Math.Max(40, button.Height);
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 1;
        button.UseVisualStyleBackColor = false;
        button.Cursor = Cursors.Hand;
        button.Padding = new Padding(11, 0, 11, 0);
        button.Margin = new Padding(4, 3, 4, 3);
        button.Font = new Font("Segoe UI Semibold", 9.5F);
        var (back, fore, border) = kind switch
        {
            UiButtonKind.Primary => (Primary, Color.White, Primary),
            UiButtonKind.Success => (Success, Color.White, Success),
            UiButtonKind.Warning => (Color.FromArgb(255, 247, 237), Color.FromArgb(194, 65, 12), Color.FromArgb(254, 215, 170)),
            UiButtonKind.Danger => (Color.FromArgb(254, 242, 242), Danger, Color.FromArgb(254, 202, 202)),
            UiButtonKind.Ghost => (Color.Transparent, TextSecondary, Color.Transparent),
            _ => (Card, TextPrimary, Border)
        };
        button.BackColor = back;
        button.ForeColor = fore;
        button.FlatAppearance.BorderColor = border;
        button.FlatAppearance.MouseOverBackColor = kind switch
        {
            UiButtonKind.Primary => PrimaryHover,
            UiButtonKind.Success => Color.FromArgb(5, 122, 85),
            UiButtonKind.Warning => Color.FromArgb(255, 237, 213),
            UiButtonKind.Danger => Color.FromArgb(254, 226, 226),
            UiButtonKind.Ghost => Color.FromArgb(241, 245, 249),
            _ => Color.FromArgb(248, 250, 252)
        };
        button.FlatAppearance.MouseDownBackColor = kind switch
        {
            UiButtonKind.Primary => Color.FromArgb(55, 48, 163),
            UiButtonKind.Success => Color.FromArgb(4, 120, 87),
            UiButtonKind.Warning => Color.FromArgb(254, 215, 170),
            UiButtonKind.Danger => Color.FromArgb(254, 202, 202),
            UiButtonKind.Ghost => Color.FromArgb(226, 232, 240),
            _ => Color.FromArgb(241, 245, 249)
        };
        ApplyRoundedCorners(button, 10);
    }

    public static void ApplyRoundedCorners(Control control, int radius = 9)
    {
        void UpdateRegion()
        {
            if (control.IsDisposed || control.Width <= 0 || control.Height <= 0) return;
            var diameter = Math.Max(2, Math.Min(radius * 2, Math.Min(control.Width, control.Height)));
            using var path = new System.Drawing.Drawing2D.GraphicsPath();
            path.AddArc(0, 0, diameter, diameter, 180, 90);
            path.AddArc(control.Width - diameter - 1, 0, diameter, diameter, 270, 90);
            path.AddArc(control.Width - diameter - 1, control.Height - diameter - 1, diameter, diameter, 0, 90);
            path.AddArc(0, control.Height - diameter - 1, diameter, diameter, 90, 90);
            path.CloseFigure();
            var previous = control.Region;
            control.Region = new Region(path);
            previous?.Dispose();
        }

        UpdateRegion();
        control.Resize += (_, _) => UpdateRegion();
    }

    public static void StyleGrid(DataGridView grid)
    {
        grid.BackgroundColor = Card;
        grid.BorderStyle = BorderStyle.None;
        grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        grid.GridColor = Color.FromArgb(235, 239, 245);
        grid.EnableHeadersVisualStyles = false;
        grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        grid.ColumnHeadersHeight = Math.Max(44, grid.ColumnHeadersHeight);
        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(248, 250, 252);
        grid.ColumnHeadersDefaultCellStyle.ForeColor = TextSecondary;
        grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 9F);
        grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(248, 250, 252);
        grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = TextSecondary;
        grid.DefaultCellStyle.BackColor = Card;
        grid.DefaultCellStyle.ForeColor = TextPrimary;
        grid.DefaultCellStyle.SelectionBackColor = PrimarySoft;
        grid.DefaultCellStyle.SelectionForeColor = Color.FromArgb(49, 46, 129);
        grid.DefaultCellStyle.Padding = new Padding(5, 2, 5, 2);
        grid.RowTemplate.Height = Math.Max(48, grid.RowTemplate.Height);
        grid.RowHeadersVisible = false;
    }
}
