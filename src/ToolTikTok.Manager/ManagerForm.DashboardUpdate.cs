using ToolTikTokV12.Utils;
using System.Diagnostics;
using System.Net.Http;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ToolTikTokV12.Controls;

namespace ToolTikTokManagerV13;

public sealed partial class ManagerForm
{
    static string ManagerDisplayVersion => AppVersionInfo.Current;
    const string UpdateSettingsFileName = "manager_update.json";

    sealed class DashboardMarker { }
    sealed class UpdateSettings
    {
        // version.json: manifest ngắn cho bản mới nhất, giữ tương thích với cấu hình cũ.
        public string ManifestUrl { get; set; } = "";
        // versions.json: lịch sử nhiều phiên bản. Để trống sẽ tự suy ra từ ManifestUrl.
        public string VersionsManifestUrl { get; set; } = "";
        public string Channel { get; set; } = "stable";
        public bool AutoCheck { get; set; } = true;
        // Khi người dùng chủ động downgrade, giữ ở đúng bản này để không nhắc nâng lại ngay.
        public string PinnedVersion { get; set; } = "";
    }

    sealed class UpdateManifest
    {
        public string Version { get; set; } = "";
        public string SetupUrl { get; set; } = "";
        public string Sha256 { get; set; } = "";
        public string Notes { get; set; } = "";
        public string Channel { get; set; } = "stable";
        public string Status { get; set; } = ""; // stable | beta | withdrawn
        public string ReleaseDate { get; set; } = "";
        public bool? AllowInstall { get; set; }

        public string EffectiveStatus
        {
            get
            {
                var raw = string.IsNullOrWhiteSpace(Status) ? Channel : Status;
                raw = (raw ?? "stable").Trim().ToLowerInvariant();
                return raw switch
                {
                    "test" => "beta",
                    "stable" or "beta" or "withdrawn" => raw,
                    _ => "stable"
                };
            }
        }

        public bool IsInstallAllowed => AllowInstall ?? !EffectiveStatus.Equals("withdrawn", StringComparison.OrdinalIgnoreCase);
    }

    sealed class VersionChoice
    {
        public required UpdateManifest Manifest { get; init; }
        public bool IsLatest { get; init; }
        public bool IsCurrent { get; init; }

        public override string ToString()
        {
            var version = (Manifest.Version ?? "").Trim().TrimStart('v', 'V');
            var status = Manifest.EffectiveStatus switch
            {
                "withdrawn" => "Đã thu hồi",
                "beta" => "Beta",
                _ => "Stable"
            };
            var badges = new List<string>();
            if (IsLatest) badges.Add("Mới nhất");
            if (IsCurrent) badges.Add("Đang dùng");
            badges.Add(status);
            if (!string.IsNullOrWhiteSpace(Manifest.ReleaseDate)) badges.Add(Manifest.ReleaseDate.Trim());
            var notes = CompactVersionNotes(Manifest.Notes, 70);
            return $"{version} — {string.Join(" · ", badges)}{(notes.Length > 0 ? " — " + notes : "")}";
        }
    }

    readonly DashboardMarker _dashboardMarker = new();
    readonly Dictionary<string, string> _dashboardAccountCache = new(StringComparer.OrdinalIgnoreCase);
    DateTime _dashboardAccountCacheUtc = DateTime.MinValue;
    TabPage? _dashboardTab;
    DataGridView? _dashboardGrid;
    Label? _dashboardSummary;
    Label? _dashboardOpenValue;
    Label? _dashboardRunningValue;
    Label? _dashboardPausedValue;
    Label? _dashboardRecoveringValue;
    Label? _dashboardRuntimeValue;
    TextBox? _dashboardSearchBox;
    ComboBox? _dashboardStatusFilter;
    Button? _dashboardStatusFilterButton;
    ContextMenuStrip? _dashboardStatusFilterMenu;
    Panel? _dashboardEmptyState;
    Label? _dashboardUpdateStatus;
    Button? _dashboardUpdateButton;
    Button? _dashboardVersionMenuButton;
    ContextMenuStrip? _dashboardVersionMenu;
    Label? _dashboardLatestBadge;
    ComboBox? _dashboardVersionSelector;
    CheckBox? _dashboardHoldVersionToggle;
    CheckBox? _dashboardShowAllProfilesToggle;
    UpdateManifest? _latestUpdate;
    readonly List<UpdateManifest> _availableVersions = new();
    bool _updateCheckInProgress;
    bool _updateDownloadInProgress;
    bool _updatingHoldToggle;

    string UpdateSettingsPath => Path.Combine(_baseDir, UpdateSettingsFileName);

    void InitializeDashboardAndUpdater()
    {
        EnsureDashboardTab();
        RefreshDashboard();

        // Dashboard chỉ đọc LastSnapshot vốn đã được Manager refresh định kỳ,
        // không tạo thêm một vòng status IPC riêng cho từng Worker.
        _refreshTimer.Tick += (_, _) => RefreshDashboard();

        Shown += async (_, _) =>
        {
            RefreshDashboard();
            var settings = LoadUpdateSettings();
            RefreshUpdatePanel(settings);
            if (settings.AutoCheck && !string.IsNullOrWhiteSpace(settings.ManifestUrl))
            {
                await Task.Delay(1200);
                await CheckForUpdatesAsync(showWhenCurrent: false);
            }
        };
    }

