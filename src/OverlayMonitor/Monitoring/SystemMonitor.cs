using System.ComponentModel;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using LibreHardwareMonitor.Hardware;
using OverlayMonitor.Configuration;
using OverlayMonitor.Models;

namespace OverlayMonitor.Monitoring;
public sealed class SystemMonitor : IDisposable
{
    private static readonly string[] NonPhysicalInterfaceMarkers =
    [
        "virtual", "hyper-v", "vmware", "virtualbox", "wintun", "tap-windows", "tap adapter",
        "tun", "tailscale", "zerotier", "wireguard", "openvpn", "cloudflare warp", "hamachi",
        "radmin vpn", "clash", "sing-box", "v2ray", "npcap", "wan miniport", "wfp", "qos packet scheduler",
        "network monitor", "ndis filter", "filter driver", "lightweight filter", "native wifi filter"
    ];
    private readonly Computer _computer = new() { IsCpuEnabled = true, IsGpuEnabled = true };
    private readonly UpdateVisitor _visitor = new();
    private ISensor? _cpuTemp, _cpuLoad, _gpuTemp, _gpuLoad;
    private ulong _prevIdle, _prevKernel, _prevUser, _prevRx, _prevTx;
    private List<NetworkInterface>? _interfaces;
    private uint _lastMemoryLoad;
    private DateTime _previous = DateTime.UtcNow;
    public SystemMonitor()
    {
        try { _computer.Open(); _computer.Accept(_visitor); ScanSensors(); } catch (Exception ex) { AppLog.Error("LibreHardwareMonitor 初始化失败。", ex); }
        try { ReadSystemTimes(out _prevIdle, out _prevKernel, out _prevUser); } catch (Exception ex) { AppLog.Error("读取系统时间失败。", ex); }
        try { ReadNetwork(out _prevRx, out _prevTx); } catch (Exception ex) { AppLog.Error("读取网络计数器失败。", ex); }
        NetworkChange.NetworkAddressChanged += OnNetworkChanged;
        NetworkChange.NetworkAvailabilityChanged += OnNetworkChanged;
    }
    private void OnNetworkChanged(object? sender, EventArgs e) => _interfaces = null;
    private void ScanSensors()
    {
        var sensors = _computer.Hardware.SelectMany(Flatten).SelectMany(x => x.Sensors).ToList();
        var cpuSensors = sensors.Where(s => s.Hardware.HardwareType == HardwareType.Cpu).ToList();
        var gpuSensors = sensors.Where(s => s.Hardware.HardwareType is HardwareType.GpuAmd or HardwareType.GpuNvidia or HardwareType.GpuIntel).ToList();
        _cpuTemp = Pick(cpuSensors, SensorType.Temperature, ["CPU Package", "Core Average", "Tctl/Tdie", "CPU Die", "Core Max"]);
        _cpuLoad = Pick(cpuSensors, SensorType.Load, ["CPU Total", "Total"]);
        _gpuTemp = Pick(gpuSensors, SensorType.Temperature, ["GPU Core", "GPU Temperature", "Core"]);
        _gpuLoad = Pick(gpuSensors, SensorType.Load, ["GPU Core", "Core", "D3D 3D"]);
        LogSensors("CPU 温度传感器", cpuSensors.Where(s => s.SensorType == SensorType.Temperature));
        LogSensors("CPU 负载传感器", cpuSensors.Where(s => s.SensorType == SensorType.Load));
        LogSensors("GPU 温度传感器", gpuSensors.Where(s => s.SensorType == SensorType.Temperature));
        AppLog.Info($"已选择 CPU 温度传感器：{Describe(_cpuTemp)}");
        AppLog.Info($"已选择 CPU 负载传感器：{Describe(_cpuLoad)}");
        AppLog.Info($"已选择 GPU 温度传感器：{Describe(_gpuTemp)}");
    }
    private static IEnumerable<IHardware> Flatten(IHardware h) { yield return h; foreach (var s in h.SubHardware) foreach (var item in Flatten(s)) yield return item; }
    private static ISensor? Pick(IEnumerable<ISensor> sensors, SensorType type, string[] preferred) => preferred.Select(name => sensors.FirstOrDefault(s => s.SensorType == type && s.Name.Contains(name, StringComparison.OrdinalIgnoreCase))).FirstOrDefault(s => s is not null) ?? sensors.FirstOrDefault(s => s.SensorType == type);
    private static void LogSensors(string title, IEnumerable<ISensor> sensors)
    {
        var names = sensors.Select(Describe).ToArray();
        AppLog.Info($"{title}：{(names.Length == 0 ? "未发现" : string.Join("；", names))}");
    }
    private static string Describe(ISensor? sensor) => sensor is null ? "未选择" : $"{sensor.Hardware.Name}/{sensor.Name}（初始值：{(sensor.Value is null ? "null" : sensor.Value.Value.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture))}）";
    public MonitorSnapshot Sample()
    {
        try { _computer.Accept(_visitor); } catch (Exception ex) { AppLog.Error("更新硬件传感器失败。", ex); }
        var now = DateTime.UtcNow; var seconds = Math.Max((now - _previous).TotalSeconds, .001); _previous = now;
        ulong idle = _prevIdle, kernel = _prevKernel, user = _prevUser, rx = _prevRx, tx = _prevTx;
        try { ReadSystemTimes(out idle, out kernel, out user); } catch (Exception ex) { AppLog.Error("采样 CPU 占用率失败。", ex); }
        var total = (kernel - _prevKernel) + (user - _prevUser); var cpu = total == 0 ? 0 : 100f * (1f - (float)(idle - _prevIdle) / total); (_prevIdle, _prevKernel, _prevUser) = (idle, kernel, user);
        try { ReadNetwork(out rx, out tx); } catch (Exception ex) { AppLog.Error("采样网络速度失败。", ex); }
        var down = Delta(rx, _prevRx) / seconds; var up = Delta(tx, _prevTx) / seconds; (_prevRx, _prevTx) = (rx, tx);
        try { _lastMemoryLoad = GetMemoryLoad(); } catch (Exception ex) { AppLog.Error("采样内存占用率失败。", ex); }
        var cpuLoad = ValidLoad(_cpuLoad?.Value) ?? Math.Clamp(cpu, 0, 100);
        return new(ValidTemp(_cpuTemp?.Value), ValidTemp(_gpuTemp?.Value), cpuLoad, ValidLoad(_gpuLoad?.Value), _lastMemoryLoad, down, up);
    }
    private static float? ValidTemp(float? value) => value is > 0 and < 150 ? value : null;
    private static float? ValidLoad(float? value) => value is >= 0 and <= 100 ? value : null;
    private static ulong Delta(ulong current, ulong previous) => current >= previous ? current - previous : 0;
    private static bool IsPhysicalInterface(NetworkInterface networkInterface)
    {
        if (networkInterface.NetworkInterfaceType is not (
            NetworkInterfaceType.Ethernet or NetworkInterfaceType.GigabitEthernet or
            NetworkInterfaceType.FastEthernetFx or NetworkInterfaceType.FastEthernetT or
            NetworkInterfaceType.Wireless80211 or NetworkInterfaceType.Wman or
            NetworkInterfaceType.Wwanpp or NetworkInterfaceType.Wwanpp2))
            return false;

        var identity = $"{networkInterface.Name} {networkInterface.Description}";
        return !NonPhysicalInterfaceMarkers.Any(marker => identity.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }
    private void ReadNetwork(out ulong rx, out ulong tx)
    {
        rx = tx = 0;
        if (_interfaces is null)
        {
            _interfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up && IsPhysicalInterface(n))
                .ToList();
            AppLog.Info($"网络统计网卡：{(_interfaces.Count == 0 ? "未发现" : string.Join("；", _interfaces.Select(n => $"{n.Name}/{n.Description}")))}");
        }
        foreach (var n in _interfaces)
        {
            try { var s = n.GetIPStatistics(); rx += (ulong)Math.Max(s.BytesReceived, 0); tx += (ulong)Math.Max(s.BytesSent, 0); }
            catch (Exception ex) { AppLog.Error($"读取网络接口 {n.Name} 计数器失败。", ex); }
        }
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)] private struct MEMORYSTATUSEX { public uint dwLength, dwMemoryLoad; public ulong ullTotalPhys, ullAvailPhys, ullTotalPageFile, ullAvailPageFile, ullTotalVirtual, ullAvailVirtual, ullAvailExtendedVirtual; }
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX buffer);
    private static uint GetMemoryLoad() { var memory = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() }; if (!GlobalMemoryStatusEx(ref memory)) throw new Win32Exception(Marshal.GetLastWin32Error()); return memory.dwMemoryLoad; }
    [StructLayout(LayoutKind.Sequential)] private struct FILETIME { public uint LowDateTime, HighDateTime; }
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool GetSystemTimes(out FILETIME idle, out FILETIME kernel, out FILETIME user);
    private static void ReadSystemTimes(out ulong idle, out ulong kernel, out ulong user) { if (!GetSystemTimes(out var i, out var k, out var u)) throw new Win32Exception(Marshal.GetLastWin32Error()); idle = ((ulong)i.HighDateTime << 32) | i.LowDateTime; kernel = ((ulong)k.HighDateTime << 32) | k.LowDateTime; user = ((ulong)u.HighDateTime << 32) | u.LowDateTime; }
    public void Dispose()
    {
        NetworkChange.NetworkAddressChanged -= OnNetworkChanged;
        NetworkChange.NetworkAvailabilityChanged -= OnNetworkChanged;
        _computer.Close();
    }
    private sealed class UpdateVisitor : IVisitor { public void VisitComputer(IComputer c) => c.Traverse(this); public void VisitHardware(IHardware h) { h.Update(); foreach (var s in h.SubHardware) s.Accept(this); } public void VisitSensor(ISensor s) { } public void VisitParameter(IParameter p) { } }
}
