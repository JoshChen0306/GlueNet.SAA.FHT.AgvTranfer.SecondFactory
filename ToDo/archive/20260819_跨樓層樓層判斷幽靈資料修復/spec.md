# 需求規格書 — 二廠跨樓層樓層判斷幽靈資料修復

## 建立日期
2026-08-19

## 主題與背景

### 客訴事件
2026-08-19 客戶反映：AGV 位於 2F，SCP 建立 3F→1F（J1→G1）搬運任務，AGV 不承接；
查 RCS 任務紀錄只看到 3→1 的任務，沒有「先把車從 2 樓叫到 3 樓」的預調度任務。

### 根因（已由 log 逐筆驗證）
`Dispatch.UpdateAGVStatus()` 逐張 MapCode 輪詢 RCS，**查到一筆就立刻寫 DB**，
而 `SQLData.Update_oShuttle()` 的 `where` 條件只有 `ShuttleId = robotCode`、不含 MapCode，
因此同一輪內多張地圖回報同一台車時，兩句 UPDATE 打在同一列上，**後者無條件覆蓋前者**。

輪詢順序來自 `MapCodeFloorMapping` 的 key 順序（現場 `AA:1F,BB:2F,DD:3F,FF:4F`），
DD 永遠排在 BB 後面，所以只要車同時出現在 BB 與 DD，DB 最終一定是 DD(3F)。

2026-08-19 11:29:24 派發當下的實際回報：

```
IP=[192.168.7.72] X=269147 Y=269989 MAP=BB SPD=0   ← 真實：車在 2F
IP=[]             X=270072 Y=269997 MAP=DD SPD=243 ← 幽靈：座標自 11:29:14 起完全凍結
11:29:24.192  [CrossFloor] 同樓層任務優先派發：J1→G1
```

`CrossFloorManager.GetFloorByMapCode()` 讀到 DD → 判定車在 3F →
`DecideNextCrossFloorAction()` 認定 J1(3F) 為同樓層 → 走 `SameFloor` 分支
**完全跳過預調度**，直接把起點在 3F 的任務送 RCS。
RCS 在 3F 找不到車可指派，oMission 的 ShuttleId 一直是 0，
任務掛在 `OkFlag=R` 從 11:29:24 到 11:48:19（19 分鐘）直到人工取消。

同一時間 H3→K1（2F 起點、建立時間更早、本該優先派發）也一起被餓死。

### 既有緩解不足
`SQLData.cs:149-152` 的註解已記載此問題：
「依 ShuttleId 即時更新 oShuttle.MapCode（callback end/cancel 時呼叫）
　用於縮短 UpdateAGVStatus 輪詢窗口中被幽靈 MapCode 覆蓋的時間」
—— 當時只做「callback 搶著寫回正確值以縮短窗口」，輪詢每秒仍會蓋回去，
本次事故即發生在該窗口內。本任務要治本。

---

## 需求範圍

### 包含
1. **資料層**：`UpdateAGVStatus` 改為「整輪收集 → 同輪去重挑勝出者 → MapCode 變更需連續確認 → 才寫 DB」
2. **決策層**：`CrossFloorManager` 取不到可信樓層時不派發跨樓層任務

### 不包含
- **oMission 卡住看門狗**（原分析的第 ③ 項）— 使用者 2026-08-19 決定本次不執行
- **電梯中介程式 `WebApplication1`** — 非我方負責（產生 `LOGS/ACC/` 的 ASP.NET Core 程式，
  原始碼路徑 `C:\Users\user\Desktop\高技二厰\WebApplication1`，不在本 repo 亦不在公司專案清單）
- **`Update_oShuttle` 的 SQL 語句本身** — 維持 `where ShuttleId = robotCode` 不變
- **callback 路徑 `Update_oShuttleMapCode`** — 維持現狀，僅需與新機制同步

---

## 限制條件

- **不改資料庫 schema**：`oShuttle` 欄位不增不減
- **不改 `Update_oShuttle` 的 SQL**：裁決在 C# 端完成，傳進去的 `AGVStatusData` 已是裁決後結果
- **不動 SCP / svrPair**：改動全部侷限在 `ACC/HikAGVWebAPI`
- **不得使用 `robotIp` 作為判準**：
  - 該欄位在整個 ACC 沒有任何業務用途（僅 model 宣告 + `ToString()` 印 log）
  - 本地 mock RCS `NormalAPI.cs:411,428` 硬寫 `robotIp = ""`，若以「IP 空即淘汰」為規則，開發機所有車輛狀態會被丟棄
  - 8/19 資料中 11:29:05~11:29:13 兩筆皆有 IP，IP 判準在該區間本就無效
