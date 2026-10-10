; Inno Setup — Command Palette extension (WinGet EXE)
; 安装完成后自动注册 MSIX 包，命令面板立即可发现扩展。
; 平台由 build-exe.ps1 通过 /DPlatform=x64|arm64 传入。
#ifndef Platform
  #define Platform "x64"
#endif
#define AppVersion "0.1.5.0"
#define ExtensionName "HardwareMonitorExtension"
#define DisplayName "Hardware Monitor (PawnIO)"
#define DeveloperName "zhang-astronaut"
#define MsixBase "HardwareMonitorExtension_" + AppVersion + "_" + Platform

[Setup]
AppId={{3F8A2C91-6B4E-4D2A-9C17-8E5F0A7B4D63}}
AppName={#DisplayName}
AppVersion={#AppVersion}
AppPublisher={#DeveloperName}
DefaultDirName={autopf}\{#ExtensionName}
OutputDir=bin\Release\installer
OutputBaseFilename={#ExtensionName}-Setup-{#AppVersion}-{#Platform}
Compression=lzma
SolidCompression=yes
MinVersion=10.0.19041
PrivilegesRequired=admin
#if Platform == "arm64"
ArchitecturesAllowed=arm64
ArchitecturesInstallIn64BitMode=arm64
#else
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
#endif

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
; 签名好的 MSIX（构建时由 build-exe.ps1 用自签名证书签出）
Source: "HardwareMonitorExtension\AppPackages\{#MsixBase}_Test\{#MsixBase}.msix"; DestDir: "{app}"; DestName: "HardwareMonitorExtension.msix"; Flags: ignoreversion
; 签名证书（公钥），安装时导入 TrustedPeople
Source: "cert\HardwareMonitor.cer"; DestDir: "{app}"; Flags: ignoreversion

; 清掉旧版本（0.1.4.0 及更早）遗留的散装发布文件
[InstallDelete]
Type: filesandordirs; Name: "{app}\AppPackages"
Type: files; Name: "{app}\*.dll"
Type: files; Name: "{app}\*.exe"
Type: files; Name: "{app}\*.pdb"
Type: files; Name: "{app}\*.json"
Type: files; Name: "{app}\*.pri"
Type: files; Name: "{app}\*.winmd"
Type: files; Name: "{app}\app.manifest"

[Run]
; 1) 导入签名证书到本机 TrustedPeople（需要管理员，Inno 本身已提权）
Filename: "powershell.exe"; \
  Parameters: "-NoProfile -ExecutionPolicy Bypass -Command ""Import-Certificate -FilePath '{app}\HardwareMonitor.cer' -CertStoreLocation Cert:\LocalMachine\TrustedPeople | Out-Null"""; \
  Flags: runhidden

; 2) 以发起安装的原始用户身份注册 MSIX 包（Add-AppxPackage 注册到当前用户）
Filename: "powershell.exe"; \
  Parameters: "-NoProfile -ExecutionPolicy Bypass -Command ""Add-AppxPackage -Path '{app}\HardwareMonitorExtension.msix' -ForceApplicationShutdown"""; \
  Flags: runhidden runasoriginaluser

[UninstallRun]
; 移除包（卸载在提权上下文运行，用 -AllUsers 覆盖所有用户），再删除 TrustedPeople 里的证书
Filename: "powershell.exe"; \
  Parameters: "-NoProfile -ExecutionPolicy Bypass -Command ""Get-AppxPackage -AllUsers HardwareMonitorExtension | Remove-AppxPackage -AllUsers"""; \
  RunOnceId: "RemoveAppxPackage"; Flags: runhidden
Filename: "powershell.exe"; \
  Parameters: "-NoProfile -ExecutionPolicy Bypass -Command ""Get-ChildItem Cert:\LocalMachine\TrustedPeople | Where-Object Subject -eq 'CN=HardwareMonitor' | Remove-Item"""; \
  RunOnceId: "RemoveSigningCert"; Flags: runhidden
