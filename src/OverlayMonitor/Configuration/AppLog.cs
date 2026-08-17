namespace OverlayMonitor.Configuration;

public static class AppLog
{
    private static readonly object Gate = new();
    private const long MaximumBytes = 512 * 1024;
    private static string? _path;
    private static StreamWriter? _writer;

    public static void Initialize()
    {
        try
        {
            var directory = Path.Combine(AppContext.BaseDirectory, "OverlayMonitor");
            Directory.CreateDirectory(directory);
            _path = Path.Combine(directory, "overlay-monitor.log");
            RotateIfNeeded();
            _writer = new StreamWriter(new FileStream(_path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite)) { AutoFlush = true };
            Write($"OverlayMonitor started: {DateTimeOffset.Now:O}");
        }
        catch { /* Logging must never terminate the overlay. */ }
    }

    private static void RotateIfNeeded()
    {
        var path = _path!;
        var info = new FileInfo(path);
        if (!info.Exists || info.Length <= MaximumBytes) return;
        File.Delete(path + ".old");
        File.Move(path, path + ".old");
    }

    public static void Error(string message, Exception exception)
    {
        System.Diagnostics.Debug.WriteLine(exception);
        lock (Gate) Write($"[{DateTimeOffset.Now:O}] {message}{Environment.NewLine}{exception}");
    }

    public static void Info(string message)
    {
        lock (Gate) Write($"[{DateTimeOffset.Now:O}] {message}");
    }

    private static void Write(string line)
    {
        if (_writer is null) return;
        try { _writer.WriteLine(line); }
        catch { /* Logging must never terminate the overlay. */ }
    }

    public static void Shutdown()
    {
        lock (Gate)
        {
            try { _writer?.Dispose(); }
            catch { /* Logging must never terminate the overlay. */ }
            _writer = null;
        }
    }
}
