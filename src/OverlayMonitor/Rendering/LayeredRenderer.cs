using System.ComponentModel;
using OverlayMonitor.Window;

namespace OverlayMonitor.Rendering;

public sealed class LayeredRenderer : IDisposable
{
    public int Draw(nint hwnd, string text, int rightEdge, int top, bool movingMode, out int renderedWidth)
    {
        var height = movingMode ? 74 : 42;
        const int horizontalPadding = 12;
        const int minimumWidth = 510;
        var dc = NativeMethods.CreateCompatibleDC(0);
        if (dc == 0) throw new Win32Exception();

        try
        {
            var font = NativeMethods.CreateFont(22, 0, 0, 0, 300, 0, 0, 0, 1, 0, 0, 5, 0, "Segoe UI Light");
            if (font == 0) throw new Win32Exception();
            var previousFont = NativeMethods.SelectObject(dc, font);
            try
            {
                if (!NativeMethods.GetTextExtentPoint32(dc, text, text.Length, out var textSize)) throw new Win32Exception();
                renderedWidth = Math.Max(minimumWidth, textSize.cx + horizontalPadding * 2);
                var left = rightEdge - renderedWidth;
                var info = new NativeMethods.BITMAPINFO
                {
                    bmiHeader = new() { biSize = 40, biWidth = renderedWidth, biHeight = -height, biPlanes = 1, biBitCount = 32, biCompression = 0 }
                };
                var bitmap = NativeMethods.CreateDIBSection(dc, ref info, 0, out var bits, 0, 0);
                if (bitmap == 0) throw new Win32Exception();

                try
                {
                    unsafe { new Span<uint>((void*)bits, renderedWidth * height).Clear(); }
                    var previousBitmap = NativeMethods.SelectObject(dc, bitmap);
                    try
                    {
                        NativeMethods.SetBkMode(dc, 1);
                        NativeMethods.SetTextColor(dc, 0x00FFFFFF);
                        NativeMethods.TextOut(dc, renderedWidth - horizontalPadding - textSize.cx, 10, text, text.Length);
                        ConvertTextToAlpha(bits, renderedWidth * height);

                        var destination = new NativeMethods.POINT { x = left, y = top };
                        var source = new NativeMethods.POINT();
                        var size = new NativeMethods.SIZE { cx = renderedWidth, cy = height };
                        var blend = new NativeMethods.BLENDFUNCTION { BlendOp = 0, SourceConstantAlpha = 255, AlphaFormat = 1 };
                        if (!NativeMethods.UpdateLayeredWindow(hwnd, 0, ref destination, ref size, dc, ref source, 0, ref blend, 2)) throw new Win32Exception();
                    }
                    finally { NativeMethods.SelectObject(dc, previousBitmap); }
                }
                finally { NativeMethods.DeleteObject(bitmap); }

                return left;
            }
            finally { NativeMethods.SelectObject(dc, previousFont); NativeMethods.DeleteObject(font); }
        }
        finally { NativeMethods.DeleteDC(dc); }
    }

    private static unsafe void ConvertTextToAlpha(nint bits, int pixelCount)
    {
        var pixels = new Span<uint>((void*)bits, pixelCount);
        for (var i = 0; i < pixels.Length; i++)
        {
            var pixel = pixels[i];
            var alpha = Math.Max((byte)pixel, Math.Max((byte)(pixel >> 8), (byte)(pixel >> 16)));
            pixels[i] = alpha == 0 ? 0u : ((uint)alpha << 24) | 0x00FFFFFF;
        }
    }

    public void Dispose() { }
}
