using ToolTikTokV12.Controls;

namespace ToolTikTokManagerV13;

public sealed partial class ManagerForm
{
    readonly object _profileManagementMarker = new();
    TabPage? _profileManagementTab;
    DataGridView? _profileManagementGrid;
    TextBox? _profileManagementSearch;
    ComboBox? _profileManagementFilter;
    ComboBox? _profileManagementSort;
    Label? _profileTotalValue;
    Label? _profileRunningValue;
    Label? _profilePausedValue;
    Label? _profileStoppedValue;
    Label? _profileSelectionLabel;
    Label? _profileFooterStatus;
    Label? _profileFooterSummary;

    void ShowProfileManagementPage()
    {
        EnsureProfileManagementPage();
        RefreshProfileManagementPage();
        if (_profileManagementTab is not null) SelectTabPageSafely(_profileManagementTab);
        UpdateWorkspaceHeaderPresentation();
    }

    void EnsureProfileManagementPage()
    {
        if (_profileManagementTab is not null && !_profileManagementTab.IsDisposed) return;

        var page = new TabPage("Quản lý hồ sơ")
        {
            Tag = _profileManagementMarker,
            BackColor = UiTheme.Canvas,
            Padding = new Padding(10)
        };
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            BackColor = UiTheme.Canvas,
            Padding = Padding.Empty
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 104F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 112F));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));

        root.Controls.Add(BuildProfileKpis(), 0, 0);
        root.Controls.Add(BuildProfileToolbar(), 0, 1);
        root.Controls.Add(BuildProfileTable(), 0, 2);
        root.Controls.Add(BuildProfileFooter(), 0, 3);
        page.Controls.Add(root);
        _profileManagementTab = page;
        _tabs.TabPages.Insert(Math.Min(1, _tabs.TabPages.Count), page);
        UpdateWorkspaceTabBarAppearance();
    }

    Control BuildProfileKpis()
    {
        var row = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1, BackColor = UiTheme.Canvas };
        for (var i = 0; i < 4; i++) row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));

        ModernCardPanel Card(string glyph, string caption, Color accent, Color tint, out Label value)
        {
            var card = new ModernCardPanel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 4, 12, 8),
                Padding = new Padding(14),
                CornerRadius = 15,
                BorderColor = UiTheme.Border,
                GradientStart = tint,
                GradientEnd = Color.White
            };
            var tile = new Label
            {
                Text = glyph,
                AutoSize = false,
                Size = new Size(48, 48),
                Location = new Point(14, 20),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = tint,
                ForeColor = accent,
                Font = new Font("Segoe UI Symbol", 18F, FontStyle.Bold)
            };
            UiTheme.ApplyRoundedCorners(tile, 12);
            value = new Label
            {
                Text = "0",
                AutoSize = false,
                Location = new Point(78, 16),
                Size = new Size(120, 34),
                ForeColor = UiTheme.TextPrimary,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI Semibold", 20F, FontStyle.Bold)
            };
            var text = new Label
            {
                Text = caption,
                AutoSize = false,
                Location = new Point(78, 52),
                Size = new Size(160, 24),
                ForeColor = UiTheme.TextSecondary,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 9F)
            };
            card.Controls.Add(tile);
            card.Controls.Add(value);
            card.Controls.Add(text);
            var valueLabel = value;
            card.Resize += (_, _) => { valueLabel.Width = text.Width = Math.Max(70, card.ClientSize.Width - 90); };
            return card;
        }

        row.Controls.Add(Card("▰", "Tổng số profile", UiTheme.Primary, UiTheme.PrimarySoft, out _profileTotalValue), 0, 0);
        row.Controls.Add(Card("▶", "Đang chạy", UiTheme.Success, Color.FromArgb(220, 252, 231), out _profileRunningValue), 1, 0);
        row.Controls.Add(Card("Ⅱ", "Tạm dừng", UiTheme.Warning, Color.FromArgb(254, 249, 195), out _profilePausedValue), 2, 0);
        var stopped = Card("■", "Đã dừng", Color.FromArgb(71, 85, 105), Color.FromArgb(241, 245, 249), out _profileStoppedValue);
        stopped.Margin = new Padding(0, 4, 0, 8);
        row.Controls.Add(stopped, 3, 0);
        return row;
    }

    Control BuildProfileToolbar()
    {
        var toolbar = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 12,
            RowCount = 2,
            BackColor = UiTheme.Canvas,
            Padding = new Padding(0, 6, 0, 8)
        };
        for (var i = 0; i < 12; i++) toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F / 12F));
        toolbar.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
        toolbar.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));

        _profileManagementSearch = new TextBox
        {
            Dock = DockStyle.Fill,
            PlaceholderText = "Tìm kiếm theo tên profile, tài khoản...",
            Margin = new Padding(0, 0, 10, 0),
            Font = new Font("Segoe UI", 9.5F)
        };
        ModernDialog.StyleTextInput(_profileManagementSearch);
        _profileManagementFilter = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, Margin = new Padding(0, 0, 10, 0) };
        _profileManagementFilter.Items.AddRange(["Tất cả trạng thái", "Đang chạy", "Tạm dừng", "Đã dừng", "Khôi phục"]);
        _profileManagementFilter.SelectedIndex = 0;
        ModernDialog.StyleSelectionInput(_profileManagementFilter);
        _profileManagementSort = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, Margin = new Padding(0, 0, 10, 0) };
        _profileManagementSort.Items.AddRange(["Sắp xếp: Tên A-Z", "Sắp xếp: Tên Z-A"]);
        _profileManagementSort.SelectedIndex = 0;
        ModernDialog.StyleSelectionInput(_profileManagementSort);

        Button Action(string text, UiButtonKind kind, EventHandler click)
        {
            var button = new Button { Text = text, AutoSize = false, Dock = DockStyle.Fill, Margin = new Padding(0, 0, 10, 0) };
            UiTheme.StyleButton(button, kind);
            button.AutoSize = false;
            button.Dock = DockStyle.Fill;
            button.Font = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold);
            button.Padding = new Padding(5, 0, 5, 0);
            button.Click += click;
            return button;
        }

        var refresh = Action("↻", UiButtonKind.Neutral, (_, _) => RefreshProfileManagementPage(forceAccounts: true));
        var open = Action("▶  Mở profile", UiButtonKind.Success, (_, _) => OpenSelectedProfilesFromManagement());
        var import = Action("↑  Nhập từ Excel", UiButtonKind.Neutral, (_, _) => ShowAutoProfileDialog());
        var add = Action("＋  Thêm hồ sơ", UiButtonKind.Primary, (_, _) => AddProfile());
        _managerToolTip.SetToolTip(refresh, "Làm mới danh sách hồ sơ.");

        refresh.Dock = DockStyle.None;
        refresh.Anchor = AnchorStyles.Right;
        refresh.Size = new Size(44, 44);
        toolbar.Controls.Add(_profileManagementSearch, 0, 0);
        toolbar.SetColumnSpan(_profileManagementSearch, 5);
        toolbar.Controls.Add(_profileManagementFilter, 5, 0); toolbar.SetColumnSpan(_profileManagementFilter, 3);
        toolbar.Controls.Add(_profileManagementSort, 8, 0); toolbar.SetColumnSpan(_profileManagementSort, 3);
        toolbar.Controls.Add(refresh, 11, 0);
        toolbar.Controls.Add(open, 6, 1); toolbar.SetColumnSpan(open, 2);
        toolbar.Controls.Add(import, 8, 1); toolbar.SetColumnSpan(import, 2);
        toolbar.Controls.Add(add, 10, 1); toolbar.SetColumnSpan(add, 2);
        _profileManagementSearch.TextChanged += (_, _) => RefreshProfileManagementPage();
        _profileManagementFilter.SelectedIndexChanged += (_, _) => RefreshProfileManagementPage();
        _profileManagementSort.SelectedIndexChanged += (_, _) => RefreshProfileManagementPage();
        return toolbar;
    }

    void OpenSelectedProfilesFromManagement()
    {
        if (_profileManagementGrid is null) return;
        var selected = _profileManagementGrid.Rows.Cast<DataGridViewRow>()
            .Where(row => row.Cells["Pick"].Value as bool? == true && row.Tag is ProfileContext)
            .Select(row => (ProfileContext)row.Tag!)
            .ToList();
        if (selected.Count == 0)
        {
            OpenProfileChooser();
            return;
        }
        if (selected.Count == 1)
            _ = OpenProfileAsync(selected[0]);
        else
            _ = OpenProfilesSequentiallyAsync(selected);
    }

    Control BuildProfileTable()
    {
        var card = new ModernCardPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(14),
            CornerRadius = 16,
            BorderColor = UiTheme.Border,
            BackColor = Color.White
        };
        _profileManagementGrid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            MultiSelect = true,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            RowHeadersVisible = false,
            AutoGenerateColumns = false,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.None
        };
        _profileManagementGrid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Pick", HeaderText = "□", Width = 42 });
        _profileManagementGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Number", HeaderText = "#", Width = 52, ReadOnly = true });
        _profileManagementGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Profile", HeaderText = "Tên profile", MinimumWidth = 150, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 120, ReadOnly = true });
        _profileManagementGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Account", HeaderText = "Tài khoản TikTok", MinimumWidth = 170, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 130, ReadOnly = true });
        _profileManagementGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status", HeaderText = "Trạng thái", Width = 130, ReadOnly = true });
        _profileManagementGrid.Columns.Add(new DataGridViewButtonColumn { Name = "Open", HeaderText = "", Text = "▶ Mở", UseColumnTextForButtonValue = true, Width = 82 });
        _profileManagementGrid.Columns.Add(new DataGridViewButtonColumn { Name = "Edit", HeaderText = "Thao tác", Text = "✎ Sửa", UseColumnTextForButtonValue = true, Width = 82 });
        _profileManagementGrid.Columns.Add(new DataGridViewButtonColumn { Name = "Delete", HeaderText = "", Text = "▤ Xóa", UseColumnTextForButtonValue = true, Width = 82 });
        UiTheme.StyleGrid(_profileManagementGrid);
        foreach (var name in new[] { "Open", "Edit", "Delete" })
        {
            var column = (DataGridViewButtonColumn)_profileManagementGrid.Columns[name];
            column.FlatStyle = FlatStyle.Flat;
            column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            column.DefaultCellStyle.BackColor = Color.White;
            column.DefaultCellStyle.SelectionBackColor = UiTheme.PrimarySoft;
            column.DefaultCellStyle.ForeColor = name == "Delete" ? UiTheme.Danger : UiTheme.TextPrimary;
            column.DefaultCellStyle.SelectionForeColor = name == "Delete" ? UiTheme.Danger : UiTheme.TextPrimary;
            column.DefaultCellStyle.Padding = new Padding(5, 6, 5, 6);
        }
        _profileManagementGrid.ColumnHeaderMouseClick += (_, e) =>
        {
            if (_profileManagementGrid.Columns[e.ColumnIndex].Name != "Pick") return;
            var shouldSelect = _profileManagementGrid.Rows.Cast<DataGridViewRow>()
                .Any(row => row.Cells["Pick"].Value as bool? != true);
            foreach (DataGridViewRow row in _profileManagementGrid.Rows)
                row.Cells["Pick"].Value = shouldSelect;
            _profileManagementGrid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            UpdateProfileSelectionLabel();
        };
        _profileManagementGrid.CellMouseEnter += (_, e) =>
        {
            if (e.RowIndex >= 0 && !_profileManagementGrid.Rows[e.RowIndex].Selected)
                _profileManagementGrid.Rows[e.RowIndex].DefaultCellStyle.BackColor = Color.FromArgb(248, 250, 255);
        };
        _profileManagementGrid.CellMouseLeave += (_, e) =>
        {
            if (e.RowIndex >= 0 && !_profileManagementGrid.Rows[e.RowIndex].Selected)
                _profileManagementGrid.Rows[e.RowIndex].DefaultCellStyle.BackColor = Color.White;
        };
        _profileManagementGrid.CellContentClick += async (_, e) =>
        {
            if (e.RowIndex < 0 || _profileManagementGrid.Rows[e.RowIndex].Tag is not ProfileContext context) return;
            var column = _profileManagementGrid.Columns[e.ColumnIndex].Name;
            if (column == "Pick")
            {
                _profileManagementGrid.CommitEdit(DataGridViewDataErrorContexts.Commit);
                UpdateProfileSelectionLabel();
            }
            else if (column == "Open") await OpenProfileAsync(context);
            else if (column == "Edit")
            {
                EnsureTab(context);
                if (context.Tab is not null) SelectTabPageSafely(context.Tab);
                await RenameSelectedProfileAsync();
                ShowProfileManagementPage();
            }
            else if (column == "Delete")
            {
                await DeleteProfilesAsync([context]);
                RefreshProfileManagementPage();
            }
        };
        _profileManagementGrid.CellValueChanged += (_, e) => { if (e.RowIndex >= 0 && e.ColumnIndex == 0) UpdateProfileSelectionLabel(); };

        _profileSelectionLabel = new Label
        {
            Dock = DockStyle.Bottom,
            Height = 34,
            Text = "Chưa chọn profile",
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = UiTheme.TextSecondary,
            Font = new Font("Segoe UI", 9F)
        };
        card.Controls.Add(_profileManagementGrid);
        card.Controls.Add(_profileSelectionLabel);
        return card;
    }

    Control BuildProfileFooter()
    {
        var footer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0, 6, 0, 0),
            Padding = new Padding(4, 0, 4, 0),
            BackColor = UiTheme.Card
        };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45F));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55F));
        _profileFooterStatus = new Label
        {
            Text = "ⓘ  Sẵn sàng",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = UiTheme.TextSecondary,
            Font = new Font("Segoe UI", 9F)
        };
        _profileFooterSummary = new Label
        {
            Text = "Tổng: 0 profile  |  Đang chạy: 0  |  Tạm dừng: 0  |  Đã dừng: 0",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight,
            AutoEllipsis = true,
            ForeColor = UiTheme.TextSecondary,
            Font = new Font("Segoe UI", 9F)
        };
        footer.Controls.Add(_profileFooterStatus, 0, 0);
        footer.Controls.Add(_profileFooterSummary, 1, 0);
        return footer;
    }

    void RefreshProfileManagementPage(bool forceAccounts = false)
    {
        var grid = _profileManagementGrid;
        if (grid is null || grid.IsDisposed) return;
        if (forceAccounts) _dashboardAccountCache.Clear();
        var selected = grid.Rows.Cast<DataGridViewRow>()
            .Where(row => row.Cells["Pick"].Value as bool? == true && row.Tag is ProfileContext)
            .Select(row => ((ProfileContext)row.Tag!).Profile.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var keyword = _profileManagementSearch?.Text.Trim() ?? "";
        var filter = _profileManagementFilter?.SelectedIndex ?? 0;
        var contexts = _contexts.Values.Where(context =>
        {
            var account = GetDashboardAccount(context);
            if (keyword.Length > 0 && !context.Profile.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                && !account.Contains(keyword, StringComparison.OrdinalIgnoreCase)) return false;
            var state = ProfileManagementState(context);
            return filter switch
            {
                1 => state == RuntimeStateRunning,
                2 => state == RuntimeStatePaused,
                3 => state == RuntimeStateStopped,
                4 => state == RuntimeStateRecovering,
                _ => true
            };
        });
        contexts = (_profileManagementSort?.SelectedIndex ?? 0) == 1
            ? contexts.OrderByDescending(context => context.Profile.Name, NaturalProfileNameOrder)
            : contexts.OrderBy(context => context.Profile.Name, NaturalProfileNameOrder);

        grid.SuspendLayout();
        grid.Rows.Clear();
        var index = 1;
        foreach (var context in contexts)
        {
            var state = ProfileManagementState(context);
            var row = grid.Rows[grid.Rows.Add(selected.Contains(context.Profile.Name), index++, context.Profile.Name,
                GetDashboardAccount(context), ProfileManagementStateLabel(state))];
            row.Tag = context;
            row.Cells["Status"].Style.ForeColor = ProfileManagementStateColor(state);
            row.Cells["Status"].Style.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
        }
        grid.ResumeLayout();

        var all = _contexts.Values.ToList();
        var runningCount = all.Count(c => ProfileManagementState(c) == RuntimeStateRunning);
        var pausedCount = all.Count(c => ProfileManagementState(c) == RuntimeStatePaused);
        var stoppedCount = all.Count(c => ProfileManagementState(c) == RuntimeStateStopped);
        _profileTotalValue!.Text = all.Count.ToString();
        _profileRunningValue!.Text = runningCount.ToString();
        _profilePausedValue!.Text = pausedCount.ToString();
        _profileStoppedValue!.Text = stoppedCount.ToString();
        if (_profileFooterSummary is not null)
            _profileFooterSummary.Text = $"Tổng: {all.Count} profile  |  Đang chạy: {runningCount}  |  Tạm dừng: {pausedCount}  |  Đã dừng: {stoppedCount}";
        UpdateProfileSelectionLabel();
    }

    string ProfileManagementState(ProfileContext context)
    {
        if (context.RuntimeRecoveryInProgress) return RuntimeStateRecovering;
        if (context.Worker is null || context.Worker.HasExited) return RuntimeStateStopped;
        return GetLastConfirmedRuntimeState(context);
    }

    static string ProfileManagementStateLabel(string state) => state switch
    {
        RuntimeStateRunning => "●  Đang chạy",
        RuntimeStatePaused => "●  Tạm dừng",
        RuntimeStateRecovering => "●  Khôi phục",
        RuntimeStateStopped => "●  Đã dừng",
        _ => "●  Không xác định"
    };

    static Color ProfileManagementStateColor(string state) => state switch
    {
        RuntimeStateRunning => UiTheme.Success,
        RuntimeStatePaused => Color.FromArgb(217, 119, 6),
        RuntimeStateRecovering => UiTheme.Purple,
        RuntimeStateStopped => Color.FromArgb(100, 116, 139),
        _ => UiTheme.Danger
    };

    void UpdateProfileSelectionLabel()
    {
        if (_profileSelectionLabel is null || _profileManagementGrid is null) return;
        var count = _profileManagementGrid.Rows.Cast<DataGridViewRow>().Count(row => row.Cells["Pick"].Value as bool? == true);
        _profileSelectionLabel.Text = count == 0 ? "Chưa chọn profile" : $"Đã chọn: {count} profile";
        if (_profileFooterStatus is not null)
            _profileFooterStatus.Text = count == 0 ? "ⓘ  Sẵn sàng" : $"ⓘ  Đã chọn {count} profile";
    }

    ProfileOpenSelection? ChooseProfilesModern(IReadOnlyList<ProfileContext> contexts, string title)
    {
        using var form = new Form
        {
            Text = title,
            Width = 760,
            Height = 610,
            MinimumSize = new Size(640, 500),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.Sizable,
            MinimizeBox = false,
            MaximizeBox = false,
            ShowInTaskbar = false,
            AutoScaleMode = AutoScaleMode.Dpi,
            Font = new Font("Segoe UI", 9.5F)
        };
        ModernDialog.Apply(form, fixedDialog: false);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(18, 14, 18, 12),
            BackColor = UiTheme.Canvas
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 74F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54F));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64F));

        var heading = new Panel { Dock = DockStyle.Fill, BackColor = UiTheme.Canvas };
        heading.Controls.Add(new Label
        {
            Text = "▶  Chọn profile cần mở",
            AutoSize = true,
            Location = new Point(0, 2),
            Font = new Font("Segoe UI Semibold", 15F, FontStyle.Bold),
            ForeColor = UiTheme.TextPrimary
        });
        heading.Controls.Add(new Label
        {
            Text = "Chọn một hoặc nhiều profile để mở. Chrome sẽ được khởi động với các profile đã chọn.",
            AutoSize = true,
            Location = new Point(0, 36),
            Font = new Font("Segoe UI", 9F),
            ForeColor = UiTheme.TextSecondary
        });

        var tools = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = UiTheme.Canvas };
        tools.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        tools.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190F));
        var search = new TextBox
        {
            Dock = DockStyle.Fill,
            PlaceholderText = "Tìm kiếm profile...",
            Margin = new Padding(0, 3, 10, 7),
            Font = new Font("Segoe UI", 9.5F)
        };
        ModernDialog.StyleTextInput(search);
        var filter = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Margin = new Padding(0, 3, 0, 7)
        };
        filter.Items.AddRange(["Tất cả trạng thái", "Đang chạy", "Tạm dừng", "Đã dừng", "Khôi phục"]);
        filter.SelectedIndex = 0;
        ModernDialog.StyleSelectionInput(filter);
        tools.Controls.Add(search, 0, 0);
        tools.Controls.Add(filter, 1, 0);

        var grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            MultiSelect = true,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            RowHeadersVisible = false,
            AutoGenerateColumns = false,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.None
        };
        grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Pick", HeaderText = "✓", Width = 46 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Number", HeaderText = "#", Width = 54, ReadOnly = true });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Profile", HeaderText = "Tên profile", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 120, ReadOnly = true });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Account", HeaderText = "Tài khoản", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 110, ReadOnly = true });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status", HeaderText = "Trạng thái", Width = 140, ReadOnly = true });
        UiTheme.StyleGrid(grid);

        var selected = new HashSet<ProfileContext>();
        var selectionText = new Label
        {
            Text = "Đã chọn 0 profile",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            ForeColor = UiTheme.TextPrimary,
            Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold)
        };
        var cancel = new Button { Text = "Hủy", DialogResult = DialogResult.Cancel, AutoSize = false, Size = new Size(105, 42) };
        var open = new Button { Text = "▶  Mở profile", AutoSize = false, Size = new Size(170, 42), Enabled = false };
        ModernDialog.StyleSecondaryButton(cancel);
        ModernDialog.StylePrimaryButton(open);
        var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = UiTheme.Canvas, Padding = new Padding(0, 10, 0, 0) };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(open);
        footer.Controls.Add(selectionText, 0, 0);
        footer.Controls.Add(buttons, 1, 0);

        void UpdateSelection()
        {
            selectionText.Text = $"Đã chọn {selected.Count} profile";
            open.Enabled = selected.Count > 0;
            open.Text = selected.Count <= 1 ? "▶  Mở profile" : $"▶  Mở profile ({selected.Count})";
        }

        void Rebuild()
        {
            var keyword = search.Text.Trim();
            var wantedState = filter.SelectedIndex;
            var visible = contexts.Where(context =>
            {
                var account = GetDashboardAccount(context);
                if (keyword.Length > 0 && !context.Profile.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                    && !account.Contains(keyword, StringComparison.OrdinalIgnoreCase)) return false;
                var state = ProfileManagementState(context);
                return wantedState switch
                {
                    1 => state == RuntimeStateRunning,
                    2 => state == RuntimeStatePaused,
                    3 => state == RuntimeStateStopped,
                    4 => state == RuntimeStateRecovering,
                    _ => true
                };
            }).OrderBy(context => context.Profile.Name, NaturalProfileNameOrder).ToList();
            grid.Rows.Clear();
            for (var index = 0; index < visible.Count; index++)
            {
                var context = visible[index];
                var state = ProfileManagementState(context);
                var row = grid.Rows[grid.Rows.Add(selected.Contains(context), index + 1, context.Profile.Name,
                    GetDashboardAccount(context), ProfileManagementStateLabel(state))];
                row.Tag = context;
                row.Cells["Status"].Style.ForeColor = ProfileManagementStateColor(state);
                row.Cells["Status"].Style.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
            }
        }

        grid.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (grid.IsCurrentCellDirty) grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };
        grid.CellValueChanged += (_, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex != 0 || grid.Rows[e.RowIndex].Tag is not ProfileContext context) return;
            if (grid.Rows[e.RowIndex].Cells["Pick"].Value as bool? == true) selected.Add(context);
            else selected.Remove(context);
            UpdateSelection();
        };
        grid.CellDoubleClick += (_, e) =>
        {
            if (e.RowIndex < 0 || grid.Rows[e.RowIndex].Tag is not ProfileContext context) return;
            selected.Clear();
            selected.Add(context);
            UpdateSelection();
            open.PerformClick();
        };
        search.TextChanged += (_, _) => Rebuild();
        filter.SelectedIndexChanged += (_, _) => Rebuild();

        ProfileOpenSelection? result = null;
        open.Click += (_, _) =>
        {
            if (selected.Count == 0) return;
            var ordered = contexts.Where(selected.Contains).ToList();
            result = new ProfileOpenSelection(ordered.Count > 1, ordered);
            form.DialogResult = DialogResult.OK;
            form.Close();
        };

        root.Controls.Add(heading, 0, 0);
        root.Controls.Add(tools, 0, 1);
        root.Controls.Add(grid, 0, 2);
        root.Controls.Add(footer, 0, 3);
        form.Controls.Add(root);
        form.AcceptButton = open;
        form.CancelButton = cancel;
        form.Shown += (_, _) => { ModernDialog.FitToWorkingArea(form); Rebuild(); search.Focus(); };
        Rebuild();
        return form.ShowDialog(this) == DialogResult.OK ? result : null;
    }

    string? PromptRenameProfile(string currentName)
    {
        using var form = new Form
        {
            Text = "Đổi tên profile",
            Width = 590,
            Height = 580,
            MinimumSize = new Size(520, 520),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.Sizable,
            MinimizeBox = false,
            MaximizeBox = false,
            ShowInTaskbar = false,
            AutoScaleMode = AutoScaleMode.Dpi,
            Font = new Font("Segoe UI", 10F)
        };
        ModernDialog.Apply(form, fixedDialog: false);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(22),
            BackColor = ModernDialog.Canvas
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));

        var heading = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
        var icon = new Label
        {
            Text = "✎",
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI Symbol", 27F, FontStyle.Bold),
            ForeColor = UiTheme.Primary,
            BackColor = Color.FromArgb(237, 236, 255),
            Location = new Point(0, 1),
            Size = new Size(68, 68)
        };
        UiTheme.ApplyRoundedCorners(icon, 15);
        var title = new Label
        {
            Text = "Đổi tên profile",
            AutoSize = true,
            Location = new Point(88, 2),
            Font = new Font("Segoe UI", 20F, FontStyle.Bold),
            ForeColor = UiTheme.TextPrimary
        };
        var subtitle = new Label
        {
            Text = "Cập nhật tên hiển thị cho profile.",
            AutoSize = true,
            Location = new Point(90, 46),
            Font = new Font("Segoe UI", 10.5F),
            ForeColor = UiTheme.TextSecondary
        };
        heading.Controls.Add(icon);
        heading.Controls.Add(title);
        heading.Controls.Add(subtitle);

        var card = new ModernCardPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(22, 18, 22, 18),
            Margin = new Padding(0),
            BackColor = Color.White,
            BorderColor = UiTheme.Border,
            CornerRadius = 14
        };
        var fields = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 7,
            BackColor = Color.Transparent,
            Padding = new Padding(0)
        };
        fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 27));
        fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 43));
        fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 43));
        fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 43));
        fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        fields.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        Label FieldLabel(string text) => new()
        {
            Text = text,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.BottomLeft,
            Font = new Font("Segoe UI Semibold", 10.5F, FontStyle.Bold),
            ForeColor = UiTheme.TextPrimary
        };

        var current = new TextBox
        {
            Text = currentName,
            ReadOnly = true,
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(244, 247, 251),
            ForeColor = UiTheme.TextPrimary,
            Font = new Font("Segoe UI Semibold", 11F),
            Margin = new Padding(0, 5, 0, 2)
        };
        ModernDialog.StyleTextInput(current);
        current.BackColor = Color.FromArgb(244, 247, 251);

        var next = new TextBox
        {
            Text = currentName,
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 11F),
            Margin = new Padding(0, 4, 0, 2),
            PlaceholderText = "Nhập tên profile mới"
        };
        ModernDialog.StyleTextInput(next);

        var helper = new Label
        {
            Text = "Tên profile sẽ hiển thị trong danh sách hồ sơ.",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = UiTheme.TextSecondary,
            Font = new Font("Segoe UI", 9F)
        };
        var info = new Label
        {
            Text = "ⓘ  Nên dùng tên dễ phân biệt khi quản lý nhiều profile.",
            Dock = DockStyle.Fill,
            Padding = new Padding(14, 10, 14, 8),
            BackColor = Color.FromArgb(235, 244, 255),
            ForeColor = Color.FromArgb(41, 82, 145),
            Font = new Font("Segoe UI", 9.5F)
        };
        UiTheme.ApplyRoundedCorners(info, 10);
        fields.Controls.Add(FieldLabel("Tên hiện tại"), 0, 0);
        fields.Controls.Add(current, 0, 1);
        fields.Controls.Add(FieldLabel("Tên mới *"), 0, 2);
        fields.Controls.Add(next, 0, 3);
        fields.Controls.Add(helper, 0, 4);
        fields.Controls.Add(new Panel { Dock = DockStyle.Fill }, 0, 5);
        fields.Controls.Add(info, 0, 6);
        card.Controls.Add(fields);

        var cancel = new Button { Text = "Hủy", DialogResult = DialogResult.Cancel, Size = new Size(112, 42) };
        var ok = new Button { Text = "✓  OK", DialogResult = DialogResult.OK, Size = new Size(132, 42) };
        ModernDialog.StyleSecondaryButton(cancel);
        ModernDialog.StylePrimaryButton(ok);
        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 14, 0, 0),
            BackColor = Color.Transparent
        };
        footer.Controls.Add(ok);
        footer.Controls.Add(cancel);

        root.Controls.Add(heading, 0, 0);
        root.Controls.Add(card, 0, 1);
        root.Controls.Add(footer, 0, 2);
        form.Controls.Add(root);
        form.AcceptButton = ok;
        form.CancelButton = cancel;
        form.Shown += (_, _) =>
        {
            ModernDialog.FitToWorkingArea(form);
            next.Focus();
            next.SelectAll();
        };
        return form.ShowDialog(this) == DialogResult.OK ? next.Text.Trim() : null;
    }

    void UpdateWorkspaceHeaderPresentation()
    {
        if (_workspaceTitle is null || _workspaceSubtitle is null || _workspaceIcon is null) return;
        var profileManagement = _profileManagementTab is not null && ReferenceEquals(_tabs.SelectedTab, _profileManagementTab);
        _workspaceIcon.Text = profileManagement ? "▰" : "▦";
        _workspaceIcon.ForeColor = profileManagement ? UiTheme.Primary : UiTheme.TextPrimary;
        _workspaceIcon.Font = profileManagement
            ? new Font("Segoe UI Symbol", 18F, FontStyle.Bold)
            : new Font("Segoe UI Symbol", 18F, FontStyle.Bold);
        _workspaceTitle.Text = profileManagement ? "Quản lý hồ sơ" : "TikTok Operations";
        _workspaceSubtitle.Text = profileManagement
            ? "Quản lý profile, mở, thêm, sửa, xóa và nhập từ Excel."
            : "Quản lý và vận hành hồ sơ tập trung.";
    }
}
