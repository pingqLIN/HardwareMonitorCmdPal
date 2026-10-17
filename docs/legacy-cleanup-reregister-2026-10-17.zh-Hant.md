# 本機舊版清理與新版重新註冊紀錄（2026-10-17）

## 最終驗收：PASS

目標裝置：Pacomo（Windows）；目標版本：HardwareMonitorExtension `0.1.7.2`。

- `Get-AppxPackage -AllUsers HardwareMonitorExtension`（管理員執行）回報僅有 `HardwareMonitorExtension_0.1.7.2_x64__xpvh0243hbvky`，對應 addra 的 Installed 狀態，沒有其他版本。
- `Get-AppxProvisionedPackage -Online` 無 HardwareMonitorExtension 預先佈建套件。
- 舊版 `0.1.7.0` Inno Setup 安裝登錄 `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{3F8A2C91-6B4E-4D2A-9C17-8E5F0A7B4D63}}_is1` 已移除。
- `C:\Program Files\HardwareMonitorExtension` 已不存在。其四個已核對檔案及登錄匯出備份封存在：`D:\projects\HardwareMonitorCmdPal-appearance-deploy\artifacts\dev-deploy\retired-inno-0.1.7.0`。
- 舊的可信任簽章指紋 `8EDB5FF7B10893C5ECCAC6BB97A5B6B84F73B9E0` 與 `A14FD368A53A687DBFAF59AEDBCDDFA3FC294D98`：最後的使用者／電腦 TrustedPeople 憑證盤點均不存在。
- 新版開發簽章 `116C7D5BB9138B35649E102557494E9767505723` 仍在使用者／電腦 TrustedPeople，且僅目前使用者 My 擁有其 NonExportable 私密金鑰，予以保留。
- 已執行 `Add-AppxPackage -Register <0.1.7.2 WindowsApps AppxManifest.xml> -DisableDevelopmentMode -ForceApplicationShutdown`，成功退出。
- 已重新啟動 `HardwareMonitorExtension.exe -RegisterProcessAsComServer -Embedding`（執行 PID 在驗收時為 63960）；擴充僅有該一個程序，路徑指向 `0.1.7.2`。
- `Get-AppxPackageManifest` 驗證 `com.microsoft.commandpalette` AppExtension、COM ExeServer 均存在；已安裝 DLL SHA-256 與 Release 建置一致。
- PowerToys 主程序仍運作。
- 最終複驗 JSON：`artifacts\dev-deploy\final-postclean-20261017.json`，`FinalPassed=true`。
- Windows 仍留有 `D:\projects\HardwareMonitorCmdPal-deploy\artifacts\AppPackages` 的 `0.1.7.1` 開發構件供回退，**非系統安裝／註冊**；沒有修改專案原始工作樹。

## 避免誤刪新版的處置

舊 Inno Setup `0.1.7.0` 解除安裝器的 `[UninstallRun]` 包含：

1. `Get-AppxPackage -AllUsers HardwareMonitorExtension | Remove-AppxPackage -AllUsers`，會不分版本解除安裝目前 `0.1.7.2`。
2. `Where-Object Subject -eq 'CN=HardwareMonitor' | Remove-Item`，可能移除新版簽章信任。

因此**沒有呼叫舊版 unins000.exe**，改為精確封存舊 Inno 安裝資料夾、備份後移除其 HKLM 解除安裝登錄，以及依舊版 SHA-1 thumbprint 清除信任。原有 MSIX `0.1.7.2` 與使用者釘選設定未解除安裝或清空。

**待處理 P1**：存放於 Git 的 `setup.iss` 仍包含上述廣泛卸載規則。若未來要重新發佈 EXE 安裝器，必須在發佈前修改為限定安裝器所擁有之版本和簽章指紋，避免誤刪使用者升級後的 MSIX。此問題並未因本機移除舊安裝器而自動修正。

## 使用者 GUI 驗收（未執行）

- 開啟 `Win+Alt+Space`，確認新版硬體監視器的 Dock、項目 Emoji／標題／副標題編輯是否正常。
- Dock 本體的原生滑鼠右鍵選單能否展示擴充的 `MoreCommands`，仍須 GUI 實際測試；程式註冊和 COM 行程正常不等於這項 Host UX 已通過。

## 本地證據（Git 忽略）

- `artifacts\dev-deploy\legacy-cleanup-20261017.json`
- `artifacts\dev-deploy\restart-reregister-20261017.json`
- `artifacts\dev-deploy\final-postclean-20261017.json`
- `artifacts\dev-deploy\retired-inno-0.1.7.0\old-inno-uninstall.reg`

以上私有系統稽核檔與可能包含環境資訊的備份均不提交 Git。
