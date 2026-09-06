using ToolTikTokV12.Controls;

namespace ToolTikTokManagerV13;

public sealed partial class ManagerForm
{
    sealed record WorkspacePageMarker(string Key, string Title, string Subtitle, string Glyph, string NavigationKey);

    /// <summary>
    /// Transitional host for feature UIs that were historically implemented as large
    /// modal forms. It keeps all existing event handlers and services intact while the
    /// surface behaves like a first-class workspace page.
    /// </summary>
    void ShowWorkspacePage(Form featureForm, string key, string title, string subtitle, string glyph, string navigationKey)
    {
        var existing = _tabs.TabPages.Cast<TabPage>()
            .FirstOrDefault(page => page.Tag is WorkspacePageMarker marker
                && string.Equals(marker.Key, key, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            featureForm.Dispose();
            SelectTabPageSafely(existing);
            SetActiveNavigation(navigationKey);
            return;
        }

        var marker = new WorkspacePageMarker(key, title, subtitle, glyph, navigationKey);
        var page = new TabPage(title)
        {
            Tag = marker,
            BackColor = UiColors.Canvas,
            Padding = new Padding(UiSpacing.Sm)
        };
        featureForm.TopLevel = false;
        featureForm.FormBorderStyle = FormBorderStyle.None;
        featureForm.ShowInTaskbar = false;
        featureForm.StartPosition = FormStartPosition.Manual;
        featureForm.Dock = DockStyle.Fill;
        featureForm.Margin = Padding.Empty;
        page.Controls.Add(featureForm);

        var addIndex = _tabs.TabPages.Cast<TabPage>().ToList().FindIndex(IsAddTab);
        _tabs.TabPages.Insert(addIndex < 0 ? _tabs.TabPages.Count : addIndex, page);
        featureForm.FormClosed += (_, _) =>
        {
            if (page.IsDisposed || _closing || IsDisposed || Disposing) return;
            try { BeginInvoke(new Action(() =>
            {
                if (page.IsDisposed || _closing || IsDisposed || Disposing) return;
                _tabs.TabPages.Remove(page);
                page.Dispose();
                ShowProfileManagementPage();
            })); } catch (InvalidOperationException) { }
        };
        featureForm.Show();
        SelectTabPageSafely(page);
        SetActiveNavigation(navigationKey);
        UpdateWorkspaceTabBarAppearance();
        UpdateWorkspaceHeaderPresentation();
    }

    bool TryActivateWorkspacePage(string key, string navigationKey)
    {
        var page = _tabs.TabPages.Cast<TabPage>()
            .FirstOrDefault(candidate => candidate.Tag is WorkspacePageMarker marker
                && string.Equals(marker.Key, key, StringComparison.OrdinalIgnoreCase));
        if (page is null) return false;
        SelectTabPageSafely(page);
        SetActiveNavigation(navigationKey);
        UpdateWorkspaceHeaderPresentation();
        return true;
    }
}
