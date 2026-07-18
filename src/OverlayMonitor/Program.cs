using OverlayMonitor.Configuration;
using OverlayMonitor.Models;
using OverlayMonitor.Monitoring;
using OverlayMonitor.Window;

namespace OverlayMonitor;
internal static class Program
{
    [STAThread]
    private static void Main()
    {
        AppLog.Initialize();
        try { Run(); }
        catch (Exception ex) { AppLog.Error("未处理的主线程异常，程序即将退出。", ex); }
    }
    private static void Run()
    {
        var configService = new ConfigService(); var config = configService.Load();
        using var window = new OverlayWindow(configService, config); window.Create();
        using var monitor = new SystemMonitor(); using var cancel = new CancellationTokenSource();
        var gate = new object(); MonitorSnapshot? latest = null;
        var worker = Task.Run(async () =>
        {
            while (!cancel.IsCancellationRequested)
            {
                try { var sample = monitor.Sample(); lock (gate) latest = sample; if (!NativeMethods.PostMessage(window.Handle, NativeMethods.WM_OVERLAY_CHANGED, 0, 0) && !cancel.IsCancellationRequested) throw new System.ComponentModel.Win32Exception(System.Runtime.InteropServices.Marshal.GetLastWin32Error()); }
                catch (OperationCanceledException) when (cancel.IsCancellationRequested) { break; }
                catch (Exception ex) { AppLog.Error("后台采样任务异常。", ex); }
                try { await Task.Delay(Math.Clamp(window.Config.SampleIntervalMs, 250, 10000), cancel.Token).ConfigureAwait(false); } catch (OperationCanceledException) { break; }
            }
        });
        NativeMethods.MSG message; int getMessage;
        while ((getMessage = NativeMethods.GetMessage(out message, 0, 0, 0)) > 0)
        {
            if (message.message == NativeMethods.WM_OVERLAY_CHANGED) { MonitorSnapshot? snapshot; lock (gate) snapshot = latest; if (snapshot is not null) window.Render(Format(window.Config, snapshot)); }
            NativeMethods.TranslateMessage(ref message); NativeMethods.DispatchMessage(ref message);
        }
        cancel.Cancel(); try { worker.GetAwaiter().GetResult(); } catch (OperationCanceledException) { }
        if (getMessage == -1) throw new System.ComponentModel.Win32Exception(System.Runtime.InteropServices.Marshal.GetLastWin32Error());
    }
    private static string Format(OverlayConfig config, MonitorSnapshot s)
    {
        var parts = config.Metrics.Where(m => m.Enabled).OrderBy(m => m.Order).Select(m => m.Id switch
        {
            "cpuTemp" => $"CPU {Temperature(s.CpuTemperature)}", "gpuTemp" => $"GPU {Temperature(s.GpuTemperature)}",
            "cpuLoad" => $"CPU {s.CpuLoad:0}%", "gpuLoad" => $"GPU {s.GpuLoad:0}%",
            "download" => $"↓ {Speed(s.DownloadBps)}", "upload" => $"↑ {Speed(s.UploadBps)}", _ => ""
        }).Where(x => x.Length > 0);
        var result = string.Join("  |  ", parts);
        return config.ShowMemoryLoad ? $"{result}  |  RAM {s.MemoryLoad}%" : result;
    }
    private static string Temperature(float? value) => value is null ? "--" : $"{value:0}°C";
    private static string Speed(double bytes) => bytes >= 1024 * 1024 ? $"{bytes / 1024 / 1024:0.0} MB/s" : bytes >= 1024 ? $"{bytes / 1024:0} KB/s" : $"{bytes:0} B/s";
}
