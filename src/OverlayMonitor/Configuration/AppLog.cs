namespace OverlayMonitor.Configuration;

public static class AppLog
{
    private static readonly object Gate = new();
    private static string? _path;

    public static void Initialize()
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "OverlayMonitor");
        Directory.CreateDirectory(directory);
        _path = Path.Combine(directory, "overlay-monitor.log");
        File.WriteAllText(_path, $"OverlayMonitor started: {DateTimeOffset.Now:O}{Environment.NewLine}");
    }

    public static void Error(string message, Exception exception)
    {
        System.Diagnostics.Debug.WriteLine(exception);
        if (_path is null) return;
        try
        {
            lock (Gate)
                File.AppendAllText(_path, $"[{DateTimeOffset.Now:O}] {message}{Environment.NewLine}{exception}{Environment.NewLine}");
        }
        catch { /* Logging must never terminate the overlay. */ }
    }

    public static void Info(string message)
    {
        if (_path is null) return;
        try
        {
            lock (Gate)
                File.AppendAllText(_path, $"[{DateTimeOffset.Now:O}] {message}{Environment.NewLine}");
        }
        catch { /* Logging must never terminate the overlay. */ }
    }
}
