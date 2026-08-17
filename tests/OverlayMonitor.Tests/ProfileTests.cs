using OverlayMonitor.Configuration;
using OverlayMonitor.Models;
using OverlayMonitor.Tray;
using OverlayMonitor.Window;
using Xunit;

namespace OverlayMonitor.Tests;

public class ProfileTests
{
    private static OverlayProfile Profile(bool showMemoryLoad, params string[] enabledIds)
    {
        var config = new OverlayConfig { ShowMemoryLoad = showMemoryLoad };
        foreach (var metric in config.Metrics) metric.Enabled = enabledIds.Contains(metric.Id);
        var window = new OverlayWindow(new ConfigService(), config);
        return window.GetProfile();
    }

    [Fact]
    public void SupplementProfileMatches() => Assert.Equal(OverlayProfile.Supplement, Profile(true, "cpuTemp", "gpuTemp", "download", "upload"));

    [Fact]
    public void FullProfileMatches() => Assert.Equal(OverlayProfile.Full, Profile(true, "cpuTemp", "gpuTemp", "cpuLoad", "gpuLoad", "download", "upload"));

    [Fact]
    public void TemperatureProfileMatches() => Assert.Equal(OverlayProfile.Temperature, Profile(false, "cpuTemp", "gpuTemp"));

    [Fact]
    public void MemoryLoadMismatchFallsBackToCustom() => Assert.Equal(OverlayProfile.Custom, Profile(false, "cpuTemp", "gpuTemp", "download", "upload"));

    [Fact]
    public void UnknownCombinationFallsBackToCustom() => Assert.Equal(OverlayProfile.Custom, Profile(true, "cpuTemp"));
}
