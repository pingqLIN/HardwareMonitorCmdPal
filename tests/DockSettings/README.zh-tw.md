# Dock 顯示設定檢查

在已備妥 .NET 10 SDK 與專案 Windows SDK 建置需求的 Windows 上執行：

```powershell
dotnet run --project tests/DockSettings/DockSettings.csproj -c Debug -p:Platform=x64 -p:AppxPackageSigningEnabled=false
```

此主控台驗證載入實際擴充功能與 Command Palette SDK 物件，透過 hub 的私有快照與事件欄位送入固定資料。它不啟動硬體取樣，也不建立 provider；固定清單僅寫入新建的暫存資料夾，完成時會列出該路徑。不會修改驅動程式、排程工作、已安裝套件或使用者設定。

涵蓋點選與即時儲存、維持相同列物件的更新、移除離線感測器、重啟後保留空白清單、零讀數時仍可設定，以及解除事件訂閱。失敗會拋出例外並以非零代碼結束。舊版清單與預設值初始化另由 PinPersistence 驗證。

宿主畫面仍需另外安裝開發套件驗收：在 Dock 的「新增擴充座項目」選取「設定 Dock 顯示項目」，點選按鈕並增減感測器，確認硬體讀數帶即時更新。隱藏全部讀數後確認仍可進入設定，再重啟擴充功能確認清單保持空白。請一併檢查 Compact 與 Default 版面；本主控台驗證不代表宿主畫面已通過驗收。
