using System.ComponentModel;
using OverlayMonitor.Models;
using OverlayMonitor.Window;

namespace OverlayMonitor.Rendering;

public sealed class LayeredRenderer : IDisposable
{
    public int Draw(nint hwnd, string text, int rightEdge, int top, bool movingMode, OverlayTheme theme, out int renderedWidth)
    {
        // 2560 × 1600 is the visual baseline.  Scale by monitor height so the
        // overlay stays legible on smaller panels without becoming oversized on 4K.
        var scale = GetMonitorScale(hwnd);
        var height = Scale(movingMode ? 74 : 42, scale);
        var padding = Scale(12, scale);
        var minimumWidth = Scale(510, scale);
        var textY = Scale(10, scale);
        var outlineSize = Math.Max(1, Scale(1, scale));
        var fontHeight = Scale(22, scale);
        // Preserve the current 2560 × 1600 appearance.  A slightly stronger
        // weight keeps the smaller rasterized text clear at compact resolutions.
        var fontWeight = scale < 0.9f ? 500 : 400;
        var dc = NativeMethods.CreateCompatibleDC(0);
        if (dc == 0) throw new Win32Exception();
        try
        {
            var font = NativeMethods.CreateFont(fontHeight, 0, 0, 0, fontWeight, 0, 0, 0, 1, 0, 0, 5, 0, "Segoe UI");
            if (font == 0) throw new Win32Exception();
            var previousFont = NativeMethods.SelectObject(dc, font);
            try
            {
                if (!NativeMethods.GetTextExtentPoint32(dc, text, text.Length, out var textSize)) throw new Win32Exception();
                renderedWidth = Math.Max(minimumWidth, textSize.cx + padding * 2);
                var left = rightEdge - renderedWidth;
                var info = new NativeMethods.BITMAPINFO { bmiHeader = new() { biSize = 40, biWidth = renderedWidth, biHeight = -height, biPlanes = 1, biBitCount = 32, biCompression = 0 } };
                var bitmap = NativeMethods.CreateDIBSection(dc, ref info, 0, out var bits, 0, 0);
                if (bitmap == 0) throw new Win32Exception();
                try
                {
                    var previousBitmap = NativeMethods.SelectObject(dc, bitmap);
                    try
                    {
                        var pixelCount = renderedWidth * height;
                        var canvas = new uint[pixelCount];
                        var textX = renderedWidth - padding - textSize.cx;
                        unsafe
                        {
                            var raw = new Span<uint>((void*)bits, pixelCount);
                            NativeMethods.SetBkMode(dc, 1);
                            switch (theme)
                            {
                                case OverlayTheme.OriginalWhite:
                                    DrawTextLayer(dc, raw, canvas, textX, textY, text, 255, 255, 255, 255);
                                    break;
                                default:
                                    DrawOutline(dc, raw, canvas, textX, textY, text, 20, 20, 20, 190, outlineSize);
                                    DrawTextLayer(dc, raw, canvas, textX, textY, text, 255, 255, 255, 255);
                                    break;
                            }
                            canvas.AsSpan().CopyTo(raw);
                        }
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

    private static unsafe void DrawOutline(nint dc, Span<uint> raw, uint[] canvas, int x, int y, string text, byte r, byte g, byte b, byte opacity, int size)
    {
        foreach (var (dx, dy) in new (int, int)[] { (-size, -size), (0, -size), (size, -size), (-size, 0), (size, 0), (-size, size), (0, size), (size, size) })
            DrawTextLayer(dc, raw, canvas, x + dx, y + dy, text, r, g, b, opacity);
    }

    private static float GetMonitorScale(nint hwnd)
    {
        var monitor = NativeMethods.MonitorFromWindow(hwnd, NativeMethods.MONITOR_DEFAULTTONEAREST);
        if (monitor == 0) return 1f;
        var info = new NativeMethods.MONITORINFO { cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.MONITORINFO>() };
        return NativeMethods.GetMonitorInfo(monitor, ref info)
            ? Math.Clamp((info.rcMonitor.bottom - info.rcMonitor.top) / 1600f, 0.75f, 1.5f)
            : 1f;
    }

    private static int Scale(int value, float scale) => Math.Max(1, (int)MathF.Round(value * scale));

    private static unsafe void DrawTextLayer(nint dc, Span<uint> raw, uint[] canvas, int x, int y, string text, byte r, byte g, byte b, byte opacity)
    {
        raw.Clear();
        NativeMethods.SetTextColor(dc, (uint)(r | (g << 8) | (b << 16)));
        NativeMethods.TextOut(dc, x, y, text, text.Length);
        var maximum = Math.Max(r, Math.Max(g, b));
        for (var i = 0; i < raw.Length; i++)
        {
            var pixel = raw[i];
            var coverage = Math.Max((byte)pixel, Math.Max((byte)(pixel >> 8), (byte)(pixel >> 16)));
            if (coverage != 0) Blend(canvas, i, r, g, b, (byte)(coverage * opacity / maximum));
        }
    }

    private static void Blend(uint[] canvas, int index, byte r, byte g, byte b, byte sourceAlpha)
    {
        var destination = canvas[index];
        var da = (byte)(destination >> 24);
        var oa = (byte)(sourceAlpha + da * (255 - sourceAlpha) / 255);
        if (oa == 0) return;
        static byte Channel(byte source, byte destination, byte sa, byte da, byte oa) => (byte)((source * sa + destination * da * (255 - sa) / 255) / oa);
        var db = (byte)destination; var dg = (byte)(destination >> 8); var dr = (byte)(destination >> 16);
        canvas[index] = (uint)(Channel(b, db, sourceAlpha, da, oa) | (Channel(g, dg, sourceAlpha, da, oa) << 8) | (Channel(r, dr, sourceAlpha, da, oa) << 16) | (oa << 24));
    }

    public void Dispose() { }
}
