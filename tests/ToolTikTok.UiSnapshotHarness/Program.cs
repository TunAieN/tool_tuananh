using System.Drawing.Imaging;
using System.Reflection;
using ToolTikTokManagerV13;
using ToolTikTokV12.Models;
using WorkerMainForm = ToolTikTokV11.MainForm;

namespace UiSnapshotHarness;

internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        using var watchdog = new System.Threading.Timer(
            _ => ForceExit(124),
            null,
            TimeSpan.FromSeconds(60),
            Timeout.InfiniteTimeSpan);
        var width = args.Length > 0 && int.TryParse(args[0], out var parsedWidth) ? parsedWidth : 1600;
        var height = args.Length > 1 && int.TryParse(args[1], out var parsedHeight) ? parsedHeight : 900;
        var output = args.Length > 2
            ? Path.GetFullPath(args[2])
            : Path.Combine(AppContext.BaseDirectory, $"dashboard-{width}x{height}.png");
        var mode = args.Length > 3 ? args[3] : "";

        if (mode.Equals("worker", StringComparison.OrdinalIgnoreCase))
        {
            CaptureWorker(width, height, output);
            watchdog.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            ForceExit(0);
            return;
        }

        using var form = new ManagerForm
        {
            StartPosition = FormStartPosition.Manual,
            Location = new Point(0, 0),
            Size = new Size(width, height),
            WindowState = FormWindowState.Normal
        };

        using var timer = new System.Windows.Forms.Timer { Interval = 2500 };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            if (mode.Equals("add-dialog", StringComparison.OrdinalIgnoreCase)
                || mode.Equals("rename-dialog", StringComparison.OrdinalIgnoreCase)) return;
            if (args.Length > 3 && args[3].Equals("bottom", StringComparison.OrdinalIgnoreCase))
            {
                var dashboard = Descendants(form).OfType<TabPage>().FirstOrDefault(page => page.AutoScroll);
                if (dashboard is not null)
                    dashboard.AutoScrollPosition = new Point(0, dashboard.VerticalScroll.Maximum);
            }
            form.Refresh();
            Application.DoEvents();
            using var bitmap = new Bitmap(form.Width, form.Height);
            form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
            Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            bitmap.Save(output, ImageFormat.Png);
            File.WriteAllLines(Path.ChangeExtension(output, ".controls.txt"), Describe(form));
            form.Close();
        };
        form.Shown += (_, _) =>
        {
            if (!mode.Equals("add-dialog", StringComparison.OrdinalIgnoreCase)
                && !mode.Equals("rename-dialog", StringComparison.OrdinalIgnoreCase))
            {
                timer.Start();
                return;
            }

            var modalTimer = new System.Windows.Forms.Timer { Interval = 500 };
            modalTimer.Tick += (_, _) =>
            {
                var modal = Application.OpenForms.Cast<Form>().FirstOrDefault(candidate => !ReferenceEquals(candidate, form));
                if (modal is null || !modal.Visible) return;
                modalTimer.Stop();
                modal.Refresh();
                Application.DoEvents();
                using var bitmap = new Bitmap(modal.Width, modal.Height);
                modal.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                Directory.CreateDirectory(Path.GetDirectoryName(output)!);
                bitmap.Save(output, ImageFormat.Png);
                File.WriteAllLines(Path.ChangeExtension(output, ".controls.txt"), Describe(modal));
                modal.Close();
                form.Close();
                modalTimer.Dispose();
            };
            modalTimer.Start();
            form.BeginInvoke(() =>
            {
                var methodName = mode.StartsWith("add", StringComparison.OrdinalIgnoreCase)
                    ? "ShowAddProfileDialog"
                    : "PromptRenameProfile";
                var method = typeof(ManagerForm).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
                    ?? throw new MissingMethodException(methodName);
                object argument = methodName == "ShowAddProfileDialog" ? new TikTokProfileCatalog() : "acc01";
                method.Invoke(form, new object?[] { argument });
            });
        };
        Application.Run(form);
        watchdog.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        ForceExit(0);
    }

    static void CaptureWorker(int width, int height, string output)
    {
        using var form = new WorkerMainForm
        {
            StartPosition = FormStartPosition.Manual,
            Location = new Point(0, 0),
            Size = new Size(width, height),
            WindowState = FormWindowState.Normal
        };
        using var timer = new System.Windows.Forms.Timer { Interval = 2200 };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            form.Refresh();
            Application.DoEvents();
            using var bitmap = new Bitmap(form.Width, form.Height);
            form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
            Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            bitmap.Save(output, ImageFormat.Png);
            File.WriteAllLines(Path.ChangeExtension(output, ".controls.txt"), Describe(form));
            form.Close();
        };
        form.Shown += (_, _) => timer.Start();
        Application.Run(form);
    }

    static void ForceExit(int exitCode)
    {
        try
        {
            foreach (var form in Application.OpenForms.Cast<Form>().ToArray())
            {
                try
                {
                    if (form.InvokeRequired) form.BeginInvoke(new Action(form.Close));
                    else form.Close();
                }
                catch { }
            }
            Application.Exit();
            Application.ExitThread();
        }
        finally
        {
            Environment.Exit(exitCode);
        }
    }

    static IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }

    static IEnumerable<string> Describe(Control root, int depth = 0)
    {
        yield return $"{new string(' ', depth * 2)}{root.GetType().Name} name={root.Name} visible={root.Visible} bounds={root.Bounds} text={root.Text}";
        foreach (Control child in root.Controls)
            foreach (var line in Describe(child, depth + 1))
                yield return line;
    }
}
