using OverlayMonitor.Window;
using Xunit;

namespace OverlayMonitor.Tests;

public sealed class WindowPositionTests
{
    [Theory]
    [InlineData(1900, 1070, 510, 42, 1410, 1038)]
    [InlineData(-100, -100, 510, 42, 0, 0)]
    [InlineData(815, 3, 510, 42, 815, 3)]
    [InlineData(10, 10, 2200, 42, -280, 10)]
    public void KeepsRenderedContentInWorkArea(int x, int y, int width, int height, int expectedX, int expectedY)
    {
        var work = new NativeMethods.RECT { right = 1920, bottom = 1080 };
        Assert.Equal((expectedX, expectedY), OverlayWindow.ClampPosition(x, y, width, height, work));
    }

    [Fact]
    public void SupportsNegativeMonitorCoordinatesAndSmallWorkAreas()
    {
        var work = new NativeMethods.RECT { left = -1280, top = -200, right = 0, bottom = 520 };
        Assert.Equal((-510, 478), OverlayWindow.ClampPosition(-10, 600, 510, 42, work));
        Assert.Equal((-1280, -200), OverlayWindow.ClampPosition(-1280, 0, 1280, 900, work));
    }
}
