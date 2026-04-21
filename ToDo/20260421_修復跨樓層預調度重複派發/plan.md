# 修復跨樓層預調度重複派發 - 技術方案書

## 選定方案概述

**方案 A + 方案 C 組合**

- **A**：在 `CallBackAPI.UpdateEnd` / `UpdateCancel` 收到 callback 當下，從 `CallbackModel.mapCode` 取值更新 `oShuttle.MapCode`。治源頭，使抵達回報後 500ms 內的 `oShuttle.MapCode` 與真實位置一致。
- **C**：在 `CrossFloorManager` 新增「同 parent 預調度冷卻期」機制，即使 A 方案被下一輪 `UpdateAGVStatus` 覆蓋（bug 還存在），也能以 end callback 事件為硬事實擋下錯誤的重派。

兩者互為保險：A 在「瞬間治標」，C 在「30 秒內防漏」。任一方案單獨失效仍有另一層保護。

---

## 架構設計

### 元件關係

```
┌──────────────────────┐      end / cancel       ┌────────────────────────┐
│   RCS (Hik)          │ ────  callback  ──────▶ │ CallBackAPI (ACC)      │
└──────────────────────┘                         │                        │
                                                 │ UpdateEnd/UpdateCancel │──── mapCode 非空 ────▶ SQLData.Update_oShuttleMapCode
                                                 │                        │                            │
                                                 │                        │                            ▼
                                                 │                        │                     oShuttle.MapCode = DD
                                                 │                        │
                                                 │ 若 TaskSource =        │
                                                 │  CROSS_FLOOR_DISPATCH  │
                                                 └────────────┬───────────┘
                                                              ▼
                                             ┌────────────────────────────────┐
                                             │ CrossFloorManager              │
                                             │                                │
                                             │ OnCrossFloorDispatchCompleted()│
                                             │   └─ RecordCompletion(parent)  │───▶ _recentCompletedParent
                                             │                                │     _dispatchCompletedAt
                                             │                                │
┌─────────────────┐     每 Tick         │ SelectNextMission              │
│ Dispatch.Tick() │ ───── 呼叫 ────────▶│   └─ 要派預調度前：            │
└─────────────────┘                    │      IsInCooldown(parent)?     │
                                             │      ├─ Yes: 跳過，回傳父任務  │
                                             │      └─ No:  DispatchCrossFloor│
                                             └────────────────────────────────┘
                                                              ▲
                                                              │
                                             ┌────────────────────────────────┐
                                             │ 建構子啟動時                  │
                                             │ RebuildCooldownFromUbMission() │
                                             │   └─ SELECT 最近 60 秒         │
                                             │      → 恢復 _recentCompleted   │
                                             └────────────────────────────────┘
```

### 資料流向

| 事件 | 觸發 | 寫入對象 |
|------|------|---------|
| RCS end callback | `AGVCallback` → `UpdateEnd` | `oShuttle.MapCode`（從 `CallbackModel.mapCode`） |
| 預調度 end callback | `UpdateEnd` → `OnCrossFloorDispatchCompleted` | `CrossFloorManager._recentCompletedParentTaskDateTime` |
| Dispatch Tick | `Dispatch.Tick` → `SelectNextMission` | 讀 `_recentCompletedParentTaskDateTime` 決定是否派預調度 |
| ACC 啟動 | `CrossFloorManager` 建構子 | 查 `ubMission` 重建 `_recentCompletedParentTaskDateTime` |

---

## 方案分析（六個面向）

### 1. 方案概述

在「callback 事件路徑」與「SelectNextMission 決策路徑」各插入一層防護，避免 `oShuttle.MapCode` 被汙染後觸發錯誤重派。

### 2. 架構設計

見上節。兩個修改點彼此獨立：
- A 只動 `CallBackAPI` + `SQLData`，不動 `CrossFloorManager`
- C 只動 `CrossFloorManager` + 測試專案，不動 `CallBackAPI`

### 3. 優點

- **治本 + 保險**：A 修正事件路徑（callback 後 MapCode 正確），C 在 A 失效時仍擋得住
- **影響面小**：不動 `Dispatch.UpdateAGVStatus` 核心迴圈（影響面最大的路徑）
- **可重啟**：C 方案啟動重建從 `ubMission` 免費取得持久化，ACC 重啟不丟冷卻狀態
- **熱路徑零 I/O**：冷卻期以記憶體 + lock 實作，不影響 Tick 頻率
- **可驗證**：冷卻期邏輯純函數化（parent + timestamp → bool），易 unit test

