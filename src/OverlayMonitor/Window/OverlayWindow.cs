using System.ComponentModel;
using System.Runtime.InteropServices;
using OverlayMonitor.Configuration;
using OverlayMonitor.Models;
using OverlayMonitor.Rendering;
using OverlayMonitor.Tray;

namespace OverlayMonitor.Window;

public sealed class OverlayWindow : IDisposable
{
    private const string ClassName = "OverlayMonitor.NativeWindow";
    private const int ToggleHotKeyId = 1;
    private const nuint HealthTimer = 1, RecoveryTimer = 2;
    private readonly List<nint> _hooks = [];
    private readonly NativeMethods.WinEventProc _eventProc;
    private long _lastRecovery, _lastCheck, _nextRenderRetry, _lastRecoveryLog = -30000;
    private int _renderFailures, _renderHeight = 42;
    private bool _renderDirty = true, _recoveryPending, _rendering, _renderAgain;

    private readonly NativeMethods.WndProc _proc;
    private readonly ConfigService _configService;
    private readonly StartupService _startupService = new();
    private readonly LayeredRenderer _renderer = new();
    private readonly uint _taskbarCreatedMessage = NativeMethods.RegisterWindowMessage("TaskbarCreated");

    private OverlayConfig _config;
    private nint _hwnd;
    private TrayIcon? _tray;
    private string _text = "";
    private int _renderWidth = 510;
    private bool _moving, _dragging, _hotKeyRegistered;
    private NativeMethods.POINT _dragStart;
    private int _windowX, _windowY;

    public nint Handle => _hwnd;
    public OverlayConfig Config => _config;

    public OverlayWindow(ConfigService service, OverlayConfig config) { _configService = service; _config = config; _proc = WndProc; _eventProc = OnWindowEvent; }

    public void Create() => Create(createTrayIcon: true);

    internal void Create(bool createTrayIcon)
    {
        var instance = Marshal.GetHINSTANCE(typeof(OverlayWindow).Module);
        var wc = new NativeMethods.WNDCLASSEX { cbSize = (uint)Marshal.SizeOf<NativeMethods.WNDCLASSEX>(), lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_proc), hInstance = instance, lpszClassName = ClassName };
        if (NativeMethods.RegisterClassEx(ref wc) == 0) { var e = Marshal.GetLastWin32Error(); if (e != 1410) throw new Win32Exception(e); }
        ClampToWorkArea();
        _hwnd = NativeMethods.CreateWindowEx(Style(), ClassName, "OverlayMonitor", NativeMethods.WS_POPUP, _config.X, _config.Y, 510, 42, 0, 0, instance, 0);
        if (_hwnd == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
        if (createTrayIcon)
        {
            _tray = new TrayIcon(_hwnd);
            _tray.Command += OnCommand;
            _tray.StateProvider = GetTrayState;
        }
        _hotKeyRegistered = NativeMethods.RegisterHotKey(_hwnd, ToggleHotKeyId, 1, 0x45);
        if (!_hotKeyRegistered) AppLog.Error("Alt+E 全局热键注册失败。", new Win32Exception(Marshal.GetLastWin32Error()));
        _text = "正在初始化监控...";
        ApplyVisibility();
        RegisterRecoveryHook(NativeMethods.EVENT_SYSTEM_FOREGROUND, NativeMethods.EVENT_SYSTEM_FOREGROUND);
        RegisterRecoveryHook(NativeMethods.EVENT_OBJECT_SHOW, NativeMethods.EVENT_OBJECT_SHOW);
        RegisterRecoveryHook(NativeMethods.EVENT_OBJECT_REORDER, NativeMethods.EVENT_OBJECT_REORDER);
        if (NativeMethods.SetTimer(_hwnd, HealthTimer, 3000, 0) == 0)
            AppLog.Error("创建窗口状态检查定时器失败。", new Win32Exception(Marshal.GetLastWin32Error()));
    }

    private uint Style() => NativeMethods.WS_EX_TOPMOST | NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_LAYERED | (!_moving ? NativeMethods.WS_EX_TRANSPARENT : 0);

