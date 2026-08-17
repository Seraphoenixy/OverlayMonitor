using System.Runtime.InteropServices;
using System.Reflection;
using OverlayMonitor.Models;
using OverlayMonitor.Window;

namespace OverlayMonitor.Tray;
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1028:EnumStorageShouldBeInt32")]
public enum OverlayProfile : byte { Custom, Supplement, Full, Temperature }
public sealed record TrayMenuState(bool Visible, bool Moving, bool AutoStart, OverlayProfile Profile, OverlayTheme Theme, int SampleIntervalMs);
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] public struct NOTIFYICONDATA { public uint cbSize; public nint hWnd; public uint uID, uFlags, uCallbackMessage; public nint hIcon; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szTip; public uint dwState, dwStateMask; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string szInfo; public uint uTimeoutOrVersion; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string szInfoTitle; public uint dwInfoFlags; public Guid guidItem; public nint hBalloonIcon; }
public sealed class TrayIcon : IDisposable
{
    private NOTIFYICONDATA _data;
    public event Action<uint>? Command;
    public Func<TrayMenuState>? StateProvider { get; set; }
    public TrayIcon(nint hwnd) { var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "OverlayMonitor.ico"); var icon = NativeMethods.LoadImage(0, iconPath, NativeMethods.IMAGE_ICON, 32, 32, NativeMethods.LR_LOADFROMFILE); if (icon == 0) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), $"无法加载托盘图标：{iconPath}"); _data = new() { cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATA>(), hWnd = hwnd, uID = 1, uFlags = NativeMethods.NIF_MESSAGE | NativeMethods.NIF_ICON | NativeMethods.NIF_TIP, uCallbackMessage = NativeMethods.WM_TRAY, hIcon = icon, szTip = "OverlayMonitor" }; AddToNotificationArea(); }
    public void Handle(nint lParam) { if ((uint)lParam == NativeMethods.WM_RBUTTONUP) ShowMenu(); }
    private void ShowMenu()
    {
        var state = StateProvider?.Invoke() ?? new(true, false, false, OverlayProfile.Custom, OverlayTheme.AdaptiveOutline, 1000);
        var menu = NativeMethods.CreatePopupMenu();
        try
        {
            Add(menu, 1, "显示/隐藏", state.Visible);
            Add(menu, 2, "移动模式", state.Moving);
            NativeMethods.AppendMenu(menu, NativeMethods.MF_SEPARATOR, 0, "");
            Add(menu, 3, "NVIDIA 补充模式", state.Profile == OverlayProfile.Supplement);
            Add(menu, 4, "完整模式", state.Profile == OverlayProfile.Full);
            Add(menu, 5, "温度模式", state.Profile == OverlayProfile.Temperature);
            NativeMethods.AppendMenu(menu, NativeMethods.MF_SEPARATOR, 0, "");
            Add(menu, 9, "主题：自适应描边", state.Theme == OverlayTheme.AdaptiveOutline);
            Add(menu, 10, "主题：轻量底板", state.Theme == OverlayTheme.OriginalWhite);
            var rate = NativeMethods.CreatePopupMenu();
            Add(rate, 11, "500 毫秒", state.SampleIntervalMs == 500);
            Add(rate, 12, "1 秒", state.SampleIntervalMs == 1000);
            Add(rate, 13, "2 秒", state.SampleIntervalMs == 2000);
            Add(rate, 14, "5 秒", state.SampleIntervalMs == 5000);
            NativeMethods.AppendMenu(menu, NativeMethods.MF_POPUP, (nuint)rate, "刷新频率");
            NativeMethods.AppendMenu(menu, NativeMethods.MF_SEPARATOR, 0, "");
            Add(menu, 6, "开机自启动", state.AutoStart);
            Add(menu, 7, "重新加载配置");
            Add(menu, 8, "退出");
            NativeMethods.AppendMenu(menu, NativeMethods.MF_SEPARATOR, 0, "");
            AddDisabled(menu, $"OverlayMonitor v{GetVersion()}");
            NativeMethods.GetCursorPos(out var p);
            NativeMethods.SetForegroundWindow(_data.hWnd);
            var id = NativeMethods.TrackPopupMenu(menu, NativeMethods.TPM_RETURNCMD, p.x, p.y, 0, _data.hWnd, 0);
            if (id != 0) Command?.Invoke(id);
            NativeMethods.PostMessage(_data.hWnd, NativeMethods.WM_NULL, 0, 0);
        }
        finally { NativeMethods.DestroyMenu(menu); }
    }
    private static void Add(nint menu, uint id, string text, bool isChecked = false) => NativeMethods.AppendMenu(menu, isChecked ? NativeMethods.MF_CHECKED : 0u, id, text);
    private static void AddSeparator(nint menu) => NativeMethods.AppendMenu(menu, NativeMethods.MF_SEPARATOR, 0, "");
    private static void AddDisabled(nint menu, string text) => NativeMethods.AppendMenu(menu, NativeMethods.MF_GRAYED, 0, text);
    private static string GetVersion() => Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "未知";
    public void Restore() => AddToNotificationArea();
    private void AddToNotificationArea() { if (!NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_ADD, ref _data)) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "无法添加托盘图标。"); }
    public void Dispose() { NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_DELETE, ref _data); if (_data.hIcon != 0) NativeMethods.DestroyIcon(_data.hIcon); }
}
