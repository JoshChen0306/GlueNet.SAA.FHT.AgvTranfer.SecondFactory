# 修復跨樓層預調度重複派發 - 需求規格書

## 主題與背景

客戶反映生產環境跨樓層任務完成後，系統又再觸發一次同一個父任務的跨樓層預調度任務，但車輛實際已抵達該樓層。

**事件時間**：2026-04-21 14:32:35 ~ 14:32:36（相隔 0.5 秒重複派發）
**事件車輛**：ShuttleId = 20106（跨樓層專用車，配置於 `CrossFloorShuttleId=3`）
**問題 LOG 來源**：
- `2026042114-API.log`（CallBackAPI）
- `2026042114 -Dispatch.log`（svrPair 調度）

## 根本原因

三個疊加漏洞同時觸發：

1. **RCS 對跨樓層車會在多個 Map 回傳多筆狀態**：DD=真實車輛（3F）、FF=電梯幽靈鎖殘留（4F）
2. **Dispatch.UpdateAGVStatus 的 foreach MapCode 會造成 oShuttle.MapCode 覆蓋**：DD 先寫入後被 FF 覆蓋
3. **CallBackAPI.UpdateEnd/UpdateCancel 不更新 oShuttle.MapCode**：即使 end callback 帶了正確 MapCode=DD，也會被下一輪 UpdateAGVStatus 覆蓋
4. **CrossFloorManager 無防重派機制**：預調度完成後立即清空狀態，下一輪 Tick 若 MapCode 被汙染即重新判定需預調度

## 需求範圍

### 包含

- **方案 A**：CallBackAPI 的 `UpdateEnd` / `UpdateCancel` 收到 callback 時，從 `CallbackModel.mapCode` 取值更新 `oShuttle.MapCode`，讓抵達回報立即糾正樓層狀態
- **方案 C**：`CrossFloorManager` 新增「同 parent 預調度冷卻期」機制：
  - 記憶體欄位 `_recentCompletedParentTaskDateTime` + `_dispatchCompletedAt`，以 `lock` 保護
  - 預調度 end/cancel callback 時記錄 parent 進入冷卻期（30 秒）
  - `SelectNextMission` 派發預調度前檢查冷卻期，命中時跳過重派、改回傳父任務 triggerTask 給 RCS
  - `CrossFloorManager` 啟動時從 `ubMission` 查詢最近 60 秒內完成的 `CROSS_FLOOR_DISPATCH` 重建冷卻狀態（ACC 重啟安全）

### 不包含

- 方案 B：`Dispatch.UpdateAGVStatus` 去重邏輯（影響面廣，本次暫不動）
- 方案 D：每次 SelectNextMission 查 ubMission 判斷（DB 查詢開銷高）
- RCS 端的幽靈 MapCode 處理（RCS 供應商範圍）
- 多台跨樓層專用車場景（當前單台，結構僅保留單欄位）

## 限制條件

### 技術限制

- 目標 Framework：.NET Framework 4.8
- 測試框架：MSTest + Moq
- 不可影響既有 SCP 取消流程（parent/child 連動取消）
- 不可影響一般 MCS 任務與 IDLE_RETURN 歸位流程

### 資料流表

| 操作 | 資料來源 | 讀寫方式 | 失敗時行為 |
|------|---------|---------|----------|
| 讀取 callback MapCode | `CallBack.mapCode`（HTTP body） | `CallbackModel.mapCode` 欄位直取 | 空字串則跳過更新（UpdateStart 時為空） |
| 寫入 oShuttle.MapCode | `CallBackAPI.UpdateEnd/UpdateCancel` | `SQLData.Update_oShuttleMapCode(shuttleId, mapCode)` | log WARN 後吞例外，不中斷 callback |
| 讀取冷卻期狀態 | `CrossFloorManager` 記憶體欄位 | `lock(_cooldownLock)` 保護 | N/A |
| 寫入冷卻期狀態 | `OnCrossFloorDispatchCompleted()` | `lock(_cooldownLock)` | N/A |
| 啟動重建冷卻期 | `ubMission` 表 | `SELECT TaskDateTime, ParentTaskDateTime, EndTime FROM ubMission WHERE TaskSource='CROSS_FLOOR_DISPATCH' AND OkFlag IN ('Y','C') AND EndTime >= DATEADD(second,-60,GETDATE()) ORDER BY EndTime DESC` | log WARN，退回初始值不啟用冷卻（不影響派發） |

