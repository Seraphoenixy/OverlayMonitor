using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using OverlayMonitor.Configuration;
using OverlayMonitor.Models;
using OverlayMonitor.Window;
using Xunit;

namespace OverlayMonitor.Tests;

public sealed class WindowRecoveryTests
{
    [DllImport("user32.dll")] private static extern nint SendMessage(nint hwnd, uint message, nuint wp, nint lp);

    [Fact]
    [Trait("Category", "Desktop")]
    public void NativeWindowRecoversAndRespectsExplicitHiding()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var config = new OverlayConfig { X = 100, Y = 100, Visible = false };
                using var overlay = new OverlayWindow(new ConfigService(), config);
                // Window recovery does not require Explorer's notification area (unavailable on CI).
                overlay.Create(createTrayIcon: false);
                var command = typeof(OverlayWindow).GetMethod("OnCommand", BindingFlags.NonPublic | BindingFlags.Instance)!;
                command.Invoke(overlay, [2u]); // Changing style while hidden must not show it.
                Assert.False(NativeMethods.IsWindowVisible(overlay.Handle));

                command.Invoke(overlay, [1u]);
                Assert.True(NativeMethods.IsWindowVisible(overlay.Handle));
                NativeMethods.ShowWindow(overlay.Handle, NativeMethods.SW_HIDE);
                Thread.Sleep(1100); // Allow the recovery rate limit to expire.
                SendMessage(overlay.Handle, NativeMethods.WM_TIMER, 1, 0);
                Assert.True(NativeMethods.IsWindowVisible(overlay.Handle));

                var blocker = NativeMethods.CreateWindowEx(NativeMethods.WS_EX_TOPMOST | NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_TOOLWINDOW,
                    "STATIC", "Overlay recovery test", NativeMethods.WS_POPUP, config.X, config.Y, 300, 100, 0, 0, 0, 0);
                Assert.NotEqual(0, blocker);
                try
                {
                    NativeMethods.ShowWindow(blocker, NativeMethods.SW_SHOWNOACTIVATE);
                    Assert.True(NativeMethods.SetWindowPos(blocker, NativeMethods.HWND_TOPMOST, 0, 0, 0, 0,
                        NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE));
                    Assert.True(IsAbove(blocker, overlay.Handle));
                    Thread.Sleep(1100);
                    SendMessage(overlay.Handle, NativeMethods.WM_TIMER, 1, 0);
                    Assert.True(IsAbove(overlay.Handle, blocker));

                    command.Invoke(overlay, [1u]);
                    SendMessage(overlay.Handle, NativeMethods.WM_TIMER, 1, 0);
                    Assert.False(NativeMethods.IsWindowVisible(overlay.Handle));
                }
                finally { NativeMethods.DestroyWindow(blocker); }
            }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(20)), "Native window verification timed out.");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private static bool IsAbove(nint above, nint below)
    {
        for (var i = 0; below != 0 && i < 256; i++)
        {
            below = NativeMethods.GetWindow(below, NativeMethods.GW_HWNDPREV);
            if (below == above) return true;
        }
        return false;
    }
}
