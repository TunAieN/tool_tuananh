using ToolTikTokV12.Controls;

// These namespace-local bridges preserve every existing MessageBox call and
// DialogResult branch while routing the visual surface through ModernDialog.
namespace ToolTikTokManagerV13
{
    internal static class MessageBox
    {
        public static DialogResult Show(string text)
            => ModernDialog.ShowLegacy(null, text, "TikTok Operations", MessageBoxButtons.OK, MessageBoxIcon.Information);
        public static DialogResult Show(string text, string caption)
            => ModernDialog.ShowLegacy(null, text, caption, MessageBoxButtons.OK, MessageBoxIcon.Information);
        public static DialogResult Show(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
            => ModernDialog.ShowLegacy(null, text, caption, buttons, icon);
        public static DialogResult Show(IWin32Window owner, string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
            => ModernDialog.ShowLegacy(owner, text, caption, buttons, icon);
    }
}

namespace ToolTikTokV11
{
    internal static class MessageBox
    {
        public static DialogResult Show(string text)
            => ModernDialog.ShowLegacy(null, text, "TikTok Automation", MessageBoxButtons.OK, MessageBoxIcon.Information);
        public static DialogResult Show(string text, string caption)
            => ModernDialog.ShowLegacy(null, text, caption, MessageBoxButtons.OK, MessageBoxIcon.Information);
        public static DialogResult Show(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
            => ModernDialog.ShowLegacy(null, text, caption, buttons, icon);
        public static DialogResult Show(IWin32Window owner, string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
            => ModernDialog.ShowLegacy(owner, text, caption, buttons, icon);
    }
}
