# HardwareMonitorCmdPal 開發稽核與待辦（2026-10-17）

## 範圍與隔離

- 原工作區：`D:\projects\HardwareMonitorCmdPal-dock`，Git worktree 分支 `feat/dock-display-settings`，HEAD `0a74f76`。
- 原工作區原有未提交修改：`tests/DockSettings/Program.cs`、`tests/DockSettings/README.zh-tw.md`。這些檔案未修改、未提交、未重設。
- 修正工作區：`D:\projects\HardwareMonitorCmdPal-live`，分支 `fix/dashboard-live-polling-20261017`，從 `0a74f76` 建立。
- 先以 Local Files MCP 稽核檔案，再由 Remote Desktop Commander 在 Pacomo 執行 Git 與測試。未安裝、啟動或變更 SYSTEM 權限的溫度輔助服務。

## 已確認與完成

1. **主專案建置**：原 Dock 工作區執行 `dotnet build HardwareMonitorExtension.sln -c Debug -p:Platform=x64 -v:quiet` 通過（增量建置 0 warning、0 error）。新獨立 worktree 冷建置成功（0 error、64 warning），重建後成功（0 warning、0 error）；冷建置警告以既有 AsciiChart 分析提示與 MSIX APPX1707 為主，尚未逐項消除。
2. **Dock 測試**：原 Dock 工作區含未提交的語系新增測試共 21 項通過；`PinPersistence` 11/11 通過。這不代表 Command Palette 宿主 UI 已驗收。
3. **國際化**：`LocalizationSmoke` 與 `dashboard-localization.test.cjs` 測試通過，支援繁中／英文／日文／簡中及其他語言回退；翻譯資源匯出 `Export-DashboardLocales.ps1 -Check` 通過。
4. **儀表板輪詢修正**（本分支）：以快照 timestamp 變化判定新鮮度，避免讀取相同 `live-data.js` 重設保活計時；逾時顯示停止更新。限制同時一個腳本請求，載入成功或失敗皆移除節點並允許重試。
5. **儀表板顯示修正**（本分支）：初始檔案帶有效快照時立即渲染；儀表板內使用者明確清空 Dock 選擇時，不再在下一次渲染自動恢復預設值。此設定**僅在該瀏覽器頁面記憶體生效**，尚未與 Command Palette 的持久化釘選同步。
6. **新自動化回歸**：`node tests/dashboard-polling.test.cjs` 測試同步／逾時／復原、輪詢節點清理、初始 render 與空白 Dock 的頁面內語意，通過。此測試使用 VM 模擬 DOM，不代表真實瀏覽器畫面與長時間運作已通過。

## 尚未完成：優先順序

| 優先 | 狀態 | 項目 | 建議驗收 |
| --- | --- | --- | --- |
| P0 | REMAINING | **Command Palette 宿主 UI**：Dock 帶、點擊設定、Compact／Default、值與焦點更新、解除安裝／重新載入 | 以已安裝開發套件實測操作並記錄截圖／日誌；隱藏全部讀數後仍可進設定 |
| P0 | REMAINING | **長時間 Live 更新**：`LiveSnapshotWriter.IsDashboardActive` 依據 NTFS 存取時間判斷活躍；Windows last-access 更新可能停用，90 秒 OpenGrace 後可能停止寫入 | 於真實 Windows + file:// 瀏覽器連續觀察超過 90 秒的 `live-data.js` 修改時間與儀表板 timestamp；若停更，改用可靠心跳／本機 IPC 或明確的低頻寫盤策略 |
| P1 | REMAINING | **live-data.js 寫入原子性**：目前 `File.WriteAllText` 覆寫，瀏覽器同步載入時可能讀到部分內容 | 使用臨時檔 + 同磁區 atomic replace，模擬多次併行讀寫驗證沒有損壞 JSON／JS |
| P1 | REMAINING | **儀表板釘選同步**：網頁中的 Pin 按鈕只改瀏覽器當頁 `state.pinned`，沒有更新 `PinnedSensorStore` | 明確定位為預覽 UI，或設計受權限控制的雙向設定管道（不得以任意檔案寫入替代權限治理） |
| P1 | REMAINING | **SYSTEM 溫度助手與 PawnIO**：無本輪權限提升、排程任務、命名管線 ACL 或真 CPU 溫度驗收 | 先查排程與管線 ACL、確認授權，再用實際 CPU/GPU/SSD 數值交叉比對；不得在無確認下安裝 |
| P1 | REMAINING | **MSIX / EXE 發布**：未執行簽署、發行安裝與回復測試；冷建置有 APPX1707 | 在隔離測試環境測試 x64 安裝／卸載／COM 註冊；評估 arm64 與簽章後再發布 |
| P2 | REMAINING | **趨勢頁生命週期**：`SensorChartPage` 對 `SnapshotUpdated` 註冊匿名事件，沒有解除訂閱路徑 | 檢查 SDK Page 生命週期；補 Dispose/unsubscribe，再測試頁面反覆開閉的訂閱計數 |
| P2 | REMAINING | **CI / 原始碼警告**：無專案內自動 CI workflow；冷建置 64 warnings | 建立 Win x64 CI；分出 AsciiChart upstream 與自有程式碼警告清單，優先處理發布警告 |

## 建議接續程序

1. 將本分支與既有 Dock worktree **分開檢閱**；先保護原兩筆 dirty 變更。
2. 執行開發分支中的 `node tests/dashboard-polling.test.cjs` 及完整 build、Dock、PinPersistence、LocalizationSmoke、Dashboard Localization、Locale Catalog 測試。
3. 在未安裝/未提升權限前，完成真實瀏覽器長時間資料新鮮度測試；勿把 VM 測試等同 E2E。
4. 確認 PowerToys 宿主安裝狀態，再安排 Dock 視覺驗收與 privileged-helper 隔離測試。
5. 通過實機驗收後，才決定要採 cherry-pick／merge 至 `feat/dock-display-settings`，再另行合併至 main；不得覆蓋已有的未提交修改。

**界線**：本次不變更正式使用者設定、排程、系統服務、驅動、憑證或已安裝 PowerToys 擴充功能。
