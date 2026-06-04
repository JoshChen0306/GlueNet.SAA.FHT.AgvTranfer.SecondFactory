# 需求規格書：空平板取消連動清源頭

## 主題與背景

2F 站內運輸有「自動化空平板搬運流程」：當 M 區（雷雕區，亦含 T/O/P/S/N 等綁定區）要把物料搬往 O/P 區時，若上料區（O/P）上方已有空平板，系統會先把空平板搬到其他地方（O→Q 等），再搬移 M 區物料。

**現況問題**：空平板回收任務（O→Q）執行中，操作員在 SCP 畫面按「取消」後，系統會「再次觸發」一張新的空平板任務，導致無法真正取消，必須進資料庫手動刪除 M 區搬運任務才能停止。

### 根因（已逐行佐證）

- M→O 物料 oNeed 被卡控攔在 `oNeed` 表（`cPair.cs:219-221`，卡控回 true 即 `break`，**不會轉成 oRequire/oMission**），所以它**不在 SCP 畫面上**（畫面只列 `oRequire`，`DispatchController.cs:142`）。
- 操作員在畫面上點取消的，實際是**子任務 O→Q（空平板回收）**——它有正常走 oNeed→oRequire→oMission。
- SCP 取消（`DeleteoNeed`，`DispatchController.cs:274-328`）只把 oMission/oRequire 標 `OkFlag='C'`，**完全不碰 oNeed 表**。
- 主迴圈每 50ms 重掃 `oNeed where AssignFlag is NULL or ''`（`cPair.cs:172`），M→O 源頭還在 → 又跑 `CheckAndHandleEmptyPlateRecovery` → 又生一張 O→Q。
- 既有清理路徑 `RecyclingoRequireByOkFlag → DeleteoNeedByAssignFlag(...,"Y")`（`cPair.cs:852/818`）只刪 `AssignFlag='Y'` 的 oNeed，碰不到 `AssignFlag=''` 的卡控源頭。

## 需求範圍

### 包含
- 取消畫面上的 O→Q 空平板任務時，**連動清除其源頭 M→O 物料 oNeed**，使源頭斷、不再重生。
- 借用跨樓層既有的 `ParentTaskDateTime` 父子連動架構，延伸到 `oNeed` / `oRequire` 兩層（跨樓層原本只加在 oMission/ubMission）。
- 同一輪迴圈內的重生 race 防護。

### 不包含
- 空平板搬到半途被取消後的實體歸位（屬現場 SOP，非程式連動範圍）。
- 跨樓層（CROSS_FLOOR_DISPATCH / IDLE_RETURN）既有取消行為的變更（僅需確保不被本次改動影響）。
- 版本號（AssemblyVersion）變更（如需 bump 另外確認）。
- 建立自動化測試專案（本次採手動驗證）。

## 限制條件

- **編碼**：依全域規則，動 `.cs` 檔前先驗 BOM；No-BOM 含中文者先整檔轉 UTF-8 BOM 再改。新檔一律 UTF-8 BOM。
- **無空 catch**：所有新增 catch 至少記 Warning Log。
- **不破壞跨樓層**：一般任務與跨樓層任務的 `ParentTaskDateTime` 維持 NULL，連動與清理行為不得改變。
- **驗證方式**：專案無測試專案、邏輯重度綁 DB，本次採**手動驗證**（DB 造資料 + 開發機/現場實測 + Log 觀察）。

### 資料流

| 操作 | 資料來源 | 讀寫方式 | 失敗時行為 |
|------|---------|---------|----------|
| 種下空平板任務父關聯 | M→O oNeed 的 `TaskDateTime` | svrPair raw SQL `INSERT INTO oNeed(... ParentTaskDateTime ...)`（`cPair.cs:730`） | 例外接住記 Log、續行（沿用單筆隔離機制） |
| oNeed→oRequire 傳遞父關聯 | oNeed `dr["ParentTaskDateTime"]`（DBNull 防呆） | raw SQL `INSERT INTO oRequire(... ParentTaskDateTime ...)`（`cPair.cs:464`） | 同上 |
| oRequire→oMission 傳遞父關聯 | oRequire `dr["ParentTaskDateTime"]` | raw SQL `INSERT INTO oMission(... ParentTaskDateTime ...)`（`cPair.cs:594`） | 同上 |
| 取消連動讀父任務 | oMission（找不到退查 oRequire）`.ParentTaskDateTime` | EF 查詢 | parent 為空則只取消當前任務（不連動） |
| 清除源頭 oNeed | `oNeed.TaskDateTime == ParentTaskDateTime` | EF `ExecuteUpdate` 標 `AssignFlag='C'` | 包在既有 transaction，失敗整筆 rollback 並記 Log |
| 實際刪除 oNeed | `AssignFlag='C'` | svrPair `RecyclingoNeedByAssignFlag → DeleteoNeedByAssignFlag`（`cPair.cs:909/818`，既有） | 下一輪重試 |
| race 防護重查 | 父 M→O oNeed `AssignFlag` | 卡控 INSERT 前 raw SQL 重查 | 已被標 C/不存在 → 不產生 O→Q |

## 驗收標準

- [ ] DB 跑完 migration 後，`oNeed`、`oRequire` 表皆有 `ParentTaskDateTime varchar(50) NULL` 欄位（`oMission` 既有）。
- [ ] 卡控產生的 O→Q 空平板回收 oNeed，其 `ParentTaskDateTime` 等於觸發它的 M→O oNeed 的 `TaskDateTime`。
- [ ] O→Q 的 `ParentTaskDateTime` 正確傳遞到 oRequire 與 oMission（三表一致）。
- [ ] 在 SCP 取消畫面上的 O→Q 任務後，M→O 源頭 oNeed 於數輪迴圈內被刪除（`AssignFlag` 先變 `C` 後消失）。
- [ ] 取消 O→Q 後，主迴圈**不再產生新的** PLATE_RECOVERY oNeed（不需手動進 DB 刪 M 區任務）。
- [ ] O→Q 若正在 AGV 執行中被取消，oMission 走既有 RCS Cancel + 歸檔 ubMission 路徑，行為不變。
- [ ] race 防護：父 M→O oNeed 已被標 `C` 後，同輪/次輪 `CheckAndHandleEmptyPlateRecovery` 不再產生 O→Q。
- [ ] 一般搬運任務與跨樓層任務（無 ParentTaskDateTime 或 parent 在 oMission）取消行為與修復前一致（回歸）。
- [ ] 新增/修改的 `.cs` 檔編碼為 UTF-8 BOM，無混合編碼亂碼；無空 catch。
