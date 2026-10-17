# Dock 外觀編輯版 — 本機部署紀錄（2026-10-17）

## 來源與版本

- 功能來源：`feat/dock-appearance-editor-20261017`，commit `26f35d6`。
- 獨立部署工作樹：`D:\projects\HardwareMonitorCmdPal-appearance-deploy`，分支 `deploy/dock-appearance-0.1.7.2-20261017`。
- 由已安裝 `0.1.7.1` 原地升級至 `0.1.7.2`（同 `CN=HardwareMonitor` 發行者與套件身分）。
- 本機 Release x64 MSIX：`artifacts\AppPackages\HardwareMonitorExtension_0.1.7.2_x64_Test\HardwareMonitorExtension_0.1.7.2_x64.msix`（Git 忽略，不提交私人金鑰）。
- 正式安裝位置：`C:\Program Files\WindowsApps\HardwareMonitorExtension_0.1.7.2_x64__xpvh0243hbvky`。
- 三處版本宣告：`HardwareMonitorExtension.csproj`、`Package.appxmanifest`、`setup.iss` 均為 `0.1.7.2`。

## 安裝與驗證

- Release x64 打包成功：0 errors / 65 warnings（沿用先前已知的靜態分析、APPX1707 和符號工具警告）。
- 在簽署前已驗證 MSIX 的 `AppxManifest.xml`：發行者、版本與套件身分正確，`Assets/i18n-data.js` 包含 `DockAppearanceEdit`，含完整 `HardwareMonitorExtension.dll`。
- 使用 `SignTool /fd SHA256` 簽署，`signtool verify /pa` 通過。
- 憑證公開部分透過 Windows UAC 管理員程序匯入 `Cert:\LocalMachine\TrustedPeople`；沒有加入 Root CA。
- 本機使用非管理員 PowerShell 執行 `Add-AppxPackage -Path <msix> -ForceApplicationShutdown`；成功更新正在使用的舊版本。
- `Get-AppxPackage`：Version `0.1.7.2`；Status `Ok`；SignatureKind `Developer`。
- `Get-AppxPackageManifest`：Command Palette AppExtension 與 COM ExeServer 均存在。
- 已安裝 `HardwareMonitorExtension.dll` 的 SHA-256 與 Release 建置輸出完全一致。
- `PowerToys.exe` 持續執行，`HardwareMonitorExtension.exe` 行程路徑指向新的 `0.1.7.2`。
- 功能回歸測試已在來源分支完成：Dock 29/29、PinPersistence 11/11、語系與儀表板回歸通過。
- **未經圖形 UI 驗證**：Dock 本體右鍵是否呈現 `MoreCommands`、Compact 模式截斷與 Emoji 實際字形。這是後續手動操作驗收項目。

## 憑證與安全

- 本版新的開發簽章指紋：`116C7D5BB9138B35649E102557494E9767505723`。
- 私人金鑰：於目前使用者的 `Cert:\CurrentUser\My`，設定為 NonExportable，**不應匯出或提交到 Git**。
- 公開憑證：`LocalMachine\TrustedPeople`，僅供 Windows 驗證目前自簽 MSIX；本機保留後續同一憑證簽署的可能性。
- 前次 `0.1.7.1` 的受信任簽章憑證獨立存在，以支援既有部署／回退需求。請勿批次移除所有 Subject 為 `CN=HardwareMonitor` 的證書。
- 舊版 `setup.iss` 解除安裝清理規則按 Subject 篩選，可能同時移除多張 `CN=HardwareMonitor` 憑證；進入 EXE 發布流程前應改為只刪除安裝器自己匯入的 Thumbprint。
- 若未來不再使用本開發版，經核對已無套件依賴後，以精確 Thumbprint 逐張移除開發信任，並審查使用者私鑰保留需求。

## PowerToys 驗收

1. 使用 `Win+Alt+Space` 開啟 Command Palette；確認「硬體監視器」擴充可用。
2. 在感測器列表的更多命令或「設定 Dock 讀值」內，點「編輯 Dock 項目外觀」。
3. 分別選一個 Emoji、設定標題與副標題；驗證即時值不會被覆寫且儲存後仍顯示。
4. 測試 Compact／一般布局、重新載入擴充及釘選移除重建後外觀仍在。
5. **探索性**：在 Dock 本體項目按右鍵，看 host 是否展示擴充的 `MoreCommands`。若只有原生功能，屬 Command Palette Host 限制，已提供列表／設定頁入口。

尚未實測不可標記為已通過。
