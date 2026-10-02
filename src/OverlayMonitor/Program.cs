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
        finally { AppLog.Shutdown(); }
    }
    private static void Run()
    {
        using var mutex = new Mutex(true, @"Local\OverlayMonitor.SingleInstance", out var createdNew);
        if (!createdNew) return;
        var configService = new ConfigService(); var config = configService.Load();
        try { new StartupService().Refresh(); }
        catch (Exception ex) { AppLog.Error("刷新开机自启动计划任务失败。", ex); }
        var install = PawnIoBootstrapper.EnsureInstalled();
        using var window = new OverlayWindow(configService, config); window.Create();
        if (install is not null) window.Render("正在安装 PawnIO 驱动...", true);
        using var cancel = new CancellationTokenSource();
        var gate = new object(); MonitorSnapshot? latest = null;
        var worker = Task.Run(async () =>
        {
            if (install is not null) await install.ConfigureAwait(false);
            using var monitor = new SystemMonitor();
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
    internal static string Format(OverlayConfig config, MonitorSnapshot s)
    {
        const int standardLabelWidth = 3;
        const int arrowLabelWidth = 1;
        const int temperatureValueWidth = 4;
        const int loadValueWidth = 4;
        const int speedValueWidth = 10;
        const int memoryValueWidth = 4;

        var parts = config.Metrics.Where(m => m.Enabled).OrderBy(m => m.Order).Select(m => m.Id switch
        {
            "cpuTemp" => FixedField("CPU", Temperature(s.CpuTemperature), standardLabelWidth, temperatureValueWidth),
            "gpuTemp" => FixedField("GPU", Temperature(s.GpuTemperature), standardLabelWidth, temperatureValueWidth),
            "cpuLoad" => FixedField("CPU", Load(s.CpuLoad), standardLabelWidth, loadValueWidth),
            "gpuLoad" => FixedField("GPU", Load(s.GpuLoad), standardLabelWidth, loadValueWidth),
            "download" => FixedField("↓", Speed(s.DownloadBps), arrowLabelWidth, speedValueWidth),
            "upload" => FixedField("↑", Speed(s.UploadBps), arrowLabelWidth, speedValueWidth),
            _ => ""
        }).Where(x => x.Length > 0);
        var result = string.Join("  |  ", parts);
        var memory = config.ShowMemoryLoad ? FixedField("RAM", MemoryLoad(s.MemoryLoad), standardLabelWidth, memoryValueWidth) : "";
        var final = config.ShowMemoryLoad ? $"{result}  |  {memory}" : result;
        return final.TrimEnd();
    }

    private static string FixedField(string label, string value, int labelWidth, int valueWidth)
    {
        var paddedLabel = label.Length >= labelWidth ? label : label.PadRight(labelWidth, ' ');
        var paddedValue = value.Length >= valueWidth ? value : value.PadRight(valueWidth, ' ');
        return $"{paddedLabel}{(label.Length == 1 ? " " : " ")}{paddedValue}";
    }

    private static string Temperature(float? value) => value is null ? "--" : $"{value:0}°C";
    private static string Load(float? value) => value is null ? "--" : $"{value:0}%";
    private static string MemoryLoad(uint value) => $"{value}%";
    private static string Speed(double bytes) => bytes >= 1024 * 1024 ? $"{bytes / 1024 / 1024:0.0} MB/s" : bytes >= 1024 ? $"{bytes / 1024:0} KB/s" : $"{bytes:0} B/s";
}
