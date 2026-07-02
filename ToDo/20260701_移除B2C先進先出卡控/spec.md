# spec — 移除 1F B→C 先進先出卡控（方案 A + 隱藏開關 EnableB2CFifo）

## 目標
1F B→C 派送選「起點（B 區物料）」時，移除先進先出（FIFO）卡控，讓 B 區所有有料站點皆可選；
同時保留一個對客戶隱形的設定檔開關，可在需要時恢復原卡控。

## 範圍
- 僅影響 `Dispatch.js` `filterBeginStationOptions()` 的 `case "C"`（1F B→C 起點下拉）。
- 不得影響同函式其他 case（H / M / T / MT 等 2F 路徑）與共用結構 `workoderMap`。

## 開關規格
| 項目 | 值 |
|------|-----|
| 設定 key | `MyConfig:EnableB2CFifo` |
| 型別 | bool |
| 預設（缺 key） | `false` |
| `false` 行為 | 方案 A：B 區所有 `HaveFlag=3` 站點全顯示、全可選 |
| `true` 行為 | 恢復現況 FIFO：同料號只顯示 PutTime 最早一盤 |
| 隱藏方式 | cshtml 只在 `true` 時 render `window.enableB2CFifo`；`false`/缺 key 不輸出任何變數 |

## 驗收標準（Acceptance Criteria）

- [ ] **AC1 建置通過**：`SCP.csproj` 建置 0 error。
- [ ] **AC2 缺 key 不出錯**：`appsettings.json` 無 `EnableB2CFifo` 時，Dispatch 頁正常載入、不丟例外（`GetValue` 回傳 false）。
- [ ] **AC3 預設放開（方案 A）**：無 key（或設 `false`）時，B→C 起點下拉中，B 區同一料號的**多盤／多批號全部顯示且皆可選**（不再只留最早一盤）。
- [ ] **AC4 顯示格式不變**：下拉每個選項文字維持 `站號-料號-批號` 格式。
- [ ] **AC5 開關可恢復卡控**：設 `MyConfig:EnableB2CFifo=true` 後，B→C 起點下拉恢復「同料號只顯示 PutTime 最早一盤」的現況行為。
- [ ] **AC6 原始碼無痕**：預設（false/缺 key）時，Dispatch 頁 View Source **看不到** `enableB2CFifo` 變數。
- [ ] **AC7 不影響 2F**：H / M / T / MT 等 2F case 的起點下拉顯示與改動前一致，未受影響。

## 驗證方式
- AC1：`industrial-test-runner` agent（已通過）。
- AC2~AC7：手動瀏覽器／實機平板回歸（SCP 無測試專案，環境限制 fallback 人工驗證）。
