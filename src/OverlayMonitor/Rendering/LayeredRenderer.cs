using System.ComponentModel;
using OverlayMonitor.Models;
using OverlayMonitor.Window;

namespace OverlayMonitor.Rendering;

public sealed class LayeredRenderer : IDisposable
{
    private readonly nint _dc;
    private readonly nint _font;
    private readonly nint _previousFont;
    private nint _bitmap;
    private nint _previousBitmap;
    private nint _bits;
    private int _bitmapWidth, _bitmapHeight;
    private uint[] _canvas = [];

    public LayeredRenderer()
    {
        _dc = NativeMethods.CreateCompatibleDC(0);
        if (_dc == 0) throw new Win32Exception();
        _font = NativeMethods.CreateFont(22, 0, 0, 0, 400, 0, 0, 0, 1, 0, 0, 5, 0, "Segoe UI");
        if (_font == 0) { NativeMethods.DeleteDC(_dc); throw new Win32Exception(); }
        _previousFont = NativeMethods.SelectObject(_dc, _font);
    }

    public int Draw(nint hwnd, string text, int rightEdge, int top, bool movingMode, OverlayTheme theme, out int renderedWidth)
    {
        var height = movingMode ? 74 : 42;
        const int padding = 12, minimumWidth = 510;
        if (!NativeMethods.GetTextExtentPoint32(_dc, text, text.Length, out var textSize)) throw new Win32Exception();
        renderedWidth = Math.Max(minimumWidth, textSize.cx + padding * 2);
        EnsureBitmap(renderedWidth, height);
        var left = rightEdge - renderedWidth;
        var textX = renderedWidth - padding - textSize.cx;
        unsafe
        {
            var raw = new Span<uint>((void*)_bits, renderedWidth * height);
            _canvas.AsSpan().Clear();
            NativeMethods.SetBkMode(_dc, 1);
            switch (theme)
            {
                case OverlayTheme.OriginalWhite:
                    DrawTextLayer(_dc, raw, _canvas, textX, 10, text, 255, 255, 255, 255);
                    break;
                default:
                    DrawOutline(_dc, raw, _canvas, textX, 10, text, 20, 20, 20, 190);
                    DrawTextLayer(_dc, raw, _canvas, textX, 10, text, 255, 255, 255, 255);
                    break;
            }
            _canvas.AsSpan().CopyTo(raw);
        }
        var destination = new NativeMethods.POINT { x = left, y = top };
        var source = new NativeMethods.POINT();
        var size = new NativeMethods.SIZE { cx = renderedWidth, cy = height };
        var blend = new NativeMethods.BLENDFUNCTION { BlendOp = 0, SourceConstantAlpha = 255, AlphaFormat = 1 };
        if (!NativeMethods.UpdateLayeredWindow(hwnd, 0, ref destination, ref size, _dc, ref source, 0, ref blend, NativeMethods.ULW_ALPHA)) throw new Win32Exception();
        return left;
    }

    private void EnsureBitmap(int width, int height)
    {
        if (_bitmap != 0 && _bitmapWidth == width && _bitmapHeight == height) return;
        if (_bitmap != 0) { NativeMethods.SelectObject(_dc, _previousBitmap); NativeMethods.DeleteObject(_bitmap); }
        var info = new NativeMethods.BITMAPINFO { bmiHeader = new() { biSize = 40, biWidth = width, biHeight = -height, biPlanes = 1, biBitCount = 32, biCompression = 0 } };
        _bitmap = NativeMethods.CreateDIBSection(_dc, ref info, 0, out _bits, 0, 0);
        if (_bitmap == 0) throw new Win32Exception();
        _previousBitmap = NativeMethods.SelectObject(_dc, _bitmap);
        _bitmapWidth = width;
        _bitmapHeight = height;
        _canvas = new uint[width * height];
    }

    private static unsafe void DrawOutline(nint dc, Span<uint> raw, uint[] canvas, int x, int y, string text, byte r, byte g, byte b, byte opacity)
    {
        foreach (var (dx, dy) in new (int, int)[] { (-1, -1), (0, -1), (1, -1), (-1, 0), (1, 0), (-1, 1), (0, 1), (1, 1) })
            DrawTextLayer(dc, raw, canvas, x + dx, y + dy, text, r, g, b, opacity);
    }

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

    internal static void Blend(uint[] canvas, int index, byte r, byte g, byte b, byte sourceAlpha)
    {
        var destination = canvas[index];
        var da = (byte)(destination >> 24);
        var oa = (byte)(sourceAlpha + da * (255 - sourceAlpha) / 255);
        if (oa == 0) return;
        static byte Channel(byte source, byte destination, byte sa, byte da, byte oa) => (byte)((source * sa + destination * da * (255 - sa) / 255) / oa);
        var db = (byte)destination; var dg = (byte)(destination >> 8); var dr = (byte)(destination >> 16);
        canvas[index] = (uint)(Channel(b, db, sourceAlpha, da, oa) | (Channel(g, dg, sourceAlpha, da, oa) << 8) | (Channel(r, dr, sourceAlpha, da, oa) << 16) | (oa << 24));
    }

    public void Dispose()
    {
        if (_bitmap != 0) { NativeMethods.SelectObject(_dc, _previousBitmap); NativeMethods.DeleteObject(_bitmap); _bitmap = 0; }
        NativeMethods.SelectObject(_dc, _previousFont);
        NativeMethods.DeleteObject(_font);
        NativeMethods.DeleteDC(_dc);
    }
}
