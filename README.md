# OverlayMonitor

轻量级 Windows 性能悬浮窗，定位为 NVIDIA 性能覆盖层的补充：显示 CPU/GPU 温度、实时上下行网速和内存占用率。

项目使用 C#、.NET 10 与原生 Win32 API 实现，不依赖 WinForms、WPF、WinUI、MAUI、Electron 或 Avalonia。

## 特性

- 原生 `WS_POPUP` 分层窗口：置顶、不出现在任务栏或 Alt+Tab、不主动抢焦点；拦截 `WM_WINDOWPOSCHANGING` 防止降层，并通过进程外窗口事件监听和每 3 秒状态检查恢复隐藏、位置偏移及被其他窗口遮挡的状态。事件合并处理，恢复最多每秒一次；正常状态不重绘、不重复置顶。
- `UpdateLayeredWindow`、32 位 DIB 与 GDI 逐像素透明文字绘制，无黑色底框；DC、字体与 DIB 按尺寸缓存复用。
- 正常状态默认点击穿透；移动模式提供更大的透明拖拽区域，并保存窗口位置。
- 窗口位置按实际渲染尺寸钳制到最近显示器的工作区，并响应显示器、DPI 和工作区变化，避免断开外接屏后窗口飘出可视区域。
- 单实例保护：重复启动时第二个实例立即退出。
- CPU/GPU 温度和 GPU 占用率通过 `LibreHardwareMonitorLib 0.9.6` 获取；启动后缓存目标传感器，采样值经有效性过滤。
- CPU 总占用率通过 `GetSystemTimes` 计算；内存占用率通过 `GlobalMemoryStatusEx` 获取；网络速度通过 `NetworkInterface` 计数器计算，合并 IPv4 与 IPv6 流量，活动网卡列表缓存并在网络变化时重建。
- 后台周期采样，使用 `PostMessage` 通知 UI 线程；显示文字未变化时不重绘。
- 托盘菜单支持显示/隐藏、移动模式、三种预设显示模式、刷新频率（500 毫秒 / 1 秒 / 2 秒 / 5 秒）、开机自启动、配置重载和退出。
- `Alt + E` 全局快捷键切换悬浮窗显示/隐藏。
- 启动立即显示初始化文字；渲染失败记录日志并退避重试，不因单次渲染异常退出。Explorer 重启后自动恢复托盘图标并检查悬浮窗状态。
- 支持 Per-Monitor V2 DPI Awareness。
- 单元测试覆盖文本格式化、配置迁移、预设匹配与像素合成逻辑（`tests/OverlayMonitor.Tests`，xUnit）。
- 窗口测试覆盖实际桌面的遮挡恢复、外部隐藏恢复和主动隐藏状态；这项测试需要交互式 Windows 桌面。无桌面测试环境可使用 `dotnet test --filter "Category!=Desktop"` 运行其余测试。

## 预设显示模式

| 模式 | 显示内容 |
| --- | --- |
| NVIDIA 补充模式 | 下载、上传、CPU 温度、GPU 温度、RAM 占用率 |
| 完整模式 | 补充模式内容 + CPU 占用率 + GPU 占用率 |
| 温度模式 | CPU 温度、GPU 温度 |

下载/上传位于最左侧，整行文字右对齐；网速长度变化不会推动右侧温度指标。

## 显示主题

右键托盘图标可切换并自动保存主题：

- **自适应描边（默认）**：白字加深色 1px 描边，兼顾深色与浅色背景。
- **轻量底板**：恢复最初的白色细字、无描边和无背景。

## 系统要求

- Windows 10/11
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)（构建时需要）
- GitHub Actions 发布包自带 .NET 运行时，无需另外安装 .NET；普通本地构建和不自带运行时的发布版本需要安装与程序架构匹配的 .NET 10 Runtime。
- 用于 CPU 温度读取的管理员权限和 PawnIO 底层驱动（程序首次启动时会自动检测，须经用户确认后才会下载并安装）

程序清单使用 `requireAdministrator`。LibreHardwareMonitor 0.9.6 读取 CPU 温度时使用 PawnIO 访问底层硬件：启动时会检查 PawnIO 是否已安装，缺失时会说明用途并请求用户确认；仅在用户同意后，才从 PawnIO 官方 GitHub 发布页下载 2.2.0 安装器，校验 SHA-256 后以静默方式安装。下载安装在后台进行，悬浮窗会立即显示并渲染“正在安装 PawnIO 驱动...”占位文本，安装完成后自动开始采样。拒绝、下载、校验或安装失败不会阻止程序启动，但 CPU 温度可能显示为 `--`，详细原因会写入日志。某些安全软件、VBS/内存完整性、设备驱动或 BIOS 也可能阻止该访问。GPU 温度、网速和其他数据是否可用也取决于硬件与驱动是否暴露对应数据。

