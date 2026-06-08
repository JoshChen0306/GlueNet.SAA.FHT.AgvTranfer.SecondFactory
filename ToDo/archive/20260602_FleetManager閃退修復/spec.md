# 需求規格書 — FleetManager 閃退修復

## 主題與背景

高技二廠（DB `agvDB_1400004` @ 現場 127.0.0.1）的 **agvFleetManager（實際 exe = `cTest.exe`，svrPair.cPair 配對程序）** 反覆閃退。

### 根因（已逐行驗證，高信心）

1. **直接觸發**：`oNeed` 表存在一筆「毒資料」，在 `cPair.ProcessoNeedToRequire`（`Dispatch/svrPair/cPair.cs:319`）以**字串拼接**組 SQL 時造成 `SqlException`。最可能是 `WorkOrder` / `RackId` 含**單引號 `'`**（平板可自由輸入；log 可見 `yes`、`cycuyycy` 等人為測試輸入），單引號打斷 SQL 字串字面值 → 語法錯誤。
2. **為何閃退而非略過（結構性根因）**：
   - `SqlHelper.WriteSqlByAutoOpen/QuerySqlByAutoOpen`（`Dispatch/svrPair/SqlHelper.cs:151,209`）重試 2 次後 **`throw` 往上拋**，不吞例外。
   - `cPair.MainProcess`（`cPair.cs:100-142`）在背景執行緒 `mThread` 上跑，**整段 while 迴圈無任何 try/catch**。
   - 全 solution 搜尋 `UnhandledException`/`ThreadException`/`DispatcherUnhandledException` → **零命中**，無任何全域攔截。
   - .NET 2.0 起，背景執行緒未攔截例外 → **整個進程被終止**。
3. **為何反覆閃退**：毒資料持續存在 `oNeed`，每次重啟都讀到它、在同一步再爆。

### Log 證據

| 時間 | 現象 |
|------|------|
| 5/31 02:22:22 | 跑到 `05.處理平板配對` 後死，整日未再起 |
| 6/01 15:10 / 15:11 / 17:20 | 三次重啟，每次都死在 `05.處理平板配對`（下一步 `09.產生oRequire` 從未出現）|

正常迴圈為 `05→09→10→11→12→20→…→32→05…`；閃退時 log 卡在 `05` 後消失，證明死在 `ProcessoNeedToRequire` 的 SQL（`cPair.cs:339/347/354`）。

### Schema 關鍵發現（DESKTOP-2I3FKA2 已連線確認）

- `oNeed.WorkOrder` = `nvarchar(100)`、`oNeed.RackId` = `nvarchar(50)`
- **`oRequire.RackId` = `nvarchar(20)`（比 oNeed 短！）** → oNeed RackId 超過 20 字塞進 oRequire 會 **truncation 例外**，是「就算參數化也照炸」的第二個天然炸點，將作為韌性測試素材。

## 需求範圍

### 包含

1. **止血**：讓單筆毒資料不再拖垮整個進程，現場最快恢復。
2. **診斷強化**：`05` 步驟補印當下 `ObjStation/EndStation/WorkOrder/RackId`，下次能從 log 直接點名毒資料。
3. **隔離**：捕捉到「資料類例外」時，將該筆 `oNeed` 標記 `X`（異常），交由既有 `RecyclingoNeedByAssignFlag` 清除、不再重試；「連線類例外」則保留重試。
4. **全域攔截網**：`cTest/Program.cs` 加 `AppDomain.UnhandledException` + `Application.ThreadException`，把 stack trace 寫進 log（最後防線）。
5. **治本 — SQL 參數化**：`cPair.cs` 全檔字串拼接 SQL 改為 `SqlParameter`，杜絕特殊字元造成的語法錯誤與 SQL injection。`SqlHelper` 新增參數化多載。
6. **測試**：以打真實 DB（DESKTOP-2I3FKA2）的整合測試保護以上行為（紅→綠）。

### 不包含

- **SCP / 平板 / WEB 前端輸入驗證**（另開工作；前端擋字元為治標，本案以後端參數化治本）。
- `cPair` 的演算法 / 派送邏輯變更（僅改錯誤處理與 SQL 組法，不動業務邏輯）。
- 一廠（OneFactory）對應程式（僅二廠 SecondFactory）。

## 限制條件

### 資料流