### 4. 風險與缺點

- **A 有被覆蓋的時間窗**：A 寫入 MapCode 後，下一輪 `UpdateAGVStatus` 仍會覆蓋（約 1 秒內），所以需要 C 補強
- **C 冷卻期長度是人工參數**：30 秒基於 LOG 觀察（錯誤重派發生於 0.5 秒內），若未來 MapCode 覆蓋頻率改變需重新評估
- **C 單欄位結構不支援多台跨樓層車**：若未來改多台需改 `Dictionary<parent, timestamp>`
- **C 啟動重建未涵蓋 OkFlag 為空（尚未落 ubMission）的情境**：極端情境（callback 收到但尚未 Insert_ubMission 時 ACC 崩潰），理論上存在但機率極低

### 5. 技術債評估

- A 方案：**低**。單純新增 SQL 方法 + 兩行呼叫，與現有結構一致
- C 方案：**低至中**。新增欄位與單一 lock，邏輯封裝在 `IsInCooldown` / `RecordCompletion` 內；若未來需擴充多車，單欄位改 Dictionary 成本小
- 啟動重建的 SQL 與 CrossFloorManager 耦合：若未來 `SQLData` 介面抽離可透過 DI 解耦

### 6. 建議適用情境

- 本專案現況（單台跨樓層車 + MapCode 可能被汙染）最適用
- 若未來改多車場景：C 方案需把單欄位擴充為 `Dictionary<string, DateTime>`
- 若未來 RCS 解決幽靈 MapCode：C 方案仍可保留作為 defensive programming

---

## 被捨棄的方案

### 方案 B：Dispatch.UpdateAGVStatus 去重邏輯

**做法**：在 `Dispatch.UpdateAGVStatus` foreach MapCode 的回應中，判斷哪一筆是真實車輛（Robot IP 非空）才更新 `oShuttle`。

**捨棄原因**：
- 影響面最大（所有車輛狀態更新都會經過）
- 需要理解 RCS 對「真實車 vs 幽靈鎖」的完整語意，風險高
- 現階段以 A+C 觀察效果，若未來確認 RCS 行為穩定再考慮

### 方案 D：SelectNextMission 每次查 ubMission

**做法**：`SelectNextMission` 每次決策前直接查 `ubMission`。

**捨棄原因**：
- 查詢頻率高（每 Tick 一次）
- ubMission 資料量會累積（一年 5~10 萬筆），無索引時全表掃描 ~50ms，影響 Tick
- 若要可接受必須建複合索引，需要 DBA 協調
- 方案 C 啟動重建已達成「重啟安全」目標，無需每次查 DB

---

## 實作注意事項

### 編碼規則

- 所有新/改 `.cs` 檔必須 **UTF-8 BOM**（參考全域 CLAUDE.md）
- 修改前先用 PowerShell 確認檔案 BOM：`[System.IO.File]::ReadAllBytes(path)[0..2]`

### Thread Safety

- `_recentCompletedParentTaskDateTime` 與 `_dispatchCompletedAt` 的讀寫必須透過同一個 `_cooldownLock`
- 存取點：
  - IIS Callback 緒：`OnCrossFloorDispatchCompleted`
  - Dispatch Tick 緒：`SelectNextMission`
  - 啟動緒：建構子 `RebuildCooldownFromUbMission`（啟動階段無競爭，但仍一併上鎖保持對稱）

### LOG 規格

以下字串固定，便於 grep 追蹤：

```
[CrossFloor] 啟動：從 ubMission 恢復冷卻狀態 parent=xxx, completedAt=yyy
[CrossFloor] 預調度 end，進入冷卻 parent=xxx, 冷卻至 yyy
[CrossFloor] 冷卻期攔截：parent=xxx 已於 yyy 完成預調度，跳過重派，直接派父任務
```

### 回歸測試範圍

- `HikAGVWebAPITests.ElevatorPathCalculatorTests`（既有）必須全過
- 新增 `CrossFloorManagerCooldownTests` 覆蓋冷卻期 8 個案例
