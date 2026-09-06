namespace ToolTikTokV12.Controls;

/// <summary>Semantic colors for every Manager and Worker surface.</summary>
public static class UiColors
{
    public static readonly Color Canvas = Color.FromArgb(246, 248, 252);
    public static readonly Color Surface = Color.White;
    public static readonly Color SurfaceMuted = Color.FromArgb(248, 250, 252);
    public static readonly Color SurfaceHover = Color.FromArgb(241, 245, 249);
    public static readonly Color Border = Color.FromArgb(226, 232, 240);
    public static readonly Color BorderStrong = Color.FromArgb(203, 213, 225);
    public static readonly Color Primary = Color.FromArgb(79, 70, 229);
    public static readonly Color PrimaryHover = Color.FromArgb(67, 56, 202);
    public static readonly Color PrimaryPressed = Color.FromArgb(55, 48, 163);
    public static readonly Color PrimarySoft = Color.FromArgb(238, 242, 255);
    public static readonly Color Text = Color.FromArgb(15, 23, 42);
    public static readonly Color TextMuted = Color.FromArgb(100, 116, 139);
    public static readonly Color TextSubtle = Color.FromArgb(148, 163, 184);
    public static readonly Color Sidebar = Color.FromArgb(15, 23, 42);
    public static readonly Color SidebarHover = Color.FromArgb(30, 41, 59);
    public static readonly Color Success = Color.FromArgb(22, 163, 74);
    public static readonly Color SuccessSoft = Color.FromArgb(220, 252, 231);
    public static readonly Color Warning = Color.FromArgb(245, 158, 11);
    public static readonly Color WarningSoft = Color.FromArgb(254, 249, 195);
    public static readonly Color Danger = Color.FromArgb(239, 68, 68);
    public static readonly Color DangerSoft = Color.FromArgb(254, 242, 242);
    public static readonly Color Purple = Color.FromArgb(168, 85, 247);
    public static readonly Color Cyan = Color.FromArgb(14, 165, 233);
}

public static class UiTypography
{
    public const string Family = "Segoe UI";
    public const string EmphasisFamily = "Segoe UI Semibold";
    public const string SymbolFamily = "Segoe UI Symbol";

    public static Font Caption(FontStyle style = FontStyle.Regular) => new(Family, 8.5F, style);
    public static Font Body(FontStyle style = FontStyle.Regular) => new(Family, 9.5F, style);
    public static Font BodyStrong() => new(EmphasisFamily, 9.5F, FontStyle.Bold);
    public static Font SectionTitle() => new(EmphasisFamily, 11F, FontStyle.Bold);
    public static Font PageTitle() => new(EmphasisFamily, 15F, FontStyle.Bold);
    public static Font Metric() => new(EmphasisFamily, 20F, FontStyle.Bold);
    public static Font Symbol(float size = 14F) => new(SymbolFamily, size, FontStyle.Bold);
}

public static class UiSpacing
{
    public const int Xs = 4;
    public const int Sm = 8;
    public const int Md = 12;
    public const int Lg = 16;
    public const int Xl = 20;
    public const int Xxl = 24;
}

public static class UiMetrics
{
    public const int SidebarWidth = 236;
    public const int HeaderHeight = 76;
    public const int ButtonHeight = 40;
    public const int InputHeight = 40;
    public const int GridHeaderHeight = 42;
    public const int GridRowHeight = 46;
    public const int CardRadius = 14;
    public const int ControlRadius = 9;
}

/// <summary>One source for glyphs; feature code should not introduce ad-hoc emoji.</summary>
public static class IconGlyphs
{
    public const string Overview = "▦";
    public const string Profiles = "▰";
    public const string Messages = "✉";
    public const string Monitor = "▣";
    public const string Accounts = "◫";
    public const string Logs = "☷";
    public const string Settings = "⚙";
    public const string Add = "+";
    public const string More = "•••";
    public const string Refresh = "↻";
    public const string Open = "▶";
    public const string Pause = "Ⅱ";
    public const string Stop = "■";
    public const string Import = "↑";
    public const string Info = "ⓘ";
}

/// <summary>Reusable KPI surface with a stable layout at common DPI settings.</summary>
public sealed class UiMetricCard : ModernCardPanel
{
    readonly Label _icon;
    public Label ValueLabel { get; }
    readonly Label _caption;

    public UiMetricCard(string glyph, string caption, Color accent, Color tint)
    {
        Dock = DockStyle.Fill;
        Margin = new Padding(0, UiSpacing.Xs, UiSpacing.Md, UiSpacing.Sm);
        Padding = new Padding(UiSpacing.Lg);
        CornerRadius = UiMetrics.CardRadius;
        BorderColor = UiColors.Border;
        BackColor = UiColors.Surface;

        _icon = new Label
        {
            Text = glyph,
            AutoSize = false,
            Size = new Size(46, 46),
            Location = new Point(14, 20),
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = tint,
            ForeColor = accent,
            Font = UiTypography.Symbol(17F)
        };
        UiTheme.ApplyRoundedCorners(_icon, 11);
        ValueLabel = new Label
        {
            Text = "0",
            AutoSize = false,
            Location = new Point(76, 15),
            Size = new Size(120, 34),
            ForeColor = UiColors.Text,
            BackColor = Color.Transparent,
            Font = UiTypography.Metric()
        };
        _caption = new Label
        {
            Text = caption,
            AutoSize = false,
            Location = new Point(76, 50),
            Size = new Size(160, 24),
            ForeColor = UiColors.TextMuted,
            BackColor = Color.Transparent,
            Font = UiTypography.Body()
        };
        Controls.Add(_icon);
        Controls.Add(ValueLabel);
        Controls.Add(_caption);
        Resize += (_, _) => ValueLabel.Width = _caption.Width = Math.Max(66, ClientSize.Width - 88);
    }
}

public sealed class UiStatusBadge : Label
{
    public UiStatusBadge(string text, Color foreground, Color background)
    {
        Text = text;
        AutoSize = false;
        Height = 28;
        TextAlign = ContentAlignment.MiddleCenter;
        ForeColor = foreground;
        BackColor = background;
        Font = UiTypography.Caption(FontStyle.Bold);
        Padding = new Padding(UiSpacing.Sm, 0, UiSpacing.Sm, 0);
        UiTheme.ApplyRoundedCorners(this, UiMetrics.ControlRadius);
    }
}