| 操作 | 資料來源 | 讀寫方式 | 失敗時行為 |
|------|---------|---------|----------|
| 讀取待處理 oNeed | DB `agvDB_1400004` | `QuerySqlByAutoOpen` | 拋例外 → 由新加的 try/catch 接住，記 log 後跳過該輪/該筆 |
| 產生 oRequire（含使用者輸入 WorkOrder/RackId）| DB | `WriteSqlByAutoOpen`（改參數化多載）| 資料類例外 → 隔離標 X；連線類例外 → 保留重試 |
| 標記毒資料 oNeed = X | DB | 參數化 UPDATE，以 `TaskDateTime`（程式產生純數字、無注入風險）為 key | 標記失敗本身也記 log，不可再拋出拖垮迴圈 |
| 全域未攔截例外 | 進程 | `AppDomain.UnhandledException` / `Application.ThreadException` | 僅記 log（進程已終止，無法復原）|

### 例外分類規則（隔離 vs 重試）

- **資料類（隔離標 X）**：`SqlException` 且為語法錯誤 / 字串截斷 / 約束違反等「重試也不會好」的錯誤。
- **連線類（保留重試）**：連線逾時、連線中斷、`database is locked` 等暫時性錯誤 → 不標 X，下輪重試，避免 DB 短暫抖動誤判隔離好資料。

### 測試環境限制

- **只連 `DESKTOP-2I3FKA2`（開發機）**，FleetManager 服務未在此跑，建刪測試資料安全。
- **嚴禁連 `127.0.0.1`（現場）**：測試 base class 偵測到連線字串為 127.0.0.1 立即 `Assert.Inconclusive` 拒跑。
- 隔離：測試資料 `TaskSource='UTEST'` + 測試專屬站號；`TestCleanup` 無論成敗一律清除。
- 已獲使用者同意：可在開發機 DB 暫時新增/刪除 `oPort` 測試站與 `oNeed`/`oRequire` 測試資料。

### 編碼

- `cPair.cs` = UTF-8 BOM（可直接改）。
- **`SqlHelper.cs` = Big5/No-BOM**：新增參數化多載前須**整檔轉 UTF-8 BOM（0 邏輯變更、獨立 commit）**，再行修改，避免混合編碼亂碼。
- 新建 `.cs` / `.md` 一律 UTF-8 BOM。

## 驗收標準

### 🧪 診斷強化 + 單筆隔離（Layer B）

- [ ] Given `oNeed` 含一筆 RackId 30 字（對 oNeed 合法、塞 oRequire 會 truncation）的毒資料置於正常筆之前，When 呼叫 `GenerateoRequireByoNeed()`，Then 方法**不向外拋例外**
- [ ] Given 同上，When 處理完成，Then 毒資料那筆 `oNeed.AssignFlag` 被標為 `X`（隔離），且後續正常筆仍成功產生 `oRequire`
- [ ] Given 毒資料觸發例外，When 捕捉，Then log 記錄該筆 `ObjStation/EndStation/WorkOrder/RackId` 與例外訊息（非空 catch）
- [ ] Given 連線類例外（模擬），When 捕捉，Then 該筆**不**標 X、保留下輪重試

### 🧪 SQL 參數化正確性（Layer A）

- [ ] Given `oNeed.WorkOrder = O'Brien`（含單引號）的合法配對筆，When 呼叫 `GenerateoRequireByoNeed()`，Then 不丟例外且 `oRequire` 出現該筆、`WorkOrder` 原值完整保存
- [ ] Given `WorkOrder = x');DROP TABLE oRequire;--`，When 處理，Then 原值完整存入且 `oRequire` 資料表仍存在（防注入）
- [ ] Given `RackId` 含單引號，When 處理，Then 原值完整保存、不丟例外

### 🧪 Happy path 回歸（Layer C）

- [ ] Given 正常 A 區配對 oNeed（合法工單、oPort 起終點 UseFlag=Y 且未註冊），When 呼叫 `GenerateoRequireByoNeed()`，Then 正確產生 oRequire、oNeed 標 Y、oPort 註冊 BgnToEnd（與改前行為一致）

### 一般項

- [ ] `MainProcess` while 迴圈外層具 try/catch，任一步驟例外不終止進程、記 log 後續行
- [ ] `cTest/Program.cs` 已掛 `AppDomain.UnhandledException` + `Application.ThreadException`，觸發時寫入 log（手動驗證）
- [ ] `SqlHelper.cs` 已整檔轉 UTF-8 BOM 且新增參數化多載；轉碼 commit 與功能 commit 分離
- [ ] `cPair.cs` 內**所有**字串拼接 SQL 已改參數化（逐處盤點，無遺漏）
- [ ] 既有 3 個 `GetoPort` 測試在開發機 DB 仍通過（無回歸）
- [ ] 整體建置 0 error
