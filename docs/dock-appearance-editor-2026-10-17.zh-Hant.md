# Dock 項目外觀編輯（2026-10-17）

## 分支與範圍

- Worktree：`D:\projects\HardwareMonitorCmdPal-live`
- 分支：`feat/dock-appearance-editor-20261017`（源自 `3949c65`）
- 不修改舊有 `pinned-sensors.json` 的格式。每項外觀分開保存在使用者設定目錄的 `dock-appearance.json`，key 為感測器 Id。
- **目前僅修改開發分支原始碼；尚未部署到現有 Windows 0.1.7.1 測試安裝套件。**

## 功能

1. 每個感測器可選自動圖示、16 個內建 Emoji，或輸入自訂 Emoji／圖示文字；自訂輸入優先於預設。
2. 可自訂主要標題名稱與副標題。Dock Title 保持即時讀值，將名稱放在讀值前面，例如 `CPU package 61°`，避免 Compact 隱藏副標題後只剩數字。
3. 預設 Compact 標題含 `CPU/GPU/SSD` 等來源標示及溫度／負載／功率類型識別（`PKG/CORE/T/%/W`）。
4. 自訂值使用獨立、原子檔案取代儲存，重新載入延續，個別重設後恢復自動圖示與標題。
5. 可從「設定 Dock 讀值」列表的項目更多操作，以及「硬體監視器」感測器列表的右鍵／更多操作開啟「編輯 Dock 項目外觀」。Dock 的 WrappedDockItem 項目也提供相同的 `MoreCommands`。
6. 支援繁體中文（zh-Hant）、簡體中文（zh-Hans）、英文與日文介面。儀表板語系字典透過 `scripts/Export-DashboardLocales.ps1` 同步。

## 重要介面限制

Command Palette 的官方 Dock 文件描述右鍵原生選單主要用於顯示／隱藏標題、副標題與解除釘選，**未保證 Dock 會呈現擴充項目的 `MoreCommands`**。因此：

- 支援的編輯入口：Command Palette → 硬體監視器感測器列表 → 右鍵或更多操作 → 編輯 Dock 項目外觀。
- 備援入口：Command Palette → 設定 Dock 讀值 → 選擇感測器 → 更多操作 → 編輯 Dock 項目外觀。
- 待主機實機測試：Dock 本體項目右鍵選單是否顯示擴充自訂命令；若主機只顯示原生命令，屬 Host SDK/UI 能力限制，不應以滑鼠事件攔截或 hook 對抗。

本功能使用 SDK `ContentPage`／`FormContent` 的 Adaptive Cards 表單；不修改 PowerToys 內部、不需要管理員權限。

## 開發驗收

在專案資料夾執行：

```powershell
dotnet build HardwareMonitorExtension.sln -c Debug -p:Platform=x64 -v:quiet
dotnet run --project tests/DockSettings/DockSettings.csproj -c Debug -p:Platform=x64 -p:AppxPackageSigningEnabled=false
dotnet run --project tests/PinPersistence/PinPersistence.csproj -c Release
dotnet run --project tests/LocalizationSmoke/LocalizationSmoke.csproj -p:Platform=x64
node tests/dashboard-polling.test.cjs
node tests/dashboard-localization.test.cjs
powershell -NoProfile -File scripts/Export-DashboardLocales.ps1 -Check
```

實機測試：至少準備 2 個 CPU、2 個 GPU 感測器；核對預設 Compact 識別、選單開啟、Emoji 顯示、文字修改後即時更新、取消釘選後重釘、重啟後保留、還原預設。更改語系後重啟主機驗證。右鍵 Dock 本體為探索性測試，不能僅憑 SDK 物件存在便認定通過。
