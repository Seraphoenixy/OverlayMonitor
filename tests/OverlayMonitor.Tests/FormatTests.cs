using OverlayMonitor.Models;
using Xunit;

namespace OverlayMonitor.Tests;

public class FormatTests
{
    private static OverlayConfig Config(bool showMemoryLoad = false, params (string Id, int Order)[] metrics) => new()
    {
        ShowMemoryLoad = showMemoryLoad,
        Metrics = metrics.Select(m => new MetricConfig { Id = m.Id, Order = m.Order, Enabled = true }).ToList()
    };

    private static MonitorSnapshot Snapshot(float? cpuTemp = null, float? gpuTemp = null, float? cpuLoad = null, float? gpuLoad = null, uint memoryLoad = 0, double downloadBps = 0, double uploadBps = 0)
        => new(cpuTemp, gpuTemp, cpuLoad, gpuLoad, memoryLoad, downloadBps, uploadBps);

    [Fact]
    public void NullTemperatureShowsDashes() => Assert.Equal("CPU --", Program.Format(Config(false, ("cpuTemp", 0)), Snapshot()));

    [Fact]
    public void TemperatureRoundsToInteger() => Assert.Equal("CPU 46°C", Program.Format(Config(false, ("cpuTemp", 0)), Snapshot(cpuTemp: 45.6f)));

    [Fact]
    public void LoadsShowPercentage() => Assert.Equal("CPU 37%  |  GPU 82%", Program.Format(Config(false, ("cpuLoad", 0), ("gpuLoad", 1)), Snapshot(cpuLoad: 37f, gpuLoad: 82f)));

    [Theory]
    [InlineData(500, "↓ 500 B/s")]
    [InlineData(2048, "↓ 2 KB/s")]
    public void DownloadSpeedUsesLowerUnits(double bytes, string expected) => Assert.Equal(expected, Program.Format(Config(false, ("download", 0)), Snapshot(downloadBps: bytes)));

    [Fact]
    public void DownloadSpeedUsesMegabytes() => Assert.Contains("MB/s", Program.Format(Config(false, ("download", 0)), Snapshot(downloadBps: 3 * 1024 * 1024)));

    [Fact]
    public void UploadSpeedUsesArrow() => Assert.Equal("↑ 1 KB/s", Program.Format(Config(false, ("upload", 0)), Snapshot(uploadBps: 1024)));

    [Fact]
    public void MemoryLoadAppendedWhenEnabled() => Assert.Equal("CPU --  |  RAM 42%", Program.Format(Config(true, ("cpuTemp", 0)), Snapshot(memoryLoad: 42)));

    [Fact]
    public void MemoryLoadOmittedWhenDisabled() => Assert.Equal("CPU --", Program.Format(Config(false, ("cpuTemp", 0)), Snapshot(memoryLoad: 42)));

    [Fact]
    public void MetricsOrderedByOrderField() => Assert.Equal("GPU --  |  CPU --", Program.Format(Config(false, ("cpuTemp", 1), ("gpuTemp", 0)), Snapshot()));

    [Fact]
    public void DisabledMetricsSkipped()
    {
        var config = Config(false, ("cpuTemp", 0), ("gpuTemp", 1));
        config.Metrics[1].Enabled = false;
        Assert.Equal("CPU --", Program.Format(config, Snapshot()));
    }
}
