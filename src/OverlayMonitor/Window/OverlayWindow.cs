using System.ComponentModel;
using System.Runtime.InteropServices;
using OverlayMonitor.Configuration;
using OverlayMonitor.Models;
using OverlayMonitor.Rendering;
using OverlayMonitor.Tray;

namespace OverlayMonitor.Window;
public sealed class OverlayWindow : IDisposable
{
    private const string ClassName = "OverlayMonitor.NativeWindow"; private const int ToggleHotKeyId = 1; private readonly NativeMethods.WndProc _proc; private readonly ConfigService _configService; private readonly StartupService _startupService = new(); private readonly LayeredRenderer _renderer = new(); private readonly uint _taskbarCreatedMessage = NativeMethods.RegisterWindowMessage("TaskbarCreated"); private OverlayConfig _config; private nint _hwnd; private TrayIcon? _tray; private string _text = ""; private int _renderWidth = 510; private bool _moving, _dragging, _hotKeyRegistered; private NativeMethods.POINT _dragStart; private int _windowX, _windowY;
    public nint Handle => _hwnd;
    public OverlayConfig Config => _config;
    public OverlayWindow(ConfigService service, OverlayConfig config) { _configService = service; _config = config; _proc = WndProc; }
    public void Create()
    {
        var instance = Marshal.GetHINSTANCE(typeof(OverlayWindow).Module); var wc = new NativeMethods.WNDCLASSEX { cbSize = (uint)Marshal.SizeOf<NativeMethods.WNDCLASSEX>(), lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_proc), hInstance = instance, lpszClassName = ClassName };
        if (NativeMethods.RegisterClassEx(ref wc) == 0) { var e = Marshal.GetLastWin32Error(); if (e != 1410) throw new Win32Exception(e); }
        _hwnd = NativeMethods.CreateWindowEx(Style(), ClassName, "OverlayMonitor", NativeMethods.WS_POPUP, _config.X, _config.Y, 510, 42, 0, 0, instance, 0); if (_hwnd == 0) throw new Win32Exception(Marshal.GetLastWin32Error()); _tray = new TrayIcon(_hwnd); _tray.Command += OnCommand; _tray.StateProvider = GetTrayState; _hotKeyRegistered = NativeMethods.RegisterHotKey(_hwnd, ToggleHotKeyId, 1, 0x45); if (!_hotKeyRegistered) AppLog.Error("Alt+E 全局热键注册失败。", new Win32Exception(Marshal.GetLastWin32Error())); if (_config.Visible) NativeMethods.ShowWindow(_hwnd, NativeMethods.SW_SHOWNOACTIVATE);
    }
    private uint Style() => NativeMethods.WS_EX_TOPMOST | NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_LAYERED | (!_moving ? NativeMethods.WS_EX_TRANSPARENT : 0);
    public void Render(string text, bool force = false) { if (!force && text == _text) return; _text = text; var right = _config.X + _renderWidth; _config.X = _renderer.Draw(_hwnd, text, right, _config.Y, _moving, _config.Theme, out _renderWidth); }
    private nint WndProc(nint h, uint msg, nuint wp, nint lp)
    {
        if (msg == _taskbarCreatedMessage) { try { _tray?.Restore(); } catch (Exception ex) { AppLog.Error("恢复托盘图标失败。", ex); } return 0; }
        if (msg == NativeMethods.WM_NCHITTEST) return !_moving ? NativeMethods.HTTRANSPARENT : NativeMethods.HTCLIENT;
        if (msg == NativeMethods.WM_HOTKEY && (int)wp == ToggleHotKeyId) { ToggleVisibility(); return 0; }
        if (msg == NativeMethods.WM_LBUTTONDOWN && _moving) { NativeMethods.GetCursorPos(out _dragStart); _windowX = _config.X; _windowY = _config.Y; NativeMethods.SetCapture(h); _dragging = true; return 0; }
        if (msg == NativeMethods.WM_MOUSEMOVE && _dragging) { NativeMethods.GetCursorPos(out var now); _config.X = _windowX + now.x - _dragStart.x; _config.Y = _windowY + now.y - _dragStart.y; NativeMethods.SetWindowPos(h, NativeMethods.HWND_TOPMOST, _config.X, _config.Y, 0, 0, NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE); return 0; }
        if (msg == NativeMethods.WM_LBUTTONUP && _dragging) { _dragging = false; NativeMethods.ReleaseCapture(); _configService.Save(_config); return 0; }
        if (msg == NativeMethods.WM_CAPTURECHANGED && _dragging) { _dragging = false; _configService.Save(_config); return 0; }
        if (msg == NativeMethods.WM_TRAY) { _tray?.Handle(lp); return 0; }
        if (msg == NativeMethods.WM_CLOSE) { NativeMethods.DestroyWindow(h); return 0; }
        if (msg == NativeMethods.WM_DESTROY) { if (_hotKeyRegistered) { NativeMethods.UnregisterHotKey(h, ToggleHotKeyId); _hotKeyRegistered = false; } NativeMethods.PostQuitMessage(0); return 0; }
        return NativeMethods.DefWindowProc(h, msg, wp, lp);
    }
    private void OnCommand(uint id) { switch (id) { case 1: ToggleVisibility(); return; case 2: _moving = !_moving; ApplyStyle(); if (_text.Length > 0) Render(_text, true); break; case 3: SetMetrics("cpuTemp,gpuTemp,download,upload", true); break; case 4: SetMetrics("cpuTemp,gpuTemp,cpuLoad,gpuLoad,download,upload", true); break; case 5: SetMetrics("cpuTemp,gpuTemp", false); break; case 6: ToggleAutoStart(); return; case 7: _config = _configService.Load(); ApplyStyle(); if (_text.Length > 0) Render(_text, true); break; case 8: NativeMethods.PostMessage(_hwnd, NativeMethods.WM_CLOSE, 0, 0); return; case 9: _config.Theme = OverlayTheme.AdaptiveOutline; break; case 10: _config.Theme = OverlayTheme.OriginalWhite; break; } if (_text.Length > 0) Render(_text, true); _configService.Save(_config); }
    private void ToggleVisibility() { _config.Visible = !_config.Visible; NativeMethods.ShowWindow(_hwnd, _config.Visible ? NativeMethods.SW_SHOWNOACTIVATE : NativeMethods.SW_HIDE); _configService.Save(_config); }
    private void SetMetrics(string ids, bool showMemoryLoad) { var enabled = ids.Split(','); foreach (var m in _config.Metrics) m.Enabled = enabled.Contains(m.Id); _config.ShowMemoryLoad = showMemoryLoad; }
    private void ToggleAutoStart() { try { _startupService.SetEnabled(!_startupService.IsEnabled()); } catch (Exception ex) { AppLog.Error("更新开机自启动设置失败。", ex); } }
    private TrayMenuState GetTrayState() { var autoStart = false; try { autoStart = _startupService.IsEnabled(); } catch (Exception ex) { AppLog.Error("读取开机自启动设置失败。", ex); } return new(_config.Visible, _moving, autoStart, GetProfile(), _config.Theme); }
    private OverlayProfile GetProfile()
    {
        var enabled = _config.Metrics.Where(m => m.Enabled).Select(m => m.Id).OrderBy(id => id).ToArray();
        return (string.Join(',', enabled), _config.ShowMemoryLoad) switch
        {
            ("cpuTemp,download,gpuTemp,upload", true) => OverlayProfile.Supplement,
            ("cpuLoad,cpuTemp,download,gpuLoad,gpuTemp,upload", true) => OverlayProfile.Full,
            ("cpuTemp,gpuTemp", false) => OverlayProfile.Temperature,
            _ => OverlayProfile.Custom
        };
    }
    private void ApplyStyle() { NativeMethods.SetWindowLongPtr(_hwnd, NativeMethods.GWL_EXSTYLE, (nint)Style()); NativeMethods.SetWindowPos(_hwnd, NativeMethods.HWND_TOPMOST, _config.X, _config.Y, 0, 0, NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW); }
    public void Dispose() { if (_hotKeyRegistered && _hwnd != 0) NativeMethods.UnregisterHotKey(_hwnd, ToggleHotKeyId); _tray?.Dispose(); _renderer.Dispose(); if (_hwnd != 0) NativeMethods.DestroyWindow(_hwnd); }
}
