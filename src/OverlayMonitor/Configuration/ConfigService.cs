using System.Text.Json;
using OverlayMonitor.Models;

namespace OverlayMonitor.Configuration;
public sealed class ConfigService
{
    private readonly string _path = Path.Combine(AppContext.BaseDirectory, "OverlayMonitor", "config.json");
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    public OverlayConfig Load()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            if (!File.Exists(_path)) { var created = new OverlayConfig(); Save(created); return created; }
            var config = JsonSerializer.Deserialize<OverlayConfig>(File.ReadAllText(_path), Options) ?? new OverlayConfig();
            var changed = MigrateOriginalDefaultOrder(config);
            changed |= NormalizeTheme(config);
            if (changed) Save(config);
            return config;
        }
        catch (Exception ex) { AppLog.Error("读取配置文件失败，已使用内置默认配置。", ex); return new OverlayConfig(); }
    }
    public void Save(OverlayConfig config)
    {
        try { Directory.CreateDirectory(Path.GetDirectoryName(_path)!); File.WriteAllText(_path, JsonSerializer.Serialize(config, Options)); }
        catch (Exception ex) { AppLog.Error("保存配置文件失败。", ex); }
    }
    private static bool MigrateOriginalDefaultOrder(OverlayConfig config)
    {
        var original = new[] { "cpuTemp", "gpuTemp", "download", "upload", "cpuLoad", "gpuLoad" };
        if (config.Metrics.Count != original.Length || config.Metrics.OrderBy(m => m.Order).Select(m => m.Id).SequenceEqual(original) is false) return false;
        var revised = new[] { "download", "upload", "cpuTemp", "gpuTemp", "cpuLoad", "gpuLoad" };
        for (var i = 0; i < revised.Length; i++) config.Metrics.Single(m => m.Id == revised[i]).Order = i;
        return true;
    }
    private static bool NormalizeTheme(OverlayConfig config)
    {
        if (Enum.IsDefined(config.Theme)) return false;
        config.Theme = OverlayTheme.AdaptiveOutline;
        return true;
    }
}
