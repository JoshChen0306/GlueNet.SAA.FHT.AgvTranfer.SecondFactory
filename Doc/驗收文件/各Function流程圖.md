# 二廠 AGV 系統 — 各 Function 流程圖

**文件版本：** v1.0
**建立日期：** 2026-06-05
**權威來源：** 每張流程圖的分支與標記均對照實際程式碼（檔名 / 方法已標註）。

---

## 1. 二樓站內運輸（空平板自動回收）

**來源：** `Dispatch/svrPair/cPair.cs → CheckAndHandleEmptyPlateRecovery()`（cs:612–758）、前端卡控 `DispatchController.InsertoNeed()`。
**觸發：** 物料派往 O / P 上料區時，先檢查 `oPortBinding` 綁定的下料區（Q）狀態。
**任務標記：** `PLATE_RECOVERY`。

```mermaid
flowchart TD
    S[物料 oNeed 終點 = O/P 上料區] --> B{該站有 oPortBinding 綁定?}
    B -- 否 --> PASS[正常派送物料]
    B -- 是 --> L{上料區有空板?<br/>HaveFlag=1}
    L -- 否 --> P{路徑仍被占用?<br/>BgnToEnd 非空}
    P -- 是 --> WAIT1[繼續卡控等待<br/>AGV 搬板途中]
    P -- 否 --> U1{下料區 Q 有空板?}
    U1 -- 是 --> PASS
    U1 -- 否 --> WAIT2[卡住等待人工補料]
    L -- 是 --> EXIST{已有回收任務?<br/>oNeed/oRequire/oMission}
    EXIST -- 是 --> SKIP[跳過, 等回收完成]
    EXIST -- 否 --> U2{下料區 Q 狀態}
    U2 -- 空架0 --> R1[空板直接送 Q 下料區]
    U2 -- 有空板1 --> R2[空板送 FallbackAreas<br/>依序 M→Q→R 找空位]
    U2 -- 料盤3 --> WAIT3[卡住等待人工]
    R1 --> GEN[產生 PLATE_RECOVERY 回收 oNeed<br/>帶 ParentTaskDateTime]
    R2 --> GEN
    GEN --> HOLD[卡住物料 oNeed, 下輪再評估]
```

---

## 2. AGV 自動復歸（IDLE_RETURN）

**來源：** `ACC/.../CrossFloorManager.cs → Tick()`（cs:111–189）。每個 Dispatch 週期呼叫一次。
**觸發：** 跨樓層車輛（現場車號 3）閒置超過 `IdleReturnTimeout`（300s）且不在母樓層（4F）。
**任務標記：** `IDLE_RETURN`。

```mermaid
flowchart TD
    T[Tick 每週期] --> ON{車輛在線?<br/>UpdateTime≤60s}
    ON -- 否 --> RST[重置計時, return]
    ON -- 是 --> RUN{Status=R 執行中?}
    RUN -- 是 --> E1[不處理]
    RUN -- 否 --> XM{有跨樓層任務<br/>oMission OkFlag=R?}
    XM -- 是 --> RST
    XM -- 否 --> IDLE{Status=I 閒置?}
    IDLE -- 否 --> E2[不處理]
    IDLE -- 是 --> DISP{歸位/預調度<br/>已派發?}
    DISP -- 是 --> E3[等 RCS 執行]
    DISP -- 否 --> HOME{已在母樓層 4F?}
    HOME -- 是 --> RST
    HOME -- 否 --> TIMER{已啟動計時?}
    TIMER -- 否 --> START[啟動閒置計時]
    TIMER -- 是 --> OVER{超過 IdleReturnTimeout?}
    OVER -- 否 --> WAIT[繼續等待]
    OVER -- 是 --> GO[寫入 IDLE_RETURN<br/>oMission + oRequire<br/>歸位 4F 電梯等待點]
```

> 計時中若 `OnNewTaskArrived()` 進來新任務 → 取消計時優先派任務；歸位完成 `OnIdleReturnCompleted()` → 重置狀態。

---

## 3. 預派調度（CROSS_FLOOR_DISPATCH）

**來源：** `CrossFloorManager.cs → SelectNextMission()` / `DecideNextCrossFloorAction()`（cs:268–350）。
**觸發：** 有跨樓層待派任務，但車輛不在任務起點樓層。
**任務標記：** `CROSS_FLOOR_DISPATCH`。

```mermaid
flowchart TD
    S[待派跨樓層任務清單] --> SYS{有系統任務?<br/>CROSS_FLOOR_DISPATCH/IDLE_RETURN}
    SYS -- 是 --> RET[直接回傳讓 Dispatch 派發]
    SYS -- 否 --> PEND{預調度已送 RCS?<br/>_crossFloorDispatchPending}
    PEND -- 是 --> NULL1[回傳 null 等 Callback]
    PEND -- 否 --> SAME{有同樓層任務?<br/>起點=車輛所在樓層}
    SAME -- 是 --> DO1[同樓層任務優先派發]
    SAME -- 否 --> COOL{冷卻期命中?<br/>同 parent 30s 內}
    COOL -- 是 --> RETP[攔截重派, 回傳父任務]
    COOL -- 否 --> NEED[產生 CROSS_FLOOR_DISPATCH<br/>移車至起點樓層電梯等待點]
    NEED --> DONE[完成後記錄冷卻期]
```