    public void Render(string text, bool force = false)
    {
        if (text != _text) _renderDirty = true;
        _text = text;
        if (!_config.Visible) { _renderDirty = true; return; }
        if (_rendering) { _renderAgain = true; return; }
        if (!force && (!_renderDirty || Environment.TickCount64 < _nextRenderRetry)) return;
        _rendering = true;
        _renderAgain = false;
        try
        {
            _renderer.Draw(_hwnd, text, _moving, _config.Theme, PlaceRenderedWindow, out _renderWidth);
            _renderDirty = _renderAgain;
            if (_renderFailures > 0) AppLog.Info("悬浮窗渲染已恢复。");
            _renderFailures = 0;
            _nextRenderRetry = 0;
        }
        catch (Exception ex)
        {
            _renderDirty = true;
            _renderFailures++;
            _nextRenderRetry = Environment.TickCount64 + Math.Min(30000, 1000L * _renderFailures);
            if (_renderFailures == 1 || _renderFailures % 10 == 0) AppLog.Error("悬浮窗渲染失败，将退避重试。", ex);
        }
        finally { _rendering = false; }
    }

    private NativeMethods.POINT PlaceRenderedWindow(int width, int height)
    {
        _renderWidth = width; _renderHeight = height;
        ClampToWorkArea();
        return new() { x = _config.X, y = _config.Y };
    }

    private void RegisterRecoveryHook(uint min, uint max)
    {
        var hook = NativeMethods.SetWinEventHook(min, max, 0, _eventProc, 0, 0, NativeMethods.WINEVENT_SKIPOWNPROCESS);
        if (hook != 0) _hooks.Add(hook);
        else AppLog.Info("窗口事件监听注册失败，使用低频状态检查兜底。");
    }

    private void OnWindowEvent(nint hook, uint evt, nint hwnd, int objectId, int childId, uint thread, uint time)
    {
        // Out-of-context callbacks run on this window's message thread.
        if (!_config.Visible || _dragging || _recoveryPending || hwnd == 0 || hwnd == _hwnd) return;
        if (evt != NativeMethods.EVENT_SYSTEM_FOREGROUND &&
            (evt != NativeMethods.EVENT_OBJECT_SHOW && evt != NativeMethods.EVENT_OBJECT_REORDER || objectId != 0 || childId != 0 || NativeMethods.GetAncestor(hwnd, 2) != hwnd)) return;
        _recoveryPending = NativeMethods.SetTimer(_hwnd, RecoveryTimer, (uint)Math.Max(150, 1000 - (Environment.TickCount64 - Math.Max(_lastCheck, _lastRecovery))), 0) != 0;
    }

    private void CheckWindowState()
    {
        if (!_config.Visible || _dragging) return;
        _lastCheck = Environment.TickCount64;
        ClampToWorkArea();
        var visible = NativeMethods.IsWindowVisible(_hwnd);
        var topmost = (NativeMethods.GetWindowLongPtr(_hwnd, NativeMethods.GWL_EXSTYLE).ToInt64() & NativeMethods.WS_EX_TOPMOST) != 0;
        if (!NativeMethods.GetWindowRect(_hwnd, out var rect)) return;
        var misplaced = rect.left != _config.X || rect.top != _config.Y;
        var minimized = NativeMethods.IsIconic(_hwnd);
        if (!visible || minimized || !topmost || misplaced || HasOverlappingWindowAbove(rect))
        {
            if (Environment.TickCount64 - _lastRecovery >= 1000)
            {
                _lastRecovery = Environment.TickCount64;
                if (minimized) NativeMethods.ShowWindow(_hwnd, NativeMethods.SW_SHOWNOACTIVATE);
                var recovered = NativeMethods.SetWindowPos(_hwnd, NativeMethods.HWND_TOPMOST, _config.X, _config.Y, 0, 0,
                    NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);
                if (_lastRecovery - _lastRecoveryLog >= 30000)
                {
                    _lastRecoveryLog = _lastRecovery;
                    if (recovered) AppLog.Info($"恢复悬浮窗：可见={visible}，最小化={minimized}，置顶={topmost}，位置偏移={misplaced}。");
                    else AppLog.Error("恢复悬浮窗位置和层级失败。", new Win32Exception(Marshal.GetLastWin32Error()));
                }
            }
        }
        if (_renderDirty) Render(_text);
    }

    private bool HasOverlappingWindowAbove(NativeMethods.RECT rect)
    {
        // Bound traversal so a busy desktop cannot turn recovery into an expensive scan.
        var other = NativeMethods.GetWindow(_hwnd, NativeMethods.GW_HWNDPREV);
        for (var i = 0; other != 0 && i < 128; i++, other = NativeMethods.GetWindow(other, NativeMethods.GW_HWNDPREV))
        {
            if (!NativeMethods.IsWindowVisible(other) || NativeMethods.IsIconic(other)) continue;
            if (NativeMethods.DwmGetWindowAttribute(other, 14, out var cloaked, 4) == 0 && cloaked != 0) continue;
            if (NativeMethods.GetWindowRect(other, out var r) && r.left < rect.right && r.right > rect.left && r.top < rect.bottom && r.bottom > rect.top) return true;
        }
        return false;
    }

