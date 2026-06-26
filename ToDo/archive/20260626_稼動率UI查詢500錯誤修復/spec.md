# 稼動率 UI 查詢 500 錯誤修復 — 需求規格書

## 主題與背景

客戶（迅得／高技 二廠）反映 SCP「歷史任務統計（稼動率）」頁面查詢異常：

- 只有原本 1F 那台「1 號車」查得到歷史紀錄。
- 而且只能查「一個月內」，區間拉長就出問題。
- 其他車輛完全查不到。

經程式碼追查，根因為 `WebGui/SCP/Controllers/TasksController.cs` 的 `GetSearchData()`
對資料無防呆，任一筆異常資料即讓整支 API 拋例外回 **HTTP 500**，前端表格／直條圖全空白。

瀏覽器 Network（607005.jpg）顯示三支 XHR 同時 500：
`GetMission` / `GetBarChat` / `GetTasks`（皆共用 `GetSearchData()`）。

## 根因盤點（程式碼佐證）

| 位置 | 程式碼 | 例外 | 對應症狀 |
|------|--------|------|---------|
| `TasksController.cs:85` | `oShuttle.Where(...).FirstOrDefault().GustomerName` | 選到未登錄車輛 → `FirstOrDefault()` 回 null → NullReference | 「其他車查不到」 |
| `TasksController.cs:112` | `m.EndTime.Substring(0, 8)` | `EndTime` 為 null（未完成／取消任務）→ NullReference | 「區間拉長就掛」 |
| `TasksController.cs:116-117` | `BeginTime/EndTime.Substring(8, 4)` | 字串長度不足 12 碼 → ArgumentOutOfRange | 「區間拉長就掛」 |
| `TasksController.cs:112,116,117` | `DateTime.ParseExact(...)` | 格式非 `yyyyMMdd`/`HHmm` → FormatException | 「區間拉長就掛」 |
| 三支 Action | 無 try/catch | 上述任一例外直接冒泡 → HTTP 500 | 表格／圖全空白 |

`ubMission.BeginTime` / `ubMission.EndTime` 在 model（`Models/ubMission.cs:34-36`）皆為
**可為 null 的字串**，現場資料不保證每筆都是完整 12 碼 `yyyyMMddHHmm`。
區間越長／車輛越多 → 撈進壞資料的機率越高 → 越容易整批拋例外，與客戶症狀完全吻合。

## 需求範圍

### 包含
- `GetSearchData()` 加資料防呆：過濾／安全解析 null、空字串、長度不足、格式異常的時間欄位。
- `:85` 車輛名稱改安全取值，未登錄車輛不得造成 NRE。
- 三支 Action（`GetMission`/`GetBarChat`/`GetTasks`）加 try/catch + Warning Log（符合「禁止空 catch」規則），失敗回友善訊息而非裸 500。
- 資料面調查：確認 `oShuttle` 是否漏登錄二廠新車，釐清「其他車查不到」是程式問題或資料問題。

### 不包含
- 不重寫稼動率統計演算法／報表 UI 版面。
- 不處理 `說明.txt` 內其他題目（出電梯呼叫釋放掛掉 / 重複發報 API / v-cut 避障 / scp 區間查詢——若同屬此頁再評估）。
- 不新建測試專案（SCP/WebGui 無測試專案、邏輯耦合 EF DbContext，本次以實機／手動查詢驗證）。

## 限制條件

- **編碼**：`TasksController.cs` 修改前先確認編碼（UTF-8 BOM 檢查），避免混合編碼造成中文亂碼。
- **目標框架**：SCP 為 ASP.NET Core MVC（依 `SCP.sln`），以 `dotnet build` 驗證建置。
- **資料來源**：`ubMission`、`oShuttle`、`pShift` 皆來自 `agvDB_1400004` 資料庫；資料調查需現場或測試 DB 連線，無法純靜態完成的部分標記為需現場協助。

### 資料流

| 操作 | 資料來源 | 讀寫方式 | 失敗時行為 |
|------|---------|---------|----------|
| 查歷史任務 | DB `ubMission` | EF Core `_DBContext.ubMission.Where(...).ToList()` | 解析失敗的列「略過」，不整批拋例外 |
| 車輛名稱對照 | DB `oShuttle` | EF Core `FirstOrDefault(ShuttleId==x)` | 找不到 → 回退顯示「車輛{id}」，不 NRE |
| 班別對照 | DB `pShift` | EF Core `ToList()` 後記憶體解析 | 維持現狀（本次不動，但納入回歸觀察） |

## 驗收標準

- [ ] 選「所有車輛」+ 跨月區間（含未完成／取消任務）查詢，三支 API 皆回 200，不再 500。
- [ ] 選二廠任一新車查詢，不再因 `oShuttle` 找不到而 500；查無資料時正常顯示空表。
- [ ] `ubMission` 含 null／長度不足／格式異常的時間欄位時，該列被安全略過，其餘資料正常統計。
- [ ] 三支 Action 任一例外時，伺服器有 Warning Log 記錄（含查詢參數），前端不顯示裸 500。
- [ ] 已釐清「其他車查不到」屬程式問題或 `oShuttle` 資料漏登錄，並於工作計畫記錄結論（含後續補資料建議，若需要）。
