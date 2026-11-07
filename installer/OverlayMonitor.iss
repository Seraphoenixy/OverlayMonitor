#ifndef AppVersion
  #define AppVersion "0.0.0-local"
#endif
#ifndef AppRid
  #define AppRid "win-x64"
#endif
#ifndef AppArchitecture
  #define AppArchitecture "x64compatible"
#endif
#ifndef SourceDir
  #error SourceDir must point to the dotnet publish output.
#endif
#ifndef OutputDir
  #define OutputDir "."
#endif
#ifndef IconPath
  #error IconPath must point to the OverlayMonitor application icon.
#endif
#ifndef ChineseMessagesFile
  #define ChineseMessagesFile "compiler:Languages\ChineseSimplified.isl"
#endif

[Setup]
AppId={{6D33CE90-E7C3-4D9A-9540-175731890ED6}
AppName=OverlayMonitor
AppVersion={#AppVersion}
AppVerName=OverlayMonitor {#AppVersion}
AppPublisher=OverlayMonitor
DefaultDirName={autopf}\OverlayMonitor
DefaultGroupName=OverlayMonitor
DisableProgramGroupPage=yes
; OverlayMonitor 的清单要求管理员权限，安装到 Program Files 可让
; 开始菜单快捷方式直接以已提升权限启动，避免每次运行都弹出 UAC。
PrivilegesRequired=admin
ArchitecturesAllowed={#AppArchitecture}
ArchitecturesInstallIn64BitMode={#AppArchitecture}
OutputDir={#OutputDir}
OutputBaseFilename=OverlayMonitor-{#AppVersion}-{#AppRid}-Setup
SetupIconFile={#IconPath}
UninstallDisplayIcon={app}\OverlayMonitor.exe
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "chinesesimp"; MessagesFile: "{#ChineseMessagesFile}"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "附加快捷方式："; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\OverlayMonitor"; Filename: "{app}\OverlayMonitor.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\OverlayMonitor"; Filename: "{app}\OverlayMonitor.exe"; WorkingDir: "{app}"; Tasks: desktopicon

; 开机自启动由程序自身以计划任务 "OverlayMonitor" 管理（登录时最高权限运行）。
; 卸载时一并清理计划任务和旧版本留下的 Run 项。
[UninstallRun]
Filename: "{sys}\schtasks.exe"; Parameters: "/Delete /TN ""OverlayMonitor"" /F"; Flags: runhidden; RunOnceId: "DeleteOverlayMonitorTask"
Filename: "{sys}\reg.exe"; Parameters: "delete ""HKCU\Software\Microsoft\Windows\CurrentVersion\Run"" /v ""OverlayMonitor"" /f"; Flags: runhidden; RunOnceId: "DeleteOverlayMonitorRunValue"

[Run]
Filename: "{app}\OverlayMonitor.exe"; Description: "启动 OverlayMonitor"; WorkingDir: "{app}"; Flags: nowait postinstall skipifsilent