### 環境限制

- 生產資料庫使用者 `mcs` 已具 `ubMission` 查詢權限，不需新增權限
- 修改後需通過現有 `HikAGVWebAPITests.ElevatorPathCalculatorTests` 全部測試
- 不可動到 `Dispatch.UpdateAGVStatus` 邏輯（本次 scope 外）

## 驗收標準

### 方案 A：CallBackAPI 即時更新 MapCode

- [ ] Given 收到 `CallbackModel.method=end` 且 `mapCode=DD`，When 進入 `UpdateEnd`，Then `oShuttle.MapCode` 更新為 `DD`
- [ ] Given 收到 `CallbackModel.method=cancel` 且 `mapCode=FF`，When 進入 `UpdateCancel`，Then `oShuttle.MapCode` 更新為 `FF`
- [ ] Given 收到 `CallbackModel.mapCode=""`（空字串），When 進入 `UpdateEnd/UpdateCancel`，Then 跳過 MapCode 更新（保留原值）
- [ ] Given `Update_oShuttleMapCode` 拋出 SQL 例外，When 呼叫端收到例外，Then log WARN 後不中斷 callback 流程

### 方案 C：CrossFloorManager 冷卻期機制

- [ ] Given 呼叫 `RecordCompletion(parent="P1")`，When 再呼叫 `IsInCooldown("P1")`，Then 回傳 `true`（冷卻期內）
- [ ] Given 呼叫 `RecordCompletion(parent="P1")` 於 31 秒前，When 呼叫 `IsInCooldown("P1")`，Then 回傳 `false`（已過期）
- [ ] Given 呼叫 `RecordCompletion(parent="P1")`，When 呼叫 `IsInCooldown("P2")`，Then 回傳 `false`（不同 parent）
- [ ] Given 未呼叫過 `RecordCompletion`，When 呼叫 `IsInCooldown(任何 parent)`，Then 回傳 `false`
- [ ] Given `SelectNextMission` 準備派預調度且冷卻期命中 parent，Then 不寫 oMission，改回傳 triggerTask（父任務），並寫 LOG
- [ ] Given `SelectNextMission` 準備派預調度且冷卻期未命中，Then 正常呼叫 `DispatchCrossFloor`
- [ ] Given `OnCrossFloorDispatchCompleted` 被呼叫時該 mission 有 `ParentTaskDateTime`，Then 記錄該 parent 進入冷卻期
- [ ] Given `OnCrossFloorDispatchCompleted` 被呼叫時該 mission 無 `ParentTaskDateTime`，Then 不記錄（不觸發冷卻）

### 啟動重建

- [ ] Given ubMission 最近 60 秒內有 CROSS_FLOOR_DISPATCH OkFlag=Y 記錄，When CrossFloorManager 實例化，Then 冷卻期欄位被填入該 parent 與 EndTime
- [ ] Given ubMission 最近 60 秒內無相符記錄，When 實例化，Then 欄位維持初始值（冷卻期不啟用）
- [ ] Given ubMission 查詢拋例外，When 實例化，Then log WARN，欄位維持初始值，不中斷啟動

### 整體回歸

- [ ] 既有 `HikAGVWebAPITests` 所有測試全數通過
- [ ] 回放 2026-04-21 14:32:35 情境：預調度完成後 0.5 秒內 SelectNextMission 不再派相同 parent 的預調度