> 冷卻期由 `CooldownTracker` 管控（預設 30s），防止 MapCode 被覆蓋後重派同一 parent；ACC 啟動時由 `ubMission` 歷史重建冷卻狀態。

---

## 4. 跨樓層電梯換乘路徑

**來源：** `ElevatorPathCalculator.cs → CalculatePath()` / `BuildReturnPath()`（cs:153–300）。
**電梯配置：** 客梯服務 1F–3F、客貨梯服務 3F–4F；3F 為兩梯轉乘樓層。

```mermaid
flowchart TD
    S[起點樓層, 終點樓層] --> SAME{同樓層?}
    SAME -- 是 --> DIRECT[直接 起點→終點]
    SAME -- 否 --> NC{需客梯?<br/>起或終在 1F/2F}
    NC --> NF{需客貨梯?<br/>起或終在 4F}
    NF -- 兩者皆需 --> BOTH[雙電梯換乘 經 3F]
    NF -- 僅客梯 --> CUST[客梯 起→終]
    NF -- 僅客貨梯 --> FREI[客貨梯 起→終]
    BOTH --> UP{上行?}
    UP -- 是 --> U[客梯 起→3F → 3F等待點W1 → 客貨梯 3F→終]
    UP -- 否 --> D[客貨梯 起→3F → 3F等待點X1 → 客梯 3F→終]
```

**換乘範例（主物料路線 2F↔4F）：**
- 上行 H(2F)→K(4F)：H → 客梯(2F→3F) → W1 → 客貨梯(3F→4F) → K
- 下行 K(4F)→H(2F)：K → 客貨梯(4F→3F) → X1 → 客梯(3F→2F) → H

> 每段電梯路徑由 `AddElevatorPath()` 展開為「等待點 → 電梯內(起) → 電梯內(終)」三點；等待點 / 電梯內點來自 `SectionElevator` 設定。

---

## 5. 任務取消連動（跨樓層取消重建管控 + 空平板取消清源頭）

**來源：** `DispatchController.cs → DeleteoNeed()`（cs:274–342）＋ `cPair.RecyclingoNeedByAssignFlag()` ＋ `CooldownTracker`。
**核心：** 以 `ParentTaskDateTime` 父子關聯，取消任一任務時連動取消其父 / 子任務，並從源頭 `oNeed` 清除，避免主循環重掃 oNeed 又重生。

```mermaid
flowchart TD
    C[操作員取消任務 TaskDateTime] --> FP[查父關聯 ParentTaskDateTime<br/>先 oMission 後 oRequire]
    FP --> LIST[建立取消清單]
    FP --> PAR{被取消的是子任務?}
    PAR -- 是 --> ADDP[父任務插入清單最前<br/>父先取消]
    FP --> CHILD[查以此為 Parent 的子任務]
    CHILD --> ADDC[子任務加入清單]
    LIST --> LOOP[依序對每筆連動任務]
    ADDP --> LOOP
    ADDC --> LOOP
    LOOP --> M1[oMission OkFlag=C]
    LOOP --> M2[oRequire OkFlag=C]
    LOOP --> M3[oNeed AssignFlag=C 清源頭]
    M3 --> REC[svrPair RecyclingoNeedByAssignFlag 刪除]
    REC --> STOP[斷掉重掃 oNeed 重生回收任務的源頭]
```

> **跨樓層取消重建管控（功能 5）：** 預調度任務取消後，`CooldownTracker` 30s 冷卻期攔截同 parent 立即重派，避免「取消→又自動重建」的抖動。
> **空平板取消清源頭（功能 6）：** 卡控中的 M→O 物料任務只存在於 `oNeed`（未轉 oRequire/oMission），取消時標 `AssignFlag='C'` 交由 svrPair 清除，斷掉回收任務源頭。

---

## 6. 派送路由權限篩選（UI）

**來源：** `DispatchController.cs → Index()`（cs:32–132）。決定操作員派送頁可選的起點區與樓層。

```mermaid
flowchart TD
    S[操作員開啟派送頁] --> Q[查 pUserRoute join pRoute<br/>ControlFlag=Y]
    Q --> HAS{有設定路線權限?}
    HAS -- 否 --> OLD[舊機制: 依 GroupId switch<br/>2→A / 6→C,D / 5→F]
    HAS -- 是 --> SRC[取 DISPATCH 模式路線的<br/>SourceAreas 為可選起點區]
    SRC --> FA[依 FloorArea 過濾出<br/>使用者可用樓層 Tab]
    OLD --> FA
    FA --> SHOW[顯示派送頁<br/>起點區 + 樓層選單]
```

> `RELEASE` 模式路線不顯示於派送選單，只供 Release 空板回送功能使用。此篩選僅控 UI，後端 `InsertoNeed()` 不再二次驗證 pRoute。

---

*本文件由 Claude Code 輔助生成，所有流程分支以實際程式碼確認。*
