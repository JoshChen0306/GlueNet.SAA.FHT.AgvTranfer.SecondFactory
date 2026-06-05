# 二廠 AGV 系統 — 系統 UML

**文件版本：** v1.0
**建立日期：** 2026-06-05
**詳細度：** 架構層級（核心類別 + 關鍵時序，不逐一展開所有成員）
**權威來源：** 類別成員與互動關係以實際程式碼確認（檔名 / 行號已標註）。

---

## 1. 核心類別圖

### 1.1 ACC（跨樓層調度核心）

```mermaid
classDiagram
    class Dispatch {
        +CrossFloor : CrossFloorManager$
        -_crossFloorManager
        +主迴圈() 每週期 Tick / SelectNextMission
    }
    class CrossFloorManager {
        -_shuttleId
        -_idleReturnTimeoutSeconds
        -_idleReturnFloor
        -_mapCodeFloorMapping
        -_idleStartTime
        -_idleReturnDispatched
        -_crossFloorDispatchPending
        +Tick()
        +SelectNextMission(pending) oMissionModel
        +OnNewTaskArrived()
        +OnIdleReturnCompleted()
        +OnCrossFloorDispatchCompleted(mission)
        +DecideNextCrossFloorAction()$ CrossFloorDecision
    }
    class ElevatorPathCalculator {
        -_settings : ElevatorSettings
        +CalculatePath(b,e) List
        +BuildReturnPath(from,to) List
        +GetFloor(station)
        +IsCrossFloor(b,e)
        +GetTaskType(b,e,routes,def)
    }
    class CooldownTracker {
        +Initialize(parent,endTime)
        +RecordCompletion(parent)
        +IsInCooldown(parent) bool
    }
    class CrossFloorDecision {
        +Kind : CrossFloorDecisionKind
        +Task / FromFloor / ToFloor
        +None()$ / SameFloor()$ / NeedDispatch()$ / CooldownHit()$
    }
    class SQLData
    class CallBackAPI

    Dispatch --> CrossFloorManager : 持有(static CrossFloor)
    CrossFloorManager --> ElevatorPathCalculator
    CrossFloorManager --> CooldownTracker
    CrossFloorManager ..> CrossFloorDecision : 產生
    CrossFloorManager --> SQLData : 讀寫 oMission/oShuttle
    CallBackAPI ..> Dispatch : 回調觸發 OnXxxCompleted
```

### 1.2 Dispatch（svrPair 配對引擎）

```mermaid
classDiagram
    class cPair {
        -mSql : SqlHelper
        -mPanelDoB2C / mUnloadAuto
        +BgnPair() / EndPair()
        -MainProcess() 主循環
        +GenerateoRequireByoNeed()
        -ProcessSingleoNeed(dr)
        -CheckAndHandleEmptyPlateRecovery(dr) bool
        -GenerateoMissionByoRequire()
        -RecyclingoRequireByOkFlag()
        -RecyclingoNeedByAssignFlag()
    }
    class SqlHelper
    class Ini
    cPair --> SqlHelper
    cPair --> Ini : Recipe.ini
```

### 1.3 SCP（後台網站）

```mermaid
classDiagram
    class DispatchController {
        +Index() 路線權限篩選 UI
        +InsertoNeed(need) 手動派送+空板卡控
        +Release(data) 空板回送(終點寫死)
        +RegisterLot(data) 物料登記
        +ClearLot / MarkEmptyTray
        +DeleteoNeed(need) 取消連動
        -FindFallbackEmptySlot()
    }
    class RouteController
    class PortBindingController
    class TaskTypeRouteController
    class agvDB_1400004Context

    DispatchController --> agvDB_1400004Context
    RouteController --> agvDB_1400004Context
    PortBindingController --> agvDB_1400004Context
    TaskTypeRouteController --> agvDB_1400004Context
```

---

## 2. 關鍵時序圖

### 2.1 一般搬運：派送 → 配對 → 派車 → 回調

```mermaid
sequenceDiagram
    participant SCP
    participant DB as agvDB
    participant SP as svrPair(cPair)
    participant ACC as ACC(Dispatch)
    participant CB as CallBackAPI
    participant RCS

    SCP->>DB: InsertoNeed 寫 oNeed
    SP->>DB: GenerateoRequireByoNeed (oNeed→oRequire, 註冊 oPort)
    SP->>DB: GenerateoMissionByoRequire (oRequire→oMission)
    ACC->>DB: 取未派發 oMission
    ACC->>RCS: 派車(ElevatorPathCalculator 算路徑/TaskType)
    RCS-->>CB: 任務完成回調
    CB->>DB: 更新 oMission/oRequire OkFlag=Y
    SP->>DB: RecyclingoRequireByOkFlag 回收, 解除 oPort 註冊
```

### 2.2 跨樓層自動歸位（IDLE_RETURN）

```mermaid
sequenceDiagram
    participant ACC as Dispatch主迴圈
    participant CFM as CrossFloorManager
    participant EPC as ElevatorPathCalculator
    participant DB as agvDB
    participant RCS
    participant CB as CallBackAPI

    loop 每週期
        ACC->>CFM: Tick()
    end
    CFM->>CFM: 車 IDLE 且不在4F, 計時超時
    CFM->>EPC: BuildReturnPath(目前樓層, 4F)
    EPC-->>CFM: 電梯路徑(含換乘點)
    CFM->>DB: 寫 IDLE_RETURN oMission+oRequire
    ACC->>RCS: 派發歸位任務
    RCS-->>CB: 歸位完成回調(TaskSource=IDLE_RETURN)
    CB->>CFM: OnIdleReturnCompleted() 重置狀態
```

> 預調度（CROSS_FLOOR_DISPATCH）時序類似：`SelectNextMission()` → `DispatchCrossFloor()` 寫 oMission → 回調 `OnCrossFloorDispatchCompleted(mission)` 重置並記錄冷卻期。

---

## 3. 關鍵狀態與旗標

| 旗標（CrossFloorManager）| 意義 |
|--------------------------|------|
| `_idleStartTime` | 閒置計時起點（null＝未計時）|
| `_idleReturnDispatched` | 歸位任務已寫入 oMission，待 RCS 執行 |
| `_crossFloorDispatchPending` | 預調度任務已送出，待回調 |

| oMission.TaskSource | 任務類型 |
|---------------------|---------|
| `MCS` | 一般搬運 / Release / 1F 自動配對 |
| `PLATE_RECOVERY` | 空平板自動回收 |
| `IDLE_RETURN` | 自動歸位 |
| `CROSS_FLOOR_DISPATCH` | 跨樓層預調度 |

---

*本文件由 Claude Code 輔助生成，類別與互動以實際程式碼確認；架構層級呈現，未逐一展開所有成員。*
