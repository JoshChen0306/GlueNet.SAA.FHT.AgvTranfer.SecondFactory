# 技術方案書：空平板取消連動清源頭

## 選定方案概述

**借殼 + 補最後一哩**：沿用跨樓層既有的 `ParentTaskDateTime` 父子連動架構，但把欄位**延伸到 `oNeed` / `oRequire`**（跨樓層原本只加在 oMission/ubMission），讓「畫面可見的 O→Q（oRequire 層）」能反查到「隱形的 M→O oNeed 源頭」；再補上跨樓層用不到的 **oNeed 源頭清除**（取消時標 `AssignFlag='C'`，交既有 `RecyclingoNeedByAssignFlag` 刪除）。

## 架構設計

### 連動資料流

```
M→O oNeed (TaskDateTime=T_mo, 卡控中, AssignFlag='')
   └─ CheckAndHandleEmptyPlateRecovery 產生 ↓ (種下 ParentTaskDateTime=T_mo)
      O→Q oNeed (ParentTaskDateTime=T_mo)
         └─ ProcessoNeedToRequire ↓ (傳遞 ParentTaskDateTime)
            O→Q oRequire (ParentTaskDateTime=T_mo)   ← 畫面顯示 / 取消入口
               └─ InsertoMission ↓ (傳遞 ParentTaskDateTime)
                  O→Q oMission (ParentTaskDateTime=T_mo)

使用者在 SCP 取消 O→Q：
   DeleteoNeed(T_oq)
     → 讀 oMission[T_oq].ParentTaskDateTime（找不到退查 oRequire）= T_mo
     → cancelList = {T_oq, T_mo}
     → 對每筆標：oNeed.AssignFlag='C'（新增）+ oRequire.OkFlag='C' + oMission.OkFlag='C'（既有）
   背景迴圈各自回收：
     - svrPair RecyclingoNeedByAssignFlag 刪 M→O oNeed (T_mo)        ← 止血
     - svrPair RecyclingoRequireByOkFlag 刪 O→Q oRequire + 解除 oPort 路徑
     - svrPair GenerateoRequireByoNeed 不再選到 M→O（已 C）→ 不再生 O→Q  ← 止血關鍵
     - ACC Dispatch 對 O→Q oMission 送 RCS Cancel + 歸檔 ubMission
```

### 關鍵元件與既有程式互動

| 元件 | 角色 | 既有行為 | 本次改動 |
|------|------|---------|---------|
| `oNeed` / `oRequire` 表 | 任務佇列 | 無 ParentTaskDateTime | 加 `ParentTaskDateTime varchar(50) NULL` |
| `CheckAndHandleEmptyPlateRecovery`（`cPair.cs:608`） | 卡控、生 O→Q | INSERT oNeed 無父關聯 | 種 ParentTaskDateTime + INSERT 前 race 重查 |
| `ProcessoNeedToRequire`（`cPair.cs:428`） | oNeed→oRequire | INSERT 不含父關聯（TaskSource 寫死 'MCS'） | INSERT 增 ParentTaskDateTime（取自 oNeed 列） |
| `InsertoMission`（`cPair.cs:586`） | oRequire→oMission | INSERT 不含父關聯 | INSERT 增 ParentTaskDateTime（取自 oRequire 列） |
| `DeleteoNeed`（`DispatchController.cs:274`） | SCP 取消入口 | 只標 oMission/oRequire OkFlag='C' | 讀 parent（oMission→oRequire fallback）+ 對 cancelList 每筆標 oNeed.AssignFlag='C' |
| `RecyclingoNeedByAssignFlag`（`cPair.cs:909`） | 刪 E/X/C 的 oNeed | 既有 | 不改（自動接手刪 M→O） |

> 註：`ProcessoNeedToRequire` 與 `InsertoMission` 會把 TaskSource 寫死成 `'MCS'`，故連動**不能靠 TaskSource 辨識，必須靠 ParentTaskDateTime**。

## 方案分析（六面向）

1. **方案概述**：ParentTaskDateTime 父子連動延伸到 oNeed/oRequire + 取消時清 oNeed 源頭。
2. **架構設計**：見上。父關聯由卡控種下，沿 oNeed→oRequire→oMission 傳遞；取消由 oRequire/oMission 反查 parent，標源頭 oNeed='C'，交既有回收刪除。
3. **優點**：
   - 與跨樓層取消機制語意一致（同一根 ParentTaskDateTime 欄位、同一套連動思路），好維護。
   - 真正斷源頭——不再需要手動進 DB 刪 M 區任務。
   - 對一般/跨樓層任務零影響（ParentTaskDateTime 為 NULL）。
4. **風險與缺點**：
   - 動到取消主幹（`DeleteoNeed`）與 svrPair 三處 INSERT，屬行為變更，需回歸驗證跨樓層取消。
   - svrPair raw SQL 讀 `dr["ParentTaskDateTime"]` 需 DBNull 防呆，否則一般任務轉表會丟例外（但有單筆隔離接住）。
   - 兩個程序（svrPair / SCP）跨程序時序，靠「標 C 後下一輪不選取」收斂，存在同輪快照的極小重生窗（以 race 防護補強）。
5. **技術債評估**：低。欄位與連動沿用既有命名與模式，未引入新抽象；未來若空平板規則調整，關聯機制不受影響。
6. **建議適用情境**：源頭任務停在 oNeed（卡控）而衍生子任務已派車、需從可見子任務反向清不可見源頭的場景——正是本需求。

## 被捨棄的方案

- **方案 B：取消時用 ObjStation/EndStation 比對清源頭（不加欄位）**。捨棄原因：M→O 與 O→Q 只有「O 同站」這個弱關聯，靠站號比對易誤刪同站的其他任務，且無法區分多筆並存；ParentTaskDateTime 是精確 key。
- **方案 C：直接照抄跨樓層（只用 oMission.ParentTaskDateTime）**。捨棄原因：跨樓層的父任務本身是 oMission/oRequire，取消後既有 `RecyclingoRequireByOkFlag` 會順手清源頭 oNeed；但空平板的父任務 M→O 卡在 oNeed、從沒進 oRequire，該清理路徑碰不到它——照抄無法清源頭，必須額外加 oNeed 層清除。
- **方案 D：加冷卻期（仿跨樓層 CooldownTracker 30 秒防重派）**。捨棄原因：源頭一清就斷，上游不會自動重發（手動刪 M→O 即停可證），冷卻期是多餘複雜度。
