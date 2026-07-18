using System.Diagnostics;
using System.Reflection;
using System.Security.Principal;
using Microsoft.Win32;

namespace OverlayMonitor.Configuration;

public sealed class StartupService
{
    private const string TaskName = "OverlayMonitor";
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public bool IsEnabled() => RunSchtasks("/Query", "/TN", TaskName).ExitCode == 0;

    public void SetEnabled(bool enabled)
    {
        if (enabled)
        {
            var result = RunSchtasks("/Create", "/TN", TaskName, "/TR", GetLaunchCommand(), "/SC", "ONLOGON", "/RU", GetCurrentUser(), "/RL", "HIGHEST", "/IT", "/F");
            if (result.ExitCode != 0) throw new InvalidOperationException($"无法创建开机自启动计划任务：{result.Output}");
            RemoveLegacyRunEntry();
        }
        else
        {
            var result = RunSchtasks("/Delete", "/TN", TaskName, "/F");
            if (result.ExitCode != 0 && IsEnabled()) throw new InvalidOperationException($"无法删除开机自启动计划任务：{result.Output}");
            RemoveLegacyRunEntry();
        }
    }

    private static (int ExitCode, string Output) RunSchtasks(params string[] arguments)
    {
        var startInfo = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "schtasks.exe"))
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);
        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("无法启动 schtasks.exe。");
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        Task.WaitAll(standardOutput, standardError);
        return (process.ExitCode, (standardOutput.Result + standardError.Result).Trim());
    }

    private static string GetLaunchCommand()
    {
        var processPath = Environment.ProcessPath ?? throw new InvalidOperationException("无法确定程序启动路径。");
        if (!Path.GetFileName(processPath).Equals("dotnet.exe", StringComparison.OrdinalIgnoreCase)) return Quote(processPath);
        var assemblyPath = Assembly.GetEntryAssembly()?.Location ?? throw new InvalidOperationException("无法确定程序程序集路径。");
        return $"{Quote(processPath)} {Quote(assemblyPath)}";
    }

    private static void RemoveLegacyRunEntry()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        key?.DeleteValue(TaskName, throwOnMissingValue: false);
    }

    private static string GetCurrentUser() => WindowsIdentity.GetCurrent().Name ?? throw new InvalidOperationException("无法确定当前 Windows 用户。");

    private static string Quote(string path) => $"\"{path}\"";
}
