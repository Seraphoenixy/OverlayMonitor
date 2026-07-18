namespace OverlayMonitor.Models;

public sealed class OverlayConfig
{
    public int X { get; set; } = 815;
    public int Y { get; set; } = 3;
    public bool Visible { get; set; } = true;
    public bool ShowMemoryLoad { get; set; } = true;
    public int SampleIntervalMs { get; set; } = 1000;
    public List<MetricConfig> Metrics { get; set; } =
    [
        new() { Id = "download", Order = 0, Enabled = true }, new() { Id = "upload", Order = 1, Enabled = true },
        new() { Id = "cpuTemp", Order = 2, Enabled = true }, new() { Id = "gpuTemp", Order = 3, Enabled = true },
        new() { Id = "cpuLoad", Order = 4, Enabled = false }, new() { Id = "gpuLoad", Order = 5, Enabled = false }
    ];
}
public sealed class MetricConfig { public string Id { get; set; } = ""; public bool Enabled { get; set; } = true; public int Order { get; set; } }
public sealed record MonitorSnapshot(float? CpuTemperature, float? GpuTemperature, float? CpuLoad, float? GpuLoad, uint MemoryLoad, double DownloadBps, double UploadBps);