    void EnsureDashboardTab()
    {
        if (_dashboardTab is not null && !_dashboardTab.IsDisposed && _dashboardTab.Parent == _tabs)
            return;

        var page = new TabPage("Tổng quan")
        {
            Tag = _dashboardMarker,
            BackColor = UiTheme.Canvas,
            AutoScroll = true
        };

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 650,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(10),
            BackColor = UiTheme.Canvas
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 128F));

        var statistics = BuildDashboardStatistics();
        var updatePanel = BuildUpdatePanel();
        _dashboardGrid = BuildDashboardGrid();
        var profileCard = BuildDashboardProfileCard(_dashboardGrid);
        var actions = BuildDashboardActions();

        root.Controls.Add(statistics, 0, 0);
        root.Controls.Add(updatePanel, 0, 1);
        root.Controls.Add(profileCard, 0, 2);
        root.Controls.Add(actions, 0, 3);
        page.Controls.Add(root);
        page.Resize += (_, _) =>
        {
            root.Width = Math.Max(1, page.ClientSize.Width);
            var compactActions = page.ClientSize.Width < 1180;
            root.RowStyles[3].Height = compactActions ? 220F : 128F;
            root.Height = compactActions
                ? Math.Max(800, page.ClientSize.Height)
                : Math.Max(650, Math.Min(730, page.ClientSize.Height));
            page.AutoScrollMinSize = new Size(0, root.Height);
        };
        page.Enter += (_, _) => RefreshDashboard();

        _dashboardTab = page;
        _tabs.TabPages.Insert(0, page);
        UpdateWorkspaceTabBarAppearance();
        SelectTabPageSafely(page);
    }

    Control BuildDashboardStatistics()
    {
        var row = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 116,
            ColumnCount = 5,
            RowCount = 1,
            Margin = new Padding(0, 10, 0, 0),
            BackColor = UiTheme.Canvas
        };
        for (var i = 0; i < 5; i++) row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));

        ModernCardPanel Card(string glyph, string title, string caption, Color accent, Color tint, out Label value)
        {
            var card = new ModernCardPanel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 12, 0),
                Padding = new Padding(14, 12, 12, 10),
                BorderColor = UiTheme.Border,
                CornerRadius = 15,
                GradientStart = tint,
                GradientEnd = Color.White
            };
            var icon = new Label
            {
                Text = glyph,
                AutoSize = false,
                Size = new Size(44, 44),
                Location = new Point(14, 22),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = ControlPaint.Light(tint, .18F),
                ForeColor = accent,
                Font = new Font("Segoe UI Symbol", 17F, FontStyle.Bold)
            };
            UiTheme.ApplyRoundedCorners(icon, 11);
            value = new Label
            {
                Text = "0",
                AutoSize = false,
                Size = new Size(120, 33),
                Location = new Point(64, 38),
                ForeColor = UiTheme.TextPrimary,
                BackColor = Color.Transparent,
                UseCompatibleTextRendering = true,
                Font = new Font("Segoe UI Semibold", 20F, FontStyle.Bold)
            };
            var cardTitle = new Label
            {
                Text = title,
                AutoSize = false,
                Size = new Size(120, 22),
                Location = new Point(64, 14),
                ForeColor = accent,
                BackColor = Color.Transparent,
                UseCompatibleTextRendering = true,
                Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold)
            };
            card.Controls.Add(cardTitle);
            card.Controls.Add(value);
            var cardCaption = new Label
            {
                Text = caption,
                AutoSize = false,
                Size = new Size(120, 22),
                Location = new Point(64, 72),
                ForeColor = UiTheme.TextSecondary,
                BackColor = Color.Transparent,
                UseCompatibleTextRendering = true,
                Font = new Font("Segoe UI", 8.5F)
            };
            card.Controls.Add(cardCaption);
            card.Controls.Add(icon);
            var valueLabel = value;
            card.Resize += (_, _) =>
            {
                var textWidth = Math.Max(78, card.ClientSize.Width - 72);
                cardTitle.Width = textWidth;
                valueLabel.Width = textWidth;
                cardCaption.Width = textWidth;
            };
            return card;
        }

        row.Controls.Add(Card("▦", "Tổng quan", "Hồ sơ đang mở", UiTheme.Primary, Color.FromArgb(246, 248, 255), out _dashboardOpenValue), 0, 0);
        row.Controls.Add(Card("▶", "Đang chạy", "Đang hoạt động", UiTheme.Success, Color.FromArgb(242, 255, 247), out _dashboardRunningValue), 1, 0);
        row.Controls.Add(Card("Ⅱ", "Tạm dừng", "Đang tạm dừng", UiTheme.Warning, Color.FromArgb(255, 249, 234), out _dashboardPausedValue), 2, 0);
        row.Controls.Add(Card("↻", "Khôi phục", "Đang khôi phục", UiTheme.Purple, Color.FromArgb(255, 244, 255), out _dashboardRecoveringValue), 3, 0);
        var last = Card("◷", "Tổng thời gian", "Thời gian vận hành", UiTheme.Cyan, Color.FromArgb(241, 252, 255), out _dashboardRuntimeValue);
        _dashboardRuntimeValue.Font = new Font("Segoe UI Semibold", 17F, FontStyle.Bold);
        last.Margin = Padding.Empty;
        row.Controls.Add(last, 4, 0);
        return row;
    }

    Control BuildDashboardProfileCard(DataGridView grid)
    {
        var card = new ModernCardPanel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 0, 10),
            Padding = new Padding(16),
            BorderColor = UiTheme.Border,
            CornerRadius = 16
        };
        var heading = new Panel { Dock = DockStyle.Fill, BackColor = UiTheme.Card };
        var folderIcon = new Label
        {
            Text = "▰",
            AutoSize = false,
            Size = new Size(42, 42),
            Location = new Point(0, 7),
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = UiTheme.PrimarySoft,
            ForeColor = UiTheme.Primary,
            Font = new Font("Segoe UI Symbol", 16F, FontStyle.Bold)
        };
        UiTheme.ApplyRoundedCorners(folderIcon, 11);
        heading.Controls.Add(new Label
        {
            Text = "Danh sách hồ sơ",
            AutoSize = true,
            Location = new Point(54, 4),
            ForeColor = UiTheme.TextPrimary,
            Font = new Font("Segoe UI Semibold", 13.5F, FontStyle.Bold)
        });
        heading.Controls.Add(new Label
        {
            Text = "Theo dõi trạng thái hồ sơ theo thời gian thực.",
            AutoSize = true,
            Location = new Point(55, 33),
            ForeColor = UiTheme.TextSecondary,
            Font = new Font("Segoe UI", 8F)
        });
        // Vẫn giữ target cho code cập nhật số liệu cũ, nhưng không render lặp KPI.
        _dashboardSummary = new Label { Visible = false };
        heading.Controls.Add(folderIcon);

        var tools = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            Padding = new Padding(8, 8, 0, 8),
            BackColor = UiTheme.Card
        };
        tools.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        tools.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        tools.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200F));
        tools.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 48F));
        _dashboardSearchBox = new TextBox
        {
            Dock = DockStyle.Fill,
            PlaceholderText = "Tìm kiếm hồ sơ hoặc tài khoản...",
            Margin = Padding.Empty,
            BorderStyle = BorderStyle.None,
            Font = new Font("Segoe UI", 9.5F)
        };
        _dashboardSearchBox.TextChanged += (_, _) => RefreshDashboard();
        var searchShell = new ModernCardPanel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 10, 0),
            Padding = new Padding(36, 12, 10, 8),
            BorderColor = UiTheme.Border,
            CornerRadius = 10,
            BackColor = Color.White
        };
        var searchIcon = new Label
        {
            Text = "⌕",
            AutoSize = false,
            Size = new Size(28, 28),
            Location = new Point(7, 6),
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = UiTheme.TextSecondary,
            Font = new Font("Segoe UI Symbol", 14F)
        };
        searchShell.Controls.Add(_dashboardSearchBox);
        searchShell.Controls.Add(searchIcon);
        _dashboardStatusFilter = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Margin = new Padding(0, 0, 10, 0)
        };
        var statusOptions = new[] { "Tất cả trạng thái", "Đang chạy", "Tạm dừng", "Đang khôi phục", "Đã dừng", "Không xác định" };
        _dashboardStatusFilter.Items.AddRange(statusOptions);
        _dashboardStatusFilter.SelectedIndex = 0;
        ModernDialog.StyleSelectionInput(_dashboardStatusFilter);
        _dashboardStatusFilterMenu = new ContextMenuStrip
        {
            ShowImageMargin = false,
            BackColor = Color.White,
            ForeColor = UiTheme.TextPrimary,
            Font = new Font("Segoe UI", 9F),
            Padding = new Padding(4)
        };
        _dashboardStatusFilterButton = new Button
        {
            Text = "◉  Tất cả trạng thái  ▾",
            AutoSize = false,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 10, 0),
            TextAlign = ContentAlignment.MiddleCenter
        };
        UiTheme.StyleButton(_dashboardStatusFilterButton, UiButtonKind.Neutral);
        _dashboardStatusFilterButton.AutoSize = false;
        _dashboardStatusFilterButton.Dock = DockStyle.Fill;
        _dashboardStatusFilterButton.Margin = new Padding(0, 0, 10, 0);
        foreach (var option in statusOptions)
        {
            var item = new ToolStripMenuItem(option) { AutoSize = false, Width = 190, Height = 34 };
            item.Click += (_, _) => _dashboardStatusFilter.SelectedItem = option;
            _dashboardStatusFilterMenu.Items.Add(item);
        }
        _dashboardStatusFilterButton.Click += (_, _) =>
            _dashboardStatusFilterMenu.Show(_dashboardStatusFilterButton, new Point(0, _dashboardStatusFilterButton.Height));
        _dashboardStatusFilter.SelectedIndexChanged += (_, _) =>
        {
            if (_dashboardStatusFilterButton is not null)
                _dashboardStatusFilterButton.Text = $"◉  {_dashboardStatusFilter.SelectedItem}  ▾";
            RefreshDashboard();
        };
        // Bộ lọc "hồ sơ đang mở" cũ gây lặp và phá header. Giữ state mặc định
        // cho luồng lọc hiện hữu nhưng không render control thừa này.
        _dashboardShowAllProfilesToggle = new CheckBox { Checked = false, Visible = false };
        var refresh = new Button { Text = "↻", AutoSize = false, Dock = DockStyle.Fill, Margin = Padding.Empty };
        UiTheme.StyleButton(refresh, UiButtonKind.Neutral);
        refresh.AutoSize = false;
        refresh.Dock = DockStyle.Fill;
        refresh.Padding = Padding.Empty;
        refresh.Click += (_, _) => RefreshDashboard(forceAccountRefresh: true);
        _managerToolTip.SetToolTip(refresh, "Làm mới danh sách hồ sơ và tài khoản.");
        tools.Controls.Add(searchShell, 0, 0);
        tools.Controls.Add(_dashboardStatusFilterButton, 1, 0);
        tools.Controls.Add(refresh, 2, 0);

        var top = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 66,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = UiTheme.Card,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 570F));
        top.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        top.Controls.Add(heading, 0, 0);
        top.Controls.Add(tools, 1, 0);

        _dashboardEmptyState = new Panel { Dock = DockStyle.Fill, BackColor = UiTheme.Card, Visible = false };
        var emptyLayout = new TableLayoutPanel
        {
            AutoSize = true,
            ColumnCount = 1,
            RowCount = 4,
            Anchor = AnchorStyles.None,
            BackColor = UiTheme.Card
        };
        var emptyIcon = new ModernCardPanel
        {
            Size = new Size(96, 96),
            Anchor = AnchorStyles.None,
            BackColor = UiTheme.PrimarySoft,
            Margin = new Padding(0, 0, 0, 8),
            CornerRadius = 48,
            BorderThickness = 0,
            GradientStart = UiTheme.PrimarySoft,
            GradientEnd = Color.FromArgb(248, 250, 255)
        };
        UiTheme.ApplyRoundedCorners(emptyIcon, 48);
        emptyIcon.Controls.Add(new Label
        {
            Text = "▰",
            AutoSize = false,
            Size = new Size(66, 58),
            Location = new Point(14, 20),
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = Color.Transparent,
            ForeColor = Color.FromArgb(148, 177, 242),
            Font = new Font("Segoe UI Symbol", 31F, FontStyle.Bold)
        });
        var emptySearch = new Label
        {
            Text = "⌕",
            AutoSize = false,
            Size = new Size(38, 38),
            Location = new Point(55, 55),
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = Color.White,
            ForeColor = Color.FromArgb(48, 88, 221),
            Font = new Font("Segoe UI Symbol", 20F, FontStyle.Bold)
        };
        UiTheme.ApplyRoundedCorners(emptySearch, 19);
        emptyIcon.Controls.Add(emptySearch);
        emptyLayout.Controls.Add(emptyIcon, 0, 0);
        emptyLayout.Controls.Add(new Label
        {
            Text = "Chưa có hồ sơ phù hợp",
            AutoSize = true,
            Anchor = AnchorStyles.None,
            Font = new Font("Segoe UI Semibold", 13.5F, FontStyle.Bold),
            ForeColor = UiTheme.TextPrimary,
            Margin = new Padding(0, 6, 0, 4)
        }, 0, 1);
        emptyLayout.Controls.Add(new Label
        {
            Text = "Hãy tạo, mở hồ sơ hoặc thay đổi bộ lọc để bắt đầu.",
            AutoSize = true,
            Anchor = AnchorStyles.None,
            ForeColor = UiTheme.TextSecondary,
            Font = new Font("Segoe UI", 9.5F),
            Margin = new Padding(0, 0, 0, 10)
        }, 0, 2);
        var emptyActions = new FlowLayoutPanel { AutoSize = true, Anchor = AnchorStyles.None, WrapContents = false };
        var open = new Button { Text = "+  Mở hồ sơ", AutoSize = false, Size = new Size(150, 42) };
        var create = new Button { Text = "Tạo hồ sơ mới", AutoSize = false, Size = new Size(150, 42) };
        UiTheme.StyleButton(open, UiButtonKind.Primary);
        UiTheme.StyleButton(create, UiButtonKind.Neutral);
        open.AutoSize = false;
        create.AutoSize = false;
        open.Margin = new Padding(0, 0, 6, 0);
        create.Margin = new Padding(6, 0, 0, 0);
        open.Click += (_, _) => OpenProfileChooser();
        create.Click += (_, _) => AddProfile();
        emptyActions.Controls.Add(open);
        emptyActions.Controls.Add(create);
        emptyLayout.Controls.Add(emptyActions, 0, 3);
        _dashboardEmptyState.Controls.Add(emptyLayout);
        _dashboardEmptyState.Resize += (_, _) => emptyLayout.Location = new Point(
            Math.Max(0, (_dashboardEmptyState.ClientSize.Width - emptyLayout.Width) / 2),
            Math.Max(0, (_dashboardEmptyState.ClientSize.Height - emptyLayout.Height) / 2));

        card.Controls.Add(grid);
        card.Controls.Add(_dashboardEmptyState);
        card.Controls.Add(top);
        return card;
    }

    Panel BuildUpdatePanel()
    {
        var panel = new ModernCardPanel
        {
            Dock = DockStyle.Top,
            Height = 100,
            Padding = new Padding(16, 12, 16, 12),
            Margin = new Padding(0, 10, 0, 10),
            BackColor = UiTheme.Card,
            BorderColor = UiTheme.Border,
            CornerRadius = 16,
            GradientStart = Color.FromArgb(248, 250, 255),
            GradientEnd = Color.White
        };

        var updateIcon = new Label
        {
            Text = "⚙",
            AutoSize = false,
            Size = new Size(44, 44),
            Location = new Point(0, 16),
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = UiTheme.PrimarySoft,
            ForeColor = UiTheme.Primary,
            Font = new Font("Segoe UI Symbol", 17F, FontStyle.Bold)
        };
        UiTheme.ApplyRoundedCorners(updateIcon, 12);
        var updateTitle = new Label
        {
            AutoSize = true,
            Text = $"Phiên bản hiện tại: V{ManagerDisplayVersion}",
            Location = new Point(58, 4),
            ForeColor = UiTheme.TextPrimary,
            Font = new Font("Segoe UI Semibold", 11F, FontStyle.Bold)
        };
        _dashboardLatestBadge = new Label
        {
            Text = "Mới nhất",
            AutoSize = false,
            Size = new Size(64, 24),
            Location = new Point(300, 1),
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = Color.FromArgb(220, 252, 231),
            ForeColor = Color.FromArgb(21, 128, 61),
            Font = new Font("Segoe UI Semibold", 8F, FontStyle.Bold),
            Visible = false
        };
        UiTheme.ApplyRoundedCorners(_dashboardLatestBadge, 12);

        _dashboardUpdateStatus = new Label
        {
            AutoSize = false,
            Height = 24,
            TextAlign = ContentAlignment.MiddleLeft,
            Text = "Chưa có cấu hình nguồn cập nhật.",
            ForeColor = UiTheme.TextSecondary,
            Location = new Point(58, 29),
            AutoEllipsis = true
        };

        _dashboardVersionSelector = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Dock = DockStyle.Fill,
            Margin = new Padding(5, 15, 5, 15),
            IntegralHeight = false,
            DropDownHeight = 260
        };
        ModernDialog.StyleSelectionInput(_dashboardVersionSelector);
        _dashboardVersionSelector.Font = new Font("Segoe UI", 8.5F);
        _dashboardVersionSelector.Items.Add("Cài / Hạ phiên bản");
        _dashboardVersionSelector.SelectedIndex = 0;
        _dashboardVersionSelector.SelectedIndexChanged += (_, _) => RefreshSelectedVersionAction();

        _dashboardUpdateButton = new Button
        {
            Text = "Áp dụng",
            AutoSize = false,
            Dock = DockStyle.Fill,
            Margin = new Padding(5, 11, 5, 11),
            Enabled = false,
        };
        UiTheme.StyleButton(_dashboardUpdateButton, UiButtonKind.Primary);
        _dashboardUpdateButton.Font = new Font("Segoe UI Semibold", 8.25F, FontStyle.Bold);
        _dashboardUpdateButton.AutoSize = false;
        _dashboardUpdateButton.Dock = DockStyle.Fill;
        _dashboardUpdateButton.Click += async (_, _) => await DownloadAndInstallSelectedVersionAsync();

        _dashboardVersionMenu = new ContextMenuStrip
        {
            ShowImageMargin = false,
            BackColor = Color.White,
            ForeColor = UiTheme.TextPrimary,
            Font = new Font("Segoe UI", 9F),
            Padding = new Padding(4)
        };
        _dashboardVersionMenuButton = new Button
        {
            Text = "Cài / Hạ phiên bản  ▾",
            AutoSize = false,
            Dock = DockStyle.Fill,
            Margin = new Padding(5, 11, 5, 11)
        };
        UiTheme.StyleButton(_dashboardVersionMenuButton, UiButtonKind.Neutral);
        _dashboardVersionMenuButton.AutoSize = false;
        _dashboardVersionMenuButton.Dock = DockStyle.Fill;
        _dashboardVersionMenuButton.Click += (_, _) =>
            _dashboardVersionMenu.Show(_dashboardVersionMenuButton, new Point(0, _dashboardVersionMenuButton.Height));

        var check = new Button
        {
            Text = "Kiểm tra cập nhật",
            AutoSize = false,
            Dock = DockStyle.Fill,
            Margin = new Padding(5, 11, 5, 11)
        };
        UiTheme.StyleButton(check, UiButtonKind.Primary);
        check.Font = new Font("Segoe UI Semibold", 8.25F, FontStyle.Bold);
        check.AutoSize = false;
        check.Dock = DockStyle.Fill;
        check.Click += async (_, _) => await CheckForUpdatesAsync(showWhenCurrent: true);

        var configure = new Button
        {
            Text = "Cấu hình cập nhật",
            AutoSize = false,
            Dock = DockStyle.Fill,
            Margin = new Padding(5, 11, 0, 11)
        };
        UiTheme.StyleButton(configure, UiButtonKind.Neutral);
        configure.Font = new Font("Segoe UI Semibold", 8.25F, FontStyle.Bold);
        configure.AutoSize = false;
        configure.Dock = DockStyle.Fill;
        configure.Click += (_, _) => ShowUpdateSettingsDialog();

        _dashboardHoldVersionToggle = new CheckBox
        {
            AutoSize = false,
            Size = new Size(146, 28),
            Text = "Giữ ở phiên bản này",
            Margin = new Padding(0),
            Location = new Point(58, 53),
            Cursor = Cursors.Hand
        };
        UiTheme.StyleToggle(_dashboardHoldVersionToggle);
        _dashboardHoldVersionToggle.CheckedChanged += (_, _) => OnHoldVersionToggleChanged();

        var left = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
        left.Controls.Add(updateIcon);
        left.Controls.Add(updateTitle);
        left.Controls.Add(_dashboardLatestBadge);
        left.Controls.Add(_dashboardUpdateStatus);
        // Tùy chọn giữ phiên bản được hiển thị trong menu Cài / Hạ để card gọn hơn.
        left.Resize += (_, _) =>
        {
            _dashboardUpdateStatus.Width = Math.Max(100, left.ClientSize.Width - 68);
            _dashboardHoldVersionToggle.Width = Math.Min(180, Math.Max(130, left.ClientSize.Width - 68));
        };

        var right = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        right.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220F));
        right.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190F));
        right.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        right.Controls.Add(_dashboardVersionMenuButton, 0, 0);
        right.Controls.Add(check, 1, 0);
        right.Controls.Add(configure, 2, 0);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 650F));
        layout.Controls.Add(left, 0, 0);
        layout.Controls.Add(right, 1, 0);
        panel.Controls.Add(layout);
        return panel;
    }

    DataGridView BuildDashboardGrid()
    {
        var grid = new DataGridView
        {
            Name = "DashboardGrid",
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            ReadOnly = true,
            MultiSelect = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            RowHeadersVisible = false,
            AutoGenerateColumns = false,
            BackgroundColor = UiTheme.Card,
            BorderStyle = BorderStyle.None,
            EnableHeadersVisualStyles = false,
            ColumnHeadersHeight = 40,
            RowTemplate = { Height = 36 }
        };
        UiTheme.StyleGrid(grid);

        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Profile", HeaderText = "Profile", Width = 105,
            DefaultCellStyle = new DataGridViewCellStyle
            {
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = UiTheme.Primary,
                SelectionForeColor = Color.FromArgb(49, 46, 129)
            }
        });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Account", HeaderText = "Tài khoản", Width = 155 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "RunState", HeaderText = "Trạng thái", Width = 110 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Chrome", HeaderText = "Chrome", Width = 105 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "RunTime", HeaderText = "T/g chạy", Width = 100 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Step", HeaderText = "Bước", Width = 70 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Rounds", HeaderText = "Vòng", Width = 75 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Ram", HeaderText = "RAM chính", Width = 95 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Detail", HeaderText = "Chi tiết", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, MinimumWidth = 220 });
        LogGridSchema(grid, "DashboardGrid", "Profile", "Account", "RunState", "Chrome", "RunTime", "Step", "Rounds", "Ram", "Detail");

        grid.CellDoubleClick += async (_, e) =>
        {
            if (e.RowIndex < 0) return;
            if (grid.Rows[e.RowIndex].Tag is not ProfileContext ctx) return;
            try
            {
                await OpenProfileAsync(ctx);
                if (ctx.Tab is not null) SelectTabPageSafely(ctx.Tab);
            }
            catch (Exception ex) { ShowError(ex); }
        };
        return grid;
    }

    Panel BuildDashboardActions()
    {
        var card = new ModernCardPanel
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = new Padding(14, 8, 14, 8),
            BorderColor = UiTheme.Border,
            CornerRadius = 16
        };
        var heading = new Panel
        {
            Dock = DockStyle.Top,
            Height = 38,
            BackColor = UiTheme.Card
        };
        heading.Controls.Add(new Label
        {
            Text = "⚡  Thao tác nhanh",
            AutoSize = true,
            Location = new Point(0, 0),
            ForeColor = UiTheme.TextPrimary,
            Font = new Font("Segoe UI Semibold", 10.5F, FontStyle.Bold)
        });
        heading.Controls.Add(new Label
        {
            Text = "Truy cập nhanh các chức năng thường dùng.",
            AutoSize = true,
            Location = new Point(20, 21),
            ForeColor = UiTheme.TextSecondary,
            Font = new Font("Segoe UI", 8F)
        });
        var actions = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 6,
            RowCount = 1,
            Padding = new Padding(0, 2, 0, 0),
            Margin = Padding.Empty,
            BackColor = UiTheme.Card
        };
        for (var i = 0; i < 6; i++) actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F / 6F));
        var actionCards = new List<ModernActionCard>();

        ModernActionCard ActionButton(string glyph, string titleText, string description, Color accent, Func<ProfileContext, Task> action)
        {
            var b = new ModernActionCard
            {
                Glyph = glyph,
                Title = titleText,
                Subtitle = description,
                Accent = accent,
                AccessibleName = titleText,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 10, 0)
            };
            b.Click += async (_, _) =>
            {
                var ctx = DashboardSelectedContext();
                if (ctx is null)
                {
                    ModernDialog.ShowInfo(this, "Hãy chọn một hồ sơ trong danh sách trước.", "Chưa chọn hồ sơ");
                    return;
                }
                try
                {
                    await action(ctx);
                    try { await RefreshStatusAsync(ctx); } catch { }
                    RefreshDashboard();
                }
                catch (Exception ex) { ShowError(ex); }
            };
            actionCards.Add(b);
            return b;
        }

        actions.Controls.Add(ActionButton("▰", "Mở hồ sơ", "Mở vùng làm việc", UiTheme.Primary, async ctx =>
        {
            await OpenProfileAsync(ctx);
            if (ctx.Tab is not null) SelectTabPageSafely(ctx.Tab);
        }), 0, 0);
        actions.Controls.Add(ActionButton("◉", "Hiện Chrome", "Mở cửa sổ trình duyệt", Color.FromArgb(37, 99, 235), ViewChromeForProfileAsync), 1, 0);
        actions.Controls.Add(ActionButton("▶", "Bắt đầu chạy", "Khởi động hồ sơ đã chọn", UiTheme.Success, async ctx =>
        {
            await OpenProfileAsync(ctx);
            await StartWithNameGuardAsync(ctx, "start", TimeSpan.FromSeconds(30));
        }), 2, 0);
        actions.Controls.Add(ActionButton("Ⅱ", "Tạm dừng / Tiếp tục", "Đổi trạng thái vận hành", Color.FromArgb(217, 119, 6), async ctx =>
        {
            if (ctx.Worker is null || ctx.Worker.HasExited)
                await OpenProfileAsync(ctx);
            try { await RefreshStatusAsync(ctx); } catch { }
            var paused = string.Equals(GetLastConfirmedRuntimeState(ctx), RuntimeStatePaused, StringComparison.Ordinal);
            await SendCommandAsync(ctx, paused ? "resume" : "pause", TimeSpan.FromSeconds(8));
        }), 3, 0);
        actions.Controls.Add(ActionButton("■", "Dừng chạy", "Dừng hồ sơ đã chọn", UiTheme.Danger, async ctx =>
        {
            if (ctx.Worker is not null && !ctx.Worker.HasExited)
                await SendCommandAsync(ctx, "stop", TimeSpan.FromSeconds(8));
        }), 4, 0);
        var restartAction = ActionButton("↻", "Khởi động lại Chrome", "Tải lại trình duyệt", UiTheme.Purple, async ctx =>
        {
            if (ctx.Worker is null || ctx.Worker.HasExited)
                await OpenProfileAsync(ctx);
            try { await CloseChromeForProfileAsync(ctx); } catch { }
            await Task.Delay(500);
            await OpenChromeForProfileAsync(ctx);
        });
        restartAction.Margin = Padding.Empty;
        actions.Controls.Add(restartAction, 5, 0);

        void ArrangeActions()
        {
            var compact = actions.ClientSize.Width < 1120;
            var columns = compact ? 3 : 6;
            var rows = compact ? 2 : 1;
            if (actions.ColumnCount == columns && actions.RowCount == rows) return;

            actions.SuspendLayout();
            actions.ColumnStyles.Clear();
            actions.RowStyles.Clear();
            actions.ColumnCount = columns;
            actions.RowCount = rows;
            for (var column = 0; column < columns; column++)
                actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F / columns));
            for (var row = 0; row < rows; row++)
                actions.RowStyles.Add(new RowStyle(SizeType.Percent, 100F / rows));
            for (var index = 0; index < actionCards.Count; index++)
            {
                var column = index % columns;
                var row = index / columns;
                actions.SetCellPosition(actionCards[index], new TableLayoutPanelCellPosition(column, row));
                actionCards[index].Margin = new Padding(
                    0,
                    0,
                    column == columns - 1 ? 0 : 10,
                    row == rows - 1 ? 0 : 8);
            }
            actions.ResumeLayout(true);
        }
        actions.SizeChanged += (_, _) => ArrangeActions();
        card.HandleCreated += (_, _) => ArrangeActions();

        card.Controls.Add(actions);
        card.Controls.Add(heading);
        return card;
    }

    void UpdateDashboardProfileFilterToggleStyle()
    {
        var toggle = _dashboardShowAllProfilesToggle;
        if (toggle is null || toggle.IsDisposed) return;

        var showAll = toggle.Checked;
        toggle.Text = showAll ? "Tất cả hồ sơ" : "Hồ sơ đang mở";
        toggle.BackColor = showAll ? UiTheme.Primary : Color.White;
        toggle.ForeColor = showAll ? Color.White : Color.FromArgb(37, 77, 122);
        toggle.FlatAppearance.BorderColor = showAll ? Color.FromArgb(37, 99, 176) : Color.FromArgb(93, 128, 170);
    }

    static bool IsDashboardProfileOpen(ProfileContext ctx)
    {
        var tab = ctx.Tab;
        return tab is not null && !tab.IsDisposed && tab.Parent is not null;
    }

    ProfileContext? DashboardSelectedContext()
    {
        var grid = _dashboardGrid;
        if (grid is null || grid.SelectedRows.Count == 0) return null;
        return grid.SelectedRows[0].Tag as ProfileContext;
    }

    static void SetDashboardCellIfChanged(DataGridViewRow row, string columnName, string value)
    {
        var cell = row.Cells[columnName];
        var current = Convert.ToString(cell.Value) ?? "";
        if (!string.Equals(current, value, StringComparison.Ordinal))
            cell.Value = value;
    }

    static void ApplyDashboardRowStyle(DataGridViewRow row, string runState)
    {
        row.DefaultCellStyle.BackColor = Color.White;
        row.DefaultCellStyle.ForeColor = SystemColors.ControlText;
        if (runState == RuntimeStateRecovering)
        {
            row.DefaultCellStyle.BackColor = Color.FromArgb(255, 248, 218);
            row.DefaultCellStyle.ForeColor = Color.FromArgb(112, 82, 14);
        }
        else if (runState == RuntimeStateRunning)
        {
            row.DefaultCellStyle.BackColor = Color.FromArgb(237, 249, 240);
            row.DefaultCellStyle.ForeColor = Color.FromArgb(28, 98, 54);
        }
        else if (runState == RuntimeStatePaused)
        {
            row.DefaultCellStyle.BackColor = Color.FromArgb(255, 246, 229);
            row.DefaultCellStyle.ForeColor = Color.FromArgb(140, 83, 12);
        }
        else if (runState == RuntimeStateStopped)
        {
            row.DefaultCellStyle.ForeColor = Color.FromArgb(146, 54, 54);
        }

        // Chỉ nhấn thị giác, không thay đổi dữ liệu/logic trạng thái.
        var profileCell = row.Cells["Profile"];
        profileCell.Style.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        profileCell.Style.ForeColor = Color.FromArgb(25, 67, 112);
        profileCell.Style.SelectionForeColor = Color.FromArgb(18, 55, 95);

        var stateCell = row.Cells["RunState"];
        stateCell.Style.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        stateCell.Style.ForeColor = GetRuntimeStateColor(runState);
    }

    void RefreshDashboard(bool forceAccountRefresh = false)
    {
        if (IsDisposed || Disposing || _dashboardGrid is null || _dashboardGrid.IsDisposed) return;
        if (InvokeRequired)
        {
            try { BeginInvoke(new Action(() => RefreshDashboard(forceAccountRefresh))); } catch { }
            return;
        }

        // Khi tab Tổng quan không hiển thị, LastSnapshot vẫn được Worker refresh như cũ;
        // chỉ bỏ phần dựng/repaint DataGridView để giảm tải UI. Khi quay lại tab, Enter sẽ refresh ngay.
        if (!forceAccountRefresh && _dashboardTab is not null && !ReferenceEquals(_tabs.SelectedTab, _dashboardTab))
            return;

        if (forceAccountRefresh || DateTime.UtcNow - _dashboardAccountCacheUtc > TimeSpan.FromSeconds(15))
        {
            _dashboardAccountCache.Clear();
            _dashboardAccountCacheUtc = DateTime.UtcNow;
        }

        var selectedName = DashboardSelectedContext()?.Profile.Name;
        var allContexts = _contexts.Values.OrderByDescending(c => c.Profile.Name, NaturalProfileNameOrder).ToList();
        var showAllProfiles = _dashboardShowAllProfilesToggle?.Checked == true;
        var sourceContexts = showAllProfiles
            ? allContexts
            : allContexts.Where(IsDashboardProfileOpen).ToList();
        var query = (_dashboardSearchBox?.Text ?? "").Trim();
        var statusChoice = _dashboardStatusFilter?.SelectedIndex ?? 0;
        var contexts = sourceContexts.Where(ctx =>
        {
            if (query.Length > 0)
            {
                var account = GetDashboardAccount(ctx);
                if (!ctx.Profile.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                    && !account.Contains(query, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            var state = GetEffectiveRuntimeState(ctx);
            return statusChoice switch
            {
                1 => state == RuntimeStateRunning,
                2 => state == RuntimeStatePaused,
                3 => state == RuntimeStateRecovering,
                4 => state == RuntimeStateStopped,
                5 => state != RuntimeStateRunning && state != RuntimeStatePaused
                     && state != RuntimeStateRecovering && state != RuntimeStateStopped,
                _ => true
            };
        }).ToList();
        var running = 0;
        var paused = 0;
        var recovering = 0;
        var stopped = 0;
        var unknown = 0;
        long viewerTotal = 0;
        var viewerCount = 0;
        long totalRunSeconds = 0;

        foreach (var ctx in sourceContexts)
        {
            var state = GetEffectiveRuntimeState(ctx);
            if (state == RuntimeStateRecovering) recovering++;
            else if (state == RuntimeStateRunning) running++;
            else if (state == RuntimeStatePaused) paused++;
            else if (state == RuntimeStateStopped) stopped++;
            else unknown++;

            var snapshot = ctx.LastSnapshot;
            var viewer = snapshot?.Viewer ?? -1;
            if (viewer >= 0)
            {
                viewerTotal += viewer;
                viewerCount++;
            }
            if (snapshot?.TotalRunSeconds > 0) totalRunSeconds += snapshot.TotalRunSeconds;
        }

        _dashboardGrid.SuspendLayout();
        try
        {
            var needsRowRebuild = _dashboardGrid.Rows.Count != contexts.Count;
            if (!needsRowRebuild)
            {
                for (var i = 0; i < contexts.Count; i++)
                {
                    if (_dashboardGrid.Rows[i].Tag is not ProfileContext existing
                        || !existing.Profile.Name.Equals(contexts[i].Profile.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        needsRowRebuild = true;
                        break;
                    }
                }
            }

            if (needsRowRebuild)
            {
                _dashboardGrid.Rows.Clear();
                foreach (var ctx in contexts)
                {
                    var rowIndex = _dashboardGrid.Rows.Add(ctx.Profile.Name, "—", "—", "—", "—", "—", "—", "—", "");
                    _dashboardGrid.Rows[rowIndex].Tag = ctx;
                }
            }

            for (var i = 0; i < contexts.Count; i++)
            {
                var ctx = contexts[i];
                var row = _dashboardGrid.Rows[i];
                row.Tag = ctx;
                var snapshot = ctx.LastSnapshot;
                var runState = GetEffectiveRuntimeState(ctx);
                var detail = snapshot?.Detail ?? "";
                if (ctx.ConsecutiveStatusPollFailures > 0)
                {
                    var transient = $"Status poll tạm thời lỗi ({ctx.ConsecutiveStatusPollFailures}); giữ {GetLastConfirmedRuntimeState(ctx)}";
                    detail = string.IsNullOrWhiteSpace(detail) ? transient : $"{transient} | {detail}";
                }

                var account = GetDashboardAccount(ctx);
                var stepText = snapshot is null || snapshot.Step <= 0 ? "—" : snapshot.Step.ToString();
                var roundsText = snapshot is null ? "—" : snapshot.Rounds.ToString();
                var chrome = snapshot?.Chrome ?? "—";
                var runTime = FormatDashboardRuntime(snapshot?.TotalRunSeconds ?? -1);
                var ram = GetDashboardPrimaryRamMb(ctx, snapshot);
                var previousRunState = Convert.ToString(row.Cells["RunState"].Value) ?? "";

                SetDashboardCellIfChanged(row, "Profile", ctx.Profile.Name);
                SetDashboardCellIfChanged(row, "Account", string.IsNullOrWhiteSpace(account) ? "—" : account);
                SetDashboardCellIfChanged(row, "RunState", runState);
                SetDashboardCellIfChanged(row, "Chrome", chrome);
                SetDashboardCellIfChanged(row, "RunTime", runTime);
                SetDashboardCellIfChanged(row, "Step", stepText);
                SetDashboardCellIfChanged(row, "Rounds", roundsText);
                SetDashboardCellIfChanged(row, "Ram", ram < 0 ? "—" : $"{ram:N0} MB");
                SetDashboardCellIfChanged(row, "Detail", detail);

                if (needsRowRebuild || !string.Equals(previousRunState, runState, StringComparison.Ordinal))
                    ApplyDashboardRowStyle(row, runState);

                if (needsRowRebuild && !string.IsNullOrWhiteSpace(selectedName)
                    && ctx.Profile.Name.Equals(selectedName, StringComparison.OrdinalIgnoreCase))
                    row.Selected = true;
            }
        }
        finally { _dashboardGrid.ResumeLayout(); }

        if (_dashboardEmptyState is not null && !_dashboardEmptyState.IsDisposed)
        {
            _dashboardEmptyState.Visible = contexts.Count == 0;
            if (_dashboardEmptyState.Visible) _dashboardEmptyState.BringToFront();
        }
        _dashboardGrid.Visible = contexts.Count > 0;

        if (_dashboardOpenValue is not null) _dashboardOpenValue.Text = sourceContexts.Count.ToString();
        if (_dashboardRunningValue is not null) _dashboardRunningValue.Text = running.ToString();
        if (_dashboardPausedValue is not null) _dashboardPausedValue.Text = paused.ToString();
        if (_dashboardRecoveringValue is not null) _dashboardRecoveringValue.Text = recovering.ToString();
        if (_dashboardRuntimeValue is not null)
        {
            var total = TimeSpan.FromSeconds(totalRunSeconds);
            _dashboardRuntimeValue.Text = $"{(long)total.TotalHours:00}:{total.Minutes:00}:{total.Seconds:00}";
        }

        if (_dashboardSummary is not null && !_dashboardSummary.IsDisposed)
        {
            var avg = viewerCount == 0 ? "—" : FormatDashboardViewer((int)Math.Round((double)viewerTotal / viewerCount));
            var profileCountText = showAllProfiles
                ? $"Tổng số hồ sơ: {allContexts.Count}"
                : $"Đang mở: {sourceContexts.Count}/{allContexts.Count}";
            _dashboardSummary.Text = $"{profileCountText}  •  Đang chạy: {running}  •  Tạm dừng: {paused}  •  Khôi phục: {recovering}  •  Người xem TB: {avg}";
        }
    }

    string GetDashboardAccount(ProfileContext ctx)
    {
        if (_dashboardAccountCache.TryGetValue(ctx.Profile.Name, out var cached)) return cached;
        try
        {
            var dataRoot = _profileService.ResolveDataRoot(ctx.Profile);
            var username = _tiktokAuthService.Load(dataRoot).Username;
            _dashboardAccountCache[ctx.Profile.Name] = username;
            return username;
        }
        catch
        {
            _dashboardAccountCache[ctx.Profile.Name] = "";
            return "";
        }
    }

    static string FormatDashboardRuntime(long totalSeconds)
    {
        if (totalSeconds < 0) return "—";
        var value = TimeSpan.FromSeconds(totalSeconds);
        return $"{(long)value.TotalHours}h {value.Minutes:00}m";
    }

    static string FormatDashboardViewer(int value)
    {
        if (value >= 1_000_000) return $"{value / 1_000_000d:0.#}M";
        if (value >= 1_000) return $"{value / 1_000d:0.#}K";
        return value.ToString("N0");
    }

    static long GetDashboardPrimaryRamMb(ProfileContext ctx, WorkerSnapshot? snapshot)
    {
        long bytes = 0;
        try
        {
            if (ctx.Worker is not null && !ctx.Worker.HasExited)
                bytes += ctx.Worker.WorkingSet64;
        }
        catch { }

        // Chỉ cộng process Chrome top-level gắn với cửa sổ của profile.
        // Đây là chỉ số nhanh để phát hiện bất thường, không phải tổng mọi renderer Chrome.
        try
        {
            if (snapshot is { ChromeWindowHandle: > 0 })
            {
                GetWindowThreadProcessId(new IntPtr(snapshot.ChromeWindowHandle), out var pid);
                if (pid > 0)
                {
                    using var chrome = Process.GetProcessById((int)pid);
                    bytes += chrome.WorkingSet64;
                }
            }
        }
        catch { }
        return bytes <= 0 ? -1 : (long)Math.Round(bytes / 1024d / 1024d);
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    UpdateSettings LoadUpdateSettings()
    {
        try
        {
            if (!File.Exists(UpdateSettingsPath)) return new UpdateSettings();
            return JsonSerializer.Deserialize<UpdateSettings>(File.ReadAllText(UpdateSettingsPath), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new UpdateSettings();
        }
        catch (Exception ex)
        {
            _log.Warn("[UPDATE_SETTINGS_READ] " + ex.Message);
            return new UpdateSettings();
        }
    }

    void SaveUpdateSettings(UpdateSettings settings)
    {
        var temp = UpdateSettingsPath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
        File.Move(temp, UpdateSettingsPath, true);
    }

    void ShowUpdateSettingsDialog()
    {
        var settings = LoadUpdateSettings();
        using var form = new Form
        {
            Text = $"Trình quản lý phiên bản — V{ManagerDisplayVersion}",
            Width = 820,
            Height = 500,
            MinimumSize = new Size(720, 450),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MinimizeBox = false,
            MaximizeBox = false,
            ShowInTaskbar = false,
            AutoScaleMode = AutoScaleMode.Dpi
        };
        ModernDialog.Apply(form);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 10,
            Padding = new Padding(18)
        };
        for (var i = 0; i < 8; i++) root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var intro = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(760, 0),
            Text = "version.json dùng để kiểm tra bản mới nhất. versions.json chứa lịch sử các bản để nâng/hạ phiên bản. Nếu URL versions.json để trống, Manager sẽ tự suy ra cùng thư mục với version.json.",
            Margin = new Padding(0, 0, 0, 10)
        };
        ModernDialog.StylePrimaryLabel(intro);

        var latestLabel = new Label { Text = "URL version.json (bản mới nhất)", AutoSize = true };
        var latestUrl = new TextBox { Dock = DockStyle.Top, Text = settings.ManifestUrl };
        ModernDialog.StyleTextInput(latestUrl);

        var historyLabel = new Label { Text = "URL versions.json (lịch sử phiên bản)", AutoSize = true, Margin = new Padding(0, 10, 0, 0) };
        var historyUrl = new TextBox { Dock = DockStyle.Top, Text = settings.VersionsManifestUrl };
        ModernDialog.StyleTextInput(historyUrl);

        var options = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true, Margin = new Padding(0, 12, 0, 0) };
        var channelLabel = new Label { Text = "Kênh:", AutoSize = true, Margin = new Padding(0, 8, 6, 0) };
        var channel = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 130 };
        channel.Items.AddRange(new object[] { "stable", "beta" });
        var normalizedChannel = settings.Channel.Equals("test", StringComparison.OrdinalIgnoreCase) ? "beta" : settings.Channel;
        channel.SelectedItem = normalizedChannel.Equals("beta", StringComparison.OrdinalIgnoreCase) ? "beta" : "stable";
        ModernDialog.StyleSelectionInput(channel);
        var autoCheck = new CheckBox { Text = "Tự kiểm tra khi mở Manager", Checked = settings.AutoCheck, AutoSize = true, Margin = new Padding(18, 7, 0, 0) };
        options.Controls.Add(channelLabel);
        options.Controls.Add(channel);
        options.Controls.Add(autoCheck);

        var hold = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(760, 0),
            ForeColor = Color.DimGray,
            Text = string.IsNullOrWhiteSpace(settings.PinnedVersion)
                ? "Giữ phiên bản: đang tắt. Khi hạ phiên bản, Manager sẽ tự giữ ở bản đã chọn để không nhắc nâng lại ngay."
                : $"Giữ phiên bản hiện tại: V{settings.PinnedVersion}. Có thể bỏ giữ ngay trên trang Tổng quan.",
            Margin = new Padding(0, 12, 0, 0)
        };

        var hint = new Label
        {
            AutoSize = true,
            ForeColor = Color.DimGray,
            Text = "Mỗi mục trong versions.json: version + setupUrl + sha256 + notes + status (stable/beta/withdrawn) + releaseDate + allowInstall. Với bản lịch sử, setupUrl phải trỏ thẳng tag releases/download/vX.Y.Z/..., không dùng releases/latest/download/...",
            MaximumSize = new Size(760, 0),
            Margin = new Padding(0, 10, 0, 8)
        };

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
        var cancel = new Button { Text = "Hủy", DialogResult = DialogResult.Cancel, AutoSize = true };
        var save = new Button { Text = "Lưu", DialogResult = DialogResult.OK, AutoSize = true };
        ModernDialog.StyleSecondaryButton(cancel);
        ModernDialog.StylePrimaryButton(save);
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(save);

        root.Controls.Add(intro, 0, 0);
        root.Controls.Add(latestLabel, 0, 1);
        root.Controls.Add(latestUrl, 0, 2);
        root.Controls.Add(historyLabel, 0, 3);
        root.Controls.Add(historyUrl, 0, 4);
        root.Controls.Add(options, 0, 5);
        root.Controls.Add(hold, 0, 6);
        root.Controls.Add(hint, 0, 7);
        root.Controls.Add(new Panel { Dock = DockStyle.Fill }, 0, 8);
        root.Controls.Add(buttons, 0, 9);
        form.Controls.Add(root);
        form.AcceptButton = save;
        form.CancelButton = cancel;
        form.Shown += (_, _) => ModernDialog.FitToWorkingArea(form);

        if (form.ShowDialog(this) != DialogResult.OK) return;
        settings.ManifestUrl = latestUrl.Text.Trim();
        settings.VersionsManifestUrl = historyUrl.Text.Trim();
        settings.Channel = channel.SelectedItem?.ToString() ?? "stable";
        settings.AutoCheck = autoCheck.Checked;
        try
        {
            SaveUpdateSettings(settings);
            _latestUpdate = null;
            _availableVersions.Clear();
            RefreshVersionSelector();
            RefreshUpdatePanel(settings);
            RefreshVersionMenu();
        }
        catch (Exception ex) { ShowError(ex); }
    }

    string GetVersionsManifestUrl(UpdateSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.VersionsManifestUrl))
            return settings.VersionsManifestUrl.Trim();
        if (!Uri.TryCreate(settings.ManifestUrl, UriKind.Absolute, out var latestUri))
            return "";
        try
        {
            var builder = new UriBuilder(latestUri);
            var path = builder.Path;
            var slash = path.LastIndexOf('/');
            builder.Path = slash >= 0 ? path[..(slash + 1)] + "versions.json" : "/versions.json";
            return builder.Uri.ToString();
        }
        catch { return ""; }
    }

    void RefreshUpdatePanel(UpdateSettings? settings = null)
    {
        if (_dashboardUpdateStatus is null || _dashboardUpdateStatus.IsDisposed) return;
        settings ??= LoadUpdateSettings();

        _updatingHoldToggle = true;
        try
        {
            if (_dashboardHoldVersionToggle is not null && !_dashboardHoldVersionToggle.IsDisposed)
            {
                _dashboardHoldVersionToggle.Text = $"Giữ ở V{ManagerDisplayVersion}";
                _dashboardHoldVersionToggle.Checked = VersionEquals(settings.PinnedVersion, ManagerDisplayVersion);
            }
        }
        finally { _updatingHoldToggle = false; }

        if (string.IsNullOrWhiteSpace(settings.ManifestUrl))
        {
            _dashboardUpdateStatus.Text = "Chưa cấu hình nguồn cập nhật.";
            _dashboardUpdateStatus.ForeColor = Color.DimGray;
            RefreshSelectedVersionAction();
            return;
        }

        var current = FindAvailableVersion(ManagerDisplayVersion);
        if (current is not null && current.EffectiveStatus.Equals("withdrawn", StringComparison.OrdinalIgnoreCase))
        {
            var rollback = FindRecommendedStableRollback();
            _dashboardUpdateStatus.Text = rollback is null
                ? "Bản hiện tại đã thu hồi; chưa có bản ổn định để quay về."
                : $"Bản hiện tại đã thu hồi; đề xuất quay về V{rollback.Version}.";
            _dashboardUpdateStatus.ForeColor = Color.Firebrick;
        }
        else if (VersionEquals(settings.PinnedVersion, ManagerDisplayVersion))
        {
            _dashboardUpdateStatus.Text = "Đang giữ phiên bản này; đã tắt nhắc nâng cấp.";
            _dashboardUpdateStatus.ForeColor = Color.FromArgb(111, 78, 15);
        }
        else if (FindBestUpdateCandidate(settings) is UpdateManifest candidate)
        {
            _dashboardUpdateStatus.Text = $"Có bản V{candidate.Version} ({DisplayVersionStatus(candidate)}).";
            _dashboardUpdateStatus.ForeColor = Color.DarkGreen;
        }
        else if (_availableVersions.Count > 0)
        {
            _dashboardUpdateStatus.Text = $"Đã tải {_availableVersions.Count} phiên bản trong lịch sử.";
            _dashboardUpdateStatus.ForeColor = Color.FromArgb(55, 76, 103);
        }
        else
        {
            _dashboardUpdateStatus.Text = $"Nguồn cập nhật đã cấu hình ({settings.Channel}).";
            _dashboardUpdateStatus.ForeColor = Color.FromArgb(55, 76, 103);
        }
        RefreshSelectedVersionAction();
    }

    void OnHoldVersionToggleChanged()
    {
        if (_updatingHoldToggle || _dashboardHoldVersionToggle is null) return;
        try
        {
            var settings = LoadUpdateSettings();
            if (_dashboardHoldVersionToggle.Checked)
            {
                settings.PinnedVersion = ManagerDisplayVersion;
                SaveUpdateSettings(settings);
                _log.Info($"[VERSION_PIN] version={ManagerDisplayVersion}");
            }
            else if (VersionEquals(settings.PinnedVersion, ManagerDisplayVersion))
            {
                settings.PinnedVersion = "";
                SaveUpdateSettings(settings);
                _log.Info($"[VERSION_UNPIN] version={ManagerDisplayVersion}");
            }
            RefreshUpdatePanel(settings);
        }
        catch (Exception ex)
        {
            _log.Warn("[VERSION_PIN_FAILED] " + ex.Message);
            ModernDialog.ShowMessage(this, "Không lưu được chế độ giữ phiên bản.\n\n" + ex.Message, "Trình quản lý phiên bản", MessageBoxIcon.Warning);
        }
    }

    async Task CheckForUpdatesAsync(bool showWhenCurrent)
    {
        if (_updateCheckInProgress) return;
        var settings = LoadUpdateSettings();
        if (string.IsNullOrWhiteSpace(settings.ManifestUrl))
        {
            ModernDialog.ShowMessage(this, "Chưa cấu hình URL version.json. Hãy bấm ‘Cấu hình cập nhật’ trước.", "Trình quản lý phiên bản", MessageBoxIcon.Information);
            return;
        }

        if (!TryHttpUri(settings.ManifestUrl, out var latestUri))
        {
            ModernDialog.ShowMessage(this, "URL version.json không hợp lệ. URL phải bắt đầu bằng https:// hoặc http://.", "Trình quản lý phiên bản", MessageBoxIcon.Warning);
            return;
        }

        _updateCheckInProgress = true;
        ModernProgressDialog? progressDialog = null;
        try
        {
            // Auto-check khi mở Manager chỉ cập nhật trạng thái inline. Không mở/khóa
            // cửa sổ bằng progress dialog vì mạng chậm có thể khiến app trông như bị đơ.
            if (showWhenCurrent)
            {
                progressDialog = ModernDialog.ShowProgress(
                    this,
                    "Kiểm tra cập nhật",
                    "Đang kết nối đến nguồn phiên bản mới nhất...");
                progressDialog.SetProgress(15, "Đang kết nối đến máy chủ...", 0);
            }
            if (_dashboardUpdateStatus is not null)
            {
                _dashboardUpdateStatus.Text = "Đang tải thông tin phiên bản...";
                _dashboardUpdateStatus.ForeColor = Color.DarkOrange;
            }

            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(18) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd($"ToolTikTokManager/{AppVersionInfo.Current}");

            var latestJson = await client.GetStringAsync(latestUri);
            progressDialog?.SetProgress(45, "Đang kiểm tra phiên bản mới nhất...", 1);
            var latest = JsonSerializer.Deserialize<UpdateManifest>(latestJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (latest is null || string.IsNullOrWhiteSpace(latest.Version) || string.IsNullOrWhiteSpace(latest.SetupUrl))
                throw new InvalidDataException("version.json thiếu version hoặc setupUrl.");

            var history = new List<UpdateManifest>();
            var historyUrl = GetVersionsManifestUrl(settings);
            string? historyWarning = null;
            if (TryHttpUri(historyUrl, out var historyUri))
            {
                try
                {
                    var historyJson = await client.GetStringAsync(historyUri);
                    history.AddRange(ParseVersionCatalog(historyJson));
                }
                catch (Exception ex)
                {
                    historyWarning = ex.Message;
                    _log.Warn($"[VERSION_HISTORY_FAILED] url={historyUrl} message={ex.Message}");
                }
            }
            else if (!string.IsNullOrWhiteSpace(historyUrl))
            {
                historyWarning = "URL versions.json không hợp lệ.";
            }
            progressDialog?.SetProgress(75, "Đang tổng hợp thông tin cập nhật...", 2);

            MergeAvailableVersions(latest, history);
            _latestUpdate = FindAvailableVersion(latest.Version) ?? latest;

            var current = FindAvailableVersion(ManagerDisplayVersion);
            var rollback = current is not null && current.EffectiveStatus.Equals("withdrawn", StringComparison.OrdinalIgnoreCase)
                ? FindRecommendedStableRollback()
                : null;
            var updateCandidate = FindBestUpdateCandidate(settings);
            var preferredVersion = rollback?.Version
                ?? (VersionEquals(settings.PinnedVersion, ManagerDisplayVersion)
                    ? ManagerDisplayVersion
                    : updateCandidate?.Version ?? ManagerDisplayVersion);
            RefreshVersionSelector(preferredVersion);
            RefreshUpdatePanel(settings);
            progressDialog?.SetProgress(100, "Đã hoàn tất kiểm tra phiên bản.", 4);
            progressDialog?.CloseSafely();

            if (current is not null && current.EffectiveStatus.Equals("withdrawn", StringComparison.OrdinalIgnoreCase))
            {
                var detail = rollback is null
                    ? "Chưa có bản stable phù hợp trong versions.json."
                    : $"Nên chọn V{rollback.Version} trong danh sách và bấm ‘Hạ xuống’.";
                ModernDialog.ShowMessage(this,
                    $"V{ManagerDisplayVersion} đã được đánh dấu ĐÃ THU HỒI.\n\n{detail}",
                    "Cảnh báo phiên bản", MessageBoxIcon.Warning);
                return;
            }

            if (VersionEquals(settings.PinnedVersion, ManagerDisplayVersion))
            {
                if (showWhenCurrent)
                {
                    var availableCandidate = FindBestUpdateCandidate(settings);
                    var latestText = availableCandidate is not null
                        ? $" Bản có thể cập nhật hiện là V{availableCandidate.Version}, nhưng Manager đang giữ ở V{ManagerDisplayVersion}."
                        : "";
                    ModernDialog.ShowMessage(this,
                        $"Đang giữ ở V{ManagerDisplayVersion}, nên Manager sẽ không tự nhắc nâng phiên bản.{latestText}\n\nBỏ chọn ‘Giữ ở V{ManagerDisplayVersion}’ nếu muốn nhận nhắc cập nhật bình thường.",
                        "Trình quản lý phiên bản", MessageBoxIcon.Information);
                }
                return;
            }

            var bestCandidate = FindBestUpdateCandidate(settings);
            if (bestCandidate is not null)
            {
                if (showWhenCurrent)
                {
                    var notes = string.IsNullOrWhiteSpace(bestCandidate.Notes) ? "Không có ghi chú phiên bản." : bestCandidate.Notes.Trim();
                    var suffix = string.IsNullOrWhiteSpace(historyWarning) ? "" : $"\n\nLưu ý: không tải được đầy đủ versions.json: {historyWarning}";
                    ModernDialog.ShowMessage(this,
                        $"Có bản mới V{bestCandidate.Version} ({DisplayVersionStatus(bestCandidate)}).\n\n{notes}\n\nBạn có thể chọn phiên bản cần cài trực tiếp trên Tổng quan.{suffix}",
                        "Có bản cập nhật", MessageBoxIcon.Information);
                }
            }
            else if (showWhenCurrent)
            {
                var suffix = string.IsNullOrWhiteSpace(historyWarning) ? "" : $"\n\nKhông tải được đầy đủ versions.json: {historyWarning}";
                ModernDialog.ShowMessage(this,
                    $"Bạn đang dùng V{ManagerDisplayVersion}. Không có bản mới hơn phù hợp và được phép cài.{suffix}",
                    "Trình quản lý phiên bản", MessageBoxIcon.Information);
            }
        }
        catch (Exception ex)
        {
            progressDialog?.CloseSafely();
            _latestUpdate = null;
            if (_dashboardUpdateStatus is not null)
            {
                _dashboardUpdateStatus.Text = "Kiểm tra phiên bản thất bại: " + ex.Message;
                _dashboardUpdateStatus.ForeColor = Color.Firebrick;
            }
            _log.Warn("[UPDATE_CHECK_FAILED] " + ex);
            if (showWhenCurrent)
                ModernDialog.ShowMessage(this, "Không kiểm tra được phiên bản.\n\n" + ex.Message, "Trình quản lý phiên bản", MessageBoxIcon.Warning);
        }
        finally
        {
            progressDialog?.CloseSafely();
            _updateCheckInProgress = false;
        }
    }

    static IReadOnlyList<UpdateManifest> ParseVersionCatalog(string json)
    {
        using var document = JsonDocument.Parse(json);
        JsonElement versionsElement;
        if (document.RootElement.ValueKind == JsonValueKind.Array)
        {
            versionsElement = document.RootElement;
        }
        else if (document.RootElement.ValueKind == JsonValueKind.Object
                 && (document.RootElement.TryGetProperty("versions", out versionsElement)
                     || document.RootElement.TryGetProperty("releases", out versionsElement)))
        {
        }
        else
        {
            throw new InvalidDataException("versions.json phải là một mảng hoặc object có thuộc tính versions hoặc releases.");
        }

        if (versionsElement.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("versions trong versions.json phải là mảng.");

        var result = new List<UpdateManifest>();
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        foreach (var item in versionsElement.EnumerateArray())
        {
            try
            {
                var manifest = item.Deserialize<UpdateManifest>(options);
                if (manifest is null || string.IsNullOrWhiteSpace(manifest.Version)) continue;
                result.Add(manifest);
            }
            catch { }
        }
        if (result.Count == 0)
            throw new InvalidDataException("versions.json không có phiên bản hợp lệ.");
        return result;
    }

    void MergeAvailableVersions(UpdateManifest latest, IEnumerable<UpdateManifest> history)
    {
        var map = new Dictionary<string, UpdateManifest>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in history)
        {
            var key = NormalizeVersion(item.Version);
            if (key.Length == 0) continue;
            map[key] = item;
        }

        var latestKey = NormalizeVersion(latest.Version);
        if (map.TryGetValue(latestKey, out var fromHistory))
        {
            // versions.json quyết định status/releaseDate/allowInstall; version.json có thể bổ sung URL/hash/notes mới nhất.
            if (string.IsNullOrWhiteSpace(fromHistory.SetupUrl)) fromHistory.SetupUrl = latest.SetupUrl;
            if (string.IsNullOrWhiteSpace(fromHistory.Sha256)) fromHistory.Sha256 = latest.Sha256;
            if (string.IsNullOrWhiteSpace(fromHistory.Notes)) fromHistory.Notes = latest.Notes;
            if (string.IsNullOrWhiteSpace(fromHistory.Channel)) fromHistory.Channel = latest.Channel;
        }
        else
        {
            map[latestKey] = latest;
        }

        _availableVersions.Clear();
        _availableVersions.AddRange(map.Values);
        _availableVersions.Sort((a, b) => CompareVersions(b.Version, a.Version));
    }

    void RefreshVersionSelector(string? preferredVersion = null)
    {
        var selector = _dashboardVersionSelector;
        if (selector is null || selector.IsDisposed) return;

        var oldSelection = preferredVersion;
        if (string.IsNullOrWhiteSpace(oldSelection) && selector.SelectedItem is VersionChoice oldChoice)
            oldSelection = oldChoice.Manifest.Version;

        var versions = _availableVersions.ToList();
        if (!versions.Any(v => VersionEquals(v.Version, ManagerDisplayVersion)))
        {
            versions.Add(new UpdateManifest
            {
                Version = ManagerDisplayVersion,
                Notes = "Phiên bản đang chạy (chưa có trong versions.json).",
                Status = "stable",
                AllowInstall = false
            });
        }
        versions.Sort((a, b) => CompareVersions(b.Version, a.Version));

        selector.BeginUpdate();
        try
        {
            selector.Items.Clear();
            foreach (var item in versions)
            {
                selector.Items.Add(new VersionChoice
                {
                    Manifest = item,
                    IsLatest = _latestUpdate is not null && VersionEquals(item.Version, _latestUpdate.Version),
                    IsCurrent = VersionEquals(item.Version, ManagerDisplayVersion)
                });
            }

            var target = string.IsNullOrWhiteSpace(oldSelection) ? ManagerDisplayVersion : oldSelection;
            for (var i = 0; i < selector.Items.Count; i++)
            {
                if (selector.Items[i] is VersionChoice choice && VersionEquals(choice.Manifest.Version, target))
                {
                    selector.SelectedIndex = i;
                    break;
                }
            }
            if (selector.SelectedIndex < 0 && selector.Items.Count > 0)
                selector.SelectedIndex = 0;
        }
        finally { selector.EndUpdate(); }
        RefreshSelectedVersionAction();
        RefreshVersionMenu();
    }

    void RefreshVersionMenu()
    {
        var selector = _dashboardVersionSelector;
        var menu = _dashboardVersionMenu;
        var menuButton = _dashboardVersionMenuButton;
        if (selector is null || menu is null || menuButton is null || menuButton.IsDisposed) return;

        menu.Items.Clear();
        foreach (var choice in selector.Items.OfType<VersionChoice>())
        {
            var manifest = choice.Manifest;
            var compare = CompareVersions(manifest.Version, ManagerDisplayVersion);
            var action = compare switch
            {
                < 0 => "Hạ xuống",
                > 0 => "Cài đặt",
                _ => "Đang sử dụng"
            };
            var item = new ToolStripMenuItem($"{action} V{NormalizeVersion(manifest.Version)}")
            {
                AutoSize = false,
                Width = 270,
                Height = 36,
                Enabled = compare != 0
                    && manifest.IsInstallAllowed
                    && !manifest.EffectiveStatus.Equals("withdrawn", StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(manifest.SetupUrl)
                    && NormalizeSha256(manifest.Sha256).Length == 64
            };
            item.Click += async (_, _) =>
            {
                selector.SelectedItem = choice;
                if (_dashboardUpdateButton?.Enabled == true)
                    await DownloadAndInstallSelectedVersionAsync();
            };
            menu.Items.Add(item);
        }

        if (menu.Items.Count == 0)
            menu.Items.Add(new ToolStripMenuItem("Chưa có dữ liệu phiên bản") { Enabled = false });

        menu.Items.Add(new ToolStripSeparator());
        var keepVersion = new ToolStripMenuItem($"Giữ ở V{ManagerDisplayVersion}")
        {
            AutoSize = false,
            Width = 270,
            Height = 36,
            Checked = _dashboardHoldVersionToggle?.Checked == true,
            CheckOnClick = false
        };
        keepVersion.Click += (_, _) =>
        {
            if (_dashboardHoldVersionToggle is not null)
                _dashboardHoldVersionToggle.Checked = !_dashboardHoldVersionToggle.Checked;
        };
        menu.Items.Add(keepVersion);

        menuButton.Enabled = true;
        if (_dashboardLatestBadge is not null)
            _dashboardLatestBadge.Visible = _latestUpdate is not null
                && VersionEquals(_latestUpdate.Version, ManagerDisplayVersion)
                && !_latestUpdate.EffectiveStatus.Equals("withdrawn", StringComparison.OrdinalIgnoreCase);
    }

    UpdateManifest? SelectedVersionManifest()
        => _dashboardVersionSelector?.SelectedItem is VersionChoice choice ? choice.Manifest : null;

    void RefreshSelectedVersionAction()
    {
        var button = _dashboardUpdateButton;
        if (button is null || button.IsDisposed) return;
        var target = SelectedVersionManifest();
        if (target is null)
        {
            button.Text = "Áp dụng";
            button.Enabled = false;
            return;
        }

        var compare = CompareVersions(target.Version, ManagerDisplayVersion);
        if (compare == 0)
        {
            button.Text = "Đang sử dụng";
            button.Enabled = false;
            return;
        }
        if (!target.IsInstallAllowed || target.EffectiveStatus.Equals("withdrawn", StringComparison.OrdinalIgnoreCase))
        {
            button.Text = "Bản đã thu hồi";
            button.Enabled = false;
            return;
        }
        if (string.IsNullOrWhiteSpace(target.SetupUrl))
        {
            button.Text = "Thiếu link Setup";
            button.Enabled = false;
            return;
        }
        if (NormalizeSha256(target.Sha256).Length != 64)
        {
            button.Text = "Thiếu SHA-256";
            button.Enabled = false;
            return;
        }

        button.Text = compare < 0
            ? $"Hạ V{NormalizeVersion(target.Version)}"
            : $"Cài V{NormalizeVersion(target.Version)}";
        button.Enabled = true;
    }

    async Task DownloadAndInstallSelectedVersionAsync()
    {
        if (_updateDownloadInProgress) return;
        var manifest = SelectedVersionManifest();
        if (manifest is null)
        {
            ModernDialog.ShowMessage(this, "Hãy chọn một phiên bản trước.", "Trình quản lý phiên bản", MessageBoxIcon.Information);
            return;
        }

        var compare = CompareVersions(manifest.Version, ManagerDisplayVersion);
        if (compare == 0) return;
        var isDowngrade = compare < 0;

        if (!manifest.IsInstallAllowed || manifest.EffectiveStatus.Equals("withdrawn", StringComparison.OrdinalIgnoreCase))
        {
            ModernDialog.ShowMessage(this, $"V{manifest.Version} đã bị thu hồi hoặc không cho phép cài.", "Trình quản lý phiên bản", MessageBoxIcon.Warning);
            return;
        }
        if (!TryHttpUri(manifest.SetupUrl, out var setupUri))
        {
            ModernDialog.ShowMessage(this, "setupUrl của phiên bản đã chọn không hợp lệ.", "Trình quản lý phiên bản", MessageBoxIcon.Warning);
            return;
        }
        if (isDowngrade && manifest.SetupUrl.Contains("/releases/latest/download/", StringComparison.OrdinalIgnoreCase))
        {
            ModernDialog.ShowMessage(this,
                "Không thể hạ phiên bản bằng link releases/latest/download/.\n\nHãy sửa setupUrl trong versions.json thành link tag cố định, ví dụ releases/download/v13.6.2/ToolTikTok_V13.6.2_Setup.exe.",
                "Link phiên bản cũ không an toàn", MessageBoxIcon.Warning);
            return;
        }

        var expectedHash = NormalizeSha256(manifest.Sha256);
        if (expectedHash.Length != 64)
        {
            ModernDialog.ShowMessage(this, "Phiên bản này thiếu SHA-256 hợp lệ (phải đủ 64 ký tự hex). Tool sẽ không cài để tránh tải nhầm file.", "Thiếu SHA-256", MessageBoxIcon.Warning);
            return;
        }

        var action = isDowngrade ? "HẠ" : "NÂNG";
        var notes = CompactVersionNotes(manifest.Notes, 240);
        var confirmText = $"{action} Tool TikTok từ V{ManagerDisplayVersion} xuống/lên V{NormalizeVersion(manifest.Version)}?\n\n"
            + (notes.Length > 0 ? notes + "\n\n" : "")
            + "Manager sẽ tải Setup, bắt buộc kiểm tra SHA-256, dừng các Worker rồi mở bộ cài."
            + (isDowngrade
                ? "\n\nTrước khi hạ phiên bản, Tool sẽ backup profiles.json + cấu hình + dữ liệu profile cần thiết. Sau khi hạ, Tool tự bật ‘Giữ phiên bản’ để không nhắc nâng lại ngay."
                : "");
        if (ModernDialog.ShowConfirm(this, confirmText, isDowngrade ? "Xác nhận hạ phiên bản" : "Xác nhận cập nhật") != DialogResult.Yes) return;

        _updateDownloadInProgress = true;
        var settings = LoadUpdateSettings();
        var oldPinned = settings.PinnedVersion;
        var pinChanged = false;
        var setupLaunched = false;
        try
        {
            var updateDir = Path.Combine(Path.GetTempPath(), "ToolTikTokUpdates");
            Directory.CreateDirectory(updateDir);
            var destination = Path.Combine(updateDir, $"ToolTikTok_V{SanitizeVersionForFile(manifest.Version)}_Setup.exe");
            var temp = destination + ".download";
            if (File.Exists(temp)) File.Delete(temp);

            using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd($"ToolTikTokManager/{AppVersionInfo.Current}");
            using var response = await client.GetAsync(setupUri, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            var total = response.Content.Headers.ContentLength;
            await using (var input = await response.Content.ReadAsStreamAsync())
            await using (var output = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            {
                var buffer = new byte[81920];
                long written = 0;
                while (true)
                {
                    var read = await input.ReadAsync(buffer);
                    if (read <= 0) break;
                    await output.WriteAsync(buffer.AsMemory(0, read));
                    written += read;
                    if (_dashboardUpdateStatus is not null)
                    {
                        _dashboardUpdateStatus.Text = total is > 0
                            ? $"Đang tải V{NormalizeVersion(manifest.Version)}: {written * 100 / total.Value}%"
                            : $"Đang tải V{NormalizeVersion(manifest.Version)}: {written / 1024 / 1024} MB";
                    }
                }
            }

            var actual = ComputeSha256(temp);
            if (!actual.Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"SHA-256 không khớp. Expected={expectedHash}; Actual={actual}");

            File.Move(temp, destination, true);
            await StopWorkersForVersionInstallAsync();

            string backupPath = "";
            if (isDowngrade)
            {
                if (_dashboardUpdateStatus is not null) _dashboardUpdateStatus.Text = "Đang backup dữ liệu trước khi hạ phiên bản...";
                backupPath = CreateDowngradeBackup(manifest.Version);
                _log.Info($"[VERSION_BACKUP_OK] from={ManagerDisplayVersion} to={manifest.Version} path={backupPath}");
                settings.PinnedVersion = NormalizeVersion(manifest.Version);
                SaveUpdateSettings(settings);
                pinChanged = true;
            }
            else if (!string.IsNullOrWhiteSpace(settings.PinnedVersion))
            {
                // Người dùng đã chủ động chọn một bản khác, vì vậy bỏ pin cũ.
                settings.PinnedVersion = "";
                SaveUpdateSettings(settings);
                pinChanged = true;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = destination,
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(destination) ?? updateDir
            });
            setupLaunched = true;
            _log.Info($"[VERSION_SETUP_LAUNCHED] from={ManagerDisplayVersion} to={manifest.Version} downgrade={isDowngrade} path={destination}");
            BeginInvoke(new Action(Close));
        }
        catch (Exception ex)
        {
            if (pinChanged && !setupLaunched)
            {
                try
                {
                    settings.PinnedVersion = oldPinned;
                    SaveUpdateSettings(settings);
                }
                catch { }
            }
            _log.Error("[VERSION_INSTALL_FAILED] " + ex);
            ModernDialog.ShowMessage(this, "Không tải/cài được phiên bản đã chọn.\n\n" + ex.Message, "Trình quản lý phiên bản", MessageBoxIcon.Warning);
            RefreshUpdatePanel();
        }
        finally { _updateDownloadInProgress = false; }
    }

    async Task StopWorkersForVersionInstallAsync()
    {
        if (_dashboardUpdateStatus is not null) _dashboardUpdateStatus.Text = "Đã xác thực Setup. Đang dừng Worker...";
        foreach (var ctx in _contexts.Values.Where(c => c.Worker is not null && !c.Worker.HasExited).ToList())
        {
            try { await SendCommandAsync(ctx, "stop", TimeSpan.FromSeconds(5)); } catch { }
            try { await SendPipeAsync(ctx.Profile.Name, "shutdown", TimeSpan.FromSeconds(5)); } catch { }
            try
            {
                if (ctx.Worker is not null)
                    await WaitForProcessExitAsync(ctx.Worker, TimeSpan.FromSeconds(4));
            }
            catch { }
        }
    }

    string CreateDowngradeBackup(string targetVersion)
    {
        var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var backupRoot = Path.Combine(_baseDir, "version_backups", $"{stamp}_V{SanitizeVersionForFile(ManagerDisplayVersion)}_to_V{SanitizeVersionForFile(targetVersion)}");
        Directory.CreateDirectory(backupRoot);

        var files = new[]
        {
            "profiles.json",
            UpdateSettingsFileName,
            "tiktok_identity_tool.json",
            "tiktok_message_reply_tool.json"
        };
        foreach (var name in files)
        {
            var source = Path.Combine(_baseDir, name);
            if (File.Exists(source)) File.Copy(source, Path.Combine(backupRoot, name), true);
        }

        var directories = new[] { "profiles", "manager_default_config" };
        foreach (var name in directories)
        {
            var source = Path.Combine(_baseDir, name);
            if (Directory.Exists(source)) CopyDirectoryForVersionBackup(source, Path.Combine(backupRoot, name));
        }

        var info = new
        {
            createdAt = DateTimeOffset.Now,
            fromVersion = ManagerDisplayVersion,
            toVersion = NormalizeVersion(targetVersion),
            includes = new[] { "profiles.json", "manager_update.json", "tiktok_identity_tool.json", "tiktok_message_reply_tool.json", "profiles/", "manager_default_config/" },
            excludes = new[] { "TikTokProfiles/ (Chrome user-data lớn, không bị bộ cài ghi đè)" }
        };
        File.WriteAllText(Path.Combine(backupRoot, "backup_info.json"), JsonSerializer.Serialize(info, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
        return backupRoot;
    }

    static void CopyDirectoryForVersionBackup(string sourceDir, string destinationDir)
    {
        Directory.CreateDirectory(destinationDir);
        foreach (var file in Directory.EnumerateFiles(sourceDir))
            File.Copy(file, Path.Combine(destinationDir, Path.GetFileName(file)), true);
        foreach (var dir in Directory.EnumerateDirectories(sourceDir))
            CopyDirectoryForVersionBackup(dir, Path.Combine(destinationDir, Path.GetFileName(dir)));
    }

    UpdateManifest? FindAvailableVersion(string version)
        => _availableVersions.FirstOrDefault(v => VersionEquals(v.Version, version));

    UpdateManifest? FindBestUpdateCandidate(UpdateSettings settings)
    {
        var allowBeta = settings.Channel.Equals("beta", StringComparison.OrdinalIgnoreCase)
                        || settings.Channel.Equals("test", StringComparison.OrdinalIgnoreCase);
        return _availableVersions
            .Where(v => v.IsInstallAllowed
                        && !v.EffectiveStatus.Equals("withdrawn", StringComparison.OrdinalIgnoreCase)
                        && CompareVersions(v.Version, ManagerDisplayVersion) > 0
                        && (allowBeta || v.EffectiveStatus.Equals("stable", StringComparison.OrdinalIgnoreCase))
                        && !string.IsNullOrWhiteSpace(v.SetupUrl)
                        && NormalizeSha256(v.Sha256).Length == 64)
            .OrderByDescending(v => ParseVersionForCompare(v.Version))
            .FirstOrDefault();
    }

    UpdateManifest? FindRecommendedStableRollback()
    {
        return _availableVersions
            .Where(v => v.IsInstallAllowed
                        && v.EffectiveStatus.Equals("stable", StringComparison.OrdinalIgnoreCase)
                        && CompareVersions(v.Version, ManagerDisplayVersion) < 0
                        && !string.IsNullOrWhiteSpace(v.SetupUrl)
                        && NormalizeSha256(v.Sha256).Length == 64)
            .OrderByDescending(v => ParseVersionForCompare(v.Version))
            .FirstOrDefault();
    }

    static string DisplayVersionStatus(UpdateManifest manifest)
        => manifest.EffectiveStatus switch
        {
            "withdrawn" => "Đã thu hồi",
            "beta" => "Beta",
            _ => "Stable"
        };

    static bool TryHttpUri(string? raw, out Uri uri)
    {
        if (Uri.TryCreate((raw ?? "").Trim(), UriKind.Absolute, out var parsed)
            && (parsed.Scheme == Uri.UriSchemeHttps || parsed.Scheme == Uri.UriSchemeHttp))
        {
            uri = parsed;
            return true;
        }
        uri = null!;
        return false;
    }

    static Version ParseVersionForCompare(string? raw)
    {
        raw = NormalizeVersion(raw);
        var core = raw.Split('-', '+')[0];
        return Version.TryParse(core, out var parsed) ? parsed : new Version(0, 0, 0, 0);
    }

    static int CompareVersions(string? left, string? right)
    {
        var l = ParseVersionForCompare(left);
        var r = ParseVersionForCompare(right);
        var cmp = l.CompareTo(r);
        if (cmp != 0) return cmp;
        return string.Compare(NormalizeVersion(left), NormalizeVersion(right), StringComparison.OrdinalIgnoreCase);
    }

    static bool VersionEquals(string? left, string? right)
        => CompareVersions(left, right) == 0;

    static bool IsVersionNewer(string candidate, string current)
        => CompareVersions(candidate, current) > 0;

    static string NormalizeVersion(string? version)
        => (version ?? "").Trim().TrimStart('v', 'V');

    static string CompactVersionNotes(string? notes, int maxLength)
    {
        var clean = string.Join(" ", (notes ?? "")
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Split(' ', StringSplitOptions.RemoveEmptyEntries));
        if (clean.Length <= maxLength) return clean;
        return maxLength <= 1 ? clean[..maxLength] : clean[..(maxLength - 1)].TrimEnd() + "…";
    }

    static string NormalizeSha256(string? value)
        => new string((value ?? "").Where(Uri.IsHexDigit).ToArray()).ToLowerInvariant();

    static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    static string SanitizeVersionForFile(string version)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string((version ?? "update").Where(ch => !invalid.Contains(ch)).ToArray());
    }

}