## 构建与运行

在仓库根目录执行：

```powershell
dotnet restore
dotnet build -c Release
dotnet test OverlayMonitor.sln -c Release
dotnet run --project .\src\OverlayMonitor\OverlayMonitor.csproj -c Release
```

构建输出：

```text
src\OverlayMonitor\bin\Release\net10.0-windows\
```

`dotnet build` 生成的程序不包含 .NET 运行时，因此复制到未安装 .NET 10 的电脑上运行时，会提示安装或更新 .NET。这与 GitHub Actions 发布包不同：工作流使用 `--self-contained true`，将运行时一起打包。

发布自带运行时的 x64 版本（与 GitHub Actions 的打包方式一致，推荐用于分发）：

```powershell
dotnet publish .\src\OverlayMonitor\OverlayMonitor.csproj -c Release -r win-x64 --self-contained true -o .\publish\win-x64
```

运行 `publish\win-x64\OverlayMonitor.exe`。复制或分发时必须保留整个 `publish\win-x64` 目录，包括 DLL、运行时文件和 `Assets`，不能只复制 `.exe`。GitHub 发布 ZIP 也需要完整解压后运行。

如果目标电脑已经安装 x64 .NET 10 Runtime，可以发布体积更小、不自带运行时的版本：

```powershell
dotnet publish .\src\OverlayMonitor\OverlayMonitor.csproj -c Release -r win-x64 --self-contained false -o .\publish\win-x64-framework-dependent
```

自带运行时主要增加发布包体积，不会额外启动一个 .NET 后台服务；两种打包方式本身不代表 CPU 占用存在明显差异。这里的“无需安装 .NET”仅指运行时，程序仍需要管理员权限，CPU 温度读取仍可能需要安装 PawnIO 驱动。

首次启动会出现 Windows UAC 确认。拒绝后程序不会启动。

## 使用说明

1. 右键系统托盘中的 OverlayMonitor 图标打开菜单。
2. “移动模式”勾选后可拖动悬浮窗；再次点击该项退出移动模式并恢复点击穿透。
3. 菜单中的勾选状态会指示当前可见性、移动模式、预设显示模式、刷新频率和开机自启动状态。
4. 按 `Alt + E` 可快速显示或隐藏窗口。若该组合键已被其他程序注册，失败原因会写入日志。
5. “开机自启动”会创建或删除名为 `OverlayMonitor` 的 Windows 计划任务：当前用户登录时以最高权限在交互会话运行，以避免登录时再次弹出 UAC。任务定义取消了电池供电启动限制与运行时长限制，包含启动失败自动重试；程序每次启动时会按当前可执行文件路径刷新任务定义，便携包移动位置后无需手动重建。

## 配置与日志

程序首次运行时会在可执行文件目录创建：

```text
OverlayMonitor\config.json
OverlayMonitor\overlay-monitor.log
```

`config.json` 保存窗口位置、可见状态、采样间隔、普通指标开关和排序。RAM 占用率由预设模式控制：除温度模式外均显示。

日志以追加方式写入，超过 512 KB 时轮转为 `overlay-monitor.log.old`，用于记录传感器扫描、热键注册、配置写入和运行异常。CPU 温度显示为 `--` 时，优先检查日志中“CPU 温度传感器”和“已选择 CPU 温度传感器”条目。

## 项目结构

```text
src/OverlayMonitor/
├── Configuration/  配置、日志、自启动计划任务
├── Models/         配置与监控数据模型
├── Monitoring/     LHM、CPU、内存与网络采样，PawnIO 驱动引导
├── Rendering/      GDI/DIB 分层窗口渲染
├── Tray/           托盘图标和菜单
├── Window/         Win32 窗口与 P/Invoke
└── Assets/         应用与托盘图标

tests/OverlayMonitor.Tests/  单元测试（xUnit）
```

## 不包含的功能

本项目刻意不实现 FPS 统计、游戏注入、DirectX Hook、历史曲线、自动检测 NVIDIA Overlay 或 WMI 轮询。

置顶恢复适用于普通桌面和多数无边框全屏场景，不能保证覆盖真正的独占全屏、UAC 安全桌面或持续争抢置顶的其他程序。透明窗口合成对游戏呈现路径的影响需要在目标游戏中实测。

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
