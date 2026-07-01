# 需求規格書 — 歷史任務統計頁(Tasks)後端真分頁

## 主題與背景

歷史任務統計頁(SCP / `TasksController` / `Views/Tasks/Index.cshtml`)目前查詢區間被前端寫死限制在 **30 天內**（`wwwroot/js/common/searchdate.js` 的 `maxSpan.days = 30`）。

客戶希望開放查詢超過 30 天。但經程式碼確認，現況**後端沒有真分頁**：

- `TasksController.GetTasks` 會把整個查詢區間的明細**一次全撈、全量運算、全部組成 HTML `<tr>` 一次回傳**。
- 前端 DataTables 的 `pageLength: 8` 只是 **client-side（顯示層）分頁** —— 資料其實整批載入到瀏覽器後才切頁，並未減少 SQL 撈取、HTML 產生與網路傳輸量。
- `Commons/PaginatedList.cs` 類別雖存在，但**全專案未被任何地方使用**（死程式碼）。

因此若只放寬天數，明細筆數暴增會造成 SQL 撈取、記憶體運算、HTML 產生、網路傳輸、瀏覽器 DataTable 初始化全面變慢，極端量下可能卡死。

本需求採**後端真分頁（治本）** 方向：明細 tab 改為 server-side 分頁，單次 request 只回傳當頁資料。

## 需求範圍

### 包含
- 明細 tab（`GetTasks` + `#detail-table`）改為 **DataTables serverSide 後端分頁**。
- 後端提供符合 DataTables serverSide 協定的分頁 API（`draw` / `recordsTotal` / `recordsFiltered` / `data`），支援分頁、排序、關鍵字搜尋。
- 啟用既有 `PaginatedList` 或等效切片邏輯，只回傳當頁資料。
- CSV / Excel / Copy 匯出改為可匯出**整個查詢範圍**（避免 serverSide 後只匯出當前頁）。
- 放寬前端 `maxSpan` 天數限制（實際目標值見「限制條件」，最終與使用者確認）。

### 不包含
- 統計 tab（`GetMission`）與柱狀圖（`GetBarChat`）**不做分頁**：兩者皆按日期／班次彙總，列數隨天數線性且量小，不會爆量。僅需驗證放寬天數後行為正常。
- **不重寫 `GetSearchData` 的 SQL 查詢與班次運算**：班次（pShift）運算在記憶體端進行，將分頁下推到 SQL 需重構查詢或資料模型，風險與工作量大，列為後續技術債（見 plan.md），不在本次範圍。
- 不變更 `ubMission` 資料結構與寫入端。

## 限制條件

- 前端 `maxSpan` 目標值：**與使用者確認**（建議放寬至 90 天，或在 serverSide 完成後直接移除硬限制，僅保留合理防呆上限）。
- SCP（WebGui / ASP.NET Core）**目前無測試專案**，本任務核心改動綁定 EF DBContext 與前端 DataTables，屬整合／UI 層，**不適用 TDD**。Gate 2 / Gate 4 以**手動回歸驗證**替代，並於報告註明「環境限制：SCP 無測試專案」。
- 採**記憶體分頁**：每次翻頁仍會經 `GetSearchData` 全量撈取 `ubMission` 並跑班次運算後再切片。此為已知取捨（治本傳輸層／前端層，SQL 層留待後續優化）。

### 資料流

| 操作 | 資料來源 | 讀寫方式 | 失敗時行為 |
|------|---------|---------|----------|
| 撈明細任務 | DB `ubMission` | `_DBContext.ubMission.Where(...).ToList()` | 已有防呆：壞列（null/長度不足/格式錯）逐列略過，不整批 500 |
| 撈班次定義 | DB `pShift` | `_DBContext.pShift.ToList()` 後記憶體運算 | 沿用現況 |
| 撈車輛名稱 | DB `oShuttle` | `FirstOrDefault(s => s.ShuttleId == id)` | 已有防呆：找不到退回「車輛{id}」 |
| 回傳當頁明細 | 記憶體 `List<TaskReport>` | `Skip/Take`（PaginatedList 或等效） | 例外記 Log 並回傳空結果，不拋 500 |

## 驗收標準

- [ ] 明細 tab 改為後端分頁，單次 request 只回傳當頁資料（每頁筆數沿用或可設定，預設 8）
- [ ] 可查詢超過 30 天（依確認後的 `maxSpan`），畫面正常、翻頁正確、頁數與總筆數相符
- [ ] 明細表在 serverSide 模式下，排序與關鍵字搜尋皆正確運作
- [ ] CSV / Excel / Copy 匯出能匯出「整個查詢範圍」的資料，而非只有當前頁
- [ ] 統計 tab（`GetMission`）與柱狀圖（`GetBarChat`）行為與改動前一致
- [ ] `recordsTotal` / `recordsFiltered` / 分頁頁數與實際資料筆數相符
- [ ] 長天數（如 90 天）查詢時，首屏載入時間明顯優於改動前（不再一次傳全部明細 HTML）
- [ ] 既有防呆（壞列略過、車輛未登錄退回顯示）在分頁後仍生效，不會因單筆壞列造成整批失敗
