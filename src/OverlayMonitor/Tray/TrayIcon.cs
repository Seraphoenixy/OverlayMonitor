using System.Runtime.InteropServices;
using System.Reflection;
using OverlayMonitor.Window;

namespace OverlayMonitor.Tray;
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1028:EnumStorageShouldBeInt32")]
public enum OverlayProfile : byte { Custom, Supplement, Full, Temperature }
public sealed record TrayMenuState(bool Visible, bool Moving, bool AutoStart, OverlayProfile Profile);
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] public struct NOTIFYICONDATA { public uint cbSize; public nint hWnd; public uint uID, uFlags, uCallbackMessage; public nint hIcon; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szTip; public uint dwState, dwStateMask; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string szInfo; public uint uTimeoutOrVersion; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string szInfoTitle; public uint dwInfoFlags; public Guid guidItem; public nint hBalloonIcon; }
public sealed class TrayIcon : IDisposable
{
    private NOTIFYICONDATA _data;
    public event Action<uint>? Command;
    public Func<TrayMenuState>? StateProvider { get; set; }
    public TrayIcon(nint hwnd) { var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "OverlayMonitor.ico"); var icon = NativeMethods.LoadImage(0, iconPath, 1, 32, 32, 0x10); if (icon == 0) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), $"无法加载托盘图标：{iconPath}"); _data = new() { cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATA>(), hWnd = hwnd, uID = 1, uFlags = 1 | 2 | 4, uCallbackMessage = NativeMethods.WM_TRAY, hIcon = icon, szTip = "OverlayMonitor" }; AddToNotificationArea(); }
    public void Handle(nint lParam) { if ((uint)lParam == 0x205) ShowMenu(); }
    private void ShowMenu() { var state = StateProvider?.Invoke() ?? new(true, false, false, OverlayProfile.Custom); var menu = NativeMethods.CreatePopupMenu(); try { Add(menu, 1, "显示/隐藏", state.Visible); Add(menu, 2, "移动模式", state.Moving); AddSeparator(menu); Add(menu, 3, "NVIDIA 补充模式", state.Profile == OverlayProfile.Supplement); Add(menu, 4, "完整模式", state.Profile == OverlayProfile.Full); Add(menu, 5, "温度模式", state.Profile == OverlayProfile.Temperature); AddSeparator(menu); Add(menu, 6, "开机自启动", state.AutoStart); Add(menu, 7, "重新加载配置"); Add(menu, 8, "退出"); AddSeparator(menu); AddDisabled(menu, $"OverlayMonitor v{GetVersion()}"); NativeMethods.GetCursorPos(out var p); NativeMethods.SetForegroundWindow(_data.hWnd); var id = NativeMethods.TrackPopupMenu(menu, 0x100, p.x, p.y, 0, _data.hWnd, 0); if (id != 0) Command?.Invoke(id); NativeMethods.PostMessage(_data.hWnd, NativeMethods.WM_NULL, 0, 0); } finally { NativeMethods.DestroyMenu(menu); } }
    private static void Add(nint menu, uint id, string text, bool isChecked = false) => NativeMethods.AppendMenu(menu, isChecked ? 0x8u : 0u, id, text);
    private static void AddSeparator(nint menu) => NativeMethods.AppendMenu(menu, 0x800, 0, "");
    private static void AddDisabled(nint menu, string text) => NativeMethods.AppendMenu(menu, 0x1, 0, text);
    private static string GetVersion() => Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "未知";
    public void Restore() => AddToNotificationArea();
    private void AddToNotificationArea() { if (!NativeMethods.Shell_NotifyIcon(0, ref _data)) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "无法添加托盘图标。"); }
    public void Dispose() { NativeMethods.Shell_NotifyIcon(2, ref _data); if (_data.hIcon != 0) NativeMethods.DestroyIcon(_data.hIcon); }
}
