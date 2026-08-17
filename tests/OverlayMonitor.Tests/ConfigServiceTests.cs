using OverlayMonitor.Configuration;
using OverlayMonitor.Models;
using Xunit;

namespace OverlayMonitor.Tests;

public class ConfigServiceTests
{
    private static OverlayConfig ConfigWithOrder(string[] ids)
    {
        var config = new OverlayConfig();
        config.Metrics = ids.Select((id, i) => new MetricConfig { Id = id, Order = i, Enabled = true }).ToList();
        return config;
    }

    private static string[] OrderedIds(OverlayConfig config) => config.Metrics.OrderBy(m => m.Order).Select(m => m.Id).ToArray();

    [Fact]
    public void MigrateRevisesOriginalDefaultOrder()
    {
        var config = ConfigWithOrder(["cpuTemp", "gpuTemp", "download", "upload", "cpuLoad", "gpuLoad"]);
        Assert.True(ConfigService.MigrateOriginalDefaultOrder(config));
        Assert.Equal(["download", "upload", "cpuTemp", "gpuTemp", "cpuLoad", "gpuLoad"], OrderedIds(config));
    }

    [Fact]
    public void MigrateLeavesRevisedOrderUntouched()
    {
        var config = new OverlayConfig();
        var before = OrderedIds(config);
        Assert.False(ConfigService.MigrateOriginalDefaultOrder(config));
        Assert.Equal(before, OrderedIds(config));
    }

    [Fact]
    public void MigrateIgnoresDifferentMetricCount()
    {
        var config = ConfigWithOrder(["cpuTemp", "gpuTemp"]);
        Assert.False(ConfigService.MigrateOriginalDefaultOrder(config));
        Assert.Equal(["cpuTemp", "gpuTemp"], OrderedIds(config));
    }

    [Fact]
    public void MigrateIgnoresDifferentOrder()
    {
        var config = ConfigWithOrder(["download", "cpuTemp", "gpuTemp", "upload", "cpuLoad", "gpuLoad"]);
        Assert.False(ConfigService.MigrateOriginalDefaultOrder(config));
        Assert.Equal(["download", "cpuTemp", "gpuTemp", "upload", "cpuLoad", "gpuLoad"], OrderedIds(config));
    }

    [Fact]
    public void NormalizeThemeFixesUndefinedValue()
    {
        var config = new OverlayConfig { Theme = (OverlayTheme)99 };
        Assert.True(ConfigService.NormalizeTheme(config));
        Assert.Equal(OverlayTheme.AdaptiveOutline, config.Theme);
    }

    [Fact]
    public void NormalizeThemeKeepsDefinedValue()
    {
        var config = new OverlayConfig { Theme = OverlayTheme.OriginalWhite };
        Assert.False(ConfigService.NormalizeTheme(config));
        Assert.Equal(OverlayTheme.OriginalWhite, config.Theme);
    }
}
