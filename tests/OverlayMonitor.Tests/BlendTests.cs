using OverlayMonitor.Rendering;
using Xunit;

namespace OverlayMonitor.Tests;

public class BlendTests
{
    [Fact]
    public void ZeroAlphaOnTransparentDestinationLeavesPixelUntouched()
    {
        var canvas = new uint[1];
        LayeredRenderer.Blend(canvas, 0, 255, 0, 0, 0);
        Assert.Equal(0u, canvas[0]);
    }

    [Fact]
    public void OpaqueSourceOverwritesTransparentDestination()
    {
        var canvas = new uint[1];
        LayeredRenderer.Blend(canvas, 0, 255, 128, 0, 255);
        Assert.Equal(0xFFFF8000u, canvas[0]);
    }

    [Fact]
    public void SemiTransparentSourceOverOpaqueWhiteHalvesBrightness()
    {
        var canvas = new uint[] { 0xFFFFFFFFu };
        LayeredRenderer.Blend(canvas, 0, 0, 0, 0, 128);
        Assert.Equal(0xFF7F7F7Fu, canvas[0]);
    }

    [Fact]
    public void ResultAlphaCombinesSourceAndDestination()
    {
        var canvas = new uint[] { 0x80000000u };
        LayeredRenderer.Blend(canvas, 0, 0, 0, 0, 128);
        var expectedAlpha = (byte)(128 + 128 * (255 - 128) / 255);
        Assert.Equal((uint)(expectedAlpha << 24), canvas[0] & 0xFF000000u);
    }
}
