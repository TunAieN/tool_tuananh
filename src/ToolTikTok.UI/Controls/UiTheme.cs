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
public class ModernCardPanel : Panel
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

/// <summary>Dashboard action surface drawn consistently without native Button chrome.</summary>
public sealed class ModernActionCard : Control
{
    bool _hovered;
    public string Glyph { get; set; } = "•";
    public string Title { get; set; } = "Action";
    public string Subtitle { get; set; } = "";
    public Color Accent { get; set; } = Color.FromArgb(79, 70, 229);

    public ModernActionCard()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        Cursor = Cursors.Hand;
        TabStop = true;
        Size = new Size(180, 68);
        AccessibleRole = AccessibleRole.PushButton;
        BackColor = Color.White;
    }

    protected override void OnMouseEnter(EventArgs e) { _hovered = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hovered = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Enter or Keys.Space)
        {
            OnClick(EventArgs.Empty);
            e.Handled = true;
        }
        base.OnKeyDown(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        var bounds = new Rectangle(0, 0, Math.Max(1, Width - 1), Math.Max(1, Height - 1));
        using var cardPath = RoundedPath(bounds, 11);
        using var background = new SolidBrush(_hovered ? Color.FromArgb(248, 250, 255) : Color.White);
        using var border = new Pen(_hovered || Focused ? Color.FromArgb(199, 210, 254) : Color.FromArgb(229, 234, 242));
        e.Graphics.FillPath(background, cardPath);
        e.Graphics.DrawPath(border, cardPath);

        var compact = Width < 180;
        var tileSize = compact ? 38 : 42;
        var tile = new Rectangle(11, Math.Max(8, (Height - tileSize) / 2), tileSize, tileSize);
        using var tilePath = RoundedPath(tile, 10);
        using var tileBrush = new SolidBrush(BlendWithWhite(Accent, .84F));
        e.Graphics.FillPath(tileBrush, tilePath);
        using var glyphFont = new Font("Segoe UI Symbol", 15F, FontStyle.Bold);
        TextRenderer.DrawText(e.Graphics, Glyph, glyphFont, tile, Accent,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

        var arrowWidth = Width >= 210 ? 18 : 0;
        var textLeft = tile.Right + 10;
        var textWidth = Math.Max(12, Width - textLeft - arrowWidth - 7);
        var wrapTitle = Title.Length > 17;
        var titleBounds = new Rectangle(textLeft, wrapTitle ? 3 : Math.Max(7, Height / 2 - 25), textWidth, wrapTitle ? 36 : 22);
        var subtitleBounds = new Rectangle(textLeft, wrapTitle ? 40 : titleBounds.Bottom + 1, textWidth, Math.Max(18, Height - (wrapTitle ? 43 : titleBounds.Bottom + 3) - 4));
        using var titleFont = new Font("Segoe UI Semibold", 8F, FontStyle.Bold);
        using var subtitleFont = new Font("Segoe UI", 7.2F);
        TextRenderer.DrawText(e.Graphics, Title, titleFont, titleBounds, Color.FromArgb(15, 23, 42),
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
        TextRenderer.DrawText(e.Graphics, Subtitle, subtitleFont, subtitleBounds, Color.FromArgb(100, 116, 139),
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
        if (arrowWidth > 0)
        {
            TextRenderer.DrawText(e.Graphics, "›", titleFont,
                new Rectangle(Width - arrowWidth - 4, 0, arrowWidth, Height), Color.FromArgb(148, 163, 184),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
    }

    static Color BlendWithWhite(Color color, float whiteAmount)
        => Color.FromArgb(
            (int)Math.Round(color.R + (255 - color.R) * whiteAmount),
            (int)Math.Round(color.G + (255 - color.G) * whiteAmount),
            (int)Math.Round(color.B + (255 - color.B) * whiteAmount));

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
    // Compatibility aliases. New UI code should use the semantic token classes.
    public static readonly Color Canvas = UiColors.Canvas;
    public static readonly Color Card = UiColors.Surface;
    public static readonly Color Border = UiColors.Border;
    public static readonly Color Primary = UiColors.Primary;
    public static readonly Color PrimaryHover = UiColors.PrimaryHover;
    public static readonly Color PrimarySoft = UiColors.PrimarySoft;
    public static readonly Color TextPrimary = UiColors.Text;
    public static readonly Color TextSecondary = UiColors.TextMuted;
    public static readonly Color Sidebar = UiColors.Sidebar;
    public static readonly Color Success = UiColors.Success;
    public static readonly Color Warning = UiColors.Warning;
    public static readonly Color Danger = UiColors.Danger;
    public static readonly Color Purple = UiColors.Purple;
    public static readonly Color Cyan = UiColors.Cyan;

    public static void Apply(Control root)
    {
        // Đặt font nền ở cấp cửa sổ; không ghi đè typography riêng của từng
        // tiêu đề, KPI hay nút khi duyệt cây control.
        if (root is Form or UserControl)
        {
            root.Font = UiTypography.Body();
            root.BackColor = Canvas;
        }
        foreach (Control child in root.Controls)
        {
            if (child is GroupBox group)
            {
                group.BackColor = Card;
                group.ForeColor = TextPrimary;
                group.Font = UiTypography.BodyStrong();
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
        button.Height = Math.Max(UiMetrics.ButtonHeight, button.Height);
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 1;
        button.UseVisualStyleBackColor = false;
        button.Cursor = Cursors.Hand;
        button.Padding = new Padding(11, 0, 11, 0);
        button.Margin = new Padding(4, 3, 4, 3);
        button.Font = UiTypography.BodyStrong();
        var (back, fore, border) = kind switch
        {
            UiButtonKind.Primary => (Primary, Color.White, Primary),
            UiButtonKind.Success => (Success, Color.White, Success),
            UiButtonKind.Warning => (Color.FromArgb(255, 247, 237), Color.FromArgb(194, 65, 12), Color.FromArgb(254, 215, 170)),
            UiButtonKind.Danger => (Color.FromArgb(254, 242, 242), Danger, Color.FromArgb(254, 202, 202)),
            UiButtonKind.Ghost => (Card, TextSecondary, Border),
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
        ApplyRoundedCorners(button, UiMetrics.ControlRadius);
    }

    public static void StyleToggle(CheckBox toggle)
    {
        toggle.Appearance = Appearance.Button;
        toggle.AutoSize = false;
        toggle.Height = 30;
        toggle.FlatStyle = FlatStyle.Flat;
        toggle.FlatAppearance.BorderSize = 1;
        toggle.TextAlign = ContentAlignment.MiddleCenter;
        toggle.Cursor = Cursors.Hand;
        toggle.Font = UiTypography.Caption(FontStyle.Bold);
        toggle.Padding = new Padding(8, 0, 8, 0);

        void RefreshVisual()
        {
            toggle.BackColor = toggle.Checked ? PrimarySoft : Color.FromArgb(248, 250, 252);
            toggle.ForeColor = toggle.Checked ? Primary : TextSecondary;
            toggle.FlatAppearance.BorderColor = toggle.Checked
                ? Color.FromArgb(199, 210, 254)
                : Border;
            toggle.FlatAppearance.MouseOverBackColor = Color.FromArgb(241, 245, 249);
        }

        toggle.CheckedChanged += (_, _) => RefreshVisual();
        RefreshVisual();
        ApplyRoundedCorners(toggle, 9);
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
        grid.ColumnHeadersHeight = Math.Max(UiMetrics.GridHeaderHeight, grid.ColumnHeadersHeight);
        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(248, 250, 252);
        grid.ColumnHeadersDefaultCellStyle.ForeColor = TextSecondary;
        grid.ColumnHeadersDefaultCellStyle.Font = UiTypography.BodyStrong();
        grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(248, 250, 252);
        grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = TextSecondary;
        grid.DefaultCellStyle.BackColor = Card;
        grid.DefaultCellStyle.ForeColor = TextPrimary;
        grid.DefaultCellStyle.SelectionBackColor = PrimarySoft;
        grid.DefaultCellStyle.SelectionForeColor = Color.FromArgb(49, 46, 129);
        grid.DefaultCellStyle.Padding = new Padding(5, 2, 5, 2);
        grid.RowTemplate.Height = Math.Max(UiMetrics.GridRowHeight, grid.RowTemplate.Height);
        grid.RowHeadersVisible = false;
    }
}
