using System.Runtime.InteropServices;

namespace OverlayMonitor.Window;
internal static class NativeMethods
{
    internal const uint WS_POPUP = 0x80000000, WS_EX_TOPMOST = 0x00000008, WS_EX_TOOLWINDOW = 0x00000080, WS_EX_NOACTIVATE = 0x08000000, WS_EX_LAYERED = 0x00080000, WS_EX_TRANSPARENT = 0x00000020;
    internal const int GWL_EXSTYLE = -20, SW_SHOWNOACTIVATE = 4, SW_HIDE = 0, HWND_TOPMOST = -1, SWP_NOMOVE = 2, SWP_NOSIZE = 1, SWP_NOACTIVATE = 16, SWP_SHOWWINDOW = 64;
    internal const uint WM_NULL = 0, WM_DESTROY = 2, WM_CLOSE = 16, WM_NCHITTEST = 132, WM_LBUTTONDOWN = 513, WM_MOUSEMOVE = 512, WM_LBUTTONUP = 514, WM_CAPTURECHANGED = 533, WM_HOTKEY = 786, WM_APP = 0x8000, WM_OVERLAY_CHANGED = WM_APP + 1, WM_TRAY = WM_APP + 2;
    internal const nint HTTRANSPARENT = -1, HTCLIENT = 1;
    [StructLayout(LayoutKind.Sequential)] internal struct POINT { public int x, y; }
    [StructLayout(LayoutKind.Sequential)] internal struct SIZE { public int cx, cy; }
    [StructLayout(LayoutKind.Sequential)] internal struct MSG { public nint hwnd; public uint message; public nuint wParam; public nint lParam; public uint time; public POINT pt; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] internal struct WNDCLASSEX { public uint cbSize, style; public nint lpfnWndProc; public int cbClsExtra, cbWndExtra; public nint hInstance, hIcon, hCursor, hbrBackground; public string? lpszMenuName, lpszClassName; public nint hIconSm; }
    [StructLayout(LayoutKind.Sequential)] internal struct BLENDFUNCTION { public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat; }
    [StructLayout(LayoutKind.Sequential)] internal struct BITMAPINFOHEADER { public uint biSize; public int biWidth, biHeight; public ushort biPlanes, biBitCount; public uint biCompression, biSizeImage; public int biXPelsPerMeter, biYPelsPerMeter; public uint biClrUsed, biClrImportant; }
    [StructLayout(LayoutKind.Sequential)] internal struct BITMAPINFO { public BITMAPINFOHEADER bmiHeader; }
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] internal delegate nint WndProc(nint hwnd, uint msg, nuint wp, nint lp);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] internal static extern ushort RegisterClassEx(ref WNDCLASSEX wc);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] internal static extern nint CreateWindowEx(uint ex, string cls, string name, uint style, int x, int y, int w, int h, nint parent, nint menu, nint instance, nint param);
    [DllImport("user32.dll", SetLastError = true)] internal static extern bool DestroyWindow(nint h);
    [DllImport("user32.dll", SetLastError = true)] internal static extern int GetMessage(out MSG msg, nint h, uint min, uint max);
    [DllImport("user32.dll")] internal static extern bool TranslateMessage(ref MSG msg);
    [DllImport("user32.dll")] internal static extern nint DispatchMessage(ref MSG msg);
    [DllImport("user32.dll")] internal static extern void PostQuitMessage(int code);
    [DllImport("user32.dll", SetLastError = true)] internal static extern bool PostMessage(nint h, uint msg, nuint wp, nint lp);
    [DllImport("user32.dll", SetLastError = true)] internal static extern bool SetForegroundWindow(nint hWnd);
    [DllImport("user32.dll", SetLastError = true)] internal static extern bool RegisterHotKey(nint hWnd, int id, uint modifiers, uint virtualKey);
    [DllImport("user32.dll", SetLastError = true)] internal static extern bool UnregisterHotKey(nint hWnd, int id);
    [DllImport("user32.dll", SetLastError = true)] internal static extern bool ShowWindow(nint h, int cmd);
    [DllImport("user32.dll", SetLastError = true)] internal static extern bool SetWindowPos(nint h, nint after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll", SetLastError = true)] internal static extern nint GetWindowLongPtr(nint h, int index);
    [DllImport("user32.dll", SetLastError = true)] internal static extern nint SetWindowLongPtr(nint h, int index, nint value);
    [DllImport("user32.dll")] internal static extern bool GetCursorPos(out POINT p);
    [DllImport("user32.dll")] internal static extern bool ReleaseCapture();
    [DllImport("user32.dll")] internal static extern nint SetCapture(nint hWnd);
    [DllImport("user32.dll")] internal static extern nint DefWindowProc(nint h, uint m, nuint w, nint l);
    [DllImport("user32.dll", SetLastError = true)] internal static extern bool UpdateLayeredWindow(nint h, nint dst, ref POINT pt, ref SIZE size, nint src, ref POINT srcPt, uint key, ref BLENDFUNCTION blend, uint flags);
    [DllImport("user32.dll", SetLastError = true)] internal static extern nint CreatePopupMenu();
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)] internal static extern bool AppendMenu(nint menu, uint flags, nuint id, string text);
    [DllImport("user32.dll", SetLastError = true)] internal static extern uint TrackPopupMenu(nint menu, uint flags, int x, int y, int r, nint h, nint rect);
    [DllImport("user32.dll")] internal static extern bool DestroyMenu(nint menu);
    [DllImport("gdi32.dll", SetLastError = true)] internal static extern nint CreateCompatibleDC(nint dc);
    [DllImport("gdi32.dll", SetLastError = true)] internal static extern bool DeleteDC(nint dc);
    [DllImport("gdi32.dll", SetLastError = true)] internal static extern nint SelectObject(nint dc, nint o);
    [DllImport("gdi32.dll", SetLastError = true)] internal static extern bool DeleteObject(nint o);
    [DllImport("gdi32.dll", SetLastError = true)] internal static extern nint CreateDIBSection(nint dc, ref BITMAPINFO info, uint usage, out nint bits, nint sec, uint off);
    [DllImport("gdi32.dll", CharSet = CharSet.Unicode, SetLastError = true)] internal static extern nint CreateFont(int h, int w, int esc, int ori, int weight, uint italic, uint underline, uint strike, uint charset, uint outp, uint clip, uint quality, uint pitch, string face);
    [DllImport("gdi32.dll")] internal static extern uint SetTextColor(nint dc, uint color);
    [DllImport("gdi32.dll")] internal static extern int SetBkMode(nint dc, int mode);
    [DllImport("gdi32.dll", CharSet = CharSet.Unicode, SetLastError = true)] internal static extern bool TextOut(nint dc, int x, int y, string text, int len);
    [DllImport("gdi32.dll", CharSet = CharSet.Unicode, SetLastError = true)] internal static extern bool GetTextExtentPoint32(nint dc, string text, int length, out SIZE size);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)] internal static extern bool Shell_NotifyIcon(uint msg, ref global::OverlayMonitor.Tray.NOTIFYICONDATA data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] internal static extern nint LoadImage(nint instance, string name, uint type, int desiredWidth, int desiredHeight, uint loadFlags);
    [DllImport("user32.dll", SetLastError = true)] internal static extern bool DestroyIcon(nint hIcon);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] internal static extern uint RegisterWindowMessage(string message);
}
