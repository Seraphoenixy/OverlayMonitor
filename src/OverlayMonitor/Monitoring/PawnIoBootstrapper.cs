using System.Diagnostics;
using System.Security.Cryptography;
using LibreHardwareMonitor.PawnIo;
using OverlayMonitor.Configuration;
using OverlayMonitor.Window;

namespace OverlayMonitor.Monitoring;

/// <summary>
/// Installs the low-level driver used by current LibreHardwareMonitor builds.
/// PawnIO is machine-scoped, so this runs before <see cref="SystemMonitor"/> opens LHM.
/// </summary>
internal static class PawnIoBootstrapper
{
    private const string InstallerUrl = "https://github.com/namazso/PawnIO.Setup/releases/download/2.2.0/PawnIO_setup.exe";
    private const string InstallerSha256 = "1F519A22E47187F70A1379A48CA604981C4FCF694F4E65B734AAA74A9FBA3032";

    /// <summary>
    /// Checks for the driver and asks the user to install it when missing.
    /// Returns the background install task, or null when no install is pending.
    /// </summary>
    public static Task? EnsureInstalled()
    {
        if (IsInstalled())
        {
            AppLog.Info("PawnIO 已安装，LibreHardwareMonitor 可以访问底层硬件传感器。");
            return null;
        }

        const string prompt = "未检测到 PawnIO。LibreHardwareMonitor 需要此底层驱动来读取 CPU 温度。\n\n是否下载官方安装程序并安装 PawnIO？\n\n选择“否”会继续启动，但 CPU 温度可能显示为 --。";
        if (NativeMethods.MessageBox(0, prompt, "OverlayMonitor - 安装 PawnIO", NativeMethods.MB_YESNO | NativeMethods.MB_ICONINFORMATION) != NativeMethods.IDYES)
        {
            AppLog.Info("用户拒绝安装 PawnIO；将继续启动，CPU 温度可能无法读取。");
            return null;
        }

        AppLog.Info("用户确认安装 PawnIO，正在后台下载并自动安装官方驱动。");
        return InstallAsync();
    }

    private static async Task InstallAsync()
    {
        var installerPath = Path.Combine(Path.GetTempPath(), "OverlayMonitor", "PawnIO_setup_2.2.0.exe");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(installerPath)!);
            using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
            using var response = await client.GetAsync(InstallerUrl, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            await using (var input = response.Content.ReadAsStream())
            await using (var output = File.Create(installerPath))
                await input.CopyToAsync(output).ConfigureAwait(false);

            var hash = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(installerPath).ConfigureAwait(false)));
            if (!string.Equals(hash, InstallerSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("PawnIO 安装程序的 SHA-256 校验失败，已拒绝执行。");

            using var process = Process.Start(new ProcessStartInfo(installerPath, "-install -silent")
            {
                UseShellExecute = false,
                CreateNoWindow = true
            }) ?? throw new InvalidOperationException("无法启动 PawnIO 安装程序。");
            using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60)))
            {
                try { await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false); }
                catch (OperationCanceledException)
                {
                    process.Kill(entireProcessTree: true);
                    throw new TimeoutException("PawnIO 安装超时。");
                }
            }
            if (process.ExitCode != 0) throw new InvalidOperationException($"PawnIO 安装程序返回错误码 {process.ExitCode}。");
            if (!IsInstalled()) throw new InvalidOperationException("PawnIO 安装程序已完成，但未找到安装状态。");
            AppLog.Info("PawnIO 已自动安装完成。");
        }
        catch (Exception ex)
        {
            AppLog.Error("PawnIO 自动安装失败；CPU 温度可能无法读取。", ex);
        }
        finally
        {
            try { if (File.Exists(installerPath)) File.Delete(installerPath); } catch (Exception ex) { AppLog.Error("清理 PawnIO 临时安装程序失败。", ex); }
        }
    }

    private static bool IsInstalled() => PawnIo.IsInstalled;
}
