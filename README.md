# OverlayMonitor

轻量级 Windows 性能悬浮窗，定位为 NVIDIA 性能覆盖层的补充：显示 CPU/GPU 温度、实时上下行网速和内存占用率。

项目使用 C#、.NET 8 与原生 Win32 API 实现，不依赖 WinForms、WPF、WinUI、MAUI、Electron 或 Avalonia。

## 特性

- 原生 `WS_POPUP` 分层窗口：置顶、不出现在任务栏或 Alt+Tab、不主动抢焦点。
- `UpdateLayeredWindow`、32 位 DIB 与 GDI 逐像素透明文字绘制，无黑色底框。
- 正常状态默认点击穿透；移动模式提供更大的透明拖拽区域，并保存窗口位置。
- CPU/GPU 温度和 GPU 占用率通过 `LibreHardwareMonitorLib 0.9.6` 获取；启动后缓存目标传感器。
- CPU 总占用率通过 `GetSystemTimes` 计算；内存占用率通过 `GlobalMemoryStatusEx` 获取；网络速度通过 `NetworkInterface` 计数器计算。
- 后台周期采样，使用 `PostMessage` 通知 UI 线程；显示文字未变化时不重绘。
- 托盘菜单支持显示/隐藏、移动模式、三种预设显示模式、开机自启动、配置重载和退出。
- `Alt + E` 全局快捷键切换悬浮窗显示/隐藏。
- Explorer 重启后自动恢复托盘图标。
- 支持 Per-Monitor V2 DPI Awareness。

## 预设显示模式

| 模式 | 显示内容 |
| --- | --- |
| NVIDIA 补充模式 | 下载、上传、CPU 温度、GPU 温度、RAM 占用率 |
| 完整模式 | 补充模式内容 + CPU 占用率 + GPU 占用率 |
| 温度模式 | CPU 温度、GPU 温度 |

下载/上传位于最左侧，整行文字右对齐；网速长度变化不会推动右侧温度指标。

## 系统要求

- Windows 10/11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)（构建时需要）
- 用于 CPU 温度读取的管理员权限

程序清单使用 `requireAdministrator`。LibreHardwareMonitor 为读取部分 CPU 温度可能需要底层硬件访问驱动；某些安全软件、VBS/内存完整性、设备驱动或 BIOS 可能阻止该访问。GPU 温度、网速和其他数据是否可用也取决于硬件与驱动是否暴露对应数据。

## 构建与运行

在仓库根目录执行：

```powershell
dotnet restore
dotnet build -c Release
dotnet run --project .\src\OverlayMonitor\OverlayMonitor.csproj -c Release
```

构建输出：

```text
src\OverlayMonitor\bin\Release\net8.0-windows\
```

发布 x64 版本：

```powershell
dotnet publish .\src\OverlayMonitor\OverlayMonitor.csproj -c Release -r win-x64 --self-contained false
```

首次启动会出现 Windows UAC 确认。拒绝后程序不会启动。

## 使用说明

1. 右键系统托盘中的 OverlayMonitor 图标打开菜单。
2. “移动模式”勾选后可拖动悬浮窗；再次点击该项退出移动模式并恢复点击穿透。
3. 菜单中的勾选状态会指示当前可见性、移动模式、预设显示模式和开机自启动状态。
4. 按 `Alt + E` 可快速显示或隐藏窗口。若该组合键已被其他程序注册，失败原因会写入日志。
5. “开机自启动”会创建或删除名为 `OverlayMonitor` 的 Windows 计划任务：当前用户登录时以最高权限在交互会话运行，以避免登录时再次弹出 UAC。

## 配置与日志

程序首次运行时会在可执行文件目录创建：

```text
OverlayMonitor\config.json
OverlayMonitor\overlay-monitor.log
```

`config.json` 保存窗口位置、可见状态、采样间隔、普通指标开关和排序。RAM 占用率由预设模式控制：除温度模式外均显示。

日志在每次启动时覆盖，用于记录传感器扫描、热键注册、配置写入和运行异常。CPU 温度显示为 `--` 时，优先检查日志中“CPU 温度传感器”和“已选择 CPU 温度传感器”条目。

## 项目结构

```text
src/OverlayMonitor/
├── Configuration/  配置、日志、自启动计划任务
├── Models/         配置与监控数据模型
├── Monitoring/     LHM、CPU、内存与网络采样
├── Rendering/      GDI/DIB 分层窗口渲染
├── Tray/           托盘图标和菜单
├── Window/         Win32 窗口与 P/Invoke
└── Assets/         应用与托盘图标
```

## 不包含的功能

本项目刻意不实现 FPS 统计、游戏注入、DirectX Hook、历史曲线、自动检测 NVIDIA Overlay 或 WMI 轮询。

## 许可证

本项目采用 [MIT License](LICENSE)。

## 第三方依赖与许可证

| 依赖 | 许可证 |
| --- | --- |
| LibreHardwareMonitorLib 0.9.6 | MPL-2.0 |
| BlackSharp.Core | MPL-2.0 |
| DiskInfoToolkit | MPL-2.0 |
| RAMSPDToolkit-NDD | MPL-2.0 |
| HidSharp | Apache-2.0 |

发布包含第三方 DLL 的二进制包时，请保留对应的许可证与版权声明。MPL-2.0 为文件级 Copyleft；本项目未修改这些第三方库的源文件。