    private void ApplyVisibility()
    {
        if (_config.Visible) Render(_text, true);
        NativeMethods.ShowWindow(_hwnd, _config.Visible ? NativeMethods.SW_SHOWNOACTIVATE : NativeMethods.SW_HIDE);
        if (_config.Visible) CheckWindowState();
    }

    private nint WndProc(nint h, uint msg, nuint wp, nint lp)
    {
        if (msg == _taskbarCreatedMessage)
        {
            try { _tray?.Restore(); } catch (Exception ex) { AppLog.Error("恢复托盘图标失败。", ex); }
            _renderDirty = true;
            CheckWindowState();
            return 0;
        }
        if (msg == NativeMethods.WM_TIMER && (wp == HealthTimer || wp == RecoveryTimer))
        {
            if (wp == RecoveryTimer) { NativeMethods.KillTimer(h, RecoveryTimer); _recoveryPending = false; }
            CheckWindowState();
            return 0;
        }
        if (msg == NativeMethods.WM_NCHITTEST)
            return !_moving ? NativeMethods.HTTRANSPARENT : NativeMethods.HTCLIENT;
        if (msg == NativeMethods.WM_WINDOWPOSCHANGING)
        {
            unsafe
            {
                var pos = (NativeMethods.WINDOWPOS*)(void*)lp;
                if (_config.Visible && (pos->flags & NativeMethods.SWP_NOZORDER) == 0 && !IsInsertAfterTopmost(pos->hwndInsertAfter))
                    pos->hwndInsertAfter = NativeMethods.HWND_TOPMOST;
            }
            return 0;
        }
        if (msg == NativeMethods.WM_DISPLAYCHANGE || msg == NativeMethods.WM_DPICHANGED || msg == NativeMethods.WM_SETTINGCHANGE)
        {
            ClampToWorkArea();
            _renderDirty = true;
            Render(_text);
            CheckWindowState();
            return 0;
        }
        if (msg == NativeMethods.WM_HOTKEY && (int)wp == ToggleHotKeyId)
        {
            ToggleVisibility();
            return 0;
        }
        if (msg == NativeMethods.WM_LBUTTONDOWN && _moving)
        {
            NativeMethods.GetCursorPos(out _dragStart);
            _windowX = _config.X;
            _windowY = _config.Y;
            NativeMethods.SetCapture(h);
            _dragging = true;
            return 0;
        }
        if (msg == NativeMethods.WM_MOUSEMOVE && _dragging)
        {
            NativeMethods.GetCursorPos(out var now);
            _config.X = _windowX + now.x - _dragStart.x;
            _config.Y = _windowY + now.y - _dragStart.y;
            NativeMethods.SetWindowPos(h, NativeMethods.HWND_TOPMOST, _config.X, _config.Y, 0, 0, NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
            return 0;
        }
        if (msg == NativeMethods.WM_LBUTTONUP && _dragging)
        {
            _dragging = false;
            NativeMethods.ReleaseCapture();
            _configService.Save(_config);
            if (_text.Length > 0) Render(_text, true);
            return 0;
        }
        if (msg == NativeMethods.WM_CAPTURECHANGED && _dragging)
        {
            _dragging = false;
            _configService.Save(_config);
            return 0;
        }
        if (msg == NativeMethods.WM_TRAY)
        {
            _tray?.Handle(lp);
            return 0;
        }
        if (msg == NativeMethods.WM_CLOSE)
        {
            NativeMethods.DestroyWindow(h);
            return 0;
        }
        if (msg == NativeMethods.WM_DESTROY)
        {
            StopRecovery();
            if (_hotKeyRegistered) { NativeMethods.UnregisterHotKey(h, ToggleHotKeyId); _hotKeyRegistered = false; }
            NativeMethods.PostQuitMessage(0);
            return 0;
        }
        return NativeMethods.DefWindowProc(h, msg, wp, lp);
    }

    private static bool IsInsertAfterTopmost(nint insertAfter)
    {
        if (insertAfter == NativeMethods.HWND_TOPMOST) return true;
        return (NativeMethods.GetWindowLongPtr(insertAfter, NativeMethods.GWL_EXSTYLE).ToInt64() & NativeMethods.WS_EX_TOPMOST) != 0;
    }

    private void ClampToWorkArea()
    {
        var monitor = NativeMethods.MonitorFromPoint(new NativeMethods.POINT { x = _config.X, y = _config.Y }, NativeMethods.MONITOR_DEFAULTTONEAREST);
        if (monitor == 0) return;
        var info = new NativeMethods.MONITORINFO { cbSize = (uint)Marshal.SizeOf<NativeMethods.MONITORINFO>() };
        if (!NativeMethods.GetMonitorInfo(monitor, ref info)) return;
        var (x, y) = ClampPosition(_config.X, _config.Y, _renderWidth, _renderHeight, info.rcWork);
        if (x == _config.X && y == _config.Y) return;
        _config.X = x;
        _config.Y = y;
        _configService.Save(_config);
    }

    internal static (int X, int Y) ClampPosition(int x, int y, int width, int height, NativeMethods.RECT work)
    {
        // If the text is wider than the monitor, keep its right-aligned end visible.
        var right = work.right - width;
        return (width > work.right - work.left ? right : Math.Clamp(x, work.left, right),
            Math.Clamp(y, work.top, Math.Max(work.top, work.bottom - height)));
    }

    private void OnCommand(uint id)
    {
        switch (id)
        {
            case 1: ToggleVisibility(); return;
            case 2: _moving = !_moving; ApplyStyle(); _configService.Save(_config); return;
            case 3: SetMetrics("cpuTemp,gpuTemp,download,upload", true); break;
            case 4: SetMetrics("cpuTemp,gpuTemp,cpuLoad,gpuLoad,download,upload", true); break;
            case 5: SetMetrics("cpuTemp,gpuTemp", false); break;
            case 6: ToggleAutoStart(); return;
            case 7: _config = _configService.Load(); ApplyStyle(); return;
            case 8: NativeMethods.PostMessage(_hwnd, NativeMethods.WM_CLOSE, 0, 0); return;
            case 9: _config.Theme = OverlayTheme.AdaptiveOutline; break;
            case 10: _config.Theme = OverlayTheme.OriginalWhite; break;
            case 11: _config.SampleIntervalMs = 500; break;
            case 12: _config.SampleIntervalMs = 1000; break;
            case 13: _config.SampleIntervalMs = 2000; break;
            case 14: _config.SampleIntervalMs = 5000; break;
        }
        if (_text.Length > 0) Render(_text, true);
        _configService.Save(_config);
    }

    private void ToggleVisibility()
    {
        _config.Visible = !_config.Visible;
        ApplyVisibility();
        _configService.Save(_config);
    }

    private void SetMetrics(string ids, bool showMemoryLoad)
    {
        var enabled = ids.Split(',');
        foreach (var m in _config.Metrics) m.Enabled = enabled.Contains(m.Id);
        _config.ShowMemoryLoad = showMemoryLoad;
    }

    private void ToggleAutoStart()
    {
        try { _startupService.SetEnabled(!_startupService.IsEnabled()); }
        catch (Exception ex) { AppLog.Error("更新开机自启动设置失败。", ex); }
    }

    private TrayMenuState GetTrayState()
    {
        var autoStart = false;
        try { autoStart = _startupService.IsEnabled(); }
        catch (Exception ex) { AppLog.Error("读取开机自启动设置失败。", ex); }
        return new(_config.Visible, _moving, autoStart, GetProfile(), _config.Theme, _config.SampleIntervalMs);
    }

    internal OverlayProfile GetProfile()
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

    private void ApplyStyle()
    {
        NativeMethods.SetWindowLongPtr(_hwnd, NativeMethods.GWL_EXSTYLE, (nint)Style());
        ClampToWorkArea();
        NativeMethods.SetWindowPos(_hwnd, NativeMethods.HWND_TOPMOST, _config.X, _config.Y, 0, 0, NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE |
            (_config.Visible ? NativeMethods.SWP_SHOWWINDOW : NativeMethods.SWP_HIDEWINDOW));
        ApplyVisibility();
    }

    private void StopRecovery()
    {
        foreach (var hook in _hooks) NativeMethods.UnhookWinEvent(hook);
        _hooks.Clear();
        if (_hwnd != 0)
        {
            NativeMethods.KillTimer(_hwnd, HealthTimer);
            NativeMethods.KillTimer(_hwnd, RecoveryTimer);
        }
        _recoveryPending = false;
    }

    public void Dispose()
    {
        StopRecovery();
        if (_hotKeyRegistered && _hwnd != 0) NativeMethods.UnregisterHotKey(_hwnd, ToggleHotKeyId);
        _tray?.Dispose();
        _renderer.Dispose();
        if (_hwnd != 0) NativeMethods.DestroyWindow(_hwnd);
    }
}
