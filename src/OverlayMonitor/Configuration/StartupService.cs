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
            var definitionPath = WriteTaskDefinition();
            try
            {
                var result = RunSchtasks("/Create", "/TN", TaskName, "/XML", definitionPath, "/F");
                if (result.ExitCode != 0) throw new InvalidOperationException($"无法创建开机自启动计划任务：{result.Output}");
            }
            finally
            {
                try { File.Delete(definitionPath); } catch { /* 临时定义文件清理失败不影响功能。 */ }
            }
            RemoveLegacyRunEntry();
        }
        else
        {
            var result = RunSchtasks("/Delete", "/TN", TaskName, "/F");
            if (result.ExitCode != 0 && IsEnabled()) throw new InvalidOperationException($"无法删除开机自启动计划任务：{result.Output}");
            RemoveLegacyRunEntry();
        }
    }

    /// <summary>
    /// Recreates the task with the current executable path and latest settings.
    /// Fixes stale action paths after the portable app is moved and upgrades
    /// legacy task definitions that carried battery and time-limit restrictions.
    /// </summary>
    public void Refresh() { if (IsEnabled()) SetEnabled(true); }

    private static string WriteTaskDefinition()
    {
        var sid = WindowsIdentity.GetCurrent().User?.Value ?? throw new InvalidOperationException("无法确定当前用户的 SID。");
        var (command, arguments) = GetLaunchCommand();
        var definition = BuildTaskXml(sid, command, arguments);
        var path = Path.Combine(Path.GetTempPath(), "OverlayMonitor", "autostart-task.xml");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, definition, System.Text.Encoding.Unicode);
        return path;
    }

    private static string BuildTaskXml(string sid, string command, string arguments)
    {
        var argumentsElement = arguments.Length == 0 ? "" : $"\n      <Arguments>{EscapeXml(arguments)}</Arguments>";
        return $"""
            <?xml version="1.0" encoding="UTF-16"?>
            <Task version="1.2" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
              <Triggers>
                <LogonTrigger>
                  <Enabled>true</Enabled>
                </LogonTrigger>
              </Triggers>
              <Principals>
                <Principal id="Author">
                  <UserId>{sid}</UserId>
                  <LogonType>InteractiveToken</LogonType>
                  <RunLevel>HighestAvailable</RunLevel>
                </Principal>
              </Principals>
              <Settings>
                <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
                <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
                <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
                <AllowHardTerminate>true</AllowHardTerminate>
                <StartWhenAvailable>true</StartWhenAvailable>
                <RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>
                <AllowStartOnDemand>true</AllowStartOnDemand>
                <Enabled>true</Enabled>
                <Hidden>false</Hidden>
                <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
                <Priority>7</Priority>
                <RestartOnFailure>
                  <Interval>PT1M</Interval>
                  <Count>3</Count>
                </RestartOnFailure>
              </Settings>
              <Actions Context="Author">
                <Exec>
                  <Command>{EscapeXml(command)}</Command>{argumentsElement}
                </Exec>
              </Actions>
            </Task>
            """;
    }

    private static string EscapeXml(string value) => value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");

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

    private static (string Command, string Arguments) GetLaunchCommand()
    {
        var processPath = Environment.ProcessPath ?? throw new InvalidOperationException("无法确定程序启动路径。");
        if (!Path.GetFileName(processPath).Equals("dotnet.exe", StringComparison.OrdinalIgnoreCase)) return (processPath, "");
        var assemblyPath = Assembly.GetEntryAssembly()?.Location ?? throw new InvalidOperationException("无法确定程序程序集路径。");
        return (processPath, Quote(assemblyPath));
    }

    private static void RemoveLegacyRunEntry()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        key?.DeleteValue(TaskName, throwOnMissingValue: false);
    }

    private static string Quote(string path) => $"\"{path}\"";
}
