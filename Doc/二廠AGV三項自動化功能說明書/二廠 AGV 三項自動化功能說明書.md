# 二廠 AGV 三項自動化功能說明書

**專案名稱：** GlueNet SAA FHT AGV Transfer — 二廠
**版本：** v1.0
**日期：** 2026-03-23
**適用對象：** 系統整合工程師

---

## 目錄

1. [文件目的](#1-文件目的)
2. [功能總覽](#2-功能總覽)
3. [功能一：二樓站內運輸（空平板自動派送）](#3-功能一二樓站內運輸空平板自動派送)
4. [功能二：AGV 自動復歸](#4-功能二agv-自動復歸)
5. [功能三：預派調度（跨樓層預調度機制）](#5-功能三預派調度跨樓層預調度機制)
6. [完整部署 SOP](#6-完整部署-sop)
7. [常見問題與排查](#7-常見問題與排查)

---

## 1. 文件目的

本文件說明二廠 AGV 系統新增的三項自動化功能：

1. 二樓站內運輸（空平板自動派送）
2. AGV 自動復歸
3. 預派調度（跨樓層預調度機制）

閱讀本文件後，您將了解：
- 每項功能的用途與運作邏輯
- 部署時需要在資料庫新增哪些資料
- 如何驗證功能是否正常運作

---

## 2. 功能總覽

| 功能名稱 | 觸發條件 | 核心目的 | 影響範圍 |
|---------|---------|---------|---------|
| 二樓站內運輸 | 上料區（O/P）有需求，但下料區（Q）無空平板 | 自動回收空平板，確保生產線不中斷 | 2F 站點 O、P、Q、M、R |
| AGV 自動復歸 | 跨樓層車輛閒置超過設定秒數，且不在母樓層（4F） | 自動將車輛送回 4F 待命，縮短任務等待時間 | 跨樓層車輛（車號 1） |
| 預派調度 | 有跨樓層任務，但車輛不在任務起點樓層 | 自動先調車到起點樓層，再執行正式任務 | 跨樓層任務派發佇列 |

> **注意：** 三項功能彼此有交互關係：自動復歸與預派調度共用同一個管理器（CrossFloorManager）；當新的跨樓層任務進入時，若歸位尚未派發，系統會取消歸位計時，優先處理任務。

---

## 3. 功能一：二樓站內運輸（空平板自動派送）

### 3.1 功能說明與背景

在二樓製程中，AGV 負責將物料搬運至上料區（O 站、P 站）。機台取料後，空平板需放置於下料區（Q 站）等待回收。

**問題根源：** 當 AGV 要派物料到上料區時，若下料區沒有空平板，機台無處放置空架，導致流程卡住。

**解決方式：** 系統在產生物料搬運任務之前，先自動偵測下料區的空平板狀態。若狀態不符合，自動產生「空平板回收任務」優先處理，再繼續物料派送。整個流程無需人工介入。

### 3.2 站點角色說明

| 站點代號 | 角色 | HaveFlag 值說明 |
|---------|------|----------------|
| O1 ～ O8 | 上料區（Loading Port） | 放置待上機台的物料 |
| P1 ～ P9 | 上料區（Loading Port） | 放置待上機台的物料 |
| Q1 ～ Q9 | 下料區（Unloading Port） | 0 = 空架、1 = 空平板、3 = 料盤 |
| M 區 | 緩衝暫存區（Fallback） | 空平板回收的候補停放位置 |
| R 區 | 緩衝暫存區（Fallback） | 空平板回收的候補停放位置（次選） |

### 3.3 運作邏輯（情境對照表）

每次 svrPair 主循環執行時，會呼叫 `CheckAndHandleEmptyPlateRecovery()` 進行以下判斷：

| 情境 | 上料區狀態（HaveFlag） | 下料區狀態（HaveFlag） | 系統行為 | 物料派送 |
|------|---------------------|---------------------|---------|---------|
| 1 | ≠ 1（無空板） | = 1（有空板） | 下料區已備妥，直接派物料 | [ 立即 ] |
| 2 | ≠ 1（無空板） | ≠ 1（無空板） | 下料區無空板，等待人工補料 | [ 暫停 ] |
| 3 | = 1（有空板） | = 1（已有空板） | 先將上料區空板送 FallbackAreas（M→Q→R 依序找空位） | [ 等回收完成 ] |
| 4 | = 1（有空板） | = 0（空架） | 直接將上料區空板送到下料區 | [ 等回收完成 ] |
| 5 | = 1（有空板） | = 3（有料盤） | 下料區有料盤，無法放置空板，等待 | [ 暫停 ] |

> **注意：** 回收任務會以 `TaskSource = 'PLATE_RECOVERY'` 寫入 oNeed 表。系統不會重複產生，若偵測到相同來源的進行中任務則跳過，下一輪再評估。

*[請插入 空平板回收流程示意圖]*

### 3.4 需新增的資料庫資料

此功能需要建立 `oPortBinding` 資料表，並填入上下料區的綁定關係。

#### oPortBinding 表結構

執行 `Deploy_oPortBinding.sql` 建立資料表：

| 欄位名稱 | 資料型別 | 說明 | 範例值 |
|---------|---------|------|-------|
| LoadingPort | nvarchar(20) | 上料區站點編號 | O1、P1 |
| UnloadingPort | nvarchar(20) | 對應的下料區站點 | Q1 |
| FallbackAreas | nvarchar(100) | 空板回收候補區域，逗號分隔，系統依序尋找空位 | M,Q,R |
| UseFlag | nvarchar(1) | 是否啟用此綁定 | Y（啟用）/ N（停用） |

#### 初始綁定資料

`Deploy_oPortBinding.sql` 已內含以下初始資料（可重複執行，已存在的會跳過）：

| LoadingPort | UnloadingPort | FallbackAreas | UseFlag |
|------------|--------------|--------------|--------|
| O1 | Q1 | M,Q,R | Y |
| O2 | Q2 | M,Q,R | Y |
| O3 ～ O8 | Q3 ～ Q8 | M,Q,R | Y |
| P1 | Q1 | M,Q,R | Y |
| P2 ～ P9 | Q2 ～ Q9 | M,Q,R | Y |

> O 與 P 站可對應同一個 Q 站（共用下料區），例如 O1 和 P1 都對應 Q1，這是正常設計。

#### 後台管理介面

執行 `Deploy_pFunction_PortBindingMenu.sql` 後，後台選單會新增「上下料區綁定管理」頁面，可進行：
- 新增綁定關係
- 修改 FallbackAreas 候補順序
- 啟用／停用特定綁定（UseFlag）
- 刪除已不需要的綁定

*[請插入 後台 PortBinding 管理介面截圖]*

### 3.5 測試步驟與驗證

測試前先確認 `oPortBinding` 表有正確資料，`oPort` 各站點 `UseFlag = 'Y'`。

可手動修改 `oPort` 表的 `HaveFlag` 欄位來模擬各種情境：

| 測試情境 | oPort 手動設定 | 預期結果 | 驗證方式 |
|---------|-------------|---------|---------|
| 正常派送 | Q1.HaveFlag = 1，O1.HaveFlag ≠ 1 | 物料直接派送到 O1 | oMission 出現物料搬運任務 |
| 觸發回收（直送下料區） | O1.HaveFlag = 1，Q1.HaveFlag = 0 | 空板從 O1 送到 Q1 | oNeed 出現 TaskSource='PLATE_RECOVERY' |
| 觸發回收（送候補區） | O1.HaveFlag = 1，Q1.HaveFlag = 1 | 空板從 O1 送到 M 或 R | oNeed 出現 TaskSource='PLATE_RECOVERY'，目的地為 M |
| 無法派送（下料區有料盤） | Q1.HaveFlag = 3 | 物料 oNeed 卡住不派送 | Log 顯示等待，oMission 無新任務 |

> 觀察 Log 中的 `CheckAndHandleEmptyPlateRecovery` 訊息，可確認每個情境的判斷路徑是否正確。

---

## 4. 功能二：AGV 自動復歸

### 4.1 功能說明與背景

跨樓層車輛（目前指定車號 1）在完成任務後，可能停留在 1F 或 2F 待命。當下一筆任務的起點是 4F 時，必須先等車輛自行移動到 4F，增加等待時間。

**解決方式：** 車輛進入 IDLE 狀態後，若不在母樓層（4F），系統開始計時。超過設定時間後，自動產生歸位任務將車輛送回 4F，維持隨時待命的狀態。

### 4.2 運作邏輯

Dispatch 主循環每秒呼叫 `CrossFloorManager.Tick()`，執行以下狀態判斷：

| 情境 | 車輛 Status | 車輛位置 | 計時狀態 | 系統行為 |
|------|-----------|---------|---------|---------|
| 1 | R（執行中） | 任意 | — | 不處理，等任務完成 |
| 2 | I（IDLE） | 4F（母樓層） | — | 重置計時，不需歸位 |
| 3 | I（IDLE） | 非 4F | 未啟動 | 啟動閒置計時 |
| 4 | I（IDLE） | 非 4F | 計時中，未超時 | 繼續等待 |
| 5 | I（IDLE） | 非 4F | 已超時 | 產生 IDLE_RETURN 任務，寫入 oMission |
| 6 | I（IDLE） | 非 4F | 計時中 | 新任務進入 → 取消計時，直接派新任務 |
| 7 | I（IDLE） | 非 4F | 歸位已派發 | 等待 AGV Callback 回報完成 |
| 8 | I（IDLE） | 非 4F | 歸位執行中 | 新任務進入 → 排入佇列，等歸位完成後重評估 |

*[請插入 AGV 自動復歸狀態流程示意圖]*

> 歸位任務在 `oMission` 表中以 `TaskSource = 'IDLE_RETURN'` 標記，可用此欄位區分系統自動產生的歸位任務與一般搬運任務。

### 4.3 需調整的設定檔

修改 AGV WebAPI 的設定檔 `FHtSetting.config`（路徑：`ACC/HikAGVWebAPI/HikAGVWebAPI/Config/`）：

| 參數名稱 | 說明 | 預設值 | 注意事項 |
|---------|------|-------|---------|
| CrossFloorShuttleId | 指定哪一台車是跨樓層車輛 | 1 | 填入車輛編號，目前只支援單台 |
| IdleReturnTimeout | 閒置超過幾秒後觸發歸位 | 300（5 分鐘） | 測試時可先設 30，測完改回 |
| IdleReturnFloor | 歸位目的樓層 | 4F | 與現場確認後再調整 |
| MapCodeFloorMapping | MapCode 對應樓層（逗號分隔） | AA:1F,BB:2F,DD:3F,FF:4F | 需與 RCS 系統的 MapCode 一致 |

### 4.4 需新增的資料庫資料

此功能需要為 `oShuttle` 資料表新增 `UpdateTime` 欄位，供系統判斷車輛連線狀態。

#### oShuttle 新增 UpdateTime 欄位

執行 `Deploy_oShuttle_AddUpdateTime.sql`，語法如下：

```sql
ALTER TABLE oShuttle ADD UpdateTime datetime NULL
```

**用途說明：** 程式每秒更新此欄位。若超過 60 秒未更新，`CrossFloorManager` 判定車輛離線，暫停歸位計時。

> **警告：** 若 oShuttle 表已存在 UpdateTime 欄位（舊版已加過），請勿重複執行，否則會報錯。可先執行 `SELECT UpdateTime FROM oShuttle` 確認欄位是否存在。

### 4.5 測試步驟與驗證

測試前確認：`FHtSetting.config` 中 `CrossFloorShuttleId` 正確、`MapCodeFloorMapping` 對應正確、`IdleReturnTimeout` 設為 30 秒（方便觀察）。

| 步驟 | 操作 | 預期結果 | 驗證方式 |
|------|------|---------|---------|
| 1 | 讓跨樓層車輛執行一筆任務到非 4F 樓層（如 2F） | 任務完成後車輛進入 IDLE 狀態 | oShuttle.Status = 'I' |
| 2 | 等待超過 IdleReturnTimeout 秒數（30 秒） | 系統自動產生歸位任務 | oMission 出現 TaskSource='IDLE_RETURN' |
| 3 | 確認車輛接受任務並移動 | 車輛移動至 4F 電梯等待點 | oShuttle.MapCode 變更為 4F 對應代碼 |
| 4 | 歸位完成後，確認 Callback 正確 | 系統重置計時狀態 | Log 出現 OnIdleReturnCompleted 訊息 |

> **警告：** 若車輛 UpdateTime 超過 60 秒未更新，系統會視為離線，不啟動計時。測試前請確認 AGV WebAPI 正常收到車輛心跳：
> ```sql
> SELECT UpdateTime, DATEDIFF(SECOND,UpdateTime,GETDATE()) AS Sec
> FROM oShuttle WHERE ShuttleId='1'
> ```

---

## 5. 功能三：預派調度（跨樓層預調度機制）

### 5.1 功能說明與背景

當系統有跨樓層任務需要派發時，若跨樓層車輛停在 2F，但任務起點在 4F，車輛必須先移動到 4F 才能執行任務。

**問題：** 若沒有管控機制，可能發生：
- 同時派多筆任務給同一台車
- 車輛重複調度（來回移動）
- 任務順序混亂

**解決方式：** 透過 `CrossFloorManager` 的佇列管理，確保一次只派一筆任務，並在必要時自動產生「空車移動任務（CROSS_FLOOR_DISPATCH）」先將車輛調至起點樓層。

### 5.2 任務派發優先順序

`SelectNextMission()` 每次執行時，依下列順序決定要派發哪筆任務：

1. **第一優先：** 若已有 `CROSS_FLOOR_DISPATCH` 預調度任務在進行中 → 等待完成，不派新任務
2. **第二優先：** 佇列中有同樓層任務（車輛目前樓層 = 任務起點樓層）→ 直接派發
3. **第三優先：** 沒有同樓層任務，車輛不在最早任務的起點樓層 → 產生預調度任務送車過去
4. **第四：** 車輛已抵達起點樓層 → 派發正式跨樓層任務

*[請插入 預調度任務佇列流程示意圖]*

### 5.3 情境對照表

| 情境 | 車輛狀態 | 佇列內容 | 車輛 vs 任務起點 | 系統行為 |
|------|---------|---------|--------------|---------|
| 1 | R（執行中） | 任意 | — | 等任務完成，不處理 |
| 2 | I（IDLE） | 空 | — | 無任務，交由自動復歸機制 |
| 3 | I（IDLE） | 含同樓層任務 | 同樓層 | 直接派發同樓層任務 |
| 4 | I（IDLE） | 同/跨樓層混合 | — | 優先派發同樓層任務 |
| 5 | I（IDLE） | 只有跨樓層任務 | 不同樓層 | 產生 CROSS_FLOOR_DISPATCH 預調度任務 |
| 6 | I（IDLE） | 有任務 | 已到起點（預調度完成） | 派發正式跨樓層任務 |
| 7 | 預調度執行中 | 新任務進入 | — | 等預調度完成，Callback 後重評估 |

> 預調度任務在 `oMission` 表中以 `TaskSource = 'CROSS_FLOOR_DISPATCH'` 標記。

### 5.4 需新增的資料庫資料

此功能需要建立 `oTaskTypeRoute` 資料表，用於管理各樓層方向對應的 RCS TaskType 代碼。

#### oTaskTypeRoute 表結構

執行 `Deploy_oTaskTypeRoute.sql` 建立資料表：

| 欄位名稱 | 資料型別 | 說明 | 範例值 |
|---------|---------|------|-------|
| Id | int（自動編號） | 主鍵 | 1, 2, 3... |
| MoveType | nvarchar(20) | 移動類型 | Transport（搬運）/ EmptyMove（空車移動） |
| FromFloor | nvarchar(10) | 起點樓層 | 1F、2F、3F、4F |
| ToFloor | nvarchar(10) | 終點樓層 | 1F、2F、3F、4F |
| TaskType | nvarchar(50) | 對應的 RCS TaskType 代碼 | F001、EM24 |
| UseFlag | nvarchar(1) | 是否啟用 | Y（啟用）/ N（停用） |
| Remark | nvarchar(100) | 備註說明 | 2F 站內搬運 |

#### 初始 TaskType 路由資料

`Deploy_oTaskTypeRoute.sql` 已內含以下初始資料：

| MoveType | FromFloor | ToFloor | TaskType（初始值） | 說明 |
|----------|-----------|---------|-----------------|------|
| Transport | 2F | 2F | F001 | 2F 站內搬運 |
| Transport | 1F | 1F | F002 | 1F 站內搬運 |
| Transport | 2F | 4F | F24Test | 2F → 4F 跨樓層搬運 |
| Transport | 4F | 2F | F42Test | 4F → 2F 跨樓層搬運 |
| Transport | 1F | 3F | F13Test | 1F → 3F 跨樓層搬運 |
| Transport | 3F | 1F | F31Test | 3F → 1F 跨樓層搬運 |
| EmptyMove | 2F | 4F | EM24 | 2F → 4F 空車移動（歸位/預調度） |
| EmptyMove | 4F | 2F | EM42 | 4F → 2F 空車移動 |
| EmptyMove | 1F | 4F | EM14 | 1F → 4F 空車移動 |
| EmptyMove | 4F | 1F | EM41 | 4F → 1F 空車移動 |

> **警告：** TaskType 代碼（如 F001、EM24）必須與 RCS 系統設定完全一致，否則 AGV 會收不到任務。初始值為測試代碼（Test），上正式環境前務必與 RCS 管理員確認並更新。

#### 後台管理介面

執行 `Deploy_pFunction_TaskTypeRouteMenu.sql` 後，後台選單會新增「TaskType 路由管理」頁面，可進行：
- 查看所有路由設定
- 修改特定路線的 TaskType 代碼
- 啟用／停用特定路線

*[請插入 後台 TaskTypeRoute 管理介面截圖]*

### 5.5 測試步驟與驗證

測試前確認：`oTaskTypeRoute` 的 `EmptyMove` 路由資料齊全，且 TaskType 代碼在 RCS 中已設定。

| 步驟 | 操作 | 預期結果 | 驗證方式 |
|------|------|---------|---------|
| 1 | 讓跨樓層車輛停在 2F（非任務起點） | 車輛 Status = I，位於 2F | oShuttle 確認 MapCode |
| 2 | 建立一筆起點為 4F 的跨樓層任務（寫入 oMission） | 系統偵測到車輛不在起點樓層 | — |
| 3 | 等待系統下一輪派發（約 1 秒） | 自動產生 CROSS_FLOOR_DISPATCH 任務 | oMission 出現 TaskSource='CROSS_FLOOR_DISPATCH' |
| 4 | 等預調度任務完成 | 車輛抵達 4F，Callback 觸發 | Log 出現 OnCrossFloorDispatchCompleted |
| 5 | 確認正式任務派發 | 系統接著派發原本的跨樓層搬運任務 | oMission 出現正式任務 |

> 確認整個流程只有一筆任務在進行，不會同時出現兩筆給同一台車：
> ```sql
> SELECT * FROM oMission WHERE OkFlag IS NULL AND ShuttleId='1'
> ```

---

## 6. 完整部署 SOP

### 6.1 全新部署（首次安裝）

請依序執行以下 SQL 檔案，每步驟完成後確認輸出訊息無錯誤再繼續：

| 執行順序 | SQL 檔案名稱 | 功能說明 | 執行後確認 |
|---------|------------|---------|---------|
| 1 | Deploy_oShuttle_AddUpdateTime.sql | oShuttle 表新增 UpdateTime 欄位 | SELECT UpdateTime FROM oShuttle 不報錯 |
| 2 | Deploy_oPortBinding.sql | 建立 oPortBinding 表 + 初始綁定資料 | SELECT * FROM oPortBinding 有 17 筆資料 |
| 3 | Deploy_oTaskTypeRoute.sql | 建立 oTaskTypeRoute 表 + TaskType 路由 | SELECT * FROM oTaskTypeRoute 有 10 筆以上 |
| 4 | Deploy_pFunction_PortBindingMenu.sql | 新增後台選單：上下料區綁定管理 | 後台選單出現對應項目 |
| 5 | Deploy_pFunction_RouteMenu.sql | 新增後台選單：路線管理 | 後台選單出現對應項目 |
| 6 | Deploy_pFunction_TaskTypeRouteMenu.sql | 新增後台選單：TaskType 路由管理 | 後台選單出現對應項目 |

### 6.2 版本更新（已有舊資料庫）

若資料庫是從舊版升級，請先逐項確認再執行：

| 確認項目 | 確認 SQL | 若不存在則執行 |
|---------|---------|--------------|
| oShuttle.UpdateTime 欄位 | SELECT UpdateTime FROM oShuttle | Deploy_oShuttle_AddUpdateTime.sql |
| oPortBinding 表 | SELECT TOP 1 * FROM oPortBinding | Deploy_oPortBinding.sql |
| oTaskTypeRoute 表 | SELECT TOP 1 * FROM oTaskTypeRoute | Deploy_oTaskTypeRoute.sql |
| 後台選單項目 | SELECT * FROM pFunction WHERE FunctionName LIKE '%Binding%' | Deploy_pFunction_PortBindingMenu.sql |

### 6.3 上線前檢查清單

| 項目 | 確認內容 | 負責確認 |
|------|---------|---------|
| FHtSetting.config | TestMode 已改回 false | 工程師 |
| IdleReturnTimeout | 確認歸位秒數符合現場需求（建議 300 秒） | 工程師 + 現場 |
| oPortBinding 資料 | 綁定站點與現場實際站點一致，UseFlag = Y | 工程師 + 現場 |
| oTaskTypeRoute 資料 | TaskType 代碼已與 RCS 管理員確認並更新為正式代碼 | 工程師 + RCS 負責人 |
| MapCodeFloorMapping | 各樓層 MapCode 對應正確 | 工程師 + RCS 負責人 |
| 車輛心跳 | oShuttle.UpdateTime 每秒正常更新 | 工程師 |
| Callback API | AGV 任務完成回調正常觸發 | 工程師 |

---

## 7. 常見問題與排查

### Q1：空平板回收任務一直重複產生

**可能原因：** 系統未偵測到已有進行中的回收任務，導致每輪都重新產生。

**排查 SQL：**

| 查詢對象 | SQL 語法 |
|---------|---------|
| 進行中的回收任務（oMission） | `SELECT * FROM oMission WHERE TaskSource='PLATE_RECOVERY' AND OkFlag IS NULL` |
| 進行中的回收需求（oRequire） | `SELECT * FROM oRequire WHERE TaskSource='PLATE_RECOVERY' AND OkFlag IS NULL` |
| 等待中的回收需求（oNeed） | `SELECT * FROM oNeed WHERE TaskSource='PLATE_RECOVERY' AND AssignFlag='0'` |

> 若三張表都有未完成的記錄，表示任務已在進行，系統應正常跳過。若仍重複產生，請確認程式版本是否為最新。

### Q2：車輛閒置很久但沒有自動歸位

| 可能原因 | 確認方式 | 解決方法 |
|---------|---------|---------|
| 車輛被判定為離線（UpdateTime 超時） | `SELECT DATEDIFF(SECOND,UpdateTime,GETDATE()) AS Sec FROM oShuttle WHERE ShuttleId='1'` | 確認 AGV WebAPI 是否正常收到車輛心跳 |
| CrossFloorShuttleId 設定錯誤 | 對照 FHtSetting.config 與實際車號 | 修正設定檔後重啟服務 |
| MapCodeFloorMapping 無法解析車輛 MapCode | 確認 oShuttle.MapCode 是否在 MapCodeFloorMapping 中 | 補齊 MapCode 對應關係 |

### Q3：預調度完成後，正式任務未派發

**常見原因：** Callback API 未正確回傳任務完成訊號，`_crossFloorDispatchPending` 狀態未重置。

**排查步驟：**
1. 查看 AGV WebAPI Log，確認 `HikAGVController.AGVCallback` 有收到回調
2. 確認回調的任務 `TaskSource = 'CROSS_FLOOR_DISPATCH'`
3. 查詢以下 SQL 確認任務是否已標記完成：
   ```sql
   SELECT OkFlag FROM oMission
   WHERE TaskSource='CROSS_FLOOR_DISPATCH'
   ORDER BY TaskDateTime DESC
   ```

> 若 Callback 正常但任務仍未派發，重啟 AGV WebAPI 服務可強制重置內部狀態。

### Q4：RCS 拒絕任務（TaskType 不正確）

**排查步驟：**
1. 查詢目前所有路由設定：
   ```sql
   SELECT * FROM oTaskTypeRoute WHERE UseFlag='Y'
   ORDER BY MoveType, FromFloor, ToFloor
   ```
2. 與 RCS 管理員核對每個路線的正式 TaskType 代碼
3. 更新不符的代碼：
   ```sql
   UPDATE oTaskTypeRoute
   SET TaskType = '正確代碼'
   WHERE MoveType = '...' AND FromFloor = '...' AND ToFloor = '...'
   ```
4. 重啟 AGV WebAPI 服務使設定生效

> **警告：** 測試環境中使用的 TaskType 代碼（如 F24Test）在正式環境中無效，上線前務必更新為 RCS 正式代碼。