- **確認次數 N = 3**（使用者 2026-08-19 決定，寫成程式常數，不放設定檔）
- **衝突僅記 WARN log，不上報 FHt**（使用者 2026-08-19 決定）
- **編碼**：新增 `.cs` 檔一律 UTF-8 BOM

### 資料流

| 操作 | 資料來源 | 讀寫方式 | 失敗時行為 |
|------|---------|---------|-----------|
| 讀 AGV 狀態 | 海康 RCS `queryAgvStatus`（每 MapCode 呼叫一次） | `Dispatch.GetAGVStatus(mapCode)` → `AGVStatusAck` | 回 null 或 data 為空 → 該地圖本輪無候選（維持現有行為） |
| 讀 oShuttle 現值 | SQL Server `oShuttle` | `SQLData.Select_oShuttle()` | 例外 → 記 WARN，本輪不更新 MapCode（其他欄位照舊） |
| 寫車輛狀態 | SQL Server `oShuttle` | `SQLData.Update_oShuttle(agvData)`，`where ShuttleId = robotCode` | 沿用現有 `WriteSqlByAutoOpen` 行為，不改 |
| 寫 MapCode（callback 路徑） | SQL Server `oShuttle` | `SQLData.Update_oShuttleMapCode(shuttleId, mapCode)` | 已有 try/catch 記 WARN，不改 |
| 讀車輛樓層（派發決策用） | SQL Server `oShuttle.MapCode` | `CrossFloorManager.GetShuttleStatus()` → `GetFloorByMapCode()` | 取不到可信樓層 → `SelectNextMission` 回 null，本輪不派發 |

---

## 驗收標準

### AC1~AC8：`ShuttleMapCodeResolver`（資料層裁決）

- [ ] **AC1** Given 本輪某 robotCode 只有 1 筆回報，When 呼叫 `Resolve()`，Then 直接採用該筆且 `HasConflict = false`
- [ ] **AC2** Given 同輪 BB 座標與上一輪不同、DD 座標與上一輪相同，When 呼叫 `Resolve()`，Then 勝出者為 BB 且 `HasConflict = true`
- [ ] **AC3** Given 同輪 BB 與 DD 座標皆與上一輪相同、目前認可值為 BB，When 呼叫 `Resolve()`，Then 勝出者為 BB（黏著上一輪選擇）
- [ ] **AC4** Given 目前認可值為 DD、勝出者連續為 BB，When 第 1、2 輪 `Resolve()`，Then 輸出 MapCode 仍為 DD；When 第 3 輪 `Resolve()`，Then 輸出 MapCode 變為 BB
- [ ] **AC5** Given pending 為 BB 已累積 2 次，When 第 3 輪勝出者變為 FF，Then pending 重設為 FF 計數 1、輸出 MapCode 仍為 DD
- [ ] **AC6** Given 傳入的 DB 現值與 Resolver 內部認可值不同（callback 剛寫過），When 呼叫 `Resolve()`，Then 以 DB 現值為新認可值並清空 pending 計數
- [ ] **AC7** Given 2026-08-19 11:29:04~11:29:24 的實際回報序列逐輪餵入，When 逐輪 `Resolve()`，Then 自 11:29:08 起輸出 BB，且 11:29:24 輸出 BB（而非事故當時的 DD）
- [ ] **AC8** Given 同輪偵測到多筆同 robotCode，When 呼叫 `Resolve()`，Then 回傳的衝突描述含各筆的 mapCode、座標與勝出原因

### AC9~AC11：`CrossFloorManager`（決策層閘門）

- [ ] **AC9** Given `currentFloor` 為 null（MapCode 空或不在對照表）且待派任務的 `BeginStation` 首字母不在站點樓層對照表（`GetFloor` 亦回 null），When 呼叫 `DecideNextCrossFloorAction()`，Then **不得**回傳 `SameFloor`（修正 `null == null` 誤判為同樓層的既有缺陷）
- [ ] **AC10** Given `oShuttle.MapCode` 無法對應到任何樓層，When 呼叫 `SelectNextMission()`，Then 回傳 null，且不寫入任何 `CROSS_FLOOR_DISPATCH` 記錄
- [ ] **AC11** Given `crossFloorPending` 內含 `CROSS_FLOOR_DISPATCH` 或 `IDLE_RETURN` 系統任務、且樓層不可信，When 呼叫 `SelectNextMission()`，Then 仍回傳該系統任務（系統任務已寫入 oMission，不受樓層可信度影響）

### AC12：回歸

- [ ] **AC12** `HikAGVWebAPITests` 既有全部測試（`CrossFloorManagerCooldownTests`、`CrossFloorManagerSelectNextMissionTests`、`ElevatorPathCalculatorTests`）0 失敗
