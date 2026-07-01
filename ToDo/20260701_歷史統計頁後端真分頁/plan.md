# 技術方案書 — 歷史任務統計頁後端真分頁

## 選定方案概述

**方案 A：後端真分頁（DataTables serverSide + 記憶體切片）**

將明細 tab（`GetTasks` + `#detail-table`）從 client-side 分頁改為 **server-side 分頁**：前端 DataTables 開啟 `serverSide: true`，每次翻頁 / 排序 / 搜尋都向後端要「當頁」資料；後端在 `GetSearchData` 產出完整 `List<TaskReport>` 後，於記憶體套用搜尋、排序、`Skip/Take` 切片，只回傳當頁 JSON。

## 架構設計

### 元件關係與資料流向（改造後）

```
前端 detail-table (DataTables serverSide:true)
   │  ajax GET /Tasks/GetTasks?draw&start&length&search[value]&order...
   ▼
TasksController.GetTasks(DataTables 參數)
   │  1. GetSearchData(startDate,endDate,shuttleId,shiftId) → List<TaskReport>（全量運算，沿用現況）
   │  2. 套用 search[value] 過濾  → recordsFiltered
   │  3. 套用 order 排序
   │  4. Skip(start).Take(length)  ← 只取當頁（PaginatedList 或等效）
   ▼
回傳 JSON { draw, recordsTotal, recordsFiltered, data:[...] }
   ▼
前端只 render 當頁 N 列

匯出（CSV/Excel）：另走 /Tasks/ExportTasks 全量端點 → 一次輸出整個查詢範圍
```

### 關鍵改動點

| 檔案 | 改動 |
|------|------|
| `Controllers/TasksController.cs` | `GetTasks` 改接 DataTables serverSide 參數、回傳 JSON；新增 `ExportTasks` 全量匯出端點 |
| `Commons/PaginatedList.cs` | 啟用（或以 `Skip/Take` 等效切片）；目前為死程式碼 |
| `wwwroot/js/tasks.js` | `#detail-table` DataTable 改 `serverSide:true` + `ajax` + `columns` 定義；匯出 button 指向全量端點 |
| `Views/Tasks/_TaskDetialPartial.cshtml` | serverSide 後資料由 ajax 供給，partial 的 `foreach` render 可移除（改由 DataTables 以 JSON 建列） |
| `wwwroot/js/common/searchdate.js` | 放寬 `maxSpan`（值與使用者確認） |

## 方案分析（六面向）

1. **方案概述**：明細改 server-side 分頁，單次只傳當頁；匯出另走全量端點。
2. **架構設計**：見上。沿用既有 `GetSearchData` 運算結果，僅在其後加「過濾 → 排序 → 切片」與 JSON 封裝，改動內聚。
3. **優點**：
   - 根治網路傳輸與瀏覽器負擔：天數/筆數再大，單次 request 只傳當頁。
   - 首屏載入時間與資料量脫鉤。
   - 重用既有 `GetSearchData` 與防呆邏輯，不動 SQL/資料模型，行為風險低。
4. **風險與缺點**：
   - **匯出退化陷阱**：serverSide 後 DataTables 內建匯出只含當前頁 → 必須另做全量匯出端點（已列為子任務）。
   - **每次翻頁重算**：因班次運算在記憶體，翻頁會重跑 `GetSearchData` 全量撈取＋運算（只回當頁）。SQL/運算層未治本。
   - 前端排序／搜尋語意需與後端一致（欄位 index 對應、日期字串排序）。
5. **技術債評估**：
   - 遺留技術債：SQL 全量撈取＋記憶體班次運算。後續若量再放大，需將分頁/過濾下推 SQL（重寫 `GetSearchData` 或調整資料模型），屬獨立較大任務。
   - 本方案未擴大既有耦合，`ExportTasks` 與 `GetTasks` 共用 `GetSearchData`，維護成本可控。
6. **建議適用情境**：查詢天數放寬、明細筆數可能達數千～數萬、但短期不打算重構 SQL 查詢的情況 —— 即本需求。

## 被捨棄的方案

- **方案 B：放寬 maxSpan + 後端筆數上限防呆**（折衷）
  - 捨棄原因：僅緩解、非治本；大範圍查詢仍會被上限截斷或變慢，客戶「開放長天數」需求無法完整滿足。
- **方案 C：只放寬 maxSpan**（最小）
  - 捨棄原因：不動後端，現場資料量大時效能/卡頓風險直接暴露給客戶。
- **SQL 層真分頁（把班次運算與分頁下推 SQL）**
  - 捨棄原因（本次）：班次（pShift）運算為記憶體多層迴圈，下推 SQL 需重寫查詢或改資料模型，風險與工作量大。列為後續技術債，不在本次範圍。
