# 技術方案書 — FleetManager 閃退修復

## 選定方案：方案 2 — 止血 + 診斷 + 全檔 SQL 參數化（完整治本）

### 方案概述
一句話：後端三層防護（單筆隔離 + 迴圈保命 + 全域攔截）先止血，再以 `SqlParameter` 全檔參數化根除特殊字元/注入問題，並用打真實 DB 的整合測試（紅→綠）保護改動。

### 架構設計

```
cTest.exe (WinForms, 進入點 Program.Main)
  └─ [新增] AppDomain.UnhandledException + Application.ThreadException  ← 第3層：全域攔截網（僅記 log）
        frmMain.btnAuto_Click → cPair.BgnPair()
            └─ mThread → cPair.MainProcess()  (背景執行緒, while(mGo))
                  ├─ [新增] while 迴圈體外層 try/catch + continue        ← 第2層：迴圈保命
                  │     ├─ GenerateoRequireByoNeed()
                  │     │     └─ foreach (oNeed row)
                  │     │           └─ [新增] per-row try/catch          ← 第1層：單筆隔離（核心止血點）
                  │     │                 ├─ 資料類例外 → 標 oNeed.AssignFlag='X' + log（含站號/工單/RackId）
                  │     │                 └─ 連線類例外 → 不標 X、log、保留重試
                  │     │           └─ ProcessoNeedToRequire() ──┐
                  │     ├─ GenerateoMissionByoRequire()          │  [改] 全部 SQL
                  │     ├─ Recycling*()                          │  字串拼接 → SqlParameter
                  │     └─ Generate*ByMCS*()  ───────────────────┘
                  │
SqlHelper (Big5→UTF-8 BOM 轉碼後)
  ├─ 既有：QuerySqlByAutoOpen(string) / WriteSqlByAutoOpen(string)   ← 保留不動
  └─ [新增] 參數化多載：
        QuerySqlByAutoOpen(string sql, params SqlParameter[] ps)
        WriteSqlByAutoOpen(string sql, params SqlParameter[] ps)

svrPairTests (整合測試, 連 DESKTOP-2I3FKA2)
  ├─ [新增] IntegrationTestBase：127.0.0.1 防呆 + UTEST 哨兵 + 種/清測試站
  ├─ [新增] GenerateoRequireByoNeed_ResilienceTests   (Layer B)
  └─ [新增] GenerateoRequireByoNeed_ParameterizationTests (Layer A + C)
```

### 三層防護為何都要

| 層 | 位置 | 守備範圍 | 可測性 |
|----|------|---------|--------|
| 1 單筆隔離 | `GenerateoRequireByoNeed` foreach 內 | 已知閃退點（05 後）；能拿到該筆 `dr` → 才能精準隔離標 X | 直接呼叫 public 方法 → Layer B 整合測試 |
| 2 迴圈保命 | `MainProcess` while 外層 | 其餘步驟（GenerateoMission/Recycling/其他 Generate）的非預期例外 | 無法測（無窮迴圈）→ 防禦縱深 |
| 3 全域攔截 | `cTest/Program.cs` | 任何漏網之魚 → 進程終止前留下 stack trace | 手動驗證 |

### 測試策略：整合測試（打真實 DB）而非 Mock

- **理由**：本 bug 的本質是「C# 字串處理 ↔ 真實 SQL Server 解析器」的互動。Mock 只能驗「有無呼叫參數化 API」，驗不到真實 DB 收不收、原值是否完整 round-trip。整合測試最誠實。
- **不需** 抽 `ISqlHelper` / 注入 / Moq → `GenerateoRequireByoNeed()` 本就是 `public`，可直接呼叫測試 → 重構面積最小。
- **韌性測試**用真實會炸的資料（`oRequire.RackId nvarchar(20)` truncation），不必偽造例外。
- 與既有 `GetoPort` 測試（同樣打真實 DB）風格一致。

### 優點
- 治本：參數化後前端輸入任何字元都安全，連注入一併防掉，且不誤殺合法字元。
- 止血快：第 1、2 層先上即可讓現場不再閃退（不必等參數化全部完成）。
- 診斷可持續：日後再有毒資料，log 直接點名，不必連 DB 撈。
- 測試誠實且重構面積小（不動 cPair 建構子）。

### 風險與缺點
- 改動面大：`cPair.cs` 全檔 SQL 逐處改參數化，需逐一盤點避免遺漏（推論安全規則：用 Grep 列全部 SQL 點再逐一改）。
- `SqlHelper.cs` 須先整檔轉碼（Big5→UTF-8 BOM），轉碼若不慎會造成混合編碼亂碼 → 嚴格獨立 commit、轉完先 build 確認。
- 整合測試依賴開發機 DB 連線與 schema，CI 無 DB 時這些測試需標記/排除（屬已知環境限制）。
- 隔離標 X 的例外分類若判斷錯（把連線類誤判資料類）會誤隔離好資料 → 分類規則須保守，預設「無法判定 → 不隔離、保留重試」。

### 技術債評估
- 參數化後 `cPair` 的 SQL 可讀性、安全性大幅提升，技術債下降。
- 殘留：`cPair` 仍直接 new `SqlHelper`（無 DI），未來若要做更細的單元測試仍需抽介面；本案刻意不做（YAGNI），列為未來可選。
- `MainProcess` 仍為無窮迴圈 + Thread.Sleep 輪詢架構，本案不動（超出範圍）。

### 建議適用情境
適用於「現場正在閃退、需快速止血又要根治」的情況——先上第 1/2/3 層止血，再分批完成參數化，每步有測試保護。

## 被捨棄的方案

### 方案 1：止血 + 診斷（最小，不動 SQL 拼接）
- 概述：只加 try/catch + 補 log + 全域攔截，SQL 維持字串拼接，靠前端擋特殊字元。
- 捨棄原因：治標。前端擋字元防不住其他寫入途徑（WEB/其他程式/人工改 DB），且難窮舉該擋哪些字元、易誤殺合法工單。使用者明確選擇治本。

### 方案 3：止血 + 只參數化高風險寫入
- 概述：只把含使用者自由輸入欄位（WorkOrder/RackId）的 INSERT/SELECT 參數化，其餘維持。
- 捨棄原因：折衷但留死角——其他拼接點仍可能因未來資料變化出錯，且「哪些算高風險」需主觀判斷。使用者選擇全檔參數化一次到位。

## 測試 Fixture 設計要點（供實作參考）

- 測試站號用**明顯不存在的哨兵**（如 `A91` 起點、`B92` 終點；`A` 屬平板群組才會進 `05.處理平板配對` → `ProcessoNeedToRequire`）。
- `oPort` 須先種 `A91`/`B92`：`UseFlag='Y'`、`BgnToEnd` 空、`HaveFlag` 視案例設定。`oPort` 無 `TaskSource` 欄 → cleanup 以 `StationNo IN ('A91','B92')` 精準刪除。
- `oNeed`/`oRequire` 測試資料 `TaskSource='UTEST'` → cleanup `DELETE WHERE TaskSource='UTEST'`。
- Layer A/B 的 oNeed `WorkOrder` 須非空（Block 'A' 空工單會走 `06.處理異常資料` 取消路徑，到不了 INSERT）。
- 確保 `B92` 無 `oPortBinding` 綁定（否則先進 `CheckAndHandleEmptyPlateRecovery` 卡控、不進 05）。
- `TestCleanup` 用 `[TestCleanup]`，無論斷言成敗一律執行清除。
