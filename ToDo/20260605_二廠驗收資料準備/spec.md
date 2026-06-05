# 二廠 AGV 系統驗收資料準備 — 需求規格書

**建立日期：** 2026-06-05
**適用系統：** GlueNet.SAA.FHT.AgvTranfer.SecondFactory
**文件性質：** 驗收交付文件（非程式碼任務）

---

## 1. 主題與背景

二廠 AGV 自動搬運專案進入客戶驗收階段，需提交一套系統文件作為驗收資料。

**核心原則（與使用者確認）：**
- **以現有手冊為底稿**，把這次二廠新增的客製功能補上去，不從零重寫整套系統文件。
- 詳細度**停在「架構層級」**，不逐類別 / 逐行展開。
- 沿用現有手冊的 Markdown + Mermaid 風格，定稿後可轉 docx 交付。

**底稿來源：**
- `Doc/功能說明書_二廠三項自動化功能.md`（內容已相當完整）
- `高技二廠操作手冊_提供給IBU.docx`（IBU 操作手冊）

---

## 2. 需求範圍

### 2.1 涵蓋的新增客製功能（與使用者確認的清單）

1. 二樓站內運輸（空平板自動派送）
2. AGV 自動復歸
3. 預派調度（跨樓層預調度機制）
4. 跨樓層電梯換乘
5. 跨樓層任務取消後自動重建
6. 空平板取消連動清源頭
7. 派送路由 / 路線權限管理（站點 → 可派終點規則）

### 2.2 交付文件清單（6 份）

| # | 文件 | 詳細度 |
|---|------|--------|
| D1 | 系統架構 | 三系統關係圖 + 新增功能定位（架構層級）|
| D2 | 派送路由對照表 | 站點 → 可派終點完整對照（**依程式碼為權威**，非 pRoute）|
| D3 | 各 Function 流程圖 | 新增功能流程圖（Mermaid）|
| D4 | 系統建置 / 設定手冊 | DB 部署順序 + config 設定（以功能說明書 §5/§8 為底）|
| D5 | 系統操作手冊 | 以現有手冊為底，補新增功能操作 / 異常排查 |
| D6 | 系統 UML | 核心類別圖 + 1~2 張關鍵時序圖（不逐一展開）|

### 2.3 不包含

- 不重寫 SCP / Dispatch / ACC 三套系統的完整原始碼級文件。
- 不做逐類別、逐方法的 UML 全展開。
- 不含程式碼修改（純文件產出任務）。

---

## 3. 限制條件

- **格式：** Markdown + Mermaid，沿用現有手冊風格；交付時可轉 docx。
- **派送邏輯權威來源 = 程式碼（鐵律，使用者明確要求）：** 派送 / 路由相關內容一律以實際程式碼為準，不可依記憶、`*_實作計畫.md` 或 `Deploy_*.sql` 臆測。實際路由決策分散在：
  - `WebGui/SCP/Controllers/DispatchController.cs` — 手動派送 `InsertoNeed()`、空板回送 `Release()`（G→J / K→H / I→L1-4 / O,P,S,N→M→Q→R **寫死在 code**）
  - `Dispatch/svrPair/cPair.cs` — 自動配對主循環（`ProcessSingleoNeed` switch 分流；B→A / B→C / E→F / C→ABD / E,F→ABD；受 `Recipe.ini` 的 `PanelDoB2C` / `UnloadAuto` 開關控制）+ 空平板回收 `CheckAndHandleEmptyPlateRecovery()`（依 `oPortBinding`）
  - `ACC/HikAGVWebAPI/App_Start/` — 跨樓層調度 `CrossFloorManager.cs`、決策 `CrossFloorDecision.cs`、電梯路徑 `ElevatorPathCalculator.cs`、冷卻 `CooldownTracker.cs`；回調 `API/CallBackAPI.cs`
  - `pRoute` 僅為 SCP 後台 UI 的「可選起點區 / 樓層」權限篩選，**非執行期路由權威**
- **DB / config 來源：** DB 部署以 `Deploy_*.sql` 為準；config 以現場 `FHtSetting.config` / `appsettings.json` 為準。
- **核對義務：** 1F 區域代碼（EE 改 G）等可能已變動的設定，須核對現行程式碼 / SQL / config 再下筆，禁止照舊值臆測（遵守推論安全規則）。程式碼已確認 `Release()` 以 `stationArea == "G"` 判斷，EE→G 已落地。
- **跨樓層位置更正：** `CrossFloorManager` 實際在 ACC/HikAGVWebAPI，**不在 Dispatch**；功能說明書舊描述有誤，文件須以實際位置為準。
- **編碼：** 所有新建 .md 一律 UTF-8 BOM。

---

## 4. 驗收標準（依文件分項，架構層級）

### D1 系統架構
- [ ] 含一張三系統（SCP / Dispatch / ACC）關係圖，標出與外部系統（RCS / FHT / AGV / DB）的邊界。
- [ ] 標出本次 7 項新增功能各自落在哪個系統 / 模組。
- [ ] 說明 oNeed → oRequire → oMission 的核心資料流。

### D2 派送路由對照表
- [ ] 依**程式碼**列出實際「站點 → 可派終點」對照，並分類標明決策來源（手動 / Release 寫死 / svrPair 自動配對 / 空平板回收 / 跨樓層）。
- [ ] Release 回送規則（G→J、K→H、I→L1-4、O/P/S/N→M→Q→R）逐項對照 `DispatchController.Release()` 程式碼確認。
- [ ] svrPair 自動配對規則（B→A、B→C、E→F、C→ABD、E/F→ABD）對照 `cPair.cs` 主循環確認，並標明 `Recipe.ini` 開關影響。
- [ ] 附 pRoute 權限對照表，並明確標註「pRoute 僅控 UI 權限，非執行期路由」。
- [ ] 1F 區域代碼以現行程式碼 / SQL / config 核對後填寫（EE→G 已於 code 確認）。

### D3 各 Function 流程圖
- [ ] 7 項新增功能各有一張 Mermaid 流程圖（或合併同類）。
- [ ] 每張圖標出觸發條件、關鍵判斷分支、產生的 TaskSource 標記。

### D4 系統建置 / 設定手冊
- [ ] 列出全新部署的 Deploy_*.sql 執行順序。
- [ ] 列出 FHtSetting.config / appsettings.json 關鍵設定項與意義。
- [ ] 含 cTest / svrPair 部署件複製步驟（依部署拓樸）。

### D5 系統操作手冊
- [ ] 以現有 IBU 手冊為底，補上新增功能的後台操作說明。
- [ ] 含常見異常排查（沿用功能說明書 §7 並補新增功能）。

### D6 系統 UML
- [ ] 含核心類別圖（至少涵蓋 CrossFloorManager 及派送主循環核心類）。
- [ ] 含 1~2 張關鍵時序圖（派送流程、AGV 回調 / 跨樓層調度）。

---

*本規格書由 Claude Code 輔助生成。*
