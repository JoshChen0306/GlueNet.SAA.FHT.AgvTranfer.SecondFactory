# 清除物料改標空板（HaveFlag 0→1）— 需求規格

> 建立日期：2026-06-02
> 來源：客戶需求 + 二廠現場操作確認
> 分析力度：精簡模式

## 主題與背景

客戶在 SCP 看板對站點按「清除物料」時，現場實際操作是**只拿走物料、空平板（貨架）留在原位**。
但現況 `ClearLot` 會把 `oPort.HaveFlag` 設為 `0`（空架＝連板都沒有），與現場「板還在」的事實不符。

此資料不一致會造成自動回空平板流程誤判：O/P/S/N 區空板回送、G→J 跨樓層回送、O/P 綁定自動回收，
都以 `HaveFlag='0'` 搜尋落點。被清除的格子標 0 後會被當成「空位」，於是引擎可能**再投一塊空板到已經有板的格子 → 實體疊板／衝突**。

將清除後的狀態改為 `1`（空板），讓資料貼合現實，並讓回送邏輯正確略過「已有空板」的格子。

## 需求範圍

### 包含
- `DispatchController.ClearLot` 寫入的 `HaveFlag` 由 `"0"` 改為 `"1"`。
- 自然套用於所有會出現「清除物料」按鈕的區：M（雷雕）、T（V Cut）、J（3F 插針室）、Q（出貨）、R（R 區）。

### 不包含
- 不改 `RackId` 清空行為（維持現狀清空為 `''`）。
- 不改 `WorkOrder`、`PutTime` 清空行為。
- 不改前端按鈕顯示邏輯、不改 cPair 派車引擎、不改 CallBackAPI。
- 不為此建立 SCP 測試專案（採人工驗證，使用者裁定）。

## 限制條件 / 資料流

| 操作 | 資料來源 | 讀寫方式 | 失敗時行為 |
|------|---------|---------|----------|
| 清除物料寫狀態 | DB `oPort` | EF Core `ExecuteUpdate`（HaveFlag="1"、WorkOrder=""、RackId=""、PutTime=""） | 沿用現有 try/catch 回 HTTP 500 |
| 回送找落點（本案不改，僅須確認相容） | DB `oPort` | `Where(HaveFlag=="0")` | 找不到回 BadRequest |

- SCP 專案為 net6.0 + EF Core；本案無自動化測試，採人工驗證 + code review。
- RackId 維持清空：經查證，等待重新登記期間無任何流程讀取該格 RackId（回送不挑 HaveFlag=1、cPair 不碰 M/Q/R/J），下次物料登記會覆寫 RackId，故不致幽靈任務；唯一副作用為看板該格貨架條碼顯示空白。

## 驗收標準

- [ ] 在 M 區某站建立物料（HaveFlag=3）後按「清除物料」，該站 `oPort.HaveFlag` 變為 `1`（非 0）。
- [ ] 清除後該站 `RackId`、`WorkOrder`、`PutTime` 仍為清空（維持現狀）。
- [ ] 清除後（HaveFlag=1）的 M 格，不再被 O/P/S/N 空板回送（Release）選為落點（回送 SELECT 條件 HaveFlag='0' 自然排除）。
- [ ] T/J/Q/R 區清除後同樣為 HaveFlag=1，看板圖示由空架（empty.svg）變空板（trac.svg）。
- [ ] Build 0 error；既有 svrPairTests 回歸 0 失敗（不受本案影響）。
