using System.Text;

namespace ToolTikTokV11;

public static class WorkerBootLog
{
    static readonly object Gate = new();

    public static void Write(string dataRoot, string message)
    {
        try
        {
            var root = string.IsNullOrWhiteSpace(dataRoot) ? AppContext.BaseDirectory : Path.GetFullPath(dataRoot);
            var directory = Path.Combine(root, "logs");
            Directory.CreateDirectory(directory);
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}";
            lock (Gate)
                File.AppendAllText(Path.Combine(directory, "worker-bootstrap.log"), line, new UTF8Encoding(false));
        }
        catch { }
    }
}